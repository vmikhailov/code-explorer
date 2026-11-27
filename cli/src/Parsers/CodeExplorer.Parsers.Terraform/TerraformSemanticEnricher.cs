using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Parsers.Terraform.Hcl;

namespace CodeExplorer.Parsers.Terraform;

/// <summary>
/// Stateless Layer 4 semantic enricher for Terraform, OpenTofu, and Terragrunt projects.
/// Operates exclusively over in-memory <see cref="SyntaxTree"/>s and <see cref="ProjectEnrichmentContext"/>
/// with zero filesystem I/O.
/// </summary>
public static class TerraformSemanticEnricher
{
    private const string BlockResource = "resource";
    private const string AttrEngine = "engine";
    private const string ViaInput = "input";
    private const string ViaOutput = "output";
    private const string ViaTerragruntInputs = "terragrunt.inputs";
    private const string ViaTerragruntInclude = "terragrunt.include";
    private const string ProvisionedByTerraform = "terraform";
    private const string VarPrefix = "var.";
    private const string OutputPrefix = "output.";
    private const string OutputsPrefix = "outputs.";
    private const string TfSegment = "tf";
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';

    private static readonly string[] DatabaseNameAttributes =
    [
        "db_name", "database_name", "identifier", "cluster_identifier", "name", "cluster_id", "cluster_name"
    ];

    private static readonly string[] TopicNameAttributes =
    [
        "name", "cluster_id", "cluster_name"
    ];

