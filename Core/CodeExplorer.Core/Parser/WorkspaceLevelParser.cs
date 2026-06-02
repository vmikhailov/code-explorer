using CodeExplorer.Common;
using CodeExplorer.Database;

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
            await Console.Error.WriteLineAsync("[WorkspaceParser] Clearing project workspaces sequentially to avoid database lock contention...");
            foreach (var projectDir in projectDirs)
            {
                await Console.Error.WriteLineAsync($"[WorkspaceParser] Clearing previous project data for '{projectDir}'...");
                await _ctx.DbClient.ClearWorkspaceAsync(projectDir);
            }
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Clearing previous root workspace data for '{_absoluteWorkspacePath}'...");
            await _ctx.DbClient.ClearWorkspaceAsync(_absoluteWorkspacePath);
        }

        if (projectDirs.Count > 1 || (projectDirs.Count == 1 && projectDirs[0] != _absoluteWorkspacePath))
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Multi-project workspace detected. Discovering {projectDirs.Count} projects...");
            foreach (var projectDir in projectDirs)
            {
                _excludedSubdirectories.Add(projectDir);
            }

            var folderName = Path.GetFileName(_absoluteWorkspacePath);
            if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;

            var workspaceNode = Node.FromNode(new WorkspaceNode(
                _workspaceNodeId,
                folderName,
                _absoluteWorkspacePath
            ));
            await _ctx.EnqueueUploadNodesAsync(new List<Node> { workspaceNode });

            _ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Workspace);
            _ctx.AddNodesCount(1);

            // Recursively scan, discovering projects inline!
            await ScanDirectoryAsync(_absoluteWorkspacePath, _workspaceNodeId, new HashSet<string>());
        }
        else
        {
            // Single project or root workspace is a project. Just index it as a project!
            await Console.Error.WriteLineAsync("[WorkspaceParser] Single project workspace detected.");
            
            var projectDir = _absoluteWorkspacePath;
            var filesInDir = Directory.GetFiles(projectDir);
            IProjectParser? matchedParser = null;
            lock (WorkspaceParser.ProjectParsers)
            {
                matchedParser = WorkspaceParser.ProjectParsers.FirstOrDefault(p => p.IsProjectDirectory(projectDir, filesInDir));
            }

            if (matchedParser == null)
            {
                // Fallback to C# or first registered if none detected, or construct ProjectLevelParser with dummy/fallback
                lock (WorkspaceParser.ProjectParsers)
                {
                    matchedParser = WorkspaceParser.ProjectParsers.FirstOrDefault();
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
            // Find matching language parser for project signature
            var projectFilesInDir = Directory.GetFiles(currentDir);
            IProjectParser? matchedParser = null;
            lock (WorkspaceParser.ProjectParsers)
            {
                matchedParser = WorkspaceParser.ProjectParsers.FirstOrDefault(p => p.IsProjectDirectory(currentDir, projectFilesInDir));
            }

            if (matchedParser != null)
            {
                var projectParser = new ProjectLevelParser(_ctx, currentDir, currentParentId, matchedParser);
                await projectParser.ParseAsync();
            }
            return; // Bypasses recursing deeper since the ProjectLevelParser will map it recursively!
        }

        var relativeDir = Path.GetRelativePath(_absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        // 1. Check GitIgnore exclusions first
        if (!string.IsNullOrEmpty(relativeDir) && _gitignore.IsIgnored(relativeDir, true))
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] GitIgnore: Ignoring directory '{relativeDir}'");
            return;
        }

        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;
        var dirNameLower = dirName.ToLowerInvariant();

        // 2. Generic default exclusions
        var genericExclusions = new HashSet<string> { ".git", ".github", ".vscode", ".idea" };
        if (genericExclusions.Contains(dirNameLower))
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Generic: Skipping VCS/IDE folder '{relativeDir}'");
            return;
        }

        // 3. Scan folder for project signatures to propagate exclusions
        var filesInDir = Directory.GetFiles(currentDir);
        var newlyDetectedTypes = new HashSet<string>();
        lock (WorkspaceParser.ProjectParsers)
        {
            foreach (var parser in WorkspaceParser.ProjectParsers)
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
        lock (WorkspaceParser.ProjectParsers)
        {
            foreach (var type in subProjectTypes)
            {
                var parser = WorkspaceParser.ProjectParsers.FirstOrDefault(p => p.ProjectType == type);
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
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Exclusion: Skipping directory '{relativeDir}' (matches language exclusion '{matchedExclusionFolder}' for '{matchedExclusionType}' project type)");
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
            var folderNode = Node.FromNode(new WorkspaceFolderNode(currentId, dirName, relativeDir));
            await _ctx.EnqueueUploadNodesAsync(new List<Node> { folderNode });
            
            var rel = Relationship.FromRelationship(new ContainsRelationship(currentParentId, currentId));
            await _ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { rel });

            _ctx.IncrementNodeKind(OntologyConstants.NodeLabels.WorkspaceFolder);
            _ctx.AddNodesCount(1);
            _ctx.AddRelsCount(1);
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

    private List<string> FindProjectDirectories()
    {
        var projectDirs = new List<string>();
        var rootFiles = Directory.GetFiles(_absoluteWorkspacePath);
        bool rootIsProject = false;
        lock (WorkspaceParser.ProjectParsers)
        {
            foreach (var parser in WorkspaceParser.ProjectParsers)
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
        lock (WorkspaceParser.ProjectParsers)
        {
            foreach (var parser in WorkspaceParser.ProjectParsers)
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
