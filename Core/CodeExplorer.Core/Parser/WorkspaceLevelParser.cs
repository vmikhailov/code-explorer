using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using CodeExplorer.Database;
using CodeExplorer.Common;

namespace CodeExplorer.Parser;

public class WorkspaceLevelParser
{
    private readonly ParsingContext _ctx;
    private readonly string _absoluteWorkspacePath;
    private readonly string _workspaceNodeId;
    private readonly GitIgnoreMatcher _gitignore;
    private readonly HashSet<string> _excludedSubdirectories = new(StringComparer.OrdinalIgnoreCase);

    public WorkspaceLevelParser(ParsingContext ctx)
    {
        _ctx = ctx;
        _absoluteWorkspacePath = ctx.AbsoluteWorkspacePath;
        _workspaceNodeId = $"workspace:{_absoluteWorkspacePath}";
        _gitignore = new GitIgnoreMatcher(_absoluteWorkspacePath);
    }

    public async Task ParseAsync()
    {
        // 1. Discover all projects in this workspace
        var projectDirs = FindProjectDirectories();

        // 2. Sequential upfront clearances to avoid transaction lock contention
        if (_ctx.Clear)
        {
            Console.Error.WriteLine("[WorkspaceParser] Clearing project workspaces sequentially to avoid database lock contention...");
            foreach (var projectDir in projectDirs)
            {
                Console.Error.WriteLine($"[WorkspaceParser] Clearing previous project data for '{projectDir}'...");
                await _ctx.DbClient.ClearWorkspaceAsync(projectDir);
            }
            Console.Error.WriteLine($"[WorkspaceParser] Clearing previous root workspace data for '{_absoluteWorkspacePath}'...");
            await _ctx.DbClient.ClearWorkspaceAsync(_absoluteWorkspacePath);
        }

        if (projectDirs.Count > 1 || (projectDirs.Count == 1 && projectDirs[0] != _absoluteWorkspacePath))
        {
            Console.Error.WriteLine($"[WorkspaceParser] Multi-project workspace detected. Discovering {projectDirs.Count} projects...");
            foreach (var projectDir in projectDirs)
            {
                _excludedSubdirectories.Add(projectDir);
            }

            // Index all projects sequentially
            foreach (var projectDir in projectDirs)
            {
                // Find matching language parser for project signature
                var filesInDir = Directory.GetFiles(projectDir);
                ILanguageParser? matchedParser = null;
                lock (WorkspaceParser.Parsers)
                {
                    matchedParser = WorkspaceParser.Parsers.FirstOrDefault(p => p.IsProjectDirectory(projectDir, filesInDir));
                }

                if (matchedParser != null)
                {
                    var projectParser = new ProjectLevelParser(_ctx, projectDir, _workspaceNodeId, matchedParser);
                    await projectParser.ParseAsync();
                }
            }

            // Index any residual files at the root level outside any project
            Console.Error.WriteLine("[WorkspaceParser] Ingesting root files outside of any detected project...");

            var folderName = Path.GetFileName(_absoluteWorkspacePath);
            if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;

            var workspaceNode = new Node(
                _workspaceNodeId,
                OntologyConstants.NodeLabels.Workspace,
                new Dictionary<string, object>
                {
                    ["path"] = _absoluteWorkspacePath,
                    ["name"] = folderName
                }
            );
            await _ctx.SharedChannel.Writer.WriteAsync(() => _ctx.DbClient.UploadNodesAsync(new List<Node> { workspaceNode }));

            lock (_ctx.NodesByKind)
            {
                if (!_ctx.NodesByKind.ContainsKey(OntologyConstants.NodeLabels.Workspace)) _ctx.NodesByKind[OntologyConstants.NodeLabels.Workspace] = 0;
                _ctx.NodesByKind[OntologyConstants.NodeLabels.Workspace]++;
            }
            _ctx.TotalNodesCount++;

            await ScanDirectoryAsync(_absoluteWorkspacePath, _workspaceNodeId, new HashSet<string>());
        }
        else
        {
            // Single project or root workspace is a project. Just index it as a project!
            Console.Error.WriteLine("[WorkspaceParser] Single project workspace detected.");
            
            var projectDir = _absoluteWorkspacePath;
            var filesInDir = Directory.GetFiles(projectDir);
            ILanguageParser? matchedParser = null;
            lock (WorkspaceParser.Parsers)
            {
                matchedParser = WorkspaceParser.Parsers.FirstOrDefault(p => p.IsProjectDirectory(projectDir, filesInDir));
            }

            if (matchedParser == null)
            {
                // Fallback to C# or first registered if none detected, or construct ProjectLevelParser with dummy/fallback
                lock (WorkspaceParser.Parsers)
                {
                    matchedParser = WorkspaceParser.Parsers.FirstOrDefault();
                }
            }

            if (matchedParser != null)
            {
                var projectParser = new ProjectLevelParser(_ctx, projectDir, _workspaceNodeId, matchedParser);
                await projectParser.ParseAsync();
            }
        }
    }

