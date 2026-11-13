using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Stateless Layer 4 semantic enricher for Terraform, OpenTofu, and Terragrunt projects.
/// Operates exclusively over in-memory <see cref="SyntaxTree"/>s and <see cref="ProjectEnrichmentContext"/>
/// with zero filesystem I/O.
/// </summary>
public static class TerraformSemanticEnricher
{
    private const string BlockTerraform = "terraform";
    private const string BlockInclude = "include";
    private const string BlockResource = "resource";
    private const string AttrSource = "source";
    private const string AttrPath = "path";
    private const string AttrInputs = "inputs";
    private const string AttrEngine = "engine";
    private const string ViaInput = "input";
    private const string ViaOutput = "output";
    private const string ViaTerragruntInputs = "terragrunt.inputs";
    private const string ViaTerragruntInclude = "terragrunt.include";
    private const string ProvisionedByTerraform = "terraform";
    private const string VarPrefix = "var.";
    private const string OutputPrefix = "output.";
    private const string OutputsPrefix = "outputs.";

    public static ProjectEnrichmentResult Enrich(ProjectEnrichmentContext context)
    {
        var moduleStates = BuildInMemoryWorkspaceModules(context);
        var projectModuleDir = NormalizeRelativeDir(context.Project.Path);
        if (!moduleStates.TryGetValue(projectModuleDir, out var currentModule))
        {
            return ProjectEnrichmentResult.Empty;
        }

        var fileSet = BuildInMemoryFileSet(context);
        var dirSet = BuildInMemoryDirSet(context, moduleStates.Keys);

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

        EnrichIntraModuleDependencies(currentModule, AddRel);
        EnrichModuleInputsAndOutputs(currentModule, moduleStates, context.AbsoluteWorkspacePath, fileSet, dirSet, AddRel);
        EnrichTerragruntInputs(currentModule, moduleStates, context.AbsoluteWorkspacePath, fileSet, dirSet, AddRel);
        EnrichCloudInfrastructure(context.WorkspaceId, currentModule, AddSemanticNode, AddRel);

        return new ProjectEnrichmentResult(semanticNodes, semanticRels, globalRels);
    }

    private sealed class ModuleMemoryState
    {
        public required string ModuleDir { get; init; }
        public Dictionary<string, TerraformSymbol> SymbolsByCanonical { get; } = new(StringComparer.Ordinal);
        public List<TerraformSymbol> AllSymbols { get; } = [];
        public List<HclFile> HclFiles { get; } = [];
        public string? TerragruntFileNodeId { get; set; }
        public int TerragruntInputsLine { get; set; }
        public HclExpr? TerragruntSourceExpr { get; set; }
        public List<HclExpr> TerragruntIncludePathExprs { get; } = [];
        public Dictionary<string, IReadOnlyList<HclReference>> TerragruntInputs { get; } = new(StringComparer.Ordinal);
    }

