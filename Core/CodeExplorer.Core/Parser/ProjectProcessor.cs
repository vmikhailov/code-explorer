using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class ProjectProcessor
{
    private readonly ParsingContext _ctx;
    private readonly string _projectDir;
    private readonly string _relativeProjectDir;
    private readonly IProjectParser _projectParser;
    private readonly string _projectNodeId;
    private readonly GitIgnoreMatcher _gitignore;
    private readonly IOntologyNode _physicalRootNode;
    private readonly List<SyntaxTree> _projectSyntaxTrees = new();

    public static async Task<ProjectNode?> DetectAndParseAsync(ParsingContext ctx, string projectDir, IOntologyNode physicalRootNode)
    {
        var files = Directory.GetFiles(projectDir);

        var matchedParser =
            WorkspaceIndexer._projectParsers.FirstOrDefault(p => p.IsProjectDirectory(projectDir, files));

        if (matchedParser == null) return null;

        var processor = new ProjectProcessor(ctx, projectDir, matchedParser, physicalRootNode);
        return await processor.ParseAsync();
    }

    public ProjectProcessor(ParsingContext ctx, string projectDir, IProjectParser projectParser, IOntologyNode physicalRootNode)
    {
        _ctx = ctx;
        _projectDir = NormalizePath(projectDir);
        _projectParser = projectParser;
        _relativeProjectDir = NormalizePath(Path.GetRelativePath(ctx.AbsoluteWorkspacePath, projectDir));

        if (_relativeProjectDir == ".") _relativeProjectDir = "";

        _projectNodeId = $"{_ctx.WorkspaceId}:project:{_relativeProjectDir}:";
        _gitignore = new GitIgnoreMatcher(_projectDir);
        _physicalRootNode = physicalRootNode;
    }

    public async Task<ProjectNode> ParseAsync()
    {
        var folderName = Path.GetFileName(_projectDir);
        if (string.IsNullOrEmpty(folderName)) folderName = _projectDir;
        await Console.Error.WriteLineAsync($"[WorkspaceParser] Starting scan of project '{folderName}'...");

        var projectNode = new ProjectNode(_projectNodeId, folderName, _relativeProjectDir, _projectParser.ProjectType);

        // 2. Scan directory recursively and build the files directly under the physical root node of the project
        await ScanDirectoryAsync(_projectDir, _physicalRootNode, projectNode);
        await ParseDependenciesAsync(projectNode);
        await LinkProducedPackageAsync(projectNode);

        _ctx.ProjectsToEnrich.Add((this, projectNode));

        await Console.Error.WriteLineAsync($"[WorkspaceParser] Completed structural scan of project '{folderName}'.");
        return projectNode;
    }

    public async Task EnrichAsync(ProjectNode projectNode)
    {
        var folderName = Path.GetFileName(_projectDir);
        if (string.IsNullOrEmpty(folderName)) folderName = _projectDir;

        await Console.Error.WriteLineAsync(
            $"[WorkspaceParser] Starting syntax enrichment of project '{folderName}'...");

        try
        {
            var syntaxStructureNode = _ctx.SyntaxStructure;
            var semanticStructureNode = _ctx.SemanticStructure;

            // Perform semantic analysis & ontology enrichment
            foreach (var syntaxTree in _projectSyntaxTrees)
            {
                if (syntaxTree.Tree != null)
                {
                    ProcessVisitor(syntaxTree, _ctx.WorkspaceId, _ctx.AbsoluteWorkspacePath);
                }

                if (syntaxStructureNode != null && syntaxTree.FileNode != null)
                {
                    foreach (var child in syntaxTree.FileNode.Children)
                    {
                        if (child is TypeNode || child is FunctionNode || child is MemberNode)
                        {
                            syntaxStructureNode.Children.Add(child);
                        }
                    }
                }

                _ctx.RawImports.AddRange(syntaxTree.RawImports);
                _ctx.RawVariables.AddRange(syntaxTree.RawVariables);
                _ctx.RawTypeBindings.AddRange(syntaxTree.RawTypeBindings);

                var enricher = _projectParser.GetSyntaxEnricher(syntaxTree);
                await enricher.EnrichAsync(projectNode, _ctx);
            }

            // Collect and link semantic nodes parsed from files
            if (semanticStructureNode != null)
            {
                var semanticNodes = new List<IOntologyNode>();
                foreach (var syntaxTree in _projectSyntaxTrees)
                {
                    if (syntaxTree.FileNode != null)
                    {
                        CollectSemanticNodes(syntaxTree.FileNode, semanticNodes);
                    }
                }
                foreach (var semNode in semanticNodes)
                {
                    if (!semanticStructureNode.Children.Any(c => c.Id == semNode.Id))
                    {
                        semanticStructureNode.Children.Add(semNode);
                    }
                }
            }

            // Group EntryPoints under a single intermediate node to simplify browsing
            GroupEntryPoints();
        }
        finally
        {
            foreach (var st in _projectSyntaxTrees)
            {
                st.Dispose();
            }

            _projectSyntaxTrees.Clear();
        }

        await Console.Error.WriteLineAsync($"[WorkspaceParser] Completed syntax enrichment of project '{folderName}'.");
    }

    private async Task ScanDirectoryAsync(string currentDir, IOntologyNode parentNode, ProjectNode projectNode)
    {
        var dirNameLower = Path.GetFileName(currentDir).ToLowerInvariant();

        var genericExclusions = new HashSet<string>
        {
            ".git",
            ".github",
            ".vscode",
            ".idea",
            "node_modules",
            "bin",
            "obj",
            "mocks",
            "__mocks__"
        };

        if (genericExclusions.Contains(dirNameLower)) return;

        // Language specific exclusions
        foreach (var folder in _projectParser.ExcludedFolders)
        {
            if (folder.Equals(dirNameLower, StringComparison.OrdinalIgnoreCase)) return;
        }

        var currentParentNode = parentNode;

        if (currentDir != _projectDir)
        {
            var dirName = Path.GetFileName(currentDir);
            var absoluteFolderPath = Path.GetFullPath(currentDir).Replace('\\', '/');
            var folderId = $"{_ctx.WorkspaceId}:folder:{absoluteFolderPath}";
            var folderNode = new FolderNode(folderId, dirName, absoluteFolderPath);

            parentNode.Children.Add(folderNode);
            currentParentNode = folderNode;
        }

        // Recurse directories
        foreach (var subDir in Directory.GetDirectories(currentDir).Select(NormalizePath))
        {
            var files = Directory.GetFiles(subDir);
            var isProject = WorkspaceIndexer._projectParsers.Any(p => p.IsProjectDirectory(subDir, files));

            if (isProject)
            {
                var subDirName = Path.GetFileName(subDir);
                var absoluteSubDir = Path.GetFullPath(subDir).Replace('\\', '/');
                var subFolderId = $"{_ctx.WorkspaceId}:folder:{absoluteSubDir}";
                var subFolderNode = new FolderNode(subFolderId, subDirName, absoluteSubDir);

                currentParentNode.Children.Add(subFolderNode);

                var nestedProjectNode = await DetectAndParseAsync(_ctx, subDir, subFolderNode);
                if (nestedProjectNode != null)
                {
                    _ctx.ProjectsStructure?.Children.Add(nestedProjectNode);
                }
            }
            else
            {
                await ScanDirectoryAsync(subDir, currentParentNode, projectNode);
            }
        }

        // Process files
        var filesInDir = Directory.GetFiles(currentDir).Select(NormalizePath);

        foreach (var file in filesInDir)
        {
            var ext = Path.GetExtension(file).ToLower();
            var relativeFile = NormalizePath(Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, file));

            if (_gitignore.IsIgnored(relativeFile, false))
            {
                await Console.Error.WriteLineAsync($"[WorkspaceParser] GitIgnore: Ignoring file '{relativeFile}'");
                continue;
            }

            var fileParser = WorkspaceIndexer._fileParsers.FirstOrDefault(p => p.CanParse(ext));

            if (fileParser == null)
            {
                continue;
            }

            if (IsTestOrMockFile(file))
            {
                await Console.Error.WriteLineAsync(
                    $"[WorkspaceParser] Exclusion: Ignoring test/mock file '{relativeFile}'");
                continue;
            }

            var syntaxTree = await fileParser.ParseAsync(file, currentParentNode.Id, _ctx.WorkspaceId,
                _ctx.AbsoluteWorkspacePath);

            currentParentNode.Children.Add(syntaxTree.FileNode);

            _projectSyntaxTrees.Add(syntaxTree);
        }
    }

    private async Task ParseDependenciesAsync(ProjectNode projectNode)
    {
        try
        {
            var depInfo = await _projectParser.ParseDependenciesAsync(_projectDir);

            if (depInfo != null)
            {
                // A. Process local project dependencies (DependsOn relationships)
                foreach (var localPath in depInfo.LocalProjectPaths)
                {
                    var targetDir = NormalizePath(Path.GetFullPath(localPath));

                    var relativeTargetDir = NormalizePath(Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, targetDir));
                    if (relativeTargetDir == ".") relativeTargetDir = "";
                    var targetProjectNodeId = $"{_ctx.WorkspaceId}:project:{relativeTargetDir}:";

                    _ctx.AddGlobalProjectDependency(
                        Relationship.FromRelationship(new DependsOnRelationship(_projectNodeId, targetProjectNodeId)));
                }

                // B. Process external package dependencies
                if (depInfo.ExternalPackages.Count > 0)
                {
                    foreach (var extPack in depInfo.ExternalPackages)
                    {
                        var packageNodeId = $"{_ctx.WorkspaceId}:package:{extPack.Name.ToLowerInvariant()}";

                        var packageNode = new PackageNode(packageNodeId, extPack.Name, extPack.Version, extPack.Type,
                            projectNode.Path);
                        projectNode.Children.Add(packageNode);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(
                $"[WorkspaceParser] Error parsing dependencies for {_projectParser.ProjectType} in '{_projectDir}': {ex.Message}");
        }
    }

    private async Task LinkProducedPackageAsync(ProjectNode projectNode)
    {
        var packageDetected = false;

        try
        {
            var producedPackage = await _projectParser.GetProducedPackageAsync(_projectDir);

            if (producedPackage != null)
            {
                var packageNodeId = $"{_ctx.WorkspaceId}:package:{producedPackage.Name.ToLowerInvariant()}";

                var packageNode = new PackageNode(packageNodeId, producedPackage.Name, producedPackage.Version,
                    producedPackage.Type, projectNode.Path);

                projectNode.Children.Add(packageNode);

                var implRel =
                    Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, _projectNodeId));
                await _ctx.EnqueueUploadRelationshipsAsync([implRel]);
                _ctx.AddRelsCount(1);

                packageDetected = true;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(
                $"[WorkspaceParser] Error getting produced package from {_projectParser.ProjectType} parser in '{_projectDir}': {ex.Message}");
        }

        if (!packageDetected)
        {
            var dirName = Path.GetFileName(_projectDir);

            if (!string.IsNullOrEmpty(dirName))
            {
                var packageNodeId = $"{_ctx.WorkspaceId}:package:{dirName.ToLowerInvariant()}";
                var packageNode = new PackageNode(packageNodeId, dirName, "1.0.0", "unknown", projectNode.Path);

                projectNode.Children.Add(packageNode);

                var implRel =
                    Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, _projectNodeId));
                await _ctx.EnqueueUploadRelationshipsAsync([implRel]);
                _ctx.AddRelsCount(1);
            }
        }
    }

    private static bool IsTestOrMockFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        // Mock files
        if (fileName.Contains("mock")) return true;

        // C# test patterns: e.g. MyTests.cs, MyTest.cs
        if (fileName.EndsWith("tests.cs") || fileName.EndsWith("test.cs")) return true;

        // Go test pattern: e.g. my_test.go
        if (fileName.EndsWith("_test.go")) return true;

        // Python test patterns: e.g. test_my.py, my_test.py
        if (fileName.StartsWith("test_") && fileName.EndsWith(".py")) return true;
        if (fileName.EndsWith("_test.py")) return true;

        // TS/JS test patterns: e.g. my.test.ts, my.spec.ts, my.test.js, my.spec.js
        if (fileName.EndsWith(".test.ts") || fileName.EndsWith(".spec.ts") || fileName.EndsWith(".test.js") ||
            fileName.EndsWith(".spec.js")) return true;

        return false;
    }

    private void GroupEntryPoints()
    {
        var entryPoints = new List<EntryPointNode>();
        var parentMap = new Dictionary<string, string>();

        foreach (var syntaxTree in _projectSyntaxTrees)
        {
            if (syntaxTree.FileNode != null)
            {
                FindAndCollectEntryPoints(syntaxTree.FileNode, entryPoints, parentMap);
            }
        }

        if (entryPoints.Count > 0)
        {
            var semanticStructureNode = _ctx.SemanticStructure;
            if (semanticStructureNode != null)
            {
                foreach (var ep in entryPoints)
                {
                    semanticStructureNode.Children.Add(ep);

                    if (parentMap.TryGetValue(ep.Id, out var parentId))
                    {
                        var implRel = new ImplementedByRelationship(ep.Id, parentId);
                        _ctx.AddGlobalProjectDependency(Relationship.FromRelationship(implRel));
                    }
                }
            }
        }
    }

    private void CollectSemanticNodes(IOntologyNode node, List<IOntologyNode> semanticNodes)
    {
        foreach (var child in node.Children)
        {
            if (child is DatabaseNode || child is EndpointNode || child is QueryNode || child is ExternalServiceNode || child is TopicNode || child is CloudServiceNode || child is ApiInUseNode)
            {
                semanticNodes.Add(child);
            }
            CollectSemanticNodes(child, semanticNodes);
        }
    }

    private void FindAndCollectEntryPoints(
        IOntologyNode node,
        List<EntryPointNode> entryPoints,
        Dictionary<string, string> parentMap)
    {
        var epsInNode = node.Children.OfType<EntryPointNode>().ToList();

        foreach (var ep in epsInNode)
        {
            entryPoints.Add(ep);
            parentMap[ep.Id] = node.Id;
            node.Children.Remove(ep);
        }

        var childrenCopy = node.Children.ToList();

        foreach (var child in childrenCopy)
        {
            FindAndCollectEntryPoints(child, entryPoints, parentMap);
        }
    }

    public static void ProcessVisitor(SyntaxTree syntaxTree, string workspaceId, string absoluteWorkspacePath)
    {
        if (syntaxTree.Tree == null) return;

        var fileParser = syntaxTree.FileParser;
        var relativePath = syntaxTree.RelativePath;

        // Initialize with built-in library parsers
        var activeLibraryParsers = fileParser.LibraryParsers.Where(lp => lp.IsImplemented && lp.IsBuiltIn).ToList();

        var registry = new LibraryTrieRegistry(fileParser.LibraryParsers);

        var mainVisitor = fileParser.CreateVisitor(syntaxTree.Tree.RootNode, activeLibraryParsers, relativePath,
            absoluteWorkspacePath, fileParser, registry);

        // Single pass: build the actual ontology tree and collect all semantic data
        mainVisitor.Visit(syntaxTree.Tree.RootNode);

        // Map syntactic tree to ontology nodes
        foreach (var childSyntactic in mainVisitor.RootSymbol.Children)
        {
            var childNode = MapSyntacticSymbolToOntology(childSyntactic, Path.GetFileName(syntaxTree.FilePath),
                relativePath, workspaceId, syntaxTree.FileNode.Id);
            syntaxTree.FileNode.Children.Add(childNode);
        }

        foreach (var reference in mainVisitor.RootSymbol.References)
        {
            syntaxTree.FileNode.References.Add(reference with { ScopeSymbolId = syntaxTree.FileNode.Id });
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

        Console.WriteLine(
            $"Finished parsing file: {relativePath} with {syntaxTree.FileNode.Children.Count} top-level symbols.");
    }

    private static IOntologyNode MapSyntacticSymbolToOntology(
        SyntacticSymbol syntactic,
        string fileName,
        string relativePath,
        string workspaceId,
        string parentScopeId)
    {
        var node = syntactic.Node;
        var kind = syntactic.Kind;
        var name = syntactic.Name;

        var isType = kind == "Class" || kind == "Interface" || kind == OntologyConstants.NodeLabels.Type;
        var mappedKind = isType ? "Type" : kind;
        var symbolId = $"{workspaceId}:symbol:{relativePath}:{mappedKind}:{name}:{node.StartPosition.Row}";

        IOntologyNode typedNode;
        if (kind == "Class")
        {
            typedNode = new TypeNode(symbolId, name, symbolId, fileName, relativePath,
                node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column, "class");
        }
        else if (kind == "Interface")
        {
            typedNode = new TypeNode(symbolId, name, symbolId, fileName, relativePath,
                node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column, "interface");
        }
        else if (kind == OntologyConstants.NodeLabels.Function)
        {
            typedNode = new FunctionNode(symbolId, name, symbolId, fileName, relativePath,
                node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column);
        }
        else if (kind == OntologyConstants.NodeLabels.Query)
        {
            typedNode = NestedSqlParser.ParseNestedSql(syntactic.Text ?? node.Text, symbolId, relativePath) ?? 
                        new QueryNode(symbolId, name, NestedSqlParser.CleanQueryText(syntactic.Text ?? node.Text), relativePath);
        }
        else if (kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var colonIdx = name.IndexOf(':');
            var isHttp = false;
            if (colonIdx > 0)
            {
                var method = name.Substring(0, colonIdx).ToUpperInvariant();
                isHttp = method is "GET" or "POST" or "PUT" or "DELETE" or "PATCH";
            }

            if (isHttp)
            {
                typedNode = CreateEndpointNode(name, node, relativePath, workspaceId);
            }
            else
            {
                typedNode = CreateEntryPointNode(name, node, relativePath, workspaceId);
            }
        }
        else if (kind == OntologyConstants.NodeLabels.ExternalService)
        {
            typedNode = CreateExternalServiceNode(name, node, relativePath, workspaceId);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported symbol type: {kind}");
        }

        // Recursively map children
        foreach (var childSyntactic in syntactic.Children)
        {
            var childNode = MapSyntacticSymbolToOntology(childSyntactic, fileName, relativePath, workspaceId, symbolId);
            typedNode.Children.Add(childNode);
        }

        // Rewrite references to use the correct parent scope ID if empty
        foreach (var reference in syntactic.References)
        {
            var resolvedScopeId = string.IsNullOrEmpty(reference.ScopeSymbolId) ? typedNode.Id : reference.ScopeSymbolId;
            typedNode.References.Add(reference with { ScopeSymbolId = resolvedScopeId });
        }

        return typedNode;
    }

    private static EndpointNode CreateEndpointNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId)
    {
        var idx = name.IndexOf(':');
        var method = name.Substring(0, idx).ToUpperInvariant();
        var route = name.Substring(idx + 1);

        var endpointId = $"{workspaceId}:endpoint:{method}:{route}";
        return new EndpointNode(endpointId, name, relativePath, method, route);
    }

    private static EntryPointNode CreateEntryPointNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId)
    {
        var projectName = GetProjectNameFromRelativePath(relativePath);
        if (string.IsNullOrEmpty(projectName)) projectName = "default";

        var entryType = "grpc";
        var cleanName = name;

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            entryType = "queue-listener";
            cleanName = name.Substring(3);
        }
        else if (name.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            entryType = "queue-listener";
            cleanName = name.Substring(6);
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            entryType = name.Substring(0, idx);
            cleanName = name.Substring(idx + 1);
        }

        var entryPointId = $"{workspaceId}:entrypoint:{entryType}:{cleanName}";

        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath }, { "start_line", node.StartPosition.Row.ToString() }
        };
        return new EntryPointNode(entryPointId, cleanName, relativePath, entryType, ext);
    }

    private static ExternalServiceNode CreateExternalServiceNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId)
    {
        var protocol = "http";
        var domainOrService = name;

        if (domainOrService.Contains("://"))
        {
            var pIdx = domainOrService.IndexOf("://");
            protocol = domainOrService.Substring(0, pIdx);
            domainOrService = domainOrService.Substring(pIdx + 3);
        }
        else if (domainOrService.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "ws";
            domainOrService = domainOrService.Substring(3);
        }
        else if (domainOrService.Contains(':'))
        {
            var idx = domainOrService.IndexOf(':');
            protocol = domainOrService.Substring(0, idx);
            domainOrService = domainOrService.Substring(idx + 1);
        }

        var path = "/";
        var slashIdx = domainOrService.IndexOf('/');
        if (slashIdx >= 0)
        {
            path = domainOrService.Substring(slashIdx);
            domainOrService = domainOrService.Substring(0, slashIdx);
        }

        var extServiceId = $"{workspaceId}:externalservice:{protocol}:{domainOrService}";

        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath }, { "start_line", node.StartPosition.Row.ToString() }
        };
        return new ExternalServiceNode(extServiceId, domainOrService, protocol, domainOrService, path, ext);
    }

    private static string GetProjectNameFromRelativePath(string relativePath)
    {
        var cleanPath = NormalizePath(relativePath).Trim('/');
        var parts = cleanPath.Split('/');
        if (parts.Length == 0) return "default";

        if (parts.Length >= 2 && (parts[0] is "Core" or "Parsers" or "Tests"))
        {
            return parts[1];
        }

        return parts[0];
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        return path.Replace('\\', '/');
    }
}
