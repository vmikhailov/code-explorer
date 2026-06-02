using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using CodeExplorer.Database;
using CodeExplorer.Common;

namespace CodeExplorer.Parser;

public class ProjectLevelParser
{
    private readonly ParsingContext _ctx;
    private readonly string _projectDir;
    private readonly string _parentContainerId;
    private readonly ILanguageParser _languageParser;
    private readonly string _projectNodeId;
    private readonly GitIgnoreMatcher _gitignore;

    public ProjectLevelParser(ParsingContext ctx, string projectDir, string parentContainerId, ILanguageParser languageParser)
    {
        _ctx = ctx;
        _projectDir = projectDir.Replace('\\', '/');
        _parentContainerId = parentContainerId;
        _languageParser = languageParser;
        _projectNodeId = $"project:{_projectDir}:";
        _gitignore = new GitIgnoreMatcher(_projectDir);
    }

    public async Task ParseAsync()
    {
        var folderName = Path.GetFileName(_projectDir);
        if (string.IsNullOrEmpty(folderName)) folderName = _projectDir;
        await Console.Error.WriteLineAsync($"[WorkspaceParser] Starting scan of project '{folderName}'...");

        // 1. Create and Upload Project node
        var projectNode = new Node(_projectNodeId, OntologyConstants.NodeLabels.Project, new Dictionary<string, object>
        {
            ["name"] = folderName,
            ["path"] = Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, _projectDir).Replace('\\', '/'),
            ["project_type"] = _languageParser.ProjectType
        });
        await _ctx.EnqueueUploadNodesAsync(new List<Node> { projectNode });

