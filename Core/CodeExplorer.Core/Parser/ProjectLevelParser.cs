using CodeExplorer.Common;
using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public class ProjectLevelParser
{
    private readonly ParsingContext _ctx;
    private readonly string _projectDir;
    private readonly string _parentContainerId;
    private readonly IProjectParser _projectParser;
    private readonly string _projectNodeId;
    private readonly GitIgnoreMatcher _gitignore;

    public ProjectLevelParser(ParsingContext ctx, string projectDir, string parentContainerId, IProjectParser projectParser)
    {
        _ctx = ctx;
        _projectDir = projectDir.Replace('\\', '/');
        _parentContainerId = parentContainerId;
        _projectParser = projectParser;
        _projectNodeId = $"project:{_projectDir}:";
        _gitignore = new GitIgnoreMatcher(_projectDir);
    }

    public async Task ParseAsync()
    {
        var folderName = Path.GetFileName(_projectDir);
        if (string.IsNullOrEmpty(folderName)) folderName = _projectDir;
        await Console.Error.WriteLineAsync($"[WorkspaceParser] Starting scan of project '{folderName}'...");

        // 1. Create and Upload Project node
        var projectNode = Node.FromNode(new ProjectNode(
            _projectNodeId,
            folderName,
            Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, _projectDir).Replace('\\', '/'),
            _projectParser.ProjectType
        ));
        await _ctx.EnqueueUploadNodesAsync(new List<Node> { projectNode });

        // 2. Relate Parent to Project
        var parentRel = Relationship.FromRelationship(new ContainsRelationship(_parentContainerId, _projectNodeId));
        await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { parentRel });

        _ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Project);
        _ctx.AddNodesCount(1);
        _ctx.AddRelsCount(1);

        // 3. Scan directory recursively
        await ScanDirectoryAsync(_projectDir, _projectNodeId);

        // 4. Parse dependencies and produced packages
        await ParseDependenciesAsync();
        await LinkProducedPackageAsync();

        await Console.Error.WriteLineAsync($"[WorkspaceParser] Completed scan of project '{folderName}'.");
    }

    private async Task ScanDirectoryAsync(string currentDir, string currentParentId)
    {
        var relativeDir = Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        if (!string.IsNullOrEmpty(relativeDir))
        {
            if (_gitignore.IsIgnored(relativeDir, true)) return;

            var dirNameLower = Path.GetFileName(currentDir).ToLowerInvariant();
            var genericExclusions = new HashSet<string> { ".git", ".github", ".vscode", ".idea", "node_modules", "bin", "obj" };
            if (genericExclusions.Contains(dirNameLower)) return;

            // Language specific exclusions
            foreach (var folder in _projectParser.ExcludedFolders)
            {
                if (folder.Equals(dirNameLower, StringComparison.OrdinalIgnoreCase)) return;
            }
        }

        string currentId;
        if (currentDir == _projectDir)
        {
            currentId = _projectNodeId;
        }
        else
        {
            var dirName = Path.GetFileName(currentDir);
            currentId = $"projectfolder:{_ctx.AbsoluteWorkspacePath}:{relativeDir}";
            
            var folderNode = Node.FromNode(new ProjectFolderNode(currentId, dirName, relativeDir));
            await _ctx.EnqueueUploadNodesAsync(new List<Node> { folderNode });

            var rel = Relationship.FromRelationship(new ContainsRelationship(currentParentId, currentId));
            await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { rel });

            _ctx.IncrementNodeKind(OntologyConstants.NodeLabels.ProjectFolder);
            _ctx.AddNodesCount(1);
            _ctx.AddRelsCount(1);
        }

        // Recurse directories
        foreach (var subDir in Directory.GetDirectories(currentDir))
        {
            await ScanDirectoryAsync(subDir, currentId);
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
                var flParser = new FileLevelParser(_ctx, file, currentId, fileParser);
                await flParser.ParseAsync();
            }
        }
    }

    private async Task ParseDependenciesAsync()
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
                    var targetProjectNodeId = $"project:{targetDir}:";
                    _ctx.AddGlobalProjectDependency(Relationship.FromRelationship(new DependsOnRelationship(_projectNodeId, targetProjectNodeId)));
                }

                // B. Process external package dependencies
                foreach (var extPack in depInfo.ExternalPackages)
                {
                    var packageNodeId = $"package:{extPack.Name.ToLowerInvariant()}";
                    var packageNode = Node.FromNode(new PackageNode(packageNodeId, extPack.Name, extPack.Version, extPack.Type));

                    await _ctx.EnqueueUploadNodesAsync(new List<Node> { packageNode });
                    var rel = Relationship.FromRelationship(new DependsOnRelationship(_projectNodeId, packageNodeId));
                    await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { rel });

                    _ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Package);
                    _ctx.AddNodesCount(1);
                    _ctx.AddRelsCount(1);
                }
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Error parsing dependencies for {_projectParser.ProjectType} in '{_projectDir}': {ex.Message}");
        }
    }

    private async Task LinkProducedPackageAsync()
    {
        bool packageDetected = false;
        try
        {
            var producedPackage = await _projectParser.GetProducedPackageAsync(_projectDir);
            if (producedPackage != null)
            {
                var packageNodeId = $"package:{producedPackage.Name.ToLowerInvariant()}";
                var packageNode = Node.FromNode(new PackageNode(packageNodeId, producedPackage.Name, producedPackage.Version, producedPackage.Type));

                await _ctx.EnqueueUploadNodesAsync(new List<Node> { packageNode });
                var implRel = Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, _projectNodeId));
                await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { implRel });

                _ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Package);
                _ctx.AddNodesCount(1);
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
                var packageNodeId = $"package:{dirName.ToLowerInvariant()}";
                var packageNode = Node.FromNode(new PackageNode(packageNodeId, dirName, "1.0.0", "unknown"));

                await _ctx.EnqueueUploadNodesAsync(new List<Node> { packageNode });
                var implRel = Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, _projectNodeId));
                await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { implRel });

                _ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Package);
                _ctx.AddNodesCount(1);
                _ctx.AddRelsCount(1);
            }
        }
    }
}