    private static Dictionary<string, ModuleMemoryState> BuildInMemoryWorkspaceModules(ProjectEnrichmentContext context)
    {
        var modules = new Dictionary<string, ModuleMemoryState>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, trees) in context.TreesByProjectId)
        {
            foreach (var tree in trees)
            {
                var hclDoc = TryGetHclDocument(tree);
                if (hclDoc == null || tree.FileNode == null)
                {
                    continue;
                }

                var relPath = tree.FileNode.Path.Replace('\\', '/');
                var moduleDir = TerraformDeclarationMapper.GetModuleDir(relPath);
                if (!modules.TryGetValue(moduleDir, out var state))
                {
                    state = new ModuleMemoryState { ModuleDir = moduleDir };
                    modules[moduleDir] = state;
                }

                state.HclFiles.Add(hclDoc.HclFile);
                foreach (var sym in hclDoc.Declarations)
                {
                    state.AllSymbols.Add(sym);
                    state.SymbolsByCanonical.TryAdd(sym.CanonicalName, sym);
                }

                ExtractTerragruntMetadata(hclDoc.HclFile, tree.FileNode.Id, state);
            }
        }

        return modules;
    }

    private static HclDocumentSyntaxNode? TryGetHclDocument(SyntaxTree tree)
    {
        var root = tree.Document?.RootNode;
        if (root == null) return null;
        if (root is HclDocumentSyntaxNode direct) return direct;
        return root.FirstChildOfType<HclDocumentSyntaxNode>();
    }

    private static void ExtractTerragruntMetadata(HclFile hclFile, string fileNodeId, ModuleMemoryState state)
    {
        foreach (var block in hclFile.Blocks)
        {
            if (string.Equals(block.BlockHeader, BlockTerraform, StringComparison.Ordinal))
            {
                var srcAttr = block.FindAttribute(AttrSource);
                if (srcAttr != null)
                {
                    state.TerragruntSourceExpr = srcAttr.Value;
                    state.TerragruntFileNodeId ??= fileNodeId;
                }
            }
            else if (string.Equals(block.BlockHeader, BlockInclude, StringComparison.Ordinal))
            {
                var pathAttr = block.FindAttribute(AttrPath);
                if (pathAttr != null)
                {
                    state.TerragruntIncludePathExprs.Add(pathAttr.Value);
                }
            }
        }

        var inputsAttr = hclFile.FindAttribute(AttrInputs);
        if (inputsAttr?.Value is HclExpr.ObjectExpr inputsObj)
        {
            state.TerragruntFileNodeId = fileNodeId;
            state.TerragruntInputsLine = inputsAttr.Line;
            foreach (var entry in inputsObj.Entries)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    state.TerragruntInputs[entry.Name] = entry.Value.CollectReferences(entry.Name);
                }
            }
        }
    }

    private static void EnrichIntraModuleDependencies(
        ModuleMemoryState module,
        Action<OntologyRelationship> addRel)
    {
        foreach (var sym in module.AllSymbols)
        {
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

                addRel(new DependsOnRelationship(sym.NodeId, target.NodeId)
                {
                    Via = reference.Via,
                    ReferenceExpression = reference.CanonicalTarget,
                    Line = reference.Line
                });
            }
        }
    }

    private static void EnrichModuleInputsAndOutputs(
        ModuleMemoryState module,
        Dictionary<string, ModuleMemoryState> allModules,
        string workspaceRoot,
        HashSet<string> fileSet,
        HashSet<string> dirSet,
        Action<OntologyRelationship> addRel)
    {
        var absModuleDir = CombineWorkspacePath(workspaceRoot, module.ModuleDir);

        foreach (var sym in module.AllSymbols)
        {
            if (sym.Kind == TerraformSymbolKind.Module &&
                !string.IsNullOrEmpty(sym.ModuleSourceRaw) &&
                TerraformPathResolver.IsLocalModuleSource(sym.ModuleSourceRaw))
            {
                var resolvedAbs = TerraformPathResolver.ResolveStringPath(
                    sym.ModuleSourceRaw,
                    absModuleDir,
                    workspaceRoot,
                    fileSet.Contains,
                    dirSet.Contains);
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
                    string.IsNullOrEmpty(modSym.ModuleSourceRaw) ||
                    !TerraformPathResolver.IsLocalModuleSource(modSym.ModuleSourceRaw))
                {
                    continue;
                }

                var resolvedAbs = TerraformPathResolver.ResolveStringPath(
                    modSym.ModuleSourceRaw,
                    absModuleDir,
                    workspaceRoot,
                    fileSet.Contains,
                    dirSet.Contains);
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
        ModuleMemoryState callerModule,
        ModuleMemoryState targetModule,
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
        ModuleMemoryState module,
        Dictionary<string, ModuleMemoryState> allModules,
        string workspaceRoot,
        HashSet<string> fileSet,
        HashSet<string> dirSet,
        Action<OntologyRelationship> addRel)
    {
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
            foreach (var r in refsInInput)
            {
                if (module.SymbolsByCanonical.TryGetValue(r.CanonicalTarget, out var tgSym))
                {
                    addRel(new ConfiguresRelationship(tgSym.NodeId, targetVar.NodeId)
                    {
                        Via = ViaTerragruntInputs,
                        Line = r.Line
                    });

                    addRel(new DependsOnRelationship(targetVar.NodeId, tgSym.NodeId)
                    {
                        Via = ViaTerragruntInputs,
                        ReferenceExpression = r.CanonicalTarget,
                        Line = r.Line
                    });
                    linkedAnySymbol = true;
                }
                else if (r.Kind == HclRefKind.Dependency &&
                         module.SymbolsByCanonical.TryGetValue($"dependency.{r.TargetName}", out var depSym))
                {
                    var outAttr = r.Attribute != null && r.Attribute.StartsWith(OutputsPrefix, StringComparison.Ordinal)
                        ? r.Attribute[OutputsPrefix.Length..]
                        : r.Attribute;
                    addRel(new DependsOnRelationship(targetVar.NodeId, depSym.NodeId)
                    {
                        Via = ViaTerragruntInputs,
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

    private static string? ResolveTerragruntTargetModuleDir(
        ModuleMemoryState module,
        Dictionary<string, ModuleMemoryState> allModules,
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
                dirSet.Contains);
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
                dirSet.Contains);
            var parentRelDir = ToRelativeModuleDir(workspaceRoot, includedAbs);
            if (parentRelDir != null &&
                allModules.TryGetValue(parentRelDir, out var parentMod) &&
                parentMod.TerragruntSourceExpr != null)
            {
                var parentAbsDir = CombineWorkspacePath(workspaceRoot, parentMod.ModuleDir);
                var resolvedFromParent = TerraformPathResolver.EvaluatePathExpr(
                    parentMod.TerragruntSourceExpr,
                    parentAbsDir,
                    workspaceRoot,
                    fileSet.Contains,
                    dirSet.Contains);
                var rel = ToRelativeModuleDir(workspaceRoot, resolvedFromParent);
                if (rel != null) return rel;
            }
        }

        return null;
    }

    private static void EnrichCloudInfrastructure(
        string workspaceId,
        ModuleMemoryState module,
        Action<IOntologyNode> addSemanticNode,
        Action<OntologyRelationship> addRel)
    {
        var semanticTargetByResourceCanonical = new Dictionary<string, (string SemanticId, InfraClassification Classification)>(StringComparer.Ordinal);

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
                var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:tf:{sym.ModuleDir}:{sym.ShortName}";
                var explicitEngine = FindResourceAttributeString(module, sym, AttrEngine);
                var dbNode = new DatabaseNode(dbId, sym.ShortName, sym.RelativeFilePath, classification.DbSystem)
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

                semanticTargetByResourceCanonical[sym.CanonicalName] = (dbId, classification);
            }
            else if (classification.Category == InfraCategory.Topic && classification.BrokerEngine != null)
            {
                var topicId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Topic}:tf:{sym.ModuleDir}:{sym.ShortName}";
                var topicNode = new TopicNode(topicId, sym.ShortName, sym.RelativeFilePath, classification.BrokerEngine)
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

                semanticTargetByResourceCanonical[sym.CanonicalName] = (topicId, classification);
            }
            else
            {
                semanticTargetByResourceCanonical[sym.CanonicalName] = (sym.NodeId, classification);
            }
        }

        foreach (var sym in module.AllSymbols)
        {
            foreach (var r in sym.References)
            {
                if (r.Kind != HclRefKind.Resource) continue;
                if (!semanticTargetByResourceCanonical.TryGetValue(r.CanonicalTarget, out var target) ||
                    target.SemanticId == sym.NodeId)
                {
                    continue;
                }

                OntologyRelationship usageRel = target.Classification.Category switch
                {
                    InfraCategory.Database => new UsesDbRelationship(sym.NodeId, target.SemanticId)
                    {
                        Line = r.Line,
                        IsInferred = true
                    },
                    InfraCategory.Topic => new PublishesToRelationship(sym.NodeId, target.SemanticId)
                    {
                        Line = r.Line,
                        IsInferred = true
                    },
                    _ => new UsesCloudRelationship(sym.NodeId, target.SemanticId)
                    {
                        Line = r.Line,
                        IsInferred = true
                    }
                };
                addRel(usageRel);
            }
        }
    }

    private static string? FindResourceAttributeString(ModuleMemoryState module, TerraformSymbol sym, string attrName)
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

    private static HashSet<string> BuildInMemoryFileSet(ProjectEnrichmentContext context)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in context.AllFiles)
        {
            if (!string.IsNullOrEmpty(file.FullPath))
            {
                set.Add(TerraformPathResolver.NormalizeDirPath(file.FullPath));
            }
            if (!string.IsNullOrEmpty(file.Path))
            {
                var full = CombineWorkspacePath(context.AbsoluteWorkspacePath, file.Path);
                set.Add(TerraformPathResolver.NormalizeDirPath(full));
            }
        }
        return set;
    }

    private static HashSet<string> BuildInMemoryDirSet(ProjectEnrichmentContext context, IEnumerable<string> moduleDirs)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            TerraformPathResolver.NormalizeDirPath(context.AbsoluteWorkspacePath)
        };

        foreach (var proj in context.AllProjects)
        {
            set.Add(CombineWorkspacePath(context.AbsoluteWorkspacePath, proj.Path));
        }

        foreach (var modDir in moduleDirs)
        {
            set.Add(CombineWorkspacePath(context.AbsoluteWorkspacePath, modDir));
        }

        foreach (var file in context.AllFiles)
        {
            var relDir = TerraformDeclarationMapper.GetModuleDir(file.Path);
            set.Add(CombineWorkspacePath(context.AbsoluteWorkspacePath, relDir));
        }

        return set;
    }

    private static string CombineWorkspacePath(string workspaceRoot, string relativePath)
    {
        var normRoot = TerraformPathResolver.NormalizeDirPath(workspaceRoot);
        var normRel = NormalizeRelativeDir(relativePath);
        return string.IsNullOrEmpty(normRel) ? normRoot : $"{normRoot}/{normRel}";
    }

    private static string? ToRelativeModuleDir(string workspaceRoot, string? resolvedAbsDir)
    {
        if (string.IsNullOrEmpty(resolvedAbsDir)) return null;
        var normRoot = TerraformPathResolver.NormalizeDirPath(workspaceRoot);
        var normTarget = TerraformPathResolver.NormalizeDirPath(resolvedAbsDir);
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

    private static string NormalizeRelativeDir(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == ".") return string.Empty;
        return path.Replace('\\', '/').Trim('/');
    }
}
