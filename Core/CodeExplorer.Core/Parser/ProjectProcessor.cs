using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class ProjectProcessor
{
    private readonly ParsingContext _ctx;
    private readonly string _projectDir;
    private readonly string _parentContainerId;
    private readonly IProjectParser _projectParser;
    private readonly string _projectNodeId;
    private readonly GitIgnoreMatcher _gitignore;
    private readonly List<SyntaxTree> _projectSyntaxTrees = new();

    public ProjectProcessor(ParsingContext ctx, string projectDir, string parentContainerId, IProjectParser projectParser)
    {
        _ctx = ctx;
        _projectDir = projectDir.Replace('\\', '/');
        _parentContainerId = parentContainerId;
        _projectParser = projectParser;
        var relativeProjectDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, projectDir).Replace('\\', '/');
        if (relativeProjectDir == ".") relativeProjectDir = "";
        _projectNodeId = $"{_ctx.WorkspaceId}:project:{relativeProjectDir}:";
        _gitignore = new GitIgnoreMatcher(_projectDir);
    }

    public async Task<ProjectNode> ParseStructureAsync()
    {
        var folderName = Path.GetFileName(_projectDir);
        if (string.IsNullOrEmpty(folderName)) folderName = _projectDir;
        await Console.Error.WriteLineAsync($"[WorkspaceParser] Starting scan of project '{folderName}'...");

        var projectNode = new ProjectNode(
            _projectNodeId,
            folderName,
            Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, _projectDir).Replace('\\', '/'),
            _projectParser.ProjectType
        );

        // 1. Scan directory recursively and build the rich node tree under FilesNode
        var filesNodeId = $"{_projectNodeId}files";
        var filesNode = new FilesNode(filesNodeId, "Files", projectNode.Path);
        projectNode.Children.Add(filesNode);

        await ScanDirectoryAsync(_projectDir, filesNode);

        // Group EntryPoints under a single intermediate node to simplify browsing
        GroupEntryPoints(projectNode);

        // 2. Parse dependencies and produced packages
        await ParseDependenciesAsync(projectNode);
        await LinkProducedPackageAsync(projectNode);

        lock (_ctx.ProjectsToEnrich)
        {
            _ctx.ProjectsToEnrich.Add((this, projectNode));
        }

        await Console.Error.WriteLineAsync($"[WorkspaceParser] Completed structural scan of project '{folderName}'.");
        return projectNode;
    }

    public async Task EnrichAsync(ProjectNode projectNode)
    {
        var folderName = Path.GetFileName(_projectDir);
        if (string.IsNullOrEmpty(folderName)) folderName = _projectDir;
        await Console.Error.WriteLineAsync($"[WorkspaceParser] Starting syntax enrichment of project '{folderName}'...");

        try
        {
            // Perform semantic analysis & ontology enrichment
            foreach (var syntaxTree in _projectSyntaxTrees)
            {
                var enricher = _projectParser.GetSyntaxEnricher(syntaxTree);
                await enricher.EnrichAsync(projectNode, _ctx);
            }
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

    private async Task ScanDirectoryAsync(string currentDir, IOntologyNode parentNode)
    {
        var relativeDir = Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        if (!string.IsNullOrEmpty(relativeDir))
        {
            if (_gitignore.IsIgnored(relativeDir, true)) return;

            var dirNameLower = Path.GetFileName(currentDir).ToLowerInvariant();
            var genericExclusions = new HashSet<string> { ".git", ".github", ".vscode", ".idea", "node_modules", "bin", "obj", "mocks", "__mocks__" };
            if (genericExclusions.Contains(dirNameLower)) return;

            // Language specific exclusions
            foreach (var folder in _projectParser.ExcludedFolders)
            {
                if (folder.Equals(dirNameLower, StringComparison.OrdinalIgnoreCase)) return;
            }
        }

        var currentParentNode = parentNode;
        if (currentDir != _projectDir)
        {
            var dirName = Path.GetFileName(currentDir);
            var folderId = $"{_ctx.WorkspaceId}:projectfolder:{relativeDir}";
            
            var folderNode = new ProjectFolderNode(folderId, dirName, relativeDir);
            parentNode.Children.Add(folderNode);
            currentParentNode = folderNode;
        }

        // Recurse directories
        foreach (var subDir in Directory.GetDirectories(currentDir))
        {
            var processor = ProjectProcessorFactory.CreateProcessor(_ctx, subDir, currentParentNode.Id);
            if (processor != null)
            {
                var nestedProjectNode = await processor.ParseStructureAsync();
                currentParentNode.Children.Add(nestedProjectNode);
            }
            else
            {
                await ScanDirectoryAsync(subDir, currentParentNode);
            }
        }

        // Process files
        var filesInDir = Directory.GetFiles(currentDir);
        foreach (var file in filesInDir)
        {
            var ext = Path.GetExtension(file).ToLower();
            var relativeFile = Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, file).Replace('\\', '/');

            if (_gitignore.IsIgnored(relativeFile, false))
            {
                await Console.Error.WriteLineAsync($"[WorkspaceParser] GitIgnore: Ignoring file '{relativeFile}'");
                continue;
            }

            IFileParser? fileParser = null;
            lock (WorkspaceParser.FileParsers)
            {
                fileParser = WorkspaceParser.FileParsers.FirstOrDefault(p => p.CanParse(ext));
            }

            if (fileParser != null)
            {
                if (IsTestOrMockFile(file))
                {
                    await Console.Error.WriteLineAsync($"[WorkspaceParser] Exclusion: Ignoring test/mock file '{relativeFile}'");
                    continue;
                }

                var syntaxTree = await fileParser.ParseAsync(file, currentParentNode.Id, _ctx.WorkspaceId, _ctx.AbsoluteWorkspacePath);
                if (syntaxTree != null)
                {
                    if (syntaxTree.FileNode != null)
                    {
                        currentParentNode.Children.Add(syntaxTree.FileNode);
                    }
                    _projectSyntaxTrees.Add(syntaxTree);

                    lock (_ctx.RawImports)
                    {
                        _ctx.RawImports.AddRange(syntaxTree.RawImports);
                    }
                    lock (_ctx.RawVariables)
                    {
                        _ctx.RawVariables.AddRange(syntaxTree.RawVariables);
                    }
                    lock (_ctx.RawTypeBindings)
                    {
                        _ctx.RawTypeBindings.AddRange(syntaxTree.RawTypeBindings);
                    }
                }
            }
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
                    var targetDir = Path.GetFullPath(localPath).Replace('\\', '/');
                    var relativeTargetDir = Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, targetDir).Replace('\\', '/');
                    if (relativeTargetDir == ".") relativeTargetDir = "";
                    var targetProjectNodeId = $"{_ctx.WorkspaceId}:project:{relativeTargetDir}:";
                    _ctx.AddGlobalProjectDependency(Relationship.FromRelationship(new DependsOnRelationship(_projectNodeId, targetProjectNodeId)));
                }

                // B. Process external package dependencies
                if (depInfo.ExternalPackages.Count > 0)
                {
                    var depsNodeId = $"{_projectNodeId}dependencies";
                    var depsNode = new DependenciesNode(depsNodeId, "Dependencies", projectNode.Path);
                    projectNode.Children.Add(depsNode);

                    foreach (var extPack in depInfo.ExternalPackages)
                    {
                        var packageNodeId = $"{_ctx.WorkspaceId}:package:{extPack.Name.ToLowerInvariant()}";
                        var packageNode = new PackageNode(packageNodeId, extPack.Name, extPack.Version, extPack.Type, projectNode.Path);
                        depsNode.Children.Add(packageNode);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Error parsing dependencies for {_projectParser.ProjectType} in '{_projectDir}': {ex.Message}");
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
                var packageNode = new PackageNode(packageNodeId, producedPackage.Name, producedPackage.Version, producedPackage.Type, projectNode.Path);

                projectNode.Children.Add(packageNode);

                var implRel = Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, _projectNodeId));
                await _ctx.EnqueueUploadRelationshipsAsync([implRel]);
                _ctx.AddRelsCount(1);

                packageDetected = true;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Error getting produced package from {_projectParser.ProjectType} parser in '{_projectDir}': {ex.Message}");
        }

        if (!packageDetected)
        {
            var dirName = Path.GetFileName(_projectDir);
            if (!string.IsNullOrEmpty(dirName))
            {
                var packageNodeId = $"{_ctx.WorkspaceId}:package:{dirName.ToLowerInvariant()}";
                var packageNode = new PackageNode(packageNodeId, dirName, "1.0.0", "unknown", projectNode.Path);

                projectNode.Children.Add(packageNode);

                var implRel = Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, _projectNodeId));
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
        if (fileName.EndsWith(".test.ts") || fileName.EndsWith(".spec.ts") ||
            fileName.EndsWith(".test.js") || fileName.EndsWith(".spec.js")) return true;
            
        return false;
    }

    private void GroupEntryPoints(ProjectNode projectNode)
    {
        var entryPoints = new List<EntryPointNode>();
        var parentMap = new Dictionary<string, string>();

        FindAndCollectEntryPoints(projectNode, entryPoints, parentMap);

        if (entryPoints.Count > 0)
        {
            var entryPointsNodeId = $"{_projectNodeId}entrypoints";
            var entryPointsNode = new EntryPointsNode(entryPointsNodeId, "EntryPoints", projectNode.Path);
            projectNode.Children.Add(entryPointsNode);

            foreach (var ep in entryPoints)
            {
                entryPointsNode.Children.Add(ep);
                if (parentMap.TryGetValue(ep.Id, out var parentId))
                {
                    var implRel = new ImplementedByRelationship(ep.Id, parentId);
                    _ctx.AddGlobalProjectDependency(Relationship.FromRelationship(implRel));
                }
            }
        }
    }

    private void FindAndCollectEntryPoints(IOntologyNode node, List<EntryPointNode> entryPoints, Dictionary<string, string> parentMap)
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
}
