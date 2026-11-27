using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Parser.Components;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Core.Semantics;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer3SyntacticParser
{
    public Task<Layer3Result> ParseAsync(Layer2Result l2Result, Layer3Context ctx) =>
        ParseAsync(ctx);

    public async Task<Layer3Result> ParseAsync(Layer3Context ctx)
    {
        var sw = Stopwatch.StartNew();
        ctx.Layer3Log.ScanStarted();

        var state = await InitializeStreamingStateAsync(ctx);
        var syntaxTrees = new List<SyntaxTree>();

        await foreach (var (_, projectSyntaxNode, parsedFiles) in StreamProjectBatchesAsync(
                           ctx.Layer2,
                           ctx,
                           state.SyntaxStructure,
                           state.FilesByProjectId,
                           state.ParentByChildId,
                           state.PrunedFileIds,
                           ctx.CancellationToken))
        {
            for (var i = 0; i < parsedFiles.Count; i++)
            {
                var (file, syntaxTree, parentNode) = parsedFiles[i];
                AttachParsedSyntaxTreeToHierarchy(projectSyntaxNode, file, syntaxTree, parentNode, ctx);
                syntaxTrees.Add(syntaxTree);
            }
        }

        await FinalizeStreamingStateAsync(ctx, state, applyWorkspaceSymbolFilter: true);

        ctx.Layer3Log.ScanCompleted(syntaxTrees.Count, sw.ElapsedMilliseconds);
        return new Layer3Result(
            state.SyntaxStructure,
            syntaxTrees,
            ctx.RawImports,
            ctx.RawVariables,
            ctx.RawTypeBindings,
            ctx.GlobalReferences,
            ctx.GlobalSymbols,
            ctx.DiscoveredContentSkippedFiles);
    }

    internal static void AttachParsedSyntaxTreeToHierarchy(
        ProjectSyntaxNode projectSyntaxNode,
        FileNode file,
        SyntaxTree syntaxTree,
        IOntologyNode parentNode,
        Layer3Context ctx)
    {
        var idx = parentNode.Children.FindIndex(c => c.Id == file.Id);
        if (idx >= 0)
        {
            parentNode.Children[idx] = syntaxTree.FileNode;
        }

        foreach (var child in syntaxTree.FileNode.Children)
        {
            if (child is TypeNode or FunctionNode or MemberNode)
            {
                projectSyntaxNode.Children.Add(child);
            }
        }

        ctx.RawImports.AddRange(syntaxTree.RawImports);
        ctx.RawVariables.AddRange(syntaxTree.RawVariables);
        ctx.RawTypeBindings.AddRange(syntaxTree.RawTypeBindings);
        PipelineMemoryEvictor.ReleaseSyntaxTreeBuffersAfterLayer3(syntaxTree, ctx.LightweightAstScanMode);
    }

    internal async Task<Layer3StreamingState> InitializeStreamingStateAsync(Layer3Context ctx)
    {
        var l1Result = ctx.Layer1;
        var l2Result = ctx.Layer2;
        WorkspaceConventions.LoadFromWorkspace(l1Result.Workspace.Path);

        const string syntaxStructureLabel = "SyntaxStructure";
        var syntaxNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.SyntaxStructure}";
        var syntaxStructureNode = new SyntaxStructureNode(syntaxNodeId, syntaxStructureLabel, l1Result.Workspace.Path);
        l1Result.Workspace.Children.Add(syntaxStructureNode);
        ctx.SyntaxStructure = syntaxStructureNode;

        var parentByChildId = new Dictionary<string, IOntologyNode>();
        void IndexPhysicalTree(IOntologyNode parent)
        {
            foreach (var child in parent.Children)
            {
                parentByChildId[child.Id] = parent;
                IndexPhysicalTree(child);
            }
        }
        IndexPhysicalTree(l1Result.Workspace);

        var projectRels = new List<Relationship>();
        var filesByProjectId = new Dictionary<string, List<FileNode>>(l2Result.Projects.Count);
        var primaryOwnerByFilePath = new Dictionary<string, ProjectNode>(l1Result.Files.Count, StringComparer.OrdinalIgnoreCase);
        var fileNodeByPath = new Dictionary<string, FileNode>(l1Result.Files.Count, StringComparer.OrdinalIgnoreCase);

        Layer3ProjectFileStreamer.GroupFilesByEnclosingProject(
            l1Result,
            l2Result,
            filesByProjectId,
            primaryOwnerByFilePath,
            fileNodeByPath,
            projectRels);

        var prunedFileIds = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        ConfigurationIndex.Clear();
        ConstantRegistry.Clear();
        RouteDictionaryRegistry.Clear();

        var allProjectFileNodeIds = new HashSet<string>(filesByProjectId.Values.SelectMany(v => v).Select(f => f.Id));
        var workspaceRootFiles = l1Result.Files.Where(f => !allProjectFileNodeIds.Contains(f.Id)).ToList();
        foreach (var enrichment in ctx.WorkspaceEnrichments.OfType<ConfigurationFileInformation>())
        {
            ConfigurationIndex.Register(enrichment, null);
            if (enrichment.Values is List<ConfigValue> wsValues)
            {
                wsValues.Clear();
            }
        }

        foreach (var project in OrderProjectsTopologically(l2Result))
        {
            foreach (var enrichment in project.Enrichments.OfType<ConfigurationFileInformation>())
            {
                ConfigurationIndex.Register(enrichment, project.Name);
                if (enrichment.Values is List<ConfigValue> projValues)
                {
                    projValues.Clear();
                }
            }
        }
        ctx.Layer3Log.ConfigurationIndexRegistered(ConfigurationIndex.GetDiscoveredUrls().Count);

        if (!ctx.LightweightAstScanMode)
        {
            var allCandidateFiles = new List<(string FullPath, string? ProjectName)>();
            foreach (var wfile in workspaceRootFiles)
            {
                if (IsRouteOrConfigCandidate(wfile.Path) && !SharedFileParserRegistry.CanParse(wfile.Name))
                {
                    allCandidateFiles.Add((wfile.FullPath, null));
                }
            }

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ctx.CancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            await Parallel.ForEachAsync(allCandidateFiles, parallelOptions, async (item, ct) =>
            {
                var (fPath, pName) = item;
                try
                {
                    var content = await FileStreamReader.ReadTextOnceAsync(fPath, ct);
                    if (string.IsNullOrEmpty(content) || content.Length > 1024 * 1024) return;
                    RouteDictionaryRegistry.ScanServiceDomains(content);
                    RouteDictionaryRegistry.ScanAndRegister(content);
                    ConstantRegistry.ScanAndRegister(fPath, content, pName);
                    AstConstantExtractor.ExtractAndRegister(fPath, content, pName);

                    var fileParser = WorkspaceIndexer.GetParserForFile(fPath);
                    fileParser?.DiscoverDeclarations(fPath, content, pName, ctx.Semantic);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    const string layer3SyntacticStage = "Layer3_Syntactic";
                    const string preScanErrorCode = "pre_scan_error";
                    const string routeConfigPreScanErrorName = "RouteConfigPreScanError";
                    ctx.Layer3Log.RouteConfigPreScanFailed(fPath, ex);
                    ctx.RecordScanIssue(layer3SyntacticStage, preScanErrorCode, fPath, ex.Message, name: routeConfigPreScanErrorName, exception: ex);
                }
            });
        }

        return new Layer3StreamingState(
            syntaxStructureNode,
            parentByChildId,
            filesByProjectId,
            primaryOwnerByFilePath,
            fileNodeByPath,
            workspaceRootFiles,
            projectRels,
            prunedFileIds);
    }

    internal async Task FinalizeStreamingStateAsync(
        Layer3Context ctx,
        Layer3StreamingState state,
        bool applyWorkspaceSymbolFilter)
    {
        Layer3ProjectFileStreamer.PruneSkippedFilesAndEnqueueContains(
            ctx.Layer1,
            state.ParentByChildId,
            state.FileNodeByPath,
            state.PrimaryOwnerByFilePath,
            state.ProjectRelationships,
            state.PrunedFileIds);
        if (state.ProjectRelationships.Count > 0)
        {
            ctx.TreeRelationships.AddRange(state.ProjectRelationships);
            await ctx.EnqueueUploadRelationshipsAsync(state.ProjectRelationships);
            ctx.AddRelsCount(state.ProjectRelationships.Count);
        }

        if (applyWorkspaceSymbolFilter &&
            !ctx.LightweightAstScanMode &&
            !ctx.IsSubtreeScan &&
            ctx.IncrementalChangedFiles == null)
        {
            ctx.HasWorkspaceSymbolFilter = true;
        }

        var sharedContainsRels = ResolveCrossProjectSharedFileContains(
            ctx.RawImports,
            state.PrimaryOwnerByFilePath,
            state.FileNodeByPath,
            state.ProjectRelationships);
        if (sharedContainsRels.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(sharedContainsRels);
            ctx.AddRelsCount(sharedContainsRels.Count);
        }
    }

    internal static async IAsyncEnumerable<(ProjectNode Project, ProjectSyntaxNode ProjectSyntaxNode, List<(FileNode File, SyntaxTree SyntaxTree, IOntologyNode ParentNode)> ParsedFiles)> StreamProjectBatchesAsync(
        Layer2Result l2Result,
        Layer3Context ctx,
        SyntaxStructureNode syntaxStructureNode,
        Dictionary<string, List<FileNode>> filesByProjectId,
        Dictionary<string, IOntologyNode> parentByChildId,
        ConcurrentDictionary<string, byte> prunedFileIds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var totalProjects = l2Result.Projects.Count;
        var nProject = 0;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = ctx.LightweightAstScanMode
                ? Math.Min(Environment.ProcessorCount, 2)
                : Environment.ProcessorCount
        };

        foreach (var project in OrderProjectsTopologically(l2Result))
        {
            nProject++;
            cancellationToken.ThrowIfCancellationRequested();

            const string projectSyntaxName = "ProjectSyntax";
            const string layer3SyntacticSource = "Layer3_Syntactic";
            const string parseExceptionCode = "parse_exception";
            const string parseExceptionName = "ParseException";
            const string layer3VisitorSource = "Layer3_Visitor";
            const string visitorExceptionCode = "visitor_exception";
            const string visitorExceptionName = "VisitorException";
            const string errorSeverity = "error";

            var projectSyntaxId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{project.Path}:syntax";
            var projectSyntaxNode = new ProjectSyntaxNode(projectSyntaxId, projectSyntaxName, project.Path);
            syntaxStructureNode.Children.Add(projectSyntaxNode);

            var belongsToRel = Relationship.FromRelationship(new BelongsToRelationship(projectSyntaxId, project.Id));
            await ctx.EnqueueUploadRelationshipsAsync([belongsToRel]);
            ctx.AddRelsCount(1);

            filesByProjectId.TryGetValue(project.Id, out var projectFiles);
            if (projectFiles != null && projectFiles.Count > 0 && ctx.CustomProjectFileFilter != null)
            {
                projectFiles = ctx.CustomProjectFileFilter(projectFiles, project, l2Result).ToList();
            }

            var projectFileCount = projectFiles?.Count ?? 0;
            if (ctx.IncrementalChangedFiles != null && projectFileCount == 0)
            {
                continue;
            }

            var progressPct = totalProjects > 0 ? 14 + (int)(38.0 * nProject / totalProjects) : 52;
            const string layer3StageName = "Layer 3: Syntactic";
            ctx.ReportProgress(progressPct, layer3StageName, $"Scanning project {nProject}/{totalProjects}: {project.Name} ({projectFileCount} files)", nProject, totalProjects);
            ctx.Layer3Log.ParsingProject(nProject, totalProjects, project.Name, projectFileCount);

            if (projectFiles == null || projectFileCount == 0)
            {
                yield return (project, projectSyntaxNode, []);
                continue;
            }

            var parsedResults = new (FileNode File, SyntaxTree SyntaxTree, IOntologyNode ParentNode)?[projectFileCount];
            var lptOrder = new int[projectFileCount];
            for (var idx = 0; idx < projectFileCount; idx++)
            {
                lptOrder[idx] = idx;
            }
            Array.Sort(lptOrder, (a, b) => (projectFiles[b].SizeBytes ?? 0L).CompareTo(projectFiles[a].SizeBytes ?? 0L));

            // Step 1 (Project AST -> UAST & Declarations): Parse syntax trees into UAST and register project constants/declarations (largest files first to avoid tail stragglers)
            await Parallel.ForAsync(0, projectFileCount, parallelOptions, async (slot, ct) =>
            {
                var i = lptOrder[slot];
                var file = projectFiles[i];
                var fileParser = WorkspaceIndexer.GetParserForFile(file.FullPath);
                if (fileParser == null)
                {
                    file.PreloadedContent = null;
                    return;
                }

                if (!parentByChildId.TryGetValue(file.Id, out var parentNode)) return;
                var parentId = parentNode.Id;

                var streamedOk = await Layer3ProjectFileStreamer.EnsureSourceFileStreamedAsync(file, ctx, prunedFileIds, ct);
                if (!streamedOk) return;

                try
                {
                    var syntaxTree = await fileParser.ParseAsync(
                        file.FullPath,
                        parentId,
                        ctx.WorkspaceId,
                        ctx.AbsoluteWorkspacePath,
                        file.PreloadedContent);
                    syntaxTree.FileNode.IsTest = file.IsTest;
                    syntaxTree.FileNode.ProjectKind = file.ProjectKind;
                    syntaxTree.FileNode.ProjectName = file.ProjectName;
                    syntaxTree.FileNode.LineCount = file.LineCount;
                    syntaxTree.FileNode.SizeBytes = file.SizeBytes;
                    syntaxTree.FileNode.ContentHash = file.ContentHash;
                    syntaxTree.FileNode.LastModifiedUtc = file.LastModifiedUtc;
                    syntaxTree.FileNode.ArtifactRole = file.ArtifactRole;
                    Layer3FileSignalEnricher.EnrichFileSemanticSignalsAndRouteConstants(file, syntaxTree, project, ctx, IsRouteOrConfigCandidate);
                    if (syntaxTree.Tree != null && !ctx.LightweightAstScanMode)
                    {
                        AstConstantExtractor.ExtractAndRegister(syntaxTree, project.Name);

                        var isTestSource = syntaxTree.FileNode.IsTest || WorkspaceConventions.IsTestFilePath(file.Path);
                        if (!isTestSource || project.IsTest)
                        {
                            for (var v = 0; v < syntaxTree.RawVariables.Count; v++)
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
                                fileParser.DiscoverDeclarations(syntaxTree.Tree.RootNode, file.Path, project.Name, ctx.Semantic);
                            }
                        }
                    }

                    parsedResults[i] = (file, syntaxTree, parentNode);

                    if (!ctx.LightweightAstScanMode)
                    {
                        RecordDocumentDiagnostics(syntaxTree, file, ctx.Scope, ctx.Sink);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ctx.Layer3Log.FileParseFailed(file.Path, ex.Message, ex);
                    ctx.RecordScanIssue(layer3SyntacticSource, parseExceptionCode, file.Path, ex.Message, name: parseExceptionName, severity: errorSeverity, exception: ex, language: fileParser.LanguageName, parentNodeId: file.Id, isFileParent: true);
                }
            });

            // Step 2 (Immediate Project UAST Visitors & Component Parsers): Run language visitors and component parsers immediately on the project's UASTs
            var projectAbsDir = Path.Combine(ctx.AbsoluteWorkspacePath, project.Path).NormalizeFullPath();
            var filesInProjectDir = ctx.DirectoryIndex.GetDirectoryFiles(projectAbsDir);
            var projectContext = new ProjectContext(
                projectAbsDir,
                project.Path,
                project.Name,
                project.ProjectType,
                filesInProjectDir,
                project.Children.OfType<PackageNode>().Select(p => p.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                project.TryGetSignals()?.ManifestHints ?? new Dictionary<string, string>());
            var componentParsers = ctx.LightweightAstScanMode
                ? []
                : ComponentLibraryParserRegistry.GetParsers(projectContext);

            var projectGlobalImports = new List<string>(projectContext.Dependencies);
            if (!ctx.LightweightAstScanMode)
            {
                for (var pi = 0; pi < projectFileCount; pi++)
                {
                    var pItem = parsedResults[pi];
                    if (pItem?.SyntaxTree.Document is not { } pDoc || pDoc.Imports.Count == 0) continue;
                    var isGlobalUsingsFile = pItem.Value.File.Name.Contains("GlobalUsings", StringComparison.OrdinalIgnoreCase);
                    for (var im = 0; im < pDoc.Imports.Count; im++)
                    {
                        var impDecl = pDoc.Imports[im];
                        if (!string.IsNullOrWhiteSpace(impDecl.ModulePath) &&
                            (isGlobalUsingsFile || impDecl.AsSpan().TrimStart().StartsWith("global ", StringComparison.Ordinal)))
                        {
                            projectGlobalImports.Add(impDecl.ModulePath);
                        }
                    }
                }
            }

            await Parallel.ForAsync(0, projectFileCount, parallelOptions, (int slot, CancellationToken ct) =>
            {
                var i = lptOrder[slot];
                var item = parsedResults[i];
                if (item == null) return ValueTask.CompletedTask;

                var (file, syntaxTree, _) = item.Value;
                if (syntaxTree.Tree != null)
                {
                    try
                    {
                        var isTestFile = file.IsTest;
                        for (var cp = 0; !isTestFile && cp < componentParsers.Count; cp++)
                        {
                            isTestFile = componentParsers[cp].IsTestSource(syntaxTree);
                        }
                        if (isTestFile)
                        {
                            file.IsTest = true;
                            syntaxTree.FileNode.IsTest = true;
                        }

                        ProcessVisitor(
                            syntaxTree,
                            ctx.WorkspaceId,
                            ctx.AbsoluteWorkspacePath,
                            ctx.Semantic,
                            project.Name,
                            isTestFile,
                            project.EntityKind != ProjectEntityKind.Unknown ? project.EntityKind : null,
                            projectGlobalImports,
                            ctx.Symbols,
                            ctx.Sink);

                        if (componentParsers.Count > 0)
                        {
                            for (var cp = 0; cp < componentParsers.Count; cp++)
                            {
                                componentParsers[cp].EnrichWithSyntax(null, syntaxTree, ctx.Semantic);
                            }
                        }

                        Layer3ScopeVisibilityPass.RegisterSyntaxTreeWorkspaceSymbols(syntaxTree, ctx.Symbols);
                        PipelineMemoryEvictor.TryReleaseDocumentEarlyInLayer3(syntaxTree, ctx.LightweightAstScanMode);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        ctx.Layer3Log.VisitorFailed(syntaxTree.FilePath, ex.Message, ex);
                        ctx.RecordScanIssue(layer3VisitorSource, visitorExceptionCode, file.Path, ex.Message, name: visitorExceptionName, severity: errorSeverity, exception: ex, language: syntaxTree.FileParser.LanguageName, parentNodeId: file.Id, isFileParent: true);
                    }
                }

                return ValueTask.CompletedTask;
            });

            var batch = new List<(FileNode File, SyntaxTree SyntaxTree, IOntologyNode ParentNode)>(projectFileCount);
            for (var i = 0; i < projectFileCount; i++)
            {
                var item = parsedResults[i];
                if (item == null) continue;
                batch.Add(item.Value);
            }

            yield return (project, projectSyntaxNode, batch);
        }
    }

    private static readonly ConcurrentDictionary<IFileParser, (List<ISemanticExtension> Active, SemanticExtensionRegistry Registry)> _parserRegistryCache = new();

    public static Action<SyntaxTree, SemanticParseContext?>? SemanticPostProcessor { get; set; } = static (tree, ctx) =>
        EndpointSemanticAnalyzer.Enrich(tree, null, ctx);

    public static void ProcessVisitor(
        SyntaxTree syntaxTree,
        string workspaceId,
        string absoluteWorkspacePath,
        SemanticParseContext? ctx = null,
        string? projectName = null,
        bool isTest = false,
        ProjectEntityKind? projectKind = null,
        IReadOnlyList<string>? projectGlobalImports = null,
        SymbolResolutionBuffer? symbols = null,
        GraphWriteSink? sink = null)
    {
        if (syntaxTree.Tree == null) return;

        if (syntaxTree.FileNode != null)
        {
            if (!string.IsNullOrEmpty(projectName)) syntaxTree.FileNode.ProjectName = projectName;
            if (projectKind.HasValue) syntaxTree.FileNode.ProjectKind = projectKind.Value;
        }

        var lightweightMode = ctx?.Scope.LightweightAstScanMode == true;
        if (!lightweightMode)
        {
            SyntaxValueEvaluator.EvaluateDocument(syntaxTree.Document, projectName);
            if (ctx != null && sink != null && syntaxTree.FileNode != null)
            {
                RecordDocumentDiagnostics(syntaxTree, syntaxTree.FileNode, ctx.Scope, sink);
            }
        }

        var fileParser = syntaxTree.FileParser;
        if (!fileParser.UsesTreeSitter)
        {
            Layer3ScopeVisibilityPass.EnrichNonTreeSitterTree(syntaxTree, projectName, isTest, symbols);
            SemanticPostProcessor?.Invoke(syntaxTree, ctx);
            return;
        }

        var relativePath = syntaxTree.RelativePath;

        var activeCatalog = ctx?.Scope.DetectorCatalog is { HasCustomDetectors: true } dc
            ? dc
            : WorkspaceDetectorCatalog.CurrentScope;
        var (cachedActive, registry) = activeCatalog is { HasCustomDetectors: true }
            ? activeCatalog.GetEffectiveRegistry(fileParser)
            : _parserRegistryCache.GetOrAdd(fileParser, fp =>
            {
                var active = fp.SemanticExtensions.Where(lp => lp.IsImplemented && lp.IsBuiltIn).ToList();
                var reg = new SemanticExtensionRegistry(fp.SemanticExtensions);
                return (active, reg);
            });

        if (lightweightMode)
        {
            if (syntaxTree.Document != null)
            {
                foreach (var call in syntaxTree.Document.AllCalls)
                {
                    if (call.MethodName is "require" or "__import__" or "import_module" &&
                        call.Arguments.Count > 0 &&
                        call.Arguments[0].Value is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String } strArg &&
                        !string.IsNullOrWhiteSpace(strArg.ValueText))
                    {
                        var importPath = strArg.ValueText;
                        syntaxTree.RawImports.Add(new RawImport(
                            importPath,
                            relativePath,
                            fileParser.ResolveImportType(importPath, relativePath, absoluteWorkspacePath)));
                    }
                }
            }
            SemanticPostProcessor?.Invoke(syntaxTree, ctx);
            return;
        }

        var activeSemanticExtensions = new List<ISemanticExtension>(cachedActive);

        var mainVisitor = fileParser.CreateVisitor(syntaxTree.Tree.RootNode, activeSemanticExtensions, relativePath,
            absoluteWorkspacePath, fileParser, registry);
        mainVisitor.ProjectName = projectName;
        mainVisitor.Context = ctx;
        mainVisitor.IsTestFile = isTest || (syntaxTree.FileNode?.IsTest ?? false);

        if (projectGlobalImports is { Count: > 0 })
        {
            for (var gi = 0; gi < projectGlobalImports.Count; gi++)
            {
                mainVisitor.ResolveAndInjectLibraryParser(projectGlobalImports[gi]);
            }
        }

        mainVisitor.Visit(syntaxTree.Tree.RootNode);
        syntaxTree.RootSymbol = mainVisitor.RootSymbol;

        var effectiveIsTest = mainVisitor.IsTestFile;
        var effectiveProjectKind = projectKind ?? syntaxTree.FileNode?.ProjectKind;
        var effectiveProjectName = projectName ?? syntaxTree.FileNode?.ProjectName;

        var fileNode = syntaxTree.FileNode;
        if (fileNode == null) return;

        var rawImports = mainVisitor.RawImports.Select(ri => ri with
        {
            FilePath = relativePath,
            Type = fileParser.ResolveImportType(ri.Path, relativePath, absoluteWorkspacePath)
        }).ToList();

        Layer3ScopeVisibilityPass.RegisterFileRawImports(symbols, relativePath, rawImports);

        var rawVariables = mainVisitor.RawVariables.Select(rv => rv with { FilePath = relativePath }).ToList();
        var rawTypeBindings = mainVisitor.RawTypeBindings.Select(rt => rt with { FilePath = relativePath }).ToList();

        syntaxTree.RawImports.AddRange(rawImports);
        syntaxTree.RawVariables.AddRange(rawVariables);
        syntaxTree.RawTypeBindings.AddRange(rawTypeBindings);

        var fileSymbolCounts = new Dictionary<(SyntacticSymbolKind Kind, string Name), int>();
        foreach (var childSyntactic in mainVisitor.RootSymbol.Children)
        {
            var key = (childSyntactic.Kind, childSyntactic.Name);
            fileSymbolCounts.TryGetValue(key, out var count);
            count++;
            fileSymbolCounts[key] = count;

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, Path.GetFileName(syntaxTree.FilePath),
                relativePath, workspaceId, fileNode.Id, symbols, count > 1 ? count : 0, effectiveProjectName, effectiveIsTest, effectiveProjectKind, syntaxTree, fileSymbolCounts);
            if (childNode != null)
            {
                fileNode.Children.Add(childNode);
            }
        }

        foreach (var reference in mainVisitor.RootSymbol.References)
        {
            fileNode.References.Add(reference with { ScopeSymbolId = fileNode.Id });
        }

        if (syntaxTree.FileNode != null && rawVariables.Count > 0)
        {
            PopulateMemberNodesFromRawVariables(syntaxTree.FileNode, rawVariables, workspaceId, relativePath);
        }

        if (syntaxTree.FileNode != null && syntaxTree.Document != null)
        {
            EnrichSyntacticParametersPrototypesAndExports(
                syntaxTree,
                workspaceId,
                relativePath,
                effectiveIsTest,
                effectiveProjectKind,
                effectiveProjectName);
            Layer3ScopeVisibilityPass.PopulateSymbolScopeMetadata(
                syntaxTree.FileNode,
                syntaxTree.Document,
                relativePath,
                effectiveProjectName,
                effectiveIsTest,
                symbols);
        }

        SemanticPostProcessor?.Invoke(syntaxTree, ctx);
    }

    private static void EnrichSyntacticParametersPrototypesAndExports(
        SyntaxTree syntaxTree,
        string workspaceId,
        string relativePath,
        bool isTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        var doc = syntaxTree.Document;
        var fileNode = syntaxTree.FileNode;
        if (doc == null || fileNode == null) return;

        var functionsByLineAndName = new Dictionary<(int Line, string Name), FunctionNode>();
        var typesByName = new Dictionary<string, TypeNode>(StringComparer.Ordinal);
        var membersByLineAndName = new Dictionary<(int Line, string Name), MemberNode>();

        void IndexOntologyTree(IOntologyNode parent, TypeNode? enclosingType)
        {
            foreach (var child in parent.Children)
            {
                if (child is TypeNode tn)
                {
                    typesByName.TryAdd(tn.Name, tn);
                    IndexOntologyTree(tn, tn);
                }
                else if (child is FunctionNode fn)
                {
                    if (enclosingType != null)
                    {
                        fn.Signals().EnclosingTypeName = enclosingType.Name;
                    }
                    functionsByLineAndName.TryAdd((fn.StartLine, fn.Name), fn);
                    IndexOntologyTree(fn, enclosingType);
                }
                else if (child is MemberNode mn)
                {
                    membersByLineAndName.TryAdd((mn.StartLine, mn.Name), mn);
                }
            }
        }
        IndexOntologyTree(fileNode, null);
        Layer3ScopeVisibilityPass.EnrichMemberDeclarationsFromDocument(doc, membersByLineAndName);

        var hasLateExportMarkers = doc.SourceText.Contains("module.exports", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("exports.", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("export {", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("export default ", StringComparison.Ordinal);

        // 1. Enrich all FunctionDeclSyntax nodes with export flags and dataflow signals
        foreach (var fnDecl in doc.AllFunctions.OrderBy(f => f.Span.StartLine))
        {
            if (fnDecl.Kind == FunctionSyntaxKind.Accessor)
            {
                continue;
            }

            var fnName = ExtractSyntaxFunctionName(fnDecl);
            if (fnName.StartsWith("<anon_", StringComparison.Ordinal) || fnName is "get;" or "set;" or "init;")
            {
                continue;
            }

            var startLine = fnDecl.Span.StartLine + 1;
            var endLine = fnDecl.Span.EndLine + 1;

            if (!functionsByLineAndName.TryGetValue((startLine, fnName), out var fnNode))
            {
                var fnId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{fnName}@{startLine}";
                fnNode = new FunctionNode(
                    fnId,
                    fnName,
                    fnId,
                    relativePath,
                    relativePath,
                    startLine,
                    endLine,
                    fnDecl.Span.StartColumn + 1,
                    fnDecl.Span.EndColumn + 1,
                    isTest,
                    projectKind,
                    projectName);

                var parentFnDecl = FindEnclosingSyntaxFunction(fnDecl);
                if (parentFnDecl != null &&
                    functionsByLineAndName.TryGetValue((parentFnDecl.Span.StartLine + 1, ExtractSyntaxFunctionName(parentFnDecl)), out var parentFnOnt))
                {
                    parentFnOnt.Children.Add(fnNode);
                }
                else
                {
                    var enclosingTypeDecl = FindEnclosingSyntaxType(fnDecl);
                    var enclosingTypeName = enclosingTypeDecl != null
                        ? (!string.IsNullOrWhiteSpace(enclosingTypeDecl.Name)
                            ? enclosingTypeDecl.Name
                            : (enclosingTypeDecl.GetChildForField(SyntaxFields.Name) ?? enclosingTypeDecl.FirstChildOfType<IdentifierExprSyntax>())?.Text.Trim())
                        : null;
                    if (!string.IsNullOrEmpty(enclosingTypeName) && typesByName.TryGetValue(enclosingTypeName, out var enclosingTypeNode))
                    {
                        enclosingTypeNode.Children.Add(fnNode);
                        fnNode.Signals().EnclosingTypeName = enclosingTypeNode.Name;
                    }
                    else
                    {
                        fileNode.Children.Add(fnNode);
                    }
                }
                functionsByLineAndName[(startLine, fnName)] = fnNode;
            }

            if (IsSyntaxFunctionExported(fnDecl, doc, hasLateExportMarkers))
            {
                fnNode.IsExported = true;
                fnNode.Flags |= SymbolFlags.IsExported;
            }

            var parentDecl = FindEnclosingSyntaxFunction(fnDecl);
            if (parentDecl != null &&
                functionsByLineAndName.TryGetValue((parentDecl.Span.StartLine + 1, ExtractSyntaxFunctionName(parentDecl)), out var parentOnt) &&
                (parentOnt.IsExported == true || parentOnt.Flags.HasFlag(SymbolFlags.IsExported)))
            {
                fnNode.IsExported = true;
                fnNode.Flags |= SymbolFlags.IsExported;
            }

            if (fnDecl.DataflowSummary != null)
            {
                var fnSignals = fnNode.Signals();
                if (fnDecl.DataflowSummary.ReturnOrigins.Count > 0)
                {
                    fnSignals.DataflowReturn = string.Join(",", fnDecl.DataflowSummary.ReturnOrigins.Select(o => o.ToString()).Distinct());
                }
                if (fnDecl.DataflowSummary.FieldWrites.Count > 0)
                {
                    var writes = fnDecl.DataflowSummary.FieldWrites
                        .Select(kv => $"{kv.Key}<-{string.Join(",", kv.Value.Select(o => o.ToString()).Distinct())}");
                    fnSignals.DataflowWrites = string.Join(";", writes);
                }
                var summaryStr = fnDecl.DataflowSummary.ToCompactString();
                if (!string.IsNullOrEmpty(summaryStr))
                {
                    fnSignals.DataflowSummary = summaryStr;
                }
            }
        }

        // 2. Support ES5 Prototype method assignments (`ClassName.prototype.methodName = function(...)`)
        if (syntaxTree.RootNode != null)
        {
            if (doc.SourceText.Contains(".prototype.", StringComparison.Ordinal))
            {
                foreach (var assignExpr in EnumerateSyntaxAssignments(syntaxTree.RootNode))
                {
                    if (!TryParseSyntaxPrototypeMethod(assignExpr, out var protoClass, out var protoMethod, out var protoLambda))
                    {
                        continue;
                    }

                    if (!typesByName.TryGetValue(protoClass, out var typeNode))
                    {
                        const string defaultProtoTypeKind = "class";
                        var typeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Type:{protoClass}";
                        typeNode = new TypeNode(
                            typeId,
                            protoClass,
                            typeId,
                            relativePath,
                            relativePath,
                            protoLambda.Span.StartLine + 1,
                            protoLambda.Span.EndLine + 1,
                            1,
                            1,
                            defaultProtoTypeKind,
                            isTest,
                            projectKind,
                            projectName)
                        {
                            IsExported = true,
                            Flags = SymbolFlags.IsExported
                        };
                        fileNode.Children.Add(typeNode);
                        typesByName[protoClass] = typeNode;
                    }

                    var startLine = protoLambda.Span.StartLine + 1;
                    if (!functionsByLineAndName.TryGetValue((startLine, protoMethod), out var protoFnNode))
                    {
                        var fnId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{protoClass}.{protoMethod}";
                        protoFnNode = new FunctionNode(
                            fnId,
                            protoMethod,
                            fnId,
                            relativePath,
                            relativePath,
                            startLine,
                            protoLambda.Span.EndLine + 1,
                            protoLambda.Span.StartColumn + 1,
                            protoLambda.Span.EndColumn + 1,
                            isTest,
                            projectKind,
                            projectName);
                        typeNode.Children.Add(protoFnNode);
                        functionsByLineAndName[(startLine, protoMethod)] = protoFnNode;
                    }

                    if (!typeNode.Children.Contains(protoFnNode))
                    {
                        fileNode.Children.Remove(protoFnNode);
                        typeNode.Children.Add(protoFnNode);
                    }
                    protoFnNode.Signals().EnclosingTypeName = protoClass;
                    protoFnNode.IsExported = true;
                    protoFnNode.Flags |= SymbolFlags.IsExported;
                }
            }

            foreach (var (lambdaName, lambdaNode) in EnumerateNamedLambdaFunctions(syntaxTree.RootNode))
            {
                if (lambdaName.StartsWith("<anon_", StringComparison.Ordinal) || FindEnclosingSyntaxFunction(lambdaNode) != null)
                {
                    continue;
                }

                var startLine = lambdaNode.Span.StartLine + 1;
                if (!functionsByLineAndName.TryGetValue((startLine, lambdaName), out var lambdaFnNode))
                {
                    var fnId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{lambdaName}@{startLine}";
                    lambdaFnNode = new FunctionNode(
                        fnId,
                        lambdaName,
                        fnId,
                        relativePath,
                        relativePath,
                        startLine,
                        lambdaNode.Span.EndLine + 1,
                        lambdaNode.Span.StartColumn + 1,
                        lambdaNode.Span.EndColumn + 1,
                        isTest,
                        projectKind,
                        projectName);
                    fileNode.Children.Add(lambdaFnNode);
                    functionsByLineAndName[(startLine, lambdaName)] = lambdaFnNode;
                }

                lambdaFnNode.IsExported = true;
                lambdaFnNode.Flags |= SymbolFlags.IsExported;
            }
        }

        // 3. Emit canonical `<module_init>` FunctionNode if top-level statements/calls/references exist
        var hasTopLevelMessagingRefs = fileNode.References.Any(r =>
            r.Kind is RelationshipKind.PublishesTo or RelationshipKind.SubscribesTo ||
            (r.Kind is RelationshipKind.ReadsFrom or RelationshipKind.WritesTo &&
             (string.IsNullOrEmpty(r.ScopeSymbolId) || r.ScopeSymbolId == fileNode.Id)));
        if (HasTopLevelCallsOrStatements(doc) || hasTopLevelMessagingRefs)
        {
            const string moduleInitFunctionName = "<module_init>";
            var modInitNode = fileNode.Children.OfType<FunctionNode>().FirstOrDefault(f => f.Name == moduleInitFunctionName);
            if (modInitNode == null)
            {
                var modInitId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{moduleInitFunctionName}";
                modInitNode = new FunctionNode(
                    modInitId,
                    moduleInitFunctionName,
                    modInitId,
                    relativePath,
                    relativePath,
                    1,
                    Math.Max(1, fileNode.LineCount ?? 1),
                    1,
                    1,
                    isTest,
                    projectKind,
                    projectName)
                {
                    IsModuleInit = true,
                    Flags = SymbolFlags.IsModuleInit
                };
                fileNode.Children.Add(modInitNode);
            }
            if (hasTopLevelMessagingRefs)
            {
                for (var rIdx = fileNode.References.Count - 1; rIdx >= 0; rIdx--)
                {
                    var fileRef = fileNode.References[rIdx];
                    var isTopLevelTableAccess = fileRef.Kind is RelationshipKind.ReadsFrom or RelationshipKind.WritesTo &&
                                                (string.IsNullOrEmpty(fileRef.ScopeSymbolId) || fileRef.ScopeSymbolId == fileNode.Id);
                    if (fileRef.Kind is not (RelationshipKind.PublishesTo or RelationshipKind.SubscribesTo) && !isTopLevelTableAccess)
                    {
                        continue;
                    }
                    var movedScope = string.IsNullOrEmpty(fileRef.ScopeSymbolId) || fileRef.ScopeSymbolId == fileNode.Id
                        ? modInitNode.Id
                        : fileRef.ScopeSymbolId;
                    modInitNode.References.Add(fileRef with { ScopeSymbolId = movedScope });
                    fileNode.References.RemoveAt(rIdx);
                }
            }
            PopulateModuleInitReferences(modInitNode, doc);
        }

        Layer3SemanticArgumentCollector.CollectForDocument(
            doc,
            fileNode,
            functionsByLineAndName,
            relativePath,
            projectName);

        Layer3ScopeTreeEnricher.EnrichFunctionsWithScopesAndComplexity(
            doc,
            fileNode,
            functionsByLineAndName,
            workspaceId,
            relativePath,
            isTest,
            projectKind,
            projectName);

        CallSiteReferenceEnricher.EnrichReferencesWithCallContextAndFlow(fileNode, doc);
    }
}