    private async Task ScanDirectoryAsync(string currentDir, string currentParentId, HashSet<string> activeProjectTypes)
    {
        if (_excludedSubdirectories.Contains(currentDir))
        {
            Console.Error.WriteLine($"[WorkspaceParser] Master: Skipping project directory '{currentDir}' (will be indexed independently)");
            return;
        }

        var relativeDir = Path.GetRelativePath(_absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        // 1. Check GitIgnore exclusions first
        if (!string.IsNullOrEmpty(relativeDir) && _gitignore.IsIgnored(relativeDir, true))
        {
            Console.Error.WriteLine($"[WorkspaceParser] GitIgnore: Ignoring directory '{relativeDir}'");
            return;
        }

        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;
        var dirNameLower = dirName.ToLowerInvariant();

        // 2. Generic default exclusions
        var genericExclusions = new HashSet<string> { ".git", ".github", ".vscode", ".idea" };
        if (genericExclusions.Contains(dirNameLower))
        {
            Console.Error.WriteLine($"[WorkspaceParser] Generic: Skipping VCS/IDE folder '{relativeDir}'");
            return;
        }

        // 3. Scan folder for project signatures to propagate exclusions
        var filesInDir = Directory.GetFiles(currentDir);
        var newlyDetectedTypes = new HashSet<string>();
        lock (WorkspaceParser.Parsers)
        {
            foreach (var parser in WorkspaceParser.Parsers)
            {
                if (parser.IsProjectDirectory(currentDir, filesInDir))
                {
                    newlyDetectedTypes.Add(parser.ProjectType);
                }
            }
        }

        // Propagate exclusions
        var subProjectTypes = new HashSet<string>(activeProjectTypes);
        foreach (var type in newlyDetectedTypes)
        {
            subProjectTypes.Add(type);
        }

        // Check if excluded based on active project types and language exclusions
        bool shouldExclude = false;
        string? matchedExclusionFolder = null;
        string? matchedExclusionType = null;
        lock (WorkspaceParser.Parsers)
        {
            foreach (var type in subProjectTypes)
            {
                var parser = WorkspaceParser.Parsers.FirstOrDefault(p => p.ProjectType == type);
                if (parser != null)
                {
                    foreach (var folder in parser.ExcludedFolders)
                    {
                        if (folder.Equals(dirNameLower, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldExclude = true;
                            matchedExclusionFolder = folder;
                            matchedExclusionType = type;
                            break;
                        }
                    }
                }
                if (shouldExclude) break;
            }
        }

        if (shouldExclude)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Exclusion: Skipping directory '{relativeDir}' (matches language exclusion '{matchedExclusionFolder}' for '{matchedExclusionType}' project type)");
            return;
        }

        // Register current directory in structural nodes
        string currentId;
        if (string.IsNullOrEmpty(relativeDir))
        {
            currentId = _workspaceNodeId;
        }
        else
        {
            currentId = $"workspacefolder:{_absoluteWorkspacePath}:{relativeDir}";
            var folderNode = new Node(currentId, OntologyConstants.NodeLabels.WorkspaceFolder, new Dictionary<string, object>
            {
                ["name"] = dirName,
                ["path"] = relativeDir
            });
            await _ctx.SharedChannel.Writer.WriteAsync(() => _ctx.DbClient.UploadNodesAsync(new List<Node> { folderNode }));
            
            var rel = new Relationship(currentParentId, currentId, OntologyConstants.Relationships.Contains);
            await _ctx.SharedChannel.Writer.WriteAsync(() => _ctx.DbClient.UploadRelationshipsAsync(new List<Relationship> { rel }));

            lock (_ctx.NodesByKind)
            {
                if (!_ctx.NodesByKind.ContainsKey(OntologyConstants.NodeLabels.WorkspaceFolder)) _ctx.NodesByKind[OntologyConstants.NodeLabels.WorkspaceFolder] = 0;
                _ctx.NodesByKind[OntologyConstants.NodeLabels.WorkspaceFolder]++;
            }
            _ctx.TotalNodesCount++;
            _ctx.TotalRelsCount++;
        }

        // Recurse into subdirectories
        foreach (var subDir in Directory.GetDirectories(currentDir))
        {
            await ScanDirectoryAsync(subDir, currentId, subProjectTypes);
        }

        // Scan root level loose files
        foreach (var file in filesInDir)
        {
            var ext = Path.GetExtension(file).ToLower();
            var relativeFile = Path.GetRelativePath(_absoluteWorkspacePath, file).Replace('\\', '/');

            if (_gitignore.IsIgnored(relativeFile, false))
            {
                Console.Error.WriteLine($"[WorkspaceParser] GitIgnore: Ignoring file '{relativeFile}'");
                continue;
            }

            ILanguageParser? fileParser = null;
            lock (WorkspaceParser.Parsers)
            {
                fileParser = WorkspaceParser.Parsers.FirstOrDefault(p => p.CanParse(ext));
            }

            if (fileParser != null)
            {
                var flParser = new FileLevelParser(_ctx, file, currentId, fileParser);
                await flParser.ParseAsync();
            }
        }
    }

    private List<string> FindProjectDirectories()
    {
        var projectDirs = new List<string>();
        var rootFiles = Directory.GetFiles(_absoluteWorkspacePath);
        bool rootIsProject = false;
        lock (WorkspaceParser.Parsers)
        {
            foreach (var parser in WorkspaceParser.Parsers)
            {
                if (parser.IsProjectDirectory(_absoluteWorkspacePath, rootFiles))
                {
                    rootIsProject = true;
                    break;
                }
            }
        }

        if (rootIsProject)
        {
            return projectDirs;
        }

        FindProjectDirsInternal(_absoluteWorkspacePath, projectDirs);
        return projectDirs;
    }

    private void FindProjectDirsInternal(string currentDir, List<string> projectDirs)
    {
        var relativeDir = Path.GetRelativePath(_absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        if (!string.IsNullOrEmpty(relativeDir))
        {
            if (_gitignore.IsIgnored(relativeDir, true)) return;

            var dirNameLower = Path.GetFileName(currentDir).ToLowerInvariant();
            var genericExclusions = new HashSet<string> { ".git", ".github", ".vscode", ".idea", "node_modules", "bin", "obj" };
            if (genericExclusions.Contains(dirNameLower)) return;
        }

        var filesInDir = Directory.GetFiles(currentDir);
        bool isProject = false;
        lock (WorkspaceParser.Parsers)
        {
            foreach (var parser in WorkspaceParser.Parsers)
            {
                if (parser.IsProjectDirectory(currentDir, filesInDir))
                {
                    isProject = true;
                    break;
                }
            }
        }

        if (isProject)
        {
            projectDirs.Add(currentDir);
            return;
        }

        foreach (var subDir in Directory.GetDirectories(currentDir))
        {
            FindProjectDirsInternal(subDir, projectDirs);
        }
    }
}
