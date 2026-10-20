using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer3SyntacticParser
{
    public async Task<Layer3Result> ParseAsync(Layer2Result l2Result, ParsingContext ctx)
    {
        ctx.Log("[Layer3] Starting tree-sitter AST syntactic parsing pass...");

        WorkspaceConventions.LoadFromWorkspace(l2Result.Prev.Workspace.Path);

        var syntaxNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.SyntaxStructure}";
        var syntaxStructureNode = new SyntaxStructureNode(syntaxNodeId, "SyntaxStructure", l2Result.Prev.Workspace.Path);
        l2Result.Prev.Workspace.Children.Add(syntaxStructureNode);
        ctx.SyntaxStructure = syntaxStructureNode;

        var syntaxTrees = new List<SyntaxTree>();
        var rawImports = new List<RawImport>();
        var rawVariables = new List<RawVariable>();
        var rawTypeBindings = new List<RawTypeBinding>();
        var globalReferences = new List<Reference>();
        var globalSymbols = new Dictionary<(string Kind, string Name), string>();
        var nProject = 0;

        // O(1) parent index of Layer 1 tree (eliminates 160M recursive DFS traversals)
        var parentByChildId = new Dictionary<string, IOntologyNode>();
        void IndexPhysicalTree(IOntologyNode parent)
        {
            foreach (var child in parent.Children)
            {
                parentByChildId[child.Id] = parent;
                IndexPhysicalTree(child);
            }
        }
        IndexPhysicalTree(l2Result.Prev.Workspace);

        var projectRels = new List<Relationship>();
        // O(F * P) pre-grouping of files to enclosing projects (runs once in <2ms, eliminates 167M comparisons in inner loops)
        var filesByProjectId = new Dictionary<string, List<FileNode>>(l2Result.Projects.Count);
        foreach (var file in l2Result.Prev.Files)
        {
            ProjectNode? bestMatch = null;
            int bestMatchLength = -1;
            foreach (var p in l2Result.Projects)
            {
                if (string.IsNullOrEmpty(p.Path))
                {
                    if (bestMatchLength < 0)
                    {
                        bestMatch = p;
                        bestMatchLength = 0;
                    }
                    continue;
                }

                var prefix = p.Path.Replace('\\', '/').TrimEnd('/') + "/";
                if (file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && prefix.Length > bestMatchLength)
                {
                    bestMatch = p;
                    bestMatchLength = prefix.Length;
                }
            }

            if (bestMatch != null)
            {
                var isTest = bestMatch.IsTest || WorkspaceConventions.IsTestFilePath(file.Path);
                file.IsTest = isTest;
                file.ProjectKind = bestMatch.KindValue;
                file.ProjectName = bestMatch.Name;

                projectRels.Add(new Relationship(bestMatch.Id, file.Id, OntologyConstants.Relationships.Contains));
                projectRels.Add(new Relationship(file.Id, bestMatch.Id, OntologyConstants.Relationships.BelongsTo));

                if (!filesByProjectId.TryGetValue(bestMatch.Id, out var list))
                {
                    list = [];
                    filesByProjectId[bestMatch.Id] = list;
                }
                list.Add(file);
            }
            else
            {
                file.IsTest = WorkspaceConventions.IsTestFilePath(file.Path);
            }
        }

        if (projectRels.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(projectRels);
            ctx.AddRelsCount(projectRels.Count);
        }

        // Phase 0: Unified ConfigStore early initialization
        ConfigStore.Clear();
        ConstantRegistry.Clear();
        RouteDictionaryRegistry.Clear();

        var allProjectFileNodeIds = new HashSet<string>(filesByProjectId.Values.SelectMany(v => v).Select(f => f.Id));
        var workspaceRootFiles = l2Result.Prev.Files.Where(f => !allProjectFileNodeIds.Contains(f.Id)).ToList();
        foreach (var wfile in workspaceRootFiles)
        {
            if (ConfigStore.IsConfigurationFile(wfile.Name))
            {
                ConfigStore.LoadFile(wfile.FullPath, null, ctx);
            }
        }

        foreach (var project in l2Result.Projects)
        {
            if (filesByProjectId.TryGetValue(project.Id, out var pFiles))
            {
                foreach (var pfile in pFiles)
                {
                    if (ConfigStore.IsConfigurationFile(pfile.Name))
                    {
                        ConfigStore.LoadFile(pfile.FullPath, project.Name, ctx);
                    }
                }
            }
        }
        ctx.LogDebug($"[ConfigStore] Pre-loaded {ConfigStore.GetDiscoveredUrls().Count} service endpoints and configuration keys into ConstantRegistry.");

        // Phase 0b: RouteDictionaryRegistry and early constants pre-scanning across all candidate files
        var allCandidateFiles = new List<(string FullPath, string? ProjectName)>();
        foreach (var wfile in workspaceRootFiles)
        {
            if (IsRouteOrConfigCandidate(wfile.Path)) allCandidateFiles.Add((wfile.FullPath, null));
        }
        foreach (var project in l2Result.Projects)
        {
            if (filesByProjectId.TryGetValue(project.Id, out var pFiles))
            {
                foreach (var pfile in pFiles)
                {
                    if (IsRouteOrConfigCandidate(pfile.Path)) allCandidateFiles.Add((pfile.FullPath, project.Name));
                }
            }
        }

        var fileContents = new List<(string Content, string? ProjectName, string FullPath)>();
        foreach (var (fPath, pName) in allCandidateFiles)
        {
            try
            {
                if (File.Exists(fPath))
                {
                    var content = await File.ReadAllTextAsync(fPath);
                    RouteDictionaryRegistry.ScanServiceDomains(content);
                    fileContents.Add((content, pName, fPath));
                }
            }
            catch { }
        }

        foreach (var (content, pName, fPath) in fileContents)
        {
            try
            {
                RouteDictionaryRegistry.ScanAndRegister(content);
                ConstantRegistry.ScanAndRegister(fPath, content, pName);

                var fileParser = WorkspaceIndexer.GetParserForFile(fPath);
                fileParser?.DiscoverDeclarations(fPath, content, pName, ctx);
            }
            catch { }
        }

        foreach (var project in l2Result.Projects)
        {
            nProject++;
            ctx.CancellationToken.ThrowIfCancellationRequested();

            ctx.LogDebug($"[Layer3] Parsing project {nProject} of {l2Result.Projects.Count} at:'{project.Path}'...");

            var projectSyntaxId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{project.Path}:syntax";
            var projectSyntaxNode = new ProjectSyntaxNode(projectSyntaxId, "ProjectSyntax", project.Path);
            syntaxStructureNode.Children.Add(projectSyntaxNode);

            var belongsToRel = Relationship.FromRelationship(new BelongsToRelationship(projectSyntaxId, project.Id));
            await ctx.EnqueueUploadRelationshipsAsync([belongsToRel]);
            ctx.AddRelsCount(1);

            if (!filesByProjectId.TryGetValue(project.Id, out var projectFiles) || projectFiles.Count == 0)
            {
                continue;
            }

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ctx.CancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            var parsedResults = new (FileNode File, SyntaxTree SyntaxTree, IOntologyNode ParentNode)?[projectFiles.Count];

            // Pass 1: Parse all syntax trees and discover declarations (constants, enums, raw variables)
            await Parallel.ForAsync(0, projectFiles.Count, parallelOptions, async (i, ct) =>
            {
                var file = projectFiles[i];
                var fileParser = WorkspaceIndexer.GetParserForFile(file.FullPath);
                if (fileParser == null) return;

                if (!parentByChildId.TryGetValue(file.Id, out var parentNode)) return;
                var parentId = parentNode.Id;

                try
                {
                    var syntaxTree = await fileParser.ParseAsync(file.FullPath, parentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
                    syntaxTree.FileNode.IsTest = file.IsTest;
                    syntaxTree.FileNode.ProjectKind = file.ProjectKind;
                    syntaxTree.FileNode.ProjectName = file.ProjectName;
                    syntaxTree.FileNode.LineCount = file.LineCount;
                    syntaxTree.FileNode.SizeBytes = file.SizeBytes;
                    syntaxTree.FileNode.ContentHash = file.ContentHash;
                    syntaxTree.FileNode.LastModifiedUtc = file.LastModifiedUtc;
                    if (syntaxTree.Tree != null)
                    {
                        AstConstantExtractor.ExtractAndRegister(syntaxTree, project.Name);

                        for (int v = 0; v < syntaxTree.RawVariables.Count; v++)
                        {
                            var rv = syntaxTree.RawVariables[v];
                            if (rv.Scope != "local" && rv.IsConstant &&
                                !string.IsNullOrWhiteSpace(rv.Name) && !string.IsNullOrWhiteSpace(rv.InitializerText))
                            {
                                var rawInit = rv.InitializerText.Trim();
                                if ((rawInit.StartsWith('"') && rawInit.EndsWith('"')) ||
                                    (rawInit.StartsWith('\'') && rawInit.EndsWith('\'')) ||
                                    (rawInit.StartsWith('`') && rawInit.EndsWith('`')))
                                {
                                    ConstantRegistry.Register(project.Name, rv.Name, rawInit.Trim('\'', '"', '`'));
                                }
                            }
                        }

                        if (syntaxTree.Tree?.RootNode != null)
                        {
                            fileParser.DiscoverDeclarations(syntaxTree.Tree.RootNode, file.Path, project.Name, ctx);
                        }
                    }

                    parsedResults[i] = (file, syntaxTree, parentNode);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning($"[Layer3] Error parsing file '{file.Path}': {ex.Message}", ex);
                }
            });

            // Pass 2: Traverse ASTs with language visitors and component parsers (now that all constants and symbols are registered)
            var projectAbsDir = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, project.Path)).Replace('\\', '/');
            var filesInProjectDir = Directory.Exists(projectAbsDir) ? Directory.GetFiles(projectAbsDir) : [];
            var projectContext = new ProjectContext(
                projectAbsDir,
                project.Path,
                project.Name,
                project.ProjectType,
                filesInProjectDir,
                // Package dependencies resolved at Layer 2 — CanHandle() of component parsers
                // (e.g. the JS test runner keyed on jest/jasmine) used to receive an empty list
                // and therefore never matched.
                [
                    .. project.Children.OfType<PackageNode>().Select(p => p.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                ],
                project.Extensions ?? new Dictionary<string, string>());
            var componentParsers = Components.ComponentLibraryParserRegistry.GetParsers(projectContext);

            await Parallel.ForAsync(0, projectFiles.Count, parallelOptions, (i, ct) =>
            {
                var item = parsedResults[i];
                if (item == null) return ValueTask.CompletedTask;

                var (file, syntaxTree, _) = item.Value;
                if (syntaxTree.Tree != null)
                {
                    try
                    {
                        // File category comes from the parser that owns it (test framework component),
                        // and must be known before symbols are mapped so they inherit is_test.
                        var isTestFile = file.IsTest;
                        for (int cp = 0; !isTestFile && cp < componentParsers.Count; cp++)
                        {
                            isTestFile = componentParsers[cp].IsTestSource(syntaxTree);
                        }
                        if (isTestFile)
                        {
                            file.IsTest = true;
                            syntaxTree.FileNode.IsTest = true;
                        }

                        ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx, project.Name, isTestFile, project.KindValue);

                        if (componentParsers.Count > 0)
                        {
                            for (int cp = 0; cp < componentParsers.Count; cp++)
                            {
                                componentParsers[cp].EnrichWithSyntax(null, syntaxTree, ctx);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.LogWarning($"[Layer3] Error processing visitor for '{syntaxTree.FilePath}': {ex.Message}", ex);
                    }
                    finally
                    {
                        syntaxTree.Dispose(); // Free native TreeSitter memory immediately after Pass 2
                    }
                }

                return ValueTask.CompletedTask;
            });

            for (int i = 0; i < projectFiles.Count; i++)
            {
                var item = parsedResults[i];
                if (item == null) continue;

                var (file, syntaxTree, parentNode) = item.Value;

                // Replace the empty FileNode in physical tree with the parsed FileNode
                var idx = parentNode.Children.FindIndex(c => c.Id == file.Id);
                if (idx >= 0)
                {
                    parentNode.Children[idx] = syntaxTree.FileNode;
                }

                // Add top-level symbols to project syntax node
                foreach (var child in syntaxTree.FileNode.Children)
                {
                    if (child is TypeNode || child is FunctionNode || child is MemberNode)
                    {
                        projectSyntaxNode.Children.Add(child);
                    }
                    else if (child is EndpointNode or EntryPointNode)
                    {
                        foreach (var grandChild in child.Children)
                        {
                            if (grandChild is TypeNode || grandChild is FunctionNode || grandChild is MemberNode)
                            {
                                projectSyntaxNode.Children.Add(grandChild);
                            }
                        }
                    }
                }

                syntaxTrees.Add(syntaxTree);
                rawImports.AddRange(syntaxTree.RawImports);
                rawVariables.AddRange(syntaxTree.RawVariables);
                rawTypeBindings.AddRange(syntaxTree.RawTypeBindings);

                // Add to global lists in context for late binding / indexing compatibility
                ctx.RawImports.AddRange(syntaxTree.RawImports);
                ctx.RawVariables.AddRange(syntaxTree.RawVariables);
                ctx.RawTypeBindings.AddRange(syntaxTree.RawTypeBindings);
            }
        }

        ctx.Log($"[Layer3] Syntactic parsing pass complete. Parsed {syntaxTrees.Count} AST trees.");
        return new Layer3Result(
            l2Result,
            syntaxStructureNode,
            syntaxTrees,
            rawImports,
            rawVariables,
            rawTypeBindings,
            globalReferences,
            globalSymbols
        );
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IFileParser, (List<ISemanticExtension> Active, SemanticExtensionRegistry Registry)> _parserRegistryCache = new();

    public static void ProcessVisitor(
        SyntaxTree syntaxTree,
        string workspaceId,
        string absoluteWorkspacePath,
        ParsingContext? ctx = null,
        string? projectName = null,
        bool isTest = false,
        string? projectKind = null)
    {
        if (syntaxTree.Tree == null) return;

        var fileParser = syntaxTree.FileParser;
        var relativePath = syntaxTree.RelativePath;

        var (cachedActive, registry) = _parserRegistryCache.GetOrAdd(fileParser, fp =>
        {
            var active = fp.SemanticExtensions.Where(lp => lp.IsImplemented && lp.IsBuiltIn).ToList();
            var reg = new SemanticExtensionRegistry(fp.SemanticExtensions);
            return (active, reg);
        });

        var activeSemanticExtensions = new List<ISemanticExtension>(cachedActive);

        var mainVisitor = fileParser.CreateVisitor(syntaxTree.Tree.RootNode, activeSemanticExtensions, relativePath,
            absoluteWorkspacePath, fileParser, registry);
        mainVisitor.ProjectName = projectName;
        mainVisitor.Context = ctx;
        mainVisitor.IsTestFile = isTest || (syntaxTree.FileNode?.IsTest ?? false);

        mainVisitor.Visit(syntaxTree.Tree.RootNode);

        var effectiveIsTest = mainVisitor.IsTestFile;
        var effectiveProjectKind = projectKind ?? syntaxTree.FileNode?.ProjectKind;
        var effectiveProjectName = projectName ?? syntaxTree.FileNode?.ProjectName;

        var rootCounts = new Dictionary<(string Kind, string Name), int>();
        foreach (var childSyntactic in mainVisitor.RootSymbol.Children)
        {
            var key = (childSyntactic.Kind, childSyntactic.Name);
            rootCounts.TryGetValue(key, out var count);
            count++;
            rootCounts[key] = count;

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, Path.GetFileName(syntaxTree.FilePath),
                relativePath, workspaceId, syntaxTree.FileNode!.Id, ctx, count > 1 ? count : 0, effectiveProjectName, effectiveIsTest, effectiveProjectKind);
            syntaxTree.FileNode!.Children.Add(childNode);
        }

        foreach (var reference in mainVisitor.RootSymbol.References)
        {
            syntaxTree.FileNode!.References.Add(reference with { ScopeSymbolId = syntaxTree.FileNode!.Id });
        }

        var rawImports = mainVisitor.RawImports.Select(ri => ri with
        {
            FilePath = relativePath,
            Type = fileParser.ResolveImportType(ri.Path, relativePath, absoluteWorkspacePath)
        }).ToList();

        var rawVariables = mainVisitor.RawVariables.Select(rv => rv with { FilePath = relativePath }).ToList();
        var rawTypeBindings = mainVisitor.RawTypeBindings.Select(rt => rt with { FilePath = relativePath }).ToList();

        syntaxTree.RawImports.AddRange(rawImports);
        syntaxTree.RawVariables.AddRange(rawVariables);
        syntaxTree.RawTypeBindings.AddRange(rawTypeBindings);
    }

    private static IOntologyNode MapSyntacticSymbolToOntology(
        SyntacticSymbol syntactic,
        string fileName,
        string relativePath,
        string workspaceId,
        string parentScopeId,
        ParsingContext? ctx = null,
        int overloadIndex = 0,
        string? projectName = null,
        bool isTest = false,
        string? projectKind = null)
    {
        var node = syntactic.Node;
        var kind = syntactic.Kind;
        var name = syntactic.Name;

        var symbolIsTest = isTest ||
            (syntactic.Properties.TryGetValue(OntologyConstants.PropertyNames.IsTest, out var propIsTest) && bool.TryParse(propIsTest, out var b) && b);
        var stageOrder = syntactic.Properties.TryGetValue("stage_order", out var so) && int.TryParse(so, out var parsedOrder) ? parsedOrder : (int?)null;

        var isType = kind is "Class" or "Interface" or "Struct" or "Enum" or OntologyConstants.NodeLabels.Type;
        var mappedKind = isType ? "Type" : kind;
        var idSuffix = overloadIndex > 1 ? $"#{overloadIndex}" : "";
        var symbolId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:{mappedKind}:{name}{idSuffix}";

        IOntologyNode typedNode;
        if (kind is "Class" or "Struct" or "Enum" || kind == OntologyConstants.NodeLabels.Type)
        {
            var typeKind = syntactic.Properties.TryGetValue("type_kind", out var tk)
                ? tk
                : (kind == OntologyConstants.NodeLabels.Type ? "class" : kind.ToLowerInvariant());
            typedNode = new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, typeKind,
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind == "Interface")
        {
            typedNode = new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, "interface",
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind == OntologyConstants.NodeLabels.Function)
        {
            typedNode = new FunctionNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1,
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind == OntologyConstants.NodeLabels.Query)
        {
            if (WorkspaceConventions.IsFrontendFilePath(relativePath))
            {
                // Frontend UI strings containing SQL keywords must not be treated as SQL queries or linked to DBs
                var litId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CallLiteral}:{relativePath}:{node.StartPosition.Row + 1}:{node.StartPosition.Column + 1}";
                typedNode = new CallLiteralNode(litId, name, syntactic.Text ?? node.Text, "", 0, relativePath, node.StartPosition.Row + 1, node.StartPosition.Column + 1, symbolIsTest, projectKind, projectName, "UI", false);
            }
            else
            {
                var sqlCandidate = syntactic.Text ?? node.Text;
                if (!NestedSqlParser.TryParseSql(sqlCandidate, out _, out _))
                {
                    if (name.Contains(':'))
                    {
                        var afterColon = name[(name.IndexOf(':') + 1)..].Trim();
                        if (NestedSqlParser.TryParseSql(afterColon, out _, out _))
                        {
                            sqlCandidate = afterColon;
                        }
                    }
                }
                var qStartLine = (int)node.StartPosition.Row + 1;
                var qEndLine = (int)node.EndPosition.Row + 1;
                typedNode = NestedSqlParser.ParseNestedSql(sqlCandidate, symbolId, relativePath, ctx, name, qStartLine, qEndLine) ??
                            new QueryNode(symbolId, name, NestedSqlParser.CleanQueryText(sqlCandidate), relativePath, qStartLine, qEndLine);
            }
        }
        else if (kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var colonIdx = name.IndexOf(':');
            var isEndpoint = false;
            if (colonIdx > 0)
            {
                var method = name[..colonIdx].ToUpperInvariant();
                isEndpoint = method is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "HEAD" or "GRAPHQL" or "RPC" or "GRPC" or "QUERY" or "MUTATION" or "SUBSCRIPTION";
            }
            if (syntactic.Protocol is "GraphQL" or "gRPC" or "REST")
            {
                isEndpoint = true;
            }

            if (isEndpoint)
            {
                typedNode = CreateEndpointNode(name, node, relativePath, workspaceId, syntactic, projectName);
            }
            else
            {
                typedNode = CreateEntryPointNode(name, node, relativePath, workspaceId, projectName);
            }
        }
        else if (kind == OntologyConstants.NodeLabels.ExternalService)
        {
            typedNode = CreateExternalServiceNode(name, node, relativePath, workspaceId, parentScopeId);
            if (!string.IsNullOrEmpty(parentScopeId))
            {
                ctx?.AddGlobalProjectDependency(new Relationship(
                    parentScopeId,
                    typedNode.Id,
                    OntologyConstants.Relationships.ServiceCall,
                    new Dictionary<string, object>
                    {
                        ["dependency_type"] = "service_call",
                        ["is_external"] = "true"
                    }
                ));
            }
        }
        else if (kind == OntologyConstants.NodeLabels.Table)
        {
            var resolvedName = name;
            if (ConstantRegistry.TryResolve(relativePath, name, out var rName))
            {
                if (!string.IsNullOrWhiteSpace(rName) &&
                    !rName.Contains("://") &&
                    !rName.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    !rName.Contains('/') &&
                    !rName.Contains(' '))
                {
                    resolvedName = rName;
                }
            }
            var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{resolvedName.ToLowerInvariant()}";
            typedNode = new TableNode(tableId, resolvedName, relativePath);
        }
        else if (kind == OntologyConstants.NodeLabels.Topic)
        {
            var broker = syntactic.Properties.GetValueOrDefault("broker_type") ?? "event";
            var topicId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Topic}:{broker}:{name.ToLowerInvariant()}";
            typedNode = new TopicNode(topicId, name, relativePath, broker);
        }
        else if (kind == OntologyConstants.NodeLabels.CallLiteral)
        {
            typedNode = CreateCallLiteralNode(name, node, relativePath, workspaceId, syntactic, symbolIsTest, projectKind, projectName);
        }
        else if (kind == OntologyConstants.NodeLabels.CatchClause)
        {
            typedNode = CreateCatchClauseNode(name, node, relativePath, workspaceId, syntactic, symbolIsTest, projectKind, projectName);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported symbol type: {kind}");
        }

        var childCounts = new Dictionary<(string Kind, string Name), int>();
        foreach (var childSyntactic in syntactic.Children)
        {
            if (syntactic.Properties.TryGetValue("graphql_field", out var parentGqlField))
            {
                childSyntactic.Properties.TryAdd("graphql_field", parentGqlField);
            }
            if (syntactic.Properties.TryGetValue("graphql_schema", out var parentGqlSchema))
            {
                childSyntactic.Properties.TryAdd("graphql_schema", parentGqlSchema);
            }

            var key = (childSyntactic.Kind, childSyntactic.Name);
            childCounts.TryGetValue(key, out var count);
            count++;
            childCounts[key] = count;

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, fileName, relativePath, workspaceId, symbolId, ctx, count > 1 ? count : 0, projectName, symbolIsTest, projectKind);
            typedNode.Children.Add(childNode);
        }

        foreach (var reference in syntactic.References)
        {
            var target = reference.TargetName;
            if (reference.Kind == OntologyConstants.Relationships.PersistedIn &&
                ConstantRegistry.TryResolve(relativePath, target, out var resolvedTarget))
            {
                target = resolvedTarget;
            }
            var resolvedScopeId = string.IsNullOrEmpty(reference.ScopeSymbolId) ? typedNode.Id : reference.ScopeSymbolId;
            typedNode.References.Add(reference with { ScopeSymbolId = resolvedScopeId, TargetName = target });
        }

        foreach (var (k, v) in syntactic.Properties)
        {
            typedNode.SetExtension(k, v);
        }

        return typedNode;
    }
}