    private static readonly HashSet<string> GenericResourceLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "this", "main", "default", "db", "database", "cluster", "instance", "primary", "secondary", "replica"
    };

    public static ProjectEnrichmentResult Enrich(ProjectEnrichmentContext context)
    {
        var workspaceCache = TerraformWorkspaceCache.GetOrBuild(context);
        var moduleStates = workspaceCache.ModuleStates;
        var projectModuleDirs = CollectProjectModuleDirs(context);

        var fileSet = workspaceCache.FileSet;
        var dirSet = workspaceCache.DirSet;
        var duplicateResourceShortNames = workspaceCache.DuplicateResourceShortNames;

        var semanticNodes = new List<IOntologyNode>();
        var semanticNodesById = new Dictionary<string, IOntologyNode>(StringComparer.Ordinal);
        var semanticRels = new List<Relationship>();
        var globalRels = new List<Relationship>();
        var emittedEdges = new HashSet<(string From, string To, string Kind)>();

        void AddSemanticNode(IOntologyNode node)
        {
            if (semanticNodesById.TryAdd(node.Id, node))
            {
                semanticNodes.Add(node);
            }
        }

        void AddRel(OntologyRelationship rel)
        {
            var dbRel = Relationship.FromRelationship(rel);
            var key = (dbRel.From, dbRel.To, dbRel.Kind.ToCypherType());
            if (emittedEdges.Add(key))
            {
                semanticRels.Add(dbRel);
                globalRels.Add(dbRel);
            }
        }

        foreach (var modDir in projectModuleDirs)
        {
            if (!moduleStates.TryGetValue(modDir, out var currentModule))
            {
                continue;
            }

            EnrichIntraModuleDependencies(currentModule, AddRel);
            EnrichModuleInputsAndOutputs(currentModule, moduleStates, context.AbsoluteWorkspacePath, fileSet, dirSet, AddRel);
            EnrichTerragruntInputs(currentModule, moduleStates, context.AbsoluteWorkspacePath, fileSet, dirSet, AddRel);
            EnrichCloudInfrastructure(context.WorkspaceId, currentModule, duplicateResourceShortNames, AddSemanticNode, AddRel);
        }

        return new ProjectEnrichmentResult(semanticNodes, semanticRels, globalRels);
    }

    private static List<string> CollectProjectModuleDirs(ProjectEnrichmentContext context)
    {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tree in context.ProjectTrees)
        {
            if (tree.FileNode == null || TerraformWorkspaceCache.TryGetHclDocument(tree) == null) continue;
            var relPath = tree.FileNode.Path.Replace(BackslashChar, SlashChar);
            var modDir = TerraformDeclarationMapper.GetModuleDir(relPath);
            if (seen.Add(modDir))
            {
                dirs.Add(modDir);
            }
        }

        var fallbackDir = TerraformWorkspaceCache.NormalizeRelativeDir(context.Project.Path);
        if (seen.Add(fallbackDir))
        {
            dirs.Add(fallbackDir);
        }

        return dirs;
    }

    private static void EnrichIntraModuleDependencies(
        TerraformModuleMemoryState module,
        Action<OntologyRelationship> addRel)
    {
        foreach (var sym in module.AllSymbols)
        {
            if (sym.Kind == TerraformSymbolKind.Local &&
                !string.IsNullOrEmpty(sym.BlockClass) &&
                sym.CanonicalName.Contains(".env.", StringComparison.Ordinal) &&
                module.SymbolsByCanonical.TryGetValue(sym.BlockClass, out var ownerResSym))
            {
                addRel(new DependsOnRelationship(ownerResSym.NodeId, sym.NodeId)
                {
                    Via = "environment.variables",
                    ReferenceExpression = $"env.{sym.ShortName}",
                    Line = sym.StartLine
                });
            }

            foreach (var reference in sym.References)
            {
                if (reference.Kind is HclRefKind.Each or HclRefKind.Self or HclRefKind.Count or HclRefKind.Path or HclRefKind.Terraform)
                {
                    continue;
                }

                if (!module.SymbolsByCanonical.TryGetValue(reference.CanonicalTarget, out var target) ||
                    target.NodeId == sym.NodeId)
                {
                    continue;
                }

                var outAttr = reference.Kind == HclRefKind.Dependency &&
                              reference.Attribute != null &&
                              reference.Attribute.StartsWith(OutputsPrefix, StringComparison.Ordinal)
                    ? reference.Attribute[OutputsPrefix.Length..]
                    : reference.Kind == HclRefKind.Module && !string.IsNullOrEmpty(reference.Attribute)
                        ? reference.Attribute.Split('.')[0]
                        : null;

                var fullRefExpr = string.IsNullOrEmpty(reference.Attribute)
                    ? reference.CanonicalTarget
                    : $"{reference.CanonicalTarget}.{reference.Attribute}";

                addRel(new DependsOnRelationship(sym.NodeId, target.NodeId)
                {
                    Via = reference.Via,
                    ReferenceExpression = fullRefExpr,
                    OutputName = outAttr,
                    Line = reference.Line
                });
            }
        }
    }

    private static void EnrichModuleInputsAndOutputs(
        TerraformModuleMemoryState module,
        Dictionary<string, TerraformModuleMemoryState> allModules,
        string workspaceRoot,
        HashSet<string> fileSet,
        HashSet<string> dirSet,
        Action<OntologyRelationship> addRel)
    {
        var absModuleDir = CombineWorkspacePath(workspaceRoot, module.ModuleDir);

        foreach (var sym in module.AllSymbols)
        {
            if (sym.Kind == TerraformSymbolKind.Module &&
                !string.IsNullOrEmpty(sym.ModuleSourceRaw))
            {
                var resolvedAbs = TerraformPathResolver.ResolveStringPath(
                    sym.ModuleSourceRaw,
                    absModuleDir,
                    workspaceRoot,
                    fileSet.Contains,
                    dirSet.Contains,
                    module.LocalStrings);
                var targetRelDir = ToRelativeModuleDir(workspaceRoot, resolvedAbs);
                if (targetRelDir != null &&
                    allModules.TryGetValue(targetRelDir, out var targetMod) &&
                    sym.ModuleInputs != null)
                {
                    WireModuleInputs(sym, module, targetMod, addRel);
                }
            }

            foreach (var reference in sym.References)
            {
                if (reference.Kind != HclRefKind.Module || string.IsNullOrEmpty(reference.Attribute))
                {
                    continue;
                }

                var modCanonical = $"module.{reference.TargetName}";
                if (!module.SymbolsByCanonical.TryGetValue(modCanonical, out var modSym) ||
                    string.IsNullOrEmpty(modSym.ModuleSourceRaw))
                {
                    continue;
                }

                var resolvedAbs = TerraformPathResolver.ResolveStringPath(
                    modSym.ModuleSourceRaw,
                    absModuleDir,
                    workspaceRoot,
                    fileSet.Contains,
                    dirSet.Contains,
                    module.LocalStrings);
                var targetRelDir = ToRelativeModuleDir(workspaceRoot, resolvedAbs);
                if (targetRelDir == null || !allModules.TryGetValue(targetRelDir, out var targetMod))
                {
                    continue;
                }

                var outName = reference.Attribute.Split('.')[0];
                if (targetMod.SymbolsByCanonical.TryGetValue($"{OutputPrefix}{outName}", out var outSym))
                {
                    addRel(new DependsOnRelationship(sym.NodeId, outSym.NodeId)
                    {
                        Via = ViaOutput,
                        OutputName = outName,
                        Line = reference.Line
                    });
                }
            }
        }
    }

    private static void WireModuleInputs(
        TerraformSymbol moduleSym,
        TerraformModuleMemoryState callerModule,
        TerraformModuleMemoryState targetModule,
        Action<OntologyRelationship> addRel)
    {
        if (moduleSym.ModuleInputs == null) return;

        foreach (var (inputName, refsInInput) in moduleSym.ModuleInputs)
        {
            if (!targetModule.SymbolsByCanonical.TryGetValue($"{VarPrefix}{inputName}", out var targetVar))
            {
                continue;
            }

            addRel(new ConfiguresRelationship(moduleSym.NodeId, targetVar.NodeId)
            {
                Via = ViaInput,
                Line = moduleSym.StartLine
            });

            foreach (var inputRef in refsInInput)
            {
                if (callerModule.SymbolsByCanonical.TryGetValue(inputRef.CanonicalTarget, out var callerSym))
                {
                    addRel(new DependsOnRelationship(targetVar.NodeId, callerSym.NodeId)
                    {
                        Via = $"module.{moduleSym.ShortName}.{inputName}",
                        Line = inputRef.Line
                    });
                }
            }
        }
    }

    private static void EnrichTerragruntInputs(
        TerraformModuleMemoryState module,
        Dictionary<string, TerraformModuleMemoryState> allModules,
        string workspaceRoot,
        HashSet<string> fileSet,
        HashSet<string> dirSet,
        Action<OntologyRelationship> addRel)
    {
        EnrichTerragruntIncludes(module, allModules, workspaceRoot, fileSet, dirSet, addRel);
        EnrichTerragruntDependencyOutputs(module, allModules, workspaceRoot, fileSet, dirSet, addRel);

        if (module.TerragruntInputs.Count == 0 || string.IsNullOrEmpty(module.TerragruntFileNodeId))
        {
            return;
        }

        var targetModuleDir = ResolveTerragruntTargetModuleDir(module, allModules, workspaceRoot, fileSet, dirSet);
        if (targetModuleDir == null || !allModules.TryGetValue(targetModuleDir, out var targetModule))
        {
            return;
        }

        foreach (var (inputName, refsInInput) in module.TerragruntInputs)
        {
            if (!targetModule.SymbolsByCanonical.TryGetValue($"{VarPrefix}{inputName}", out var targetVar))
            {
                continue;
            }

            var linkedAnySymbol = false;
            if (module.SymbolsByCanonical.TryGetValue($"inputs.{inputName}", out var inputEntrySym))
            {
                addRel(new ConfiguresRelationship(inputEntrySym.NodeId, targetVar.NodeId)
                {
                    Via = ViaTerragruntInputs,
                    Line = inputEntrySym.StartLine
                });

                addRel(new DependsOnRelationship(targetVar.NodeId, inputEntrySym.NodeId)
                {
                    Via = ViaTerragruntInputs,
                    ReferenceExpression = $"inputs.{inputName}",
                    Line = inputEntrySym.StartLine
                });
            }

            foreach (var r in refsInInput)
            {
                if (module.SymbolsByCanonical.TryGetValue(r.CanonicalTarget, out var tgSym))
                {
                    var outAttr = r.Kind == HclRefKind.Dependency && r.Attribute != null && r.Attribute.StartsWith(OutputsPrefix, StringComparison.Ordinal)
                        ? r.Attribute[OutputsPrefix.Length..]
                        : null;
                    var fullRefExpr = string.IsNullOrEmpty(r.Attribute)
                        ? r.CanonicalTarget
                        : $"{r.CanonicalTarget}.{r.Attribute}";

                    addRel(new ConfiguresRelationship(tgSym.NodeId, targetVar.NodeId)
                    {
                        Via = ViaTerragruntInputs,
                        Line = r.Line
                    });

                    addRel(new DependsOnRelationship(targetVar.NodeId, tgSym.NodeId)
                    {
                        Via = ViaTerragruntInputs,
                        ReferenceExpression = fullRefExpr,
                        OutputName = outAttr,
                        Line = r.Line
                    });
                    linkedAnySymbol = true;
                }
            }

            if (!linkedAnySymbol)
            {
                addRel(new ConfiguresRelationship(module.TerragruntFileNodeId, targetVar.NodeId)
                {
                    Via = ViaTerragruntInputs,
                    Line = module.TerragruntInputsLine
                });
            }
        }
    }

    private static void EnrichTerragruntIncludes(
        TerraformModuleMemoryState module,
        Dictionary<string, TerraformModuleMemoryState> allModules,
        string workspaceRoot,
        HashSet<string> fileSet,
        HashSet<string> dirSet,
        Action<OntologyRelationship> addRel)
    {
        if (module.IncludePathExprsBySymbol.Count == 0)
        {
            return;
        }

        var absModuleDir = CombineWorkspacePath(workspaceRoot, module.ModuleDir);

        foreach (var (incCanonical, pathExpr) in module.IncludePathExprsBySymbol)
        {
            if (!module.SymbolsByCanonical.TryGetValue(incCanonical, out var incSym))
            {
                continue;
            }

            var includedAbs = TerraformPathResolver.EvaluatePathExpr(
                pathExpr,
                absModuleDir,
                workspaceRoot,
                fileSet.Contains,
                dirSet.Contains,
                module.LocalStrings);
            var parentRelDir = ToRelativeModuleDir(workspaceRoot, includedAbs);
            if (parentRelDir == null || !allModules.TryGetValue(parentRelDir, out var parentMod))
            {
                continue;
            }

            var targetFileNodeId = parentMod.TerragruntFileNodeId
                ?? parentMod.AllSymbols.FirstOrDefault()?.FileNodeId;
            if (!string.IsNullOrEmpty(targetFileNodeId))
            {
                addRel(new DependsOnRelationship(incSym.NodeId, targetFileNodeId)
                {
                    Via = ViaTerragruntInclude,
                    ReferenceExpression = incCanonical,
                    Line = incSym.StartLine
                });
            }

            foreach (var sym in module.AllSymbols)
            {
                foreach (var r in sym.References)
                {
                    if (r.Kind != HclRefKind.Include ||
                        !string.Equals(r.CanonicalTarget, incCanonical, StringComparison.Ordinal) ||
                        string.IsNullOrEmpty(r.Attribute) ||
                        !r.Attribute.StartsWith("locals.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var localName = r.Attribute["locals.".Length..].Split('.')[0];
                    if (parentMod.SymbolsByCanonical.TryGetValue($"local.{localName}", out var parentLocalSym) &&
                        parentLocalSym.NodeId != sym.NodeId)
                    {
                        addRel(new DependsOnRelationship(sym.NodeId, parentLocalSym.NodeId)
                        {
                            Via = ViaTerragruntInclude,
                            ReferenceExpression = $"{incCanonical}.{r.Attribute}",
                            OutputName = localName,
                            Line = r.Line
                        });
                    }
                }
            }
        }
    }

    private static void EnrichTerragruntDependencyOutputs(
        TerraformModuleMemoryState module,
        Dictionary<string, TerraformModuleMemoryState> allModules,
        string workspaceRoot,
        HashSet<string> fileSet,
        HashSet<string> dirSet,
        Action<OntologyRelationship> addRel)
    {
        if (module.DependencyConfigPathExprs.Count == 0)
        {
            return;
        }

        var absModuleDir = CombineWorkspacePath(workspaceRoot, module.ModuleDir);

        foreach (var sym in module.AllSymbols)
        {
            foreach (var r in sym.References)
            {
                if (r.Kind != HclRefKind.Dependency ||
                    string.IsNullOrEmpty(r.Attribute) ||
                    !r.Attribute.StartsWith(OutputsPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var outName = r.Attribute[OutputsPrefix.Length..].Split('.')[0];
                if (string.IsNullOrEmpty(outName) ||
                    !module.DependencyConfigPathExprs.TryGetValue(r.CanonicalTarget, out var cfgExpr))
                {
                    continue;
                }

                var depUnitAbs = TerraformPathResolver.EvaluatePathExpr(
                    cfgExpr,
                    absModuleDir,
                    workspaceRoot,
                    fileSet.Contains,
                    dirSet.Contains,
                    module.LocalStrings);
                var depUnitRel = ToRelativeModuleDir(workspaceRoot, depUnitAbs);
                if (depUnitRel == null || !allModules.TryGetValue(depUnitRel, out var depUnitMod))
                {
                    continue;
                }

                TerraformSymbol? targetOutSym = null;
                if (!depUnitMod.SymbolsByCanonical.TryGetValue($"{OutputPrefix}{outName}", out targetOutSym))
                {
                    var backingModDir = ResolveTerragruntTargetModuleDir(depUnitMod, allModules, workspaceRoot, fileSet, dirSet);
                    if (backingModDir != null && allModules.TryGetValue(backingModDir, out var backingMod))
                    {
                        backingMod.SymbolsByCanonical.TryGetValue($"{OutputPrefix}{outName}", out targetOutSym);
                    }
                }

                if (targetOutSym != null && targetOutSym.NodeId != sym.NodeId)
                {
                    var fullRefExpr = $"{r.CanonicalTarget}.{r.Attribute}";
                    addRel(new DependsOnRelationship(sym.NodeId, targetOutSym.NodeId)
                    {
                        Via = ViaOutput,
                        ReferenceExpression = fullRefExpr,
                        OutputName = outName,
                        Line = r.Line
                    });
                }
            }
        }
    }

    private static string? ResolveTerragruntTargetModuleDir(
        TerraformModuleMemoryState module,
        Dictionary<string, TerraformModuleMemoryState> allModules,
        string workspaceRoot,
        HashSet<string> fileSet,
        HashSet<string> dirSet)
    {
        var absModuleDir = CombineWorkspacePath(workspaceRoot, module.ModuleDir);

        if (module.TerragruntSourceExpr != null)
        {
            var resolvedAbs = TerraformPathResolver.EvaluatePathExpr(
                module.TerragruntSourceExpr,
                absModuleDir,
                workspaceRoot,
                fileSet.Contains,
                dirSet.Contains,
                module.LocalStrings);
            var rel = ToRelativeModuleDir(workspaceRoot, resolvedAbs);
            if (rel != null) return rel;
        }

        foreach (var includeExpr in module.TerragruntIncludePathExprs)
        {
            var includedAbs = TerraformPathResolver.EvaluatePathExpr(
                includeExpr,
                absModuleDir,
                workspaceRoot,
                fileSet.Contains,
                dirSet.Contains,
                module.LocalStrings);
            var parentRelDir = ToRelativeModuleDir(workspaceRoot, includedAbs);
            if (parentRelDir != null &&
                allModules.TryGetValue(parentRelDir, out var parentMod) &&
                parentMod.TerragruntSourceExpr != null)
            {
                var mergedLocals = new Dictionary<string, string>(parentMod.LocalStrings, StringComparer.Ordinal);
                foreach (var (k, v) in module.LocalStrings)
                {
                    mergedLocals[k] = v;
                }

                var parentAbsDir = CombineWorkspacePath(workspaceRoot, parentMod.ModuleDir);
                var resolvedFromParent = TerraformPathResolver.EvaluatePathExpr(
                    parentMod.TerragruntSourceExpr,
                    parentAbsDir,
                    workspaceRoot,
                    fileSet.Contains,
                    dirSet.Contains,
                    mergedLocals);
                var rel = ToRelativeModuleDir(workspaceRoot, resolvedFromParent);
                if (rel != null) return rel;
            }
        }

        return null;
    }

    private static void EnrichCloudInfrastructure(
        string workspaceId,
        TerraformModuleMemoryState module,
        HashSet<string> duplicateResourceShortNames,
        Action<IOntologyNode> addSemanticNode,
        Action<OntologyRelationship> addRel)
    {
        foreach (var sym in module.AllSymbols)
        {
            if (sym.Kind is not (TerraformSymbolKind.Resource or TerraformSymbolKind.DataSource) ||
                string.IsNullOrEmpty(sym.BlockClass))
            {
                continue;
            }

            var classification = CloudResourceCatalog.Classify(sym.BlockClass);
            var providerMatch = CloudProviderHelper.ResolveOrFallback(classification.Provider);
            var cloudId = CloudProviderHelper.BuildCloudNodeId(workspaceId, providerMatch.ProviderId);
            var cloudNode = new CloudNode(cloudId, providerMatch.DisplayName, sym.RelativeFilePath)
            {
                Provider = providerMatch.ProviderId,
                Category = providerMatch.TierSlug
            };
            addSemanticNode(cloudNode);

            addRel(new HostedInRelationship(sym.NodeId, cloudId)
            {
                FilePath = sym.RelativeFilePath,
                Provider = providerMatch.ProviderId,
                Line = sym.StartLine
            });

            if (sym.Kind != TerraformSymbolKind.Resource)
            {
                continue;
            }

            if (classification.Category == InfraCategory.Database && classification.DbSystem != null)
            {
                var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:{TfSegment}:{sym.ModuleDir}:{sym.CanonicalName}";
                var explicitEngine = FindResourceAttributeString(module, sym, AttrEngine);
                var literalDbName = FindResourceFirstAttributeString(module, sym, DatabaseNameAttributes);
                var dbNodeName = literalDbName ?? BuildQualifiedResourceName(module, duplicateResourceShortNames, sym);
                var dbNode = new DatabaseNode(dbId, dbNodeName, sym.RelativeFilePath, classification.DbSystem)
                {
                    Engine = CloudResourceCatalog.InferDatabaseEngine(sym.BlockClass, explicitEngine),
                    Provider = providerMatch.ProviderId,
                    ResourceType = sym.BlockClass,
                    ProvisionedBy = ProvisionedByTerraform
                };
                addSemanticNode(dbNode);

                addRel(new HostedInRelationship(dbId, cloudId)
                {
                    FilePath = sym.RelativeFilePath,
                    Provider = providerMatch.ProviderId,
                    Line = sym.StartLine
                });

                addRel(new ImplementedByRelationship(dbId, sym.NodeId)
                {
                    FilePath = sym.RelativeFilePath,
                    Line = sym.StartLine
                });

                addRel(new DeploysRelationship(sym.NodeId, dbId)
                {
                    FilePath = sym.RelativeFilePath,
                    Line = sym.StartLine
                });
            }
            else if (classification.Category == InfraCategory.Topic && classification.BrokerEngine != null)
            {
                var literalTopicName = FindResourceFirstAttributeString(module, sym, TopicNameAttributes);
                var topicName = literalTopicName ?? BuildQualifiedResourceName(module, duplicateResourceShortNames, sym);
                var topicId = literalTopicName != null
                    ? $"{workspaceId}:{OntologyConstants.IdPrefixes.Topic}:{classification.BrokerEngine}:{literalTopicName}"
                    : $"{workspaceId}:{OntologyConstants.IdPrefixes.Topic}:{TfSegment}:{sym.ModuleDir}:{sym.CanonicalName}";
                var topicNode = new TopicNode(topicId, topicName, sym.RelativeFilePath, classification.BrokerEngine)
                {
                    FilePath = sym.RelativeFilePath,
                    Line = sym.StartLine,
                    LineEnd = sym.EndLine,
                    ResourceType = sym.BlockClass,
                    ProvisionedBy = ProvisionedByTerraform
                };
                addSemanticNode(topicNode);

                addRel(new HostedInRelationship(topicId, cloudId)
                {
                    FilePath = sym.RelativeFilePath,
                    Provider = providerMatch.ProviderId,
                    Line = sym.StartLine
                });

                addRel(new ImplementedByRelationship(topicId, sym.NodeId)
                {
                    FilePath = sym.RelativeFilePath,
                    Line = sym.StartLine
                });

                addRel(new DeploysRelationship(sym.NodeId, topicId)
                {
                    FilePath = sym.RelativeFilePath,
                    Line = sym.StartLine
                });
            }
        }
    }

    private static string BuildQualifiedResourceName(
        TerraformModuleMemoryState module,
        HashSet<string> duplicateResourceShortNames,
        TerraformSymbol sym)
    {
        var duplicateInSameModule = false;
        foreach (var other in module.AllSymbols)
        {
            if (other.Kind == TerraformSymbolKind.Resource &&
                !ReferenceEquals(other, sym) &&
                string.Equals(other.ShortName, sym.ShortName, StringComparison.Ordinal))
            {
                duplicateInSameModule = true;
                break;
            }
        }

        var duplicateAcrossModules = duplicateResourceShortNames.Contains(sym.ShortName);

        if (!GenericResourceLabels.Contains(sym.ShortName) && !duplicateAcrossModules && !duplicateInSameModule)
        {
            return sym.ShortName;
        }

        if (string.IsNullOrEmpty(sym.ModuleDir))
        {
            return $"{sym.BlockClass}{SlashChar}{sym.ShortName}";
        }

        return duplicateInSameModule
            ? $"{sym.ModuleDir}{SlashChar}{sym.BlockClass}{SlashChar}{sym.ShortName}"
            : $"{sym.ModuleDir}{SlashChar}{sym.ShortName}";
    }

    private static string? FindResourceFirstAttributeString(
        TerraformModuleMemoryState module,
        TerraformSymbol sym,
        IReadOnlyList<string> attrNames)
    {
        foreach (var attrName in attrNames)
        {
            var val = FindResourceAttributeString(module, sym, attrName);
            if (!string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }
        return null;
    }

    private static string? FindResourceAttributeString(TerraformModuleMemoryState module, TerraformSymbol sym, string attrName)
    {
        foreach (var hclFile in module.HclFiles)
        {
            foreach (var block in hclFile.Blocks)
            {
                if (!string.Equals(block.BlockHeader, BlockResource, StringComparison.Ordinal) ||
                    block.Labels.Count < 2 ||
                    !string.Equals(block.Labels[0], sym.BlockClass, StringComparison.Ordinal) ||
                    !string.Equals(block.Labels[1], sym.ShortName, StringComparison.Ordinal))
                {
                    continue;
                }

                var val = block.FindAttribute(attrName)?.Value.TryGetStringLiteral();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
            }
        }
        return null;
    }

    private static string CombineWorkspacePath(string workspaceRoot, string relativePath) =>
        TerraformWorkspaceCache.CombineWorkspacePath(workspaceRoot, relativePath);

    private static string? ToRelativeModuleDir(string workspaceRoot, string? resolvedAbsDir)
    {
        if (string.IsNullOrEmpty(resolvedAbsDir)) return null;
        var normRoot = TerraformPathResolver.NormalizeDirPath(workspaceRoot);
        var normTarget = TerraformPathResolver.NormalizeDirPath(resolvedAbsDir);
        if (normTarget.EndsWith(".hcl", StringComparison.OrdinalIgnoreCase) ||
            normTarget.EndsWith(".tf", StringComparison.OrdinalIgnoreCase) ||
            normTarget.EndsWith(".tofu", StringComparison.OrdinalIgnoreCase))
        {
            normTarget = TerraformPathResolver.NormalizeDirPath(Path.GetDirectoryName(normTarget));
        }
        if (string.Equals(normRoot, normTarget, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }
        var prefix = normRoot + "/";
        if (normTarget.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return normTarget[prefix.Length..];
        }
        return null;
    }
}
