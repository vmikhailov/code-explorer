using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer1PhysicalParser
{
    public async Task<Layer1Result> ParseAsync(ParsingContext ctx)
    {
        ctx.Layer1Log.ScanStarted();

        // 0. Get or create Workspace ID from database (auto-incremented)
        var wsId = ctx.PersistToGraphDb && ctx.DbClient != null
            ? await ctx.DbClient.GetOrCreateWorkspaceIdAsync(ctx.HostWorkspacePath)
            : "ws0";
        ctx.WorkspaceId = wsId;
        ctx.CancellationToken.ThrowIfCancellationRequested();

        if (ctx.PersistToGraphDb && ctx.DbClient != null)
        {
            await ctx.DbClient.SaveEmptyWorkspaceNodeAsync(wsId, ctx.HostWorkspacePath);
            ctx.CancellationToken.ThrowIfCancellationRequested();
        }

        var normalizedHostPath = ctx.HostWorkspacePath.NormalizePath();
        var folderName = Path.GetFileName(normalizedHostPath);
        if (string.IsNullOrEmpty(folderName)) folderName = normalizedHostPath;

        var workspaceName = folderName;
        if (!ctx.Clear && ctx.PersistToGraphDb && ctx.DbClient != null)
        {
            try
            {
                var existingWs = await ctx.DbClient.AsRepository().GetWorkspaceAsync(ctx.CancellationToken);
                if (!string.IsNullOrWhiteSpace(existingWs?.Name))
                {
                    workspaceName = existingWs.Name;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Layer1Log.FileReadFailed(normalizedHostPath, ex);
            }
        }

        var hostPath = PathTools.NormalizeToHostPath(ctx.HostWorkspacePath);
        var workspaceNode = new WorkspaceNode(wsId, workspaceName, hostPath)
        {
            IndexedAt = DateTime.UtcNow.ToString("o")
        };

        var filesNodeId = $"{wsId}:{OntologyConstants.IdPrefixes.FilesStructure}";
        var filesStructureNode = new FilesStructureNode(filesNodeId, "FilesStructure", hostPath);
        workspaceNode.Children.Add(filesStructureNode);

        if (!ctx.LightweightAstScanMode)
        {
            var rootRepoDir = GitSettingsParser.FindRepoRoot(ctx.AbsoluteWorkspacePath);
            if (rootRepoDir != null)
            {
                var gitSettingsNode = GitSettingsParser.Parse(wsId, ctx.AbsoluteWorkspacePath, rootRepoDir);
                if (gitSettingsNode != null)
                {
                    filesStructureNode.Children.Add(gitSettingsNode);
                    ctx.RegisterGitRepository(rootRepoDir, gitSettingsNode);
                }
            }
        }

        var files = new List<FileNode>();
        var folders = new List<FolderNode>();
        var gitignore = new GitIgnoreMatcher(ctx.AbsoluteWorkspacePath, ctx.FileFilter);
        PhysicalFileStore.ClearWorkspace(ctx.AbsoluteWorkspacePath);

        var discoveredDirs = new List<DiscoveredDirectoryEntry>();
        var vscodeTaskDirs = new List<string>();

        if (!ctx.IsSubtreeScan)
        {
            DiscoverDirectoryTree(ctx.AbsoluteWorkspacePath, filesStructureNode, workspaceNode, gitignore, null, discoveredDirs, vscodeTaskDirs, ctx);
        }
        else
        {
            var initialParsers = ctx.FileFilter.ResolveAncestorProjectParsers(ctx.ScanPath, ctx.AbsoluteWorkspacePath);
            var currentParent = BuildSubtreeIntermediateFolders(wsId, filesStructureNode, folders, ctx);
            DiscoverDirectoryTree(ctx.ScanPath, currentParent, workspaceNode, gitignore, initialParsers, discoveredDirs, vscodeTaskDirs, ctx);
        }

        foreach (var taskDir in vscodeTaskDirs)
        {
            await TryStreamVscodeTasksAsync(taskDir, ctx);
        }

        var candidates = CollectCandidateFiles(discoveredDirs, gitignore, ctx);
        var streamedFiles = new FileNode?[candidates.Count];

        if (candidates.Count > 0)
        {
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ctx.CancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            await Parallel.ForEachAsync(candidates, parallelOptions, async (item, _) =>
            {
                streamedFiles[item.Index] = await StreamCandidateFileAsync(item, ctx);
            });

            for (int i = 0; i < candidates.Count; i++)
            {
                var fileNode = streamedFiles[i];
                if (fileNode == null) continue;
                candidates[i].ParentNode.Children.Add(fileNode);
                files.Add(fileNode);
            }
        }

        AttachNonEmptyFolders(discoveredDirs, folders);

        ctx.Layer1Log.ScanCompleted(files.Count, folders.Count);
        return new Layer1Result(workspaceNode, filesStructureNode, files, folders);
    }

    private sealed record DiscoveredDirectoryEntry(
        string CurrentDir,
        string RelativeDir,
        bool IsRoot,
        IOntologyNode ParentNode,
        FolderNode? FolderNode,
        FileInfo[] DirFiles,
        IReadOnlyList<IProjectParser>? ActiveParsers,
        bool IsProjectRoot);

    private sealed record CandidateFileItem(
        int Index,
        FileInfo FileInfo,
        string RelativeFile,
        string AbsoluteFilePath,
        string FileId,
        IOntologyNode ParentNode,
        bool IsManifest,
        bool IsSupportedSourceOrConfig);

    private static IOntologyNode BuildSubtreeIntermediateFolders(
        string wsId,
        FilesStructureNode filesStructureNode,
        List<FolderNode> folders,
        ParsingContext ctx)
    {
        var relativeSubtree = ctx.ScanPath.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        var segments = relativeSubtree.Split('/', StringSplitOptions.RemoveEmptyEntries);

        IOntologyNode currentParent = filesStructureNode;
        var currentPath = ctx.AbsoluteWorkspacePath;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            currentPath = Path.Combine(currentPath, segments[i]).NormalizeSlashes();
            var folderId = $"{wsId}:{OntologyConstants.IdPrefixes.Folder}:{currentPath}";
            var intermediateFolder = new FolderNode(folderId, segments[i], currentPath);

            currentParent.Children.Add(intermediateFolder);
            folders.Add(intermediateFolder);
            currentParent = intermediateFolder;
        }

        return currentParent;
    }

    private static void DiscoverDirectoryTree(
        string currentDir,
        IOntologyNode parentNode,
        WorkspaceNode workspaceNode,
        GitIgnoreMatcher gitignore,
        IReadOnlyList<IProjectParser>? inheritedParsers,
        List<DiscoveredDirectoryEntry> discoveredDirs,
        List<string> vscodeTaskDirs,
        ParsingContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        var isRoot = string.Equals(currentDir, ctx.AbsoluteWorkspacePath, StringComparison.OrdinalIgnoreCase);
        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

        var relativeDir = isRoot ? string.Empty : currentDir.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        if (relativeDir == ".") relativeDir = string.Empty;

        if (ctx.IncrementalRelevantDirectories != null &&
            !string.IsNullOrEmpty(relativeDir) &&
            !ctx.IncrementalRelevantDirectories.Contains(relativeDir))
        {
            return;
        }

        if (!isRoot && ctx.FileFilter.IsExcludedDirectory(dirName, currentDir, inheritedParsers))
        {
            ctx.Layer1Log.SkippingExcludedDirectory(relativeDir);
            return;
        }

        if (!isRoot && GitSettingsParser.IsGitWorktreeDirectory(currentDir))
        {
            ctx.Layer1Log.SkippingExcludedDirectory(relativeDir);
            return;
        }

        if (!string.IsNullOrEmpty(relativeDir))
        {
            gitignore.LoadScopedDirectory(currentDir, relativeDir);
        }

        if (!string.IsNullOrEmpty(relativeDir) && gitignore.IsIgnored(relativeDir, true))
        {
            ctx.Layer1Log.SkippingGitIgnoredDirectory(relativeDir);
            return;
        }

        var dirInfo = new DirectoryInfo(currentDir);
        var dirFiles = dirInfo.GetFiles();
        var dirFilePaths = new string[dirFiles.Length];
        for (int i = 0; i < dirFiles.Length; i++)
        {
            dirFilePaths[i] = dirFiles[i].FullName;
        }
        PhysicalFileStore.SetDirectoryFiles(currentDir, dirFilePaths);

        var directParsers = ctx.FileFilter.MatchDirectProjectParsers(
            currentDir,
            dirFiles,
            dirFilePaths,
            allowFallback: inheritedParsers is not { Count: > 0 });
        var activeParsers = directParsers is { Count: > 0 } ? directParsers : inheritedParsers;
        var parsersToNotify = activeParsers is { Count: > 0 } ? activeParsers : ctx.FileFilter.ConnectedProjectParsers;

        for (int i = 0; i < parsersToNotify.Count; i++)
        {
            parsersToNotify[i].OnDirectoryScan(currentDir, relativeDir, gitignore, ctx.Layer1Log.DirectoryScanHookMessage);
            parsersToNotify[i].InspectDirectoryFiles(currentDir, dirFiles, workspaceNode);
        }

        FolderNode? folderNode = null;
        var currentParentNode = parentNode;

        if (!isRoot)
        {
            var absoluteFolderPath = currentDir.NormalizeFullPath();
            var folderId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Folder}:{absoluteFolderPath}";
            folderNode = new FolderNode(folderId, dirName, absoluteFolderPath);
            currentParentNode = folderNode;

            AttachNestedGitSettingsIfPresent(currentDir, folderNode, ctx);
        }

        if (File.Exists(Path.Combine(currentDir, ".vscode", OntologyConstants.ManifestFields.TasksJsonFileName)))
        {
            vscodeTaskDirs.Add(currentDir);
        }

        discoveredDirs.Add(new DiscoveredDirectoryEntry(
            currentDir,
            relativeDir,
            isRoot,
            parentNode,
            folderNode,
            dirFiles,
            activeParsers,
            IsProjectRoot: directParsers is { Count: > 0 }));

        foreach (var subDir in dirInfo.GetDirectories())
        {
            DiscoverDirectoryTree(subDir.FullName, currentParentNode, workspaceNode, gitignore, activeParsers, discoveredDirs, vscodeTaskDirs, ctx);
        }
    }

    private static void AttachNestedGitSettingsIfPresent(string currentDir, FolderNode folderNode, ParsingContext ctx)
    {
        var gitPath = Path.Combine(currentDir, ".git");
        if (!Directory.Exists(gitPath) && !File.Exists(gitPath)) return;

        var subGitNode = GitSettingsParser.Parse(ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, currentDir);
        if (subGitNode != null)
        {
            folderNode.Children.Add(subGitNode);
            ctx.RegisterGitRepository(currentDir, subGitNode);
        }
    }

    private static List<CandidateFileItem> CollectCandidateFiles(
        List<DiscoveredDirectoryEntry> discoveredDirs,
        GitIgnoreMatcher gitignore,
        ParsingContext ctx)
    {
        var hasAnyOwnedDirectory = discoveredDirs.Any(d => d.ActiveParsers is { Count: > 0 });
        var candidates = new List<CandidateFileItem>();

        foreach (var dirEntry in discoveredDirs)
        {
            var fileParentNode = (IOntologyNode?)dirEntry.FolderNode ?? dirEntry.ParentNode;
            foreach (var fileInfo in dirEntry.DirFiles)
            {
                var file = fileInfo.FullName;
                var relativeFile = file.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);

                if (gitignore.IsIgnored(relativeFile, false)) continue;
                if (ctx.CustomFileExclusionPredicate?.Invoke(fileInfo.Name, relativeFile) == true) continue;

                var fileName = fileInfo.Name;
                var isManifest = ctx.FileFilter.IsManifestFile(fileName);
                var isInIncrementalScope = ctx.IncrementalChangedFiles == null || ctx.IncrementalChangedFiles.Contains(relativeFile);
                if (!isManifest && !isInIncrementalScope) continue;

                bool isSupportedSourceOrConfig;
                if (!isInIncrementalScope)
                {
                    isSupportedSourceOrConfig = false;
                }
                else if (hasAnyOwnedDirectory && dirEntry.ActiveParsers is not { Count: > 0 })
                {
                    isSupportedSourceOrConfig = WorkspaceFileFilter.IsStandaloneContractOrEnvFile(relativeFile, dirEntry.IsRoot);
                }
                else
                {
                    isSupportedSourceOrConfig = ctx.FileFilter.IsSupportedSourceOrConfigFile(relativeFile, dirEntry.ActiveParsers);
                }

                if (!isSupportedSourceOrConfig && !isManifest) continue;

                if (isSupportedSourceOrConfig && ctx.FileFilter.ShouldSkipFileMetadata(fileInfo))
                {
                    if (!isManifest) continue;
                    isSupportedSourceOrConfig = false;
                }

                var absoluteFilePath = file.NormalizeSlashes();
                var fileId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{relativeFile}";
                candidates.Add(new CandidateFileItem(
                    candidates.Count,
                    fileInfo,
                    relativeFile,
                    absoluteFilePath,
                    fileId,
                    fileParentNode,
                    isManifest,
                    isSupportedSourceOrConfig));
            }
        }

        return candidates;
    }

    private static void AttachNonEmptyFolders(
        List<DiscoveredDirectoryEntry> discoveredDirs,
        List<FolderNode> folders)
    {
        var keptFolderIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = discoveredDirs.Count - 1; i >= 0; i--)
        {
            var entry = discoveredDirs[i];
            if (entry.FolderNode == null) continue;

            var keep = entry.IsProjectRoot ||
                       entry.FolderNode.Children.Count > 0 ||
                       keptFolderIds.Contains(entry.FolderNode.Id);

            if (keep)
            {
                keptFolderIds.Add(entry.FolderNode.Id);
                if (entry.ParentNode is FolderNode parentFolder)
                {
                    keptFolderIds.Add(parentFolder.Id);
                }
            }
        }

        for (int i = 0; i < discoveredDirs.Count; i++)
        {
            var entry = discoveredDirs[i];
            if (entry.FolderNode != null && keptFolderIds.Contains(entry.FolderNode.Id))
            {
                entry.ParentNode.Children.Add(entry.FolderNode);
                folders.Add(entry.FolderNode);
            }
        }
    }

    private const string Layer1Stage = "Layer1_Physical";
    private const string IssueCategoryIoError = "io_error";
    private const string SeverityWarning = "warning";
    private const string IssueTasksJsonReadError = "TasksJsonReadError";
    private const string IssueManifestReadError = "ManifestReadError";
    private const string IssueFileReadError = "FileReadError";

    private static async Task TryStreamVscodeTasksAsync(string currentDir, ParsingContext ctx)
    {
        var vscodeTasksPath = Path.Combine(currentDir, ".vscode", OntologyConstants.ManifestFields.TasksJsonFileName);
        if (!File.Exists(vscodeTasksPath)) return;

        var relTasks = vscodeTasksPath.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        try
        {
            var absTasks = vscodeTasksPath.NormalizeFullPath();
            var tasksInfo = new FileInfo(vscodeTasksPath);
            var streamedTasks = await PhysicalFileStore.StreamAndAnalyzeFileAsync(
                absTasks,
                relTasks,
                OntologyConstants.ManifestFields.TasksJsonFileName,
                tasksInfo.Length,
                isManifest: true,
                isSupportedSourceOrConfig: false,
                ctx.FileFilter,
                ctx.CancellationToken);
            if (streamedTasks.ContentHash != null)
            {
                ctx.DiscoveredManifestFiles[relTasks] = (absTasks, streamedTasks.ContentHash, tasksInfo.LastWriteTimeUtc, tasksInfo.Length);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(relTasks, ex);
            ctx.RecordScanIssue(Layer1Stage, IssueCategoryIoError, relTasks, ex.Message, name: IssueTasksJsonReadError, severity: SeverityWarning, exception: ex);
        }
    }

    private static async Task<FileNode?> StreamCandidateFileAsync(CandidateFileItem item, ParsingContext ctx)
    {
        var sizeBytes = item.FileInfo.Length;
        int? lineCount = null;
        string? contentHash = null;
        DateTime? lastModifiedUtc = null;
        string? artifactRole = null;
        string? decodedText = null;
        var skipByContent = false;
        try
        {
            lastModifiedUtc = item.FileInfo.LastWriteTimeUtc;
            var shouldComputeHash = item.IsManifest || ctx.PersistToGraphDb;
            var streamed = await PhysicalFileStore.StreamAndAnalyzeFileAsync(
                item.AbsoluteFilePath,
                item.RelativeFile,
                item.FileInfo.Name,
                sizeBytes,
                item.IsManifest,
                item.IsSupportedSourceOrConfig,
                ctx.FileFilter,
                ctx.CancellationToken,
                computeHash: shouldComputeHash);
            skipByContent = streamed.SkipByContent;
            contentHash = streamed.ContentHash;
            lineCount = streamed.LineCount;
            artifactRole = streamed.ArtifactRole;
            decodedText = streamed.DecodedText;
            if (item.IsManifest && contentHash != null && lastModifiedUtc.HasValue)
            {
                ctx.DiscoveredManifestFiles[item.RelativeFile] = (item.AbsoluteFilePath, contentHash, lastModifiedUtc.Value, sizeBytes);
            }
            if (skipByContent && lastModifiedUtc.HasValue)
            {
                ctx.DiscoveredContentSkippedFiles[item.RelativeFile] = (item.AbsoluteFilePath, lastModifiedUtc.Value, sizeBytes);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(item.RelativeFile, ex);
            if (item.IsSupportedSourceOrConfig || item.IsManifest)
            {
                var issueName = item.IsManifest ? IssueManifestReadError : IssueFileReadError;
                ctx.RecordScanIssue(
                    Layer1Stage,
                    IssueCategoryIoError,
                    item.RelativeFile,
                    ex.Message,
                    name: issueName,
                    severity: SeverityWarning,
                    exception: ex,
                    parentNodeId: item.IsSupportedSourceOrConfig ? item.FileId : null,
                    isFileParent: item.IsSupportedSourceOrConfig);
            }
        }

        if (!item.IsSupportedSourceOrConfig || skipByContent)
        {
            return null;
        }

        return new FileNode(item.FileId, item.FileInfo.Name, item.RelativeFile, item.AbsoluteFilePath, false, null, null, lineCount, sizeBytes)
        {
            ContentHash = contentHash,
            LastModifiedUtc = lastModifiedUtc,
            ArtifactRole = artifactRole ?? ctx.FileFilter.ClassifyFileArtifactRole(item.RelativeFile, item.FileInfo.Name),
            PreloadedContent = decodedText
        };
    }

    public static HashSet<string> ExcludedDirectoryNames => new(WorkspaceFileFilter.ExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
    public static bool IsExcludedDirectory(string dirName) => WorkspaceFileFilter.Default.IsExcludedDirectory(dirName);
    public static bool IsSupportedFile(string fileName) => WorkspaceFileFilter.Default.IsSupportedSourceOrConfigFile(fileName);
    public static bool ShouldSkipFileName(string fileName) => WorkspaceFileFilter.Default.ShouldSkipFileName(fileName);
    public static bool ShouldSkipFile(FileInfo fileInfo) => WorkspaceFileFilter.Default.ShouldSkipFile(fileInfo);
    public static bool IsMinifiedContent(string filePath) => WorkspaceFileFilter.IsMinifiedContent(filePath);
}
