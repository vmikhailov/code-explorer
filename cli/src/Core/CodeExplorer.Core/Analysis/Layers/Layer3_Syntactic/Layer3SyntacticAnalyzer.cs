using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
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

    public static Action<SyntaxTree, ParsingContext?>? SemanticPostProcessor { get; set; }

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

        if (syntaxTree.FileNode != null)
        {
            if (!string.IsNullOrEmpty(projectName)) syntaxTree.FileNode.ProjectName = projectName;
            if (!string.IsNullOrEmpty(projectKind)) syntaxTree.FileNode.ProjectKind = projectKind;
        }

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
        syntaxTree.RootSymbol = mainVisitor.RootSymbol;

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
                relativePath, workspaceId, syntaxTree.FileNode!.Id, ctx, count > 1 ? count : 0, effectiveProjectName, effectiveIsTest, effectiveProjectKind, syntaxTree);
            if (childNode != null)
            {
                syntaxTree.FileNode!.Children.Add(childNode);
            }
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

        SemanticPostProcessor?.Invoke(syntaxTree, ctx);
    }

    private static IOntologyNode? MapSyntacticSymbolToOntology(
        SyntacticSymbol syntactic,
        string fileName,
        string relativePath,
        string workspaceId,
        string parentScopeId,
        ParsingContext? ctx = null,
        int overloadIndex = 0,
        string? projectName = null,
        bool isTest = false,
        string? projectKind = null,
        SyntaxTree? syntaxTree = null)
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

        IOntologyNode? typedNode = null;
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
        else if (kind == "Member" || kind == OntologyConstants.NodeLabels.Member)
        {
            var memberKind = syntactic.Properties.TryGetValue("member_kind", out var mk) ? mk : "field";
            var dataType = syntactic.Properties.TryGetValue("data_type", out var dt) ? dt : null;
            typedNode = new MemberNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1,
                memberKind, dataType);
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
            // Semantic symbols (EntryPoint, ExternalService, Table, Topic, Query)
            // are collected on the syntaxTree for Layer 4 Semantic Analysis
            syntactic.ParentScopeId = parentScopeId;
            syntaxTree?.SemanticSymbols.Add(syntactic);
            return null;
        }

        if (typedNode == null) return null;

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

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, fileName, relativePath, workspaceId, symbolId, ctx, count > 1 ? count : 0, projectName, symbolIsTest, projectKind, syntaxTree);
            if (childNode != null)
            {
                typedNode.Children.Add(childNode);
            }
        }

        foreach (var reference in syntactic.References)
        {
            var target = reference.TargetName;
            var resolvedScopeId = string.IsNullOrEmpty(reference.ScopeSymbolId) ? typedNode.Id : reference.ScopeSymbolId;
            typedNode.References.Add(reference with { ScopeSymbolId = resolvedScopeId, TargetName = target });
        }

        foreach (var (k, v) in syntactic.Properties)
        {
            typedNode.SetExtension(k, v);
        }

        return typedNode;
    }

    private static CallLiteralNode CreateCallLiteralNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol syntactic,
        bool symbolIsTest,
        string? projectKind,
        string? projectName)
    {
        var val = syntactic.Properties.GetValueOrDefault("value") ?? syntactic.Name;
        var callee = syntactic.Properties.GetValueOrDefault("callee") ?? "";
        var argIdx = int.TryParse(syntactic.Properties.GetValueOrDefault("arg_index"), out var ai) ? ai : 0;
        var line = int.TryParse(syntactic.Properties.GetValueOrDefault("line"), out var ln) ? ln : node.StartPosition.Row + 1;
        var col = int.TryParse(syntactic.Properties.GetValueOrDefault("column"), out var cl) ? cl : node.StartPosition.Column + 1;
        var role = syntactic.Properties.GetValueOrDefault("role") ?? "business";
        var isDecorator = bool.TryParse(syntactic.Properties.GetValueOrDefault("is_decorator"), out var isDec) && isDec;
        var litId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CallLiteral}:{relativePath}:{line}:{col}";

        return new CallLiteralNode(litId, name, val, callee, argIdx, relativePath, line, col, symbolIsTest, projectKind, projectName, role, isDecorator);
    }

    private static CatchClauseNode CreateCatchClauseNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol syntactic,
        bool symbolIsTest,
        string? projectKind,
        string? projectName)
    {
        var exType = syntactic.Properties.GetValueOrDefault("exception_type") ?? "";
        var isEmpty = bool.TryParse(syntactic.Properties.GetValueOrDefault("is_empty"), out var ie) && ie;
        var hasComment = bool.TryParse(syntactic.Properties.GetValueOrDefault("has_comment"), out var hc) && hc;
        var stmtCount = int.TryParse(syntactic.Properties.GetValueOrDefault("statement_count"), out var sc) ? sc : 0;
        var line = int.TryParse(syntactic.Properties.GetValueOrDefault("line"), out var ln) ? ln : node.StartPosition.Row + 1;
        var col = int.TryParse(syntactic.Properties.GetValueOrDefault("column"), out var cl) ? cl : node.StartPosition.Column + 1;
        var catchId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CatchClause}:{relativePath}:{line}:{col}";

        return new CatchClauseNode(catchId, name, exType, relativePath, line, col, isEmpty, hasComment, stmtCount, symbolIsTest, projectKind, projectName);
    }

    public static bool IsRouteOrConfigCandidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var p = path.ToLowerInvariant();
        return (p.EndsWith(".ts") || p.EndsWith(".js") || p.EndsWith(".cs") || p.EndsWith(".go") || p.EndsWith(".py") || p.EndsWith(".json")) &&
               (p.Contains("route") || p.Contains("const") || p.Contains("config") || p.Contains("api") || p.Contains("endpoint") || p.Contains("url") || p.Contains("env") ||
                p.Contains("client") || p.Contains("service") || p.Contains("importer") || p.Contains("catalog") || p.Contains("gateway") || p.Contains("http") ||
                p.Contains("main.") || p.Contains("app.") || p.Contains("bootstrap.") || p.Contains("index.") || p.Contains("program.") || p.Contains("startup."));
    }
}
