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
        ctx.Log("[Layer 3    ] Starting tree-sitter AST syntactic parsing pass...");

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
        var totalFilesAcrossProjects = 0;
        var parsedFilesCount = 0;
        var lastFileReportTicks = Environment.TickCount64;

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
                fileNodeByPath[file.Path.Replace('\\', '/').Trim('/')] = file;
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

                var normProjPath = p.Path.Replace('\\', '/').Trim('/');
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
                file.ProjectKind = primaryMatch.KindValue;
                file.ProjectName = primaryMatch.Name;
                if (!string.IsNullOrEmpty(file.Path))
                {
                    primaryOwnerByFilePath[file.Path.Replace('\\', '/').Trim('/')] = primaryMatch;
                }

                if (!filesByProjectId.TryGetValue(primaryMatch.Id, out var primaryList))
                {
                    primaryList = [];
                    filesByProjectId[primaryMatch.Id] = primaryList;
                }
                primaryList.Add(file);

                foreach (var matchProj in bestMatches)
                {
                    projectRels.Add(new Relationship(matchProj.Id, file.Id, OntologyConstants.Relationships.Contains));
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


        foreach (var project in l2Result.Projects)
        {
            foreach (var enrichment in project.Enrichments.OfType<ConfigurationFileInformation>())
            {
                ConfigurationIndex.Register(enrichment, project.Name);
            }
        }
        ctx.LogDebug($"[ConfigurationIndex] Registered {ConfigurationIndex.GetDiscoveredUrls().Count} service endpoints and configuration keys into ConstantRegistry.");

        // Phase 0b: RouteDictionaryRegistry and early constants pre-scanning across all candidate files
        var allCandidateFiles = new List<(string FullPath, string? ProjectName)>();
        foreach (var wfile in workspaceRootFiles)
        {
            if (IsRouteOrConfigCandidate(wfile.Path) && !SharedFileParserRegistry.CanParse(wfile.Name)) allCandidateFiles.Add((wfile.FullPath, null));
        }

        foreach (var project in l2Result.Projects)
        {
            if (filesByProjectId.TryGetValue(project.Id, out var pFiles))
            {
                foreach (var pfile in pFiles)
                {
                    if (IsRouteOrConfigCandidate(pfile.Path) && !SharedFileParserRegistry.CanParse(pfile.Name)) allCandidateFiles.Add((pfile.FullPath, project.Name));
                }
            }
        }

        Parallel.ForEach(allCandidateFiles, new ParallelOptions { CancellationToken = ctx.CancellationToken }, item =>
        {
            var (fPath, pName) = item;
            try
            {
                var fi = new FileInfo(fPath);
                if (!fi.Exists || fi.Length > 1024 * 1024) return;

                var content = File.ReadAllText(fPath);
                RouteDictionaryRegistry.ScanServiceDomains(content);
                RouteDictionaryRegistry.ScanAndRegister(content);
                ConstantRegistry.ScanAndRegister(fPath, content, pName);

                var fileParser = WorkspaceIndexer.GetParserForFile(fPath);
                fileParser?.DiscoverDeclarations(fPath, content, pName, ctx);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.RecordScanIssue("Layer3_Syntactic", "pre_scan_error", fPath, ex.Message, name: "RouteConfigPreScanError", exception: ex);






            }
        });


        foreach (var project in l2Result.Projects)
        {
            nProject++;
            if (nProject == 1)
            {
                totalFilesAcrossProjects = filesByProjectId.Values.Sum(list => list.Count);
            }
            ctx.CancellationToken.ThrowIfCancellationRequested();

            ctx.LogDebug($"[Layer 3    ] Parsing project {nProject} of {l2Result.Projects.Count} at: '{project.Path}'...");

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
                    RecordDocumentDiagnostics(syntaxTree, file, ctx);

                    var completed = Interlocked.Increment(ref parsedFilesCount);
                    var nowTicks = Environment.TickCount64;
                    if (totalFilesAcrossProjects > 0 &&
                        (completed == totalFilesAcrossProjects ||
                         completed % Math.Max(25, totalFilesAcrossProjects / 10) == 0 ||
                         (nowTicks - lastFileReportTicks >= 3000 && completed > 0)))
                    {
                        lastFileReportTicks = nowTicks;
                        var pct = 14 + (int)(38.0 * completed / totalFilesAcrossProjects);
                        ctx.ReportProgress(pct, "Layer 3: Syntactic", $"Parsing syntax trees: {completed}/{totalFilesAcrossProjects} files ({completed * 100.0 / totalFilesAcrossProjects:F0}%) [{project.Name}]", completed, totalFilesAcrossProjects);
                        ctx.Log($"[Layer 3    ] Parsing syntax trees: {completed}/{totalFilesAcrossProjects} files ({completed * 100.0 / totalFilesAcrossProjects:F0}%) [{project.Name}]...");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ctx.LogWarning($"[Layer 3    ] Error parsing file '{file.Path}': {ex.Message}", ex);
                    ctx.RecordScanIssue("Layer3_Syntactic", "parse_exception", file.Path, ex.Message, name: "ParseException", severity: "error", exception: ex, language: fileParser.LanguageName, parentNodeId: file.Id, isFileParent: true, logToConsole: false);
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
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        ctx.LogWarning($"[Layer 3    ] Error processing visitor for '{syntaxTree.FilePath}': {ex.Message}", ex);
                        ctx.RecordScanIssue("Layer3_Visitor", "visitor_exception", file.Path, ex.Message, name: "VisitorException", severity: "error", exception: ex, language: syntaxTree.FileParser.LanguageName, parentNodeId: file.Id, isFileParent: true, logToConsole: false);
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

        // Link shared files reused across projects via relative imports when no explicit Library project owns the target file
        var sharedContainsRels = ResolveCrossProjectSharedFileContains(rawImports, primaryOwnerByFilePath, fileNodeByPath, projectRels);
        if (sharedContainsRels.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(sharedContainsRels);
            ctx.AddRelsCount(sharedContainsRels.Count);
        }

        ctx.Log($"[Layer 3    ] Syntactic parsing pass complete. Parsed {syntaxTrees.Count} files.");
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

        SyntaxValueEvaluator.EvaluateDocument(syntaxTree.Document, projectName);
        if (ctx != null && syntaxTree.FileNode != null)
        {
            RecordDocumentDiagnostics(syntaxTree, syntaxTree.FileNode, ctx);
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

        var fileNode = syntaxTree.FileNode;
        if (fileNode == null) return;

        var fileSymbolCounts = new Dictionary<(string Kind, string Name), int>();
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

        if (syntaxTree.FileNode != null && rawVariables.Count > 0)
        {
            PopulateMemberNodesFromRawVariables(syntaxTree.FileNode, rawVariables, workspaceId, relativePath);
        }

        SemanticPostProcessor?.Invoke(syntaxTree, ctx);
    }

    private static void RecordDocumentDiagnostics(SyntaxTree syntaxTree, FileNode fileNode, ParsingContext ctx)
    {
        var diags = syntaxTree.Document?.Diagnostics;
        if (diags == null || diags.Count == 0) return;

        fileNode.SetExtension("has_parse_issues", "true");
        fileNode.SetExtension("parse_issue_count", diags.Count.ToString());

        var lang = syntaxTree.Document?.LanguageName ?? syntaxTree.FileParser.LanguageName;
        for (var d = 0; d < diags.Count; d++)
        {
            var diag = diags[d];
            ctx.RecordScanIssue(
                stage: "Layer3_Syntactic",
                category: diag.Kind,
                relativeOrFullPath: fileNode.Path,
                message: diag.Message,
                name: "SyntaxError",
                severity: "warning",
                startLine: diag.Span.StartLine + 1,
                endLine: diag.Span.EndLine + 1,
                startCol: diag.Span.StartColumn + 1,
                endCol: diag.Span.EndColumn + 1,
                language: lang,
                parentNodeId: fileNode.Id,
                isFileParent: true,
                logToConsole: d == 0);
        }
    }

    private static readonly Regex ConfigRegex = new(
        @"(?i)(config|settings?|cfg|\benv\b|db_?conn|\burl\b|\buri\b|\bport\b|\bhost\b|user(name)?|pass(word)?|token|secret|\bkey\b|auth|api_?key|connection_?string)",
        RegexOptions.Compiled
    );

    private static readonly Regex ConfigInitializerRegex = new(
        @"(?i)(process\.env|Configuration\[|Environment\.GetEnvironmentVariable|System\.Environment|import\.meta\.env|dotenv|require\(['""]dotenv['""]\))",
        RegexOptions.Compiled
    );

    private static readonly Regex EtlRegex = new(
        @"(?i)(\betl\b|\bsql\b|\bquery\b|\bselect\b|\binsert\b|\bupsert\b|\bschema\b|\btable\b|\bcolumn\b|\bdatabase\b|\bmigration\b|\bextract\b|\btransform\b|\bload\b)",
        RegexOptions.Compiled
    );

    private static readonly Regex SqlQueryRegex = new(
        @"(?i)^[\s@$""'\`]*\s*(select|insert|update|delete|create\s+table|drop\s+table|merge|alter\s+table)\b",
        RegexOptions.Compiled
    );

    private static void PopulateMemberNodesFromRawVariables(
        FileNode fileNode,
        IReadOnlyList<RawVariable> fileVariables,
        string workspaceId,
        string relativePath)
    {
        foreach (var rawVar in fileVariables)
        {
            var isConfig = (rawVar.Name.Length >= 3 && ConfigRegex.IsMatch(rawVar.Name)) ||
                           (rawVar.InitializerText.Length >= 6 && ConfigInitializerRegex.IsMatch(rawVar.InitializerText));
            var isEtl = (rawVar.Name.Length >= 3 && EtlRegex.IsMatch(rawVar.Name)) ||
                        (rawVar.InitializerText.Length >= 6 && SqlQueryRegex.IsMatch(rawVar.InitializerText));
            // ConfigInitializerRegex and EtlRegex handled above
            // SqlQueryRegex handled above
            var isConstant = rawVar.IsConstant;
            var isGlobal = rawVar.Scope == "global";
            var isProperty = rawVar.MemberKind == "property";
            var isField = rawVar.MemberKind == "field";

            if (isConfig || isEtl || isConstant || isGlobal || isProperty || isField)
            {
                var varTypeStr = new List<string>();
                if (isConfig) varTypeStr.Add("config");
                if (isEtl) varTypeStr.Add("etl");
                if (isConstant) varTypeStr.Add("constant");
                if (isGlobal) varTypeStr.Add("global");
                if (isProperty) varTypeStr.Add("property");
                if (isField) varTypeStr.Add("field");

                var varType = string.Join(",", varTypeStr);
                var varId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Member:{rawVar.Name}";

                var ext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["variable_type"] = varType,
                    ["initializer_expression"] = rawVar.InitializerText,
                    ["is_constant"] = isConstant ? "true" : "false"
                };
                if (!string.IsNullOrEmpty(rawVar.TypeName))
                {
                    ext["data_type"] = rawVar.TypeName;
                }

                var varNode = new MemberNode(
                    varId,
                    rawVar.Name,
                    varId,
                    fileNode.FullPath,
                    varId,
                    rawVar.StartLine,
                    rawVar.EndLine,
                    rawVar.StartCol,
                    rawVar.EndCol,
                    rawVar.MemberKind ?? "variable",
                    rawVar.TypeName,
                    ext
                );

                TryInsertVariable(fileNode, varNode, rawVar.StartLine);
            }
        }
    }

    private static bool TryInsertVariable(IOntologyNode parentNode, MemberNode varNode, int line)
    {
        foreach (var child in parentNode.Children)
        {
            if (child is TypeNode cn && line >= cn.StartLine && line <= cn.EndLine)
            {
                if (TryInsertVariable(cn, varNode, line)) return true;
            }

            if (child is FunctionNode fn && line >= fn.StartLine && line <= fn.EndLine)
            {
                if (TryInsertVariable(fn, varNode, line)) return true;
            }
        }

        if (parentNode.Children.All(c => c.Id != varNode.Id && !(c is MemberNode existingMember && existingMember.Name == varNode.Name)))
        {
            parentNode.Children.Add(varNode);
        }
        return true;
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
        SyntaxTree? syntaxTree = null,
        Dictionary<(string Kind, string Name), int>? fileSymbolCounts = null)
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

        var childCounts = fileSymbolCounts ?? new Dictionary<(string Kind, string Name), int>();
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

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, fileName, relativePath, workspaceId, symbolId, ctx, count > 1 ? count : 0, projectName, symbolIsTest, projectKind, syntaxTree, childCounts);
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
        SyntaxNode node,
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
        SyntaxNode node,
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
        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        var ext = Path.GetExtension(normalized);
        if (ext is not (".ts" or ".js" or ".cs" or ".go" or ".py" or ".json"))
            return false;

        var fileName = Path.GetFileName(normalized);
        if (fileName.Contains("route") || fileName.Contains("const") || fileName.Contains("config") ||
            fileName.Contains("api") || fileName.Contains("endpoint") || fileName.Contains("url") ||
            fileName.Contains("env") || fileName.Contains("client") || fileName.Contains("service") ||
            fileName.Contains("importer") || fileName.Contains("catalog") || fileName.Contains("gateway") ||
            fileName.Contains("http") || fileName.StartsWith("main.") || fileName.StartsWith("app.") ||
            fileName.StartsWith("bootstrap.") || fileName.StartsWith("index.") ||
            fileName.StartsWith("program.") || fileName.StartsWith("startup."))
        {
            return true;
        }

        // Dedicated route / configuration / constants directory segments
        return normalized.Contains("/routes/") || normalized.Contains("/route/") ||
               normalized.Contains("/config/") || normalized.Contains("/configs/") ||
               normalized.Contains("/constants/") || normalized.Contains("/endpoints/") ||
               normalized.Contains("/gateways/");
    }

    private static List<Relationship> ResolveCrossProjectSharedFileContains(
        IReadOnlyList<RawImport> rawImports,
        Dictionary<string, ProjectNode> primaryOwnerByFilePath,
        Dictionary<string, FileNode> fileNodeByPath,
        List<Relationship> existingProjectRels)
    {
        var newRels = new List<Relationship>();
        if (rawImports.Count == 0 || fileNodeByPath.Count == 0) return newRels;

        var localImportEdges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var imp in rawImports)
        {
            if (string.IsNullOrWhiteSpace(imp.FilePath) || string.IsNullOrWhiteSpace(imp.Path)) continue;
            if (!imp.Path.StartsWith('.')) continue;

            var fromNorm = imp.FilePath.Replace('\\', '/').Trim('/');
            var resolvedTarget = TryResolveRelativeFileImport(fromNorm, imp.Path, fileNodeByPath);
            if (resolvedTarget == null) continue;

            if (!localImportEdges.TryGetValue(fromNorm, out var targets))
            {
                targets = [];
                localImportEdges[fromNorm] = targets;
            }
            if (!targets.Contains(resolvedTarget, StringComparer.OrdinalIgnoreCase))
            {
                targets.Add(resolvedTarget);
            }
        }

        if (localImportEdges.Count == 0) return newRels;

        var existingContainsSet = new HashSet<(string ProjId, string FileId)>(
            existingProjectRels.Where(r => r.Kind == OntologyConstants.Relationships.Contains)
                               .Select(r => (r.From, r.To)));

        foreach (var (fromFile, directTargets) in localImportEdges)
        {
            if (!primaryOwnerByFilePath.TryGetValue(fromFile, out var consumerProj) || consumerProj.IsLibrary)
            {
                continue;
            }

            var queue = new Queue<string>();
            foreach (var dt in directTargets)
            {
                if (primaryOwnerByFilePath.TryGetValue(dt, out var targetOwner) &&
                    !string.Equals(targetOwner.Id, consumerProj.Id, StringComparison.OrdinalIgnoreCase) &&
                    !targetOwner.IsLibrary)
                {
                    queue.Enqueue(dt);
                }
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (queue.Count > 0)
            {
                var currentPath = queue.Dequeue();
                if (!visited.Add(currentPath)) continue;
                if (!fileNodeByPath.TryGetValue(currentPath, out var targetFileNode)) continue;

                if (existingContainsSet.Add((consumerProj.Id, targetFileNode.Id)))
                {
                    var rel = new Relationship(consumerProj.Id, targetFileNode.Id, OntologyConstants.Relationships.Contains);
                    newRels.Add(rel);
                    existingProjectRels.Add(rel);
                }

                if (localImportEdges.TryGetValue(currentPath, out var nestedTargets))
                {
                    foreach (var nt in nestedTargets)
                    {
                        if (!visited.Contains(nt) &&
                            primaryOwnerByFilePath.TryGetValue(nt, out var nestedOwner) &&
                            !nestedOwner.IsLibrary)
                        {
                            queue.Enqueue(nt);
                        }
                    }
                }
            }
        }

        return newRels;
    }

    private static string? TryResolveRelativeFileImport(
        string importerRelativePath,
        string rawImportSpec,
        Dictionary<string, FileNode> fileNodeByPath)
    {
        var lastSlash = importerRelativePath.LastIndexOf('/');
        var baseDir = lastSlash >= 0 ? importerRelativePath[..lastSlash] : "";
        var combined = string.IsNullOrEmpty(baseDir)
            ? rawImportSpec.Replace('\\', '/')
            : baseDir + "/" + rawImportSpec.Replace('\\', '/');

        var stack = new List<string>();
        foreach (var seg in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == ".") continue;
            if (seg == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(seg);
        }

        if (stack.Count == 0) return null;
        var normalizedCandidate = string.Join('/', stack);

        if (fileNodeByPath.ContainsKey(normalizedCandidate)) return normalizedCandidate;

        foreach (var ext in new[] { ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", "/index.ts", "/index.tsx", "/index.js", "/index.jsx" })
        {
            var withExt = normalizedCandidate + ext;
            if (fileNodeByPath.ContainsKey(withExt)) return withExt;
        }

        return null;
    }
}