        // 2. Relate Parent to Project
        var parentRel = new Relationship(_parentContainerId, _projectNodeId, OntologyConstants.Relationships.Contains);
        await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { parentRel });

        lock (_ctx.NodesByKind)
        {
            if (!_ctx.NodesByKind.ContainsKey(OntologyConstants.NodeLabels.Project)) _ctx.NodesByKind[OntologyConstants.NodeLabels.Project] = 0;
            _ctx.NodesByKind[OntologyConstants.NodeLabels.Project]++;
        }
        _ctx.TotalNodesCount++;
        _ctx.TotalRelsCount++;

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

            // Language specific exlusions
            foreach (var folder in _languageParser.ExcludedFolders)
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
            
            var folderNode = new Node(currentId, OntologyConstants.NodeLabels.ProjectFolder, new Dictionary<string, object>
            {
                ["name"] = dirName,
                ["path"] = relativeDir
            });
            await _ctx.EnqueueUploadNodesAsync(new List<Node> { folderNode });

            var rel = new Relationship(currentParentId, currentId, OntologyConstants.Relationships.Contains);
            await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { rel });

            lock (_ctx.NodesByKind)
            {
                if (!_ctx.NodesByKind.ContainsKey(OntologyConstants.NodeLabels.ProjectFolder)) _ctx.NodesByKind[OntologyConstants.NodeLabels.ProjectFolder] = 0;
                _ctx.NodesByKind[OntologyConstants.NodeLabels.ProjectFolder]++;
            }
            _ctx.TotalNodesCount++;
            _ctx.TotalRelsCount++;
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

            if (_languageParser.CanParse(ext))
            {
                var flParser = new FileLevelParser(_ctx, file, currentId, _languageParser);
                await flParser.ParseAsync();
            }
        }
    }

    private async Task ParseDependenciesAsync()
    {
        try
        {
            var depInfo = await _languageParser.ParseDependenciesAsync(_projectDir);
            if (depInfo != null)
            {
                // A. Process local project dependencies (DependsOn relationships)
                foreach (var localPath in depInfo.LocalProjectPaths)
                {
                    var targetDir = Path.GetFullPath(localPath).Replace('\\', '/');
                    var targetProjectNodeId = $"project:{targetDir}:";
                    lock (_ctx.GlobalProjectDependencies)
                    {
                        _ctx.GlobalProjectDependencies.Add(new Relationship(_projectNodeId, targetProjectNodeId, OntologyConstants.Relationships.DependsOn));
                    }
                }

                // B. Process external package dependencies
                foreach (var extPack in depInfo.ExternalPackages)
                {
                    var packageNodeId = $"package:{extPack.Name.ToLowerInvariant()}";
                    var packageNode = new Node(packageNodeId, OntologyConstants.NodeLabels.Package, new Dictionary<string, object>
                    {
                        ["name"] = extPack.Name,
                        ["version"] = extPack.Version,
                        ["type"] = extPack.Type
                    });

                    await _ctx.EnqueueUploadNodesAsync(new List<Node> { packageNode });
                    var rel = new Relationship(_projectNodeId, packageNodeId, OntologyConstants.Relationships.DependsOn);
                    await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { rel });

                    lock (_ctx.NodesByKind)
                    {
                        if (!_ctx.NodesByKind.ContainsKey(OntologyConstants.NodeLabels.Package)) _ctx.NodesByKind[OntologyConstants.NodeLabels.Package] = 0;
                        _ctx.NodesByKind[OntologyConstants.NodeLabels.Package]++;
                    }
                    _ctx.TotalNodesCount++;
                    _ctx.TotalRelsCount++;
                }
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Error parsing dependencies for {_languageParser.ProjectType} in '{_projectDir}': {ex.Message}");
        }
    }

    private async Task LinkProducedPackageAsync()
    {
        bool packageDetected = false;
        try
        {
            var producedPackage = await _languageParser.GetProducedPackageAsync(_projectDir);
            if (producedPackage != null)
            {
                var packageNodeId = $"package:{producedPackage.Name.ToLowerInvariant()}";
                var packageNode = new Node(packageNodeId, OntologyConstants.NodeLabels.Package, new Dictionary<string, object>
                {
                    ["name"] = producedPackage.Name,
                    ["version"] = producedPackage.Version,
                    ["type"] = producedPackage.Type
                });

                await _ctx.EnqueueUploadNodesAsync(new List<Node> { packageNode });
                var implRel = new Relationship(packageNodeId, _projectNodeId, OntologyConstants.Relationships.ImplementedBy);
                await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { implRel });

                lock (_ctx.NodesByKind)
                {
                    if (!_ctx.NodesByKind.ContainsKey(OntologyConstants.NodeLabels.Package)) _ctx.NodesByKind[OntologyConstants.NodeLabels.Package] = 0;
                    _ctx.NodesByKind[OntologyConstants.NodeLabels.Package]++;
                }
                _ctx.TotalNodesCount++;
                _ctx.TotalRelsCount++;

                packageDetected = true;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Error getting produced package from {_languageParser.ProjectType} parser in '{_projectDir}': {ex.Message}");
        }

        if (!packageDetected)
        {
            var dirName = Path.GetFileName(_projectDir);
            if (!string.IsNullOrEmpty(dirName))
            {
                var packageNodeId = $"package:{dirName.ToLowerInvariant()}";
                var packageNode = new Node(packageNodeId, OntologyConstants.NodeLabels.Package, new Dictionary<string, object>
                {
                    ["name"] = dirName,
                    ["version"] = "1.0.0",
                    ["type"] = "unknown"
                });

                await _ctx.EnqueueUploadNodesAsync(new List<Node> { packageNode });
                var implRel = new Relationship(packageNodeId, _projectNodeId, OntologyConstants.Relationships.ImplementedBy);
                await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { implRel });

                lock (_ctx.NodesByKind)
                {
                    if (!_ctx.NodesByKind.ContainsKey(OntologyConstants.NodeLabels.Package)) _ctx.NodesByKind[OntologyConstants.NodeLabels.Package] = 0;
                    _ctx.NodesByKind[OntologyConstants.NodeLabels.Package]++;
                }
                _ctx.TotalNodesCount++;
                _ctx.TotalRelsCount++;
            }
        }
    }
}
