using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer3SyntacticParser
{
    public async Task<Layer3Result> ParseAsync(Layer2Result l2Result, ParsingContext ctx)
    {
        ctx.Layer3Log.ScanStarted();

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
        // O(F * P) pre-grouping of files to enclosing projects (supports multiple sibling projects in the same directory)
        var filesByProjectId = new Dictionary<string, List<FileNode>>(l2Result.Projects.Count);
        var primaryOwnerByFilePath = new Dictionary<string, ProjectNode>(l2Result.Prev.Files.Count, StringComparer.OrdinalIgnoreCase);
        var fileNodeByPath = new Dictionary<string, FileNode>(l2Result.Prev.Files.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var file in l2Result.Prev.Files)
        {
            if (!string.IsNullOrEmpty(file.Path))
            {
                fileNodeByPath[file.Path.NormalizeSlashes().Trim('/')] = file;
            }

            int bestMatchLength = -1;
            var bestMatches = new List<ProjectNode>();
            foreach (var p in l2Result.Projects)
            {
                if (string.IsNullOrEmpty(p.Path))
                {
                    if (bestMatchLength < 0)
                    {
                        bestMatchLength = 0;
                        bestMatches.Clear();
                        bestMatches.Add(p);
                    }
                    else if (bestMatchLength == 0)
                    {
                        bestMatches.Add(p);
                    }
                    continue;
                }

                var normProjPath = p.Path.NormalizeSlashes().Trim('/');
                if (file.Path.Equals(normProjPath, StringComparison.OrdinalIgnoreCase))
                {
                    var exactLen = normProjPath.Length + 1;
                    if (exactLen > bestMatchLength)
                    {
                        bestMatchLength = exactLen;
                        bestMatches.Clear();
                        bestMatches.Add(p);
                    }
                    else if (exactLen == bestMatchLength)
                    {
                        bestMatches.Add(p);
                    }
                    continue;
                }

                var prefix = normProjPath + "/";
                if (file.Path.StartWithIC(prefix))
                {
                    if (prefix.Length > bestMatchLength)
                    {
                        bestMatchLength = prefix.Length;
                        bestMatches.Clear();
                        bestMatches.Add(p);
                    }
                    else if (prefix.Length == bestMatchLength)
                    {
                        bestMatches.Add(p);
                    }
                }
            }

            if (bestMatches.Count > 0)
            {
                var primaryMatch = bestMatches[0];
                var isTest = primaryMatch.IsTest || WorkspaceConventions.IsTestFilePath(file.Path);
                file.IsTest = isTest;
                file.ProjectKind = primaryMatch.EntityKind != ProjectEntityKind.Unknown ? primaryMatch.EntityKind : null;
                file.ProjectName = primaryMatch.Name;
                if (!string.IsNullOrEmpty(file.Path))
                {
                    primaryOwnerByFilePath[file.Path.NormalizeSlashes().Trim('/')] = primaryMatch;
                }

                if (!filesByProjectId.TryGetValue(primaryMatch.Id, out var primaryList))
                {
                    primaryList = [];
                    filesByProjectId[primaryMatch.Id] = primaryList;
                }
                primaryList.Add(file);

                foreach (var matchProj in bestMatches)
                {
                    projectRels.Add(new Relationship(matchProj.Id, file.Id, RelationshipKind.Contains));
                }
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

        // Phase 0: register configuration already parsed once by shared file parsers in Layer 2
        ConfigurationIndex.Clear();
        ConstantRegistry.Clear();
        RouteDictionaryRegistry.Clear();

        var allProjectFileNodeIds = new HashSet<string>(filesByProjectId.Values.SelectMany(v => v).Select(f => f.Id));
        var workspaceRootFiles = l2Result.Prev.Files.Where(f => !allProjectFileNodeIds.Contains(f.Id)).ToList();
        foreach (var enrichment in ctx.WorkspaceEnrichments.OfType<ConfigurationFileInformation>())
        {
            ConfigurationIndex.Register(enrichment, null);
        }



        foreach (var project in OrderProjectsTopologically(l2Result))
        {
            foreach (var enrichment in project.Enrichments.OfType<ConfigurationFileInformation>())
            {
                ConfigurationIndex.Register(enrichment, project.Name);
            }
        }
        ctx.Layer3Log.ConfigurationIndexRegistered(ConfigurationIndex.GetDiscoveredUrls().Count);

        // Phase 0b: RouteDictionaryRegistry and early constants pre-scanning across all candidate files
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

            foreach (var project in OrderProjectsTopologically(l2Result))
            {
                if (filesByProjectId.TryGetValue(project.Id, out var pFiles) && false)
                {
                    foreach (var pfile in pFiles)
                    {
                        if (IsRouteOrConfigCandidate(pfile.Path) && !SharedFileParserRegistry.CanParse(pfile.Name))
                        {
                            allCandidateFiles.Add((pfile.FullPath, project.Name));
                        }
                    }
                }
            }

            Parallel.ForEach(allCandidateFiles, new ParallelOptions { CancellationToken = ctx.CancellationToken }, item =>
            {
                var (fPath, pName) = item;
                try
                {
                    var content = PhysicalFileStore.ReadAllText(fPath);
                    if (string.IsNullOrEmpty(content) || content.Length > 1024 * 1024) return;
                    RouteDictionaryRegistry.ScanServiceDomains(content);
                    RouteDictionaryRegistry.ScanAndRegister(content);
                    ConstantRegistry.ScanAndRegister(fPath, content, pName);
                    AstConstantExtractor.ExtractAndRegister(fPath, content, pName);

                    var fileParser = WorkspaceIndexer.GetParserForFile(fPath);
                    fileParser?.DiscoverDeclarations(fPath, content, pName, ctx);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ctx.RecordScanIssue("Layer3_Syntactic", "pre_scan_error", fPath, ex.Message, name: "RouteConfigPreScanError", exception: ex);
                }
            });
        }

        await foreach (var (_, projectSyntaxNode, file, syntaxTree, parentNode) in StreamProjectSyntaxTreesAsync(
                           l2Result,
                           ctx,
                           syntaxStructureNode,
                           filesByProjectId,
                           parentByChildId,
                           ctx.CancellationToken))
        {
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

        if (!ctx.LightweightAstScanMode && !ctx.IsSubtreeScan)
        {
            ctx.HasWorkspaceSymbolFilter = true;
        }

        // Link shared files reused across projects via relative imports when no explicit Library project owns the target file
        var sharedContainsRels = ResolveCrossProjectSharedFileContains(rawImports, primaryOwnerByFilePath, fileNodeByPath, projectRels);
        if (sharedContainsRels.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(sharedContainsRels);
            ctx.AddRelsCount(sharedContainsRels.Count);
        }

        PhysicalFileStore.ClearWorkspace(ctx.AbsoluteWorkspacePath);
        ctx.Layer3Log.ScanCompleted(syntaxTrees.Count);
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

    private static async IAsyncEnumerable<(ProjectNode Project, ProjectSyntaxNode ProjectSyntaxNode, FileNode File, SyntaxTree SyntaxTree, IOntologyNode ParentNode)> StreamProjectSyntaxTreesAsync(
        Layer2Result l2Result,
        ParsingContext ctx,
        SyntaxStructureNode syntaxStructureNode,
        Dictionary<string, List<FileNode>> filesByProjectId,
        Dictionary<string, IOntologyNode> parentByChildId,
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
            var pctDone = totalProjects > 0 ? (int)Math.Round(nProject * 100.0 / totalProjects) : 100;
            var progressPct = totalProjects > 0 ? 14 + (int)(38.0 * nProject / totalProjects) : 52;
            const string layer3StageName = "Layer 3: Syntactic";
            ctx.ReportProgress(progressPct, layer3StageName, $"Scanning project {nProject}/{totalProjects} ({pctDone}%): {project.Name} ({projectFileCount} files)", nProject, totalProjects);
            ctx.Layer3Log.ParsingProject(nProject, totalProjects, pctDone, project.Name, projectFileCount);

            if (projectFiles == null || projectFileCount == 0)
            {
                continue;
            }

            var parsedResults = new (FileNode File, SyntaxTree SyntaxTree, IOntologyNode ParentNode)?[projectFileCount];

            // Step 1 (Project AST -> UAST & Declarations): Parse syntax trees into UAST and register project constants/declarations
            await Parallel.ForAsync(0, projectFileCount, parallelOptions, async (i, ct) =>
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
                    syntaxTree.FileNode.ArtifactRole = file.ArtifactRole;
                    Layer3FileSignalEnricher.EnrichFileSemanticSignalsAndRouteConstants(file, syntaxTree, project, ctx, IsRouteOrConfigCandidate);
                    if (syntaxTree.Tree != null && !ctx.LightweightAstScanMode)
                    {
                        AstConstantExtractor.ExtractAndRegister(syntaxTree, project.Name);

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
                            fileParser.DiscoverDeclarations(syntaxTree.Tree.RootNode, file.Path, project.Name, ctx);
                        }
                    }

                    parsedResults[i] = (file, syntaxTree, parentNode);

                    if (!ctx.LightweightAstScanMode)
                    {
                        RecordDocumentDiagnostics(syntaxTree, file, ctx);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ctx.Layer3Log.FileParseFailed(file.Path, ex.Message, ex);
                    ctx.RecordScanIssue(layer3SyntacticSource, parseExceptionCode, file.Path, ex.Message, name: parseExceptionName, severity: errorSeverity, exception: ex, language: fileParser.LanguageName, parentNodeId: file.Id, isFileParent: true, logToConsole: false);
                }
            });

            // Step 2 (Immediate Project UAST Visitors & Component Parsers): Run language visitors and component parsers immediately on the project's UASTs
            var projectAbsDir = Path.Combine(ctx.AbsoluteWorkspacePath, project.Path).NormalizeFullPath();
            var filesInProjectDir = PhysicalFileStore.GetDirectoryFiles(projectAbsDir);
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
                : Components.ComponentLibraryParserRegistry.GetParsers(projectContext);

            await Parallel.ForAsync(0, projectFileCount, parallelOptions, (int i, CancellationToken ct) =>
            {
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

                        ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx, project.Name, isTestFile, project.EntityKind != ProjectEntityKind.Unknown ? project.EntityKind : null);

                        if (componentParsers.Count > 0)
                        {
                            for (var cp = 0; cp < componentParsers.Count; cp++)
                            {
                                componentParsers[cp].EnrichWithSyntax(null, syntaxTree, ctx);
                            }
                        }

                        Layer3ScopeVisibilityPass.RegisterSyntaxTreeWorkspaceSymbols(syntaxTree, ctx);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        ctx.Layer3Log.VisitorFailed(syntaxTree.FilePath, ex.Message, ex);
                        ctx.RecordScanIssue(layer3VisitorSource, visitorExceptionCode, file.Path, ex.Message, name: visitorExceptionName, severity: errorSeverity, exception: ex, language: syntaxTree.FileParser.LanguageName, parentNodeId: file.Id, isFileParent: true, logToConsole: false);
                    }
                }

                return ValueTask.CompletedTask;
            });

            for (var i = 0; i < projectFileCount; i++)
            {
                var item = parsedResults[i];
                if (item == null) continue;

                var (file, syntaxTree, parentNode) = item.Value;
                yield return (project, projectSyntaxNode, file, syntaxTree, parentNode);
            }
        }
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
        ProjectEntityKind? projectKind = null)
    {
        if (syntaxTree.Tree == null) return;

        if (syntaxTree.FileNode != null)
        {
            if (!string.IsNullOrEmpty(projectName)) syntaxTree.FileNode.ProjectName = projectName;
            if (projectKind.HasValue) syntaxTree.FileNode.ProjectKind = projectKind.Value;
        }

        if (ctx?.LightweightAstScanMode != true)
        {
            SyntaxValueEvaluator.EvaluateDocument(syntaxTree.Document, projectName);
            if (ctx != null && syntaxTree.FileNode != null)
            {
                RecordDocumentDiagnostics(syntaxTree, syntaxTree.FileNode, ctx);
            }
        }

        var fileParser = syntaxTree.FileParser;
        if (!fileParser.UsesTreeSitter)
        {
            SemanticPostProcessor?.Invoke(syntaxTree, ctx);
            return;
        }

        var relativePath = syntaxTree.RelativePath;

        var (cachedActive, registry) = _parserRegistryCache.GetOrAdd(fileParser, fp =>
        {
            var active = fp.SemanticExtensions.Where(lp => lp.IsImplemented && lp.IsBuiltIn).ToList();
            var reg = new SemanticExtensionRegistry(fp.SemanticExtensions);
            return (active, reg);
        });

        if (ctx?.LightweightAstScanMode == true)
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

        var activeSemanticExtensions = ctx?.LightweightAstScanMode == true
            ? []
            : new List<ISemanticExtension>(cachedActive);

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

        var fileNode = syntaxTree.FileNode;
        if (fileNode == null) return;

        var rawImports = mainVisitor.RawImports.Select(ri => ri with
        {
            FilePath = relativePath,
            Type = fileParser.ResolveImportType(ri.Path, relativePath, absoluteWorkspacePath)
        }).ToList();

        Layer3ScopeVisibilityPass.RegisterFileRawImports(ctx, relativePath, rawImports);

        var rawVariables = mainVisitor.RawVariables.Select(rv => rv with { FilePath = relativePath }).ToList();
        var rawTypeBindings = mainVisitor.RawTypeBindings.Select(rt => rt with { FilePath = relativePath }).ToList();

        syntaxTree.RawImports.AddRange(rawImports);
        syntaxTree.RawVariables.AddRange(rawVariables);
        syntaxTree.RawTypeBindings.AddRange(rawTypeBindings);

        if (ctx?.LightweightAstScanMode == true)
        {
            SemanticPostProcessor?.Invoke(syntaxTree, ctx);
            return;
        }

        var fileSymbolCounts = new Dictionary<(CodeExplorer.Cypher.Common.SyntacticSymbolKind Kind, string Name), int>();
        foreach (var childSyntactic in mainVisitor.RootSymbol.Children)
        {
            var key = (childSyntactic.Kind, childSyntactic.Name);
            fileSymbolCounts.TryGetValue(key, out var count);
            count++;
            fileSymbolCounts[key] = count;

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, Path.GetFileName(syntaxTree.FilePath),
                relativePath, workspaceId, fileNode.Id, ctx, count > 1 ? count : 0, effectiveProjectName, effectiveIsTest, effectiveProjectKind, syntaxTree, fileSymbolCounts);
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
                ctx);
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
            }
        }
        IndexOntologyTree(fileNode, null);

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
                    fileNode.Children.Add(fnNode);
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
                            "class",
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

        // 3. Emit canonical `<module_init>` FunctionNode if top-level statements/calls exist
        if (HasTopLevelCallsOrStatements(doc))
        {
            var modInitNode = fileNode.Children.OfType<FunctionNode>().FirstOrDefault(f => f.Name == "<module_init>");
            if (modInitNode == null)
            {
                var modInitId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:<module_init>";
                modInitNode = new FunctionNode(
                    modInitId,
                    "<module_init>",
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
            PopulateModuleInitReferences(modInitNode, doc);
        }

        EnrichReferencesWithCallContextAndFlow(fileNode, doc);
    }

    private static void EnrichReferencesWithCallContextAndFlow(
        IOntologyNode rootNode,
        SyntaxDocument doc)
    {
        var callsByLineAndName = new Dictionary<(int Line, string MethodName), CallExprSyntax>();
        var callsByLine = new Dictionary<int, List<CallExprSyntax>>();

        foreach (var call in doc.AllCalls)
        {
            var line = call.Span.StartLine + 1;
            if (!string.IsNullOrEmpty(call.MethodName))
            {
                callsByLineAndName.TryAdd((line, call.MethodName), call);
            }
            if (!callsByLine.TryGetValue(line, out var list))
            {
                list = [];
                callsByLine[line] = list;
            }
            list.Add(call);
        }

        if (callsByLine.Count == 0) return;

        void WalkAndEnrich(IOntologyNode node)
        {
            if (node.References.Count > 0)
            {
                for (int i = 0; i < node.References.Count; i++)
                {
                    var r = node.References[i];
                    if (r.Line <= 0 || r.Kind != CodeExplorer.Cypher.Common.RelationshipKind.Calls) continue;

                    var targetMethod = r.TargetName;
                    if (targetMethod.Contains('.'))
                    {
                        targetMethod = targetMethod[(targetMethod.LastIndexOf('.') + 1)..];
                    }

                    CallExprSyntax? matchedCall = null;
                    if (!string.IsNullOrEmpty(targetMethod) && callsByLineAndName.TryGetValue((r.Line, targetMethod), out var namedCall))
                    {
                        matchedCall = namedCall;
                    }
                    else if (callsByLine.TryGetValue(r.Line, out var list) && list.Count > 0)
                    {
                        matchedCall = list.FirstOrDefault(c => c.MethodName == targetMethod) ?? list[0];
                    }

                    if (matchedCall != null)
                    {
                        var context = CallSiteContextExtractor.Extract(matchedCall);
                        string? argFlow = null;

                        SyntaxNode? curr = matchedCall.Parent;
                        while (curr != null && curr is not FunctionDeclSyntax)
                        {
                            curr = curr.Parent;
                        }

                        if (curr is FunctionDeclSyntax fnDecl && fnDecl.DataflowSummary != null)
                        {
                            var callKey = (matchedCall.MethodName, matchedCall.Span.StartLine + 1);
                            if (fnDecl.DataflowSummary.CallArgFlows.TryGetValue(callKey, out var argMap) && argMap.Count > 0)
                            {
                                argFlow = string.Join(";", argMap.Select(kv => $"{kv.Key}<-{string.Join(",", kv.Value.Select(o => o.ToString()))}"));
                            }
                        }

                        var effectiveArgFlow = string.Equals(r.ArgFlow, "callback_arg", StringComparison.Ordinal)
                            ? r.ArgFlow
                            : argFlow;
                        node.References[i] = r with { Context = context, ArgFlow = effectiveArgFlow };
                    }
                }
            }

            foreach (var child in node.Children)
            {
                if (child.References.Count > 0 || child.Children.Count > 0) WalkAndEnrich(child);
            }
        }

        WalkAndEnrich(rootNode);
    }
}
