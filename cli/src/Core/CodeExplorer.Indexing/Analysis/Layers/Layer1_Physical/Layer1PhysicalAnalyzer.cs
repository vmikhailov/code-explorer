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

        if (!ctx.IsSubtreeScan)
        {
            await ScanDirectoryAsync(ctx.AbsoluteWorkspacePath, filesStructureNode, workspaceNode, files, folders, gitignore, null, ctx);
        }
        else
        {
            var relativeSubtree = ctx.ScanPath.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
            var segments = relativeSubtree.Split('/', StringSplitOptions.RemoveEmptyEntries);

            IOntologyNode currentParent = filesStructureNode;
            var currentPath = ctx.AbsoluteWorkspacePath;

            // Build intermediate folders up to the parent of ctx.ScanPath
            for (int i = 0; i < segments.Length - 1; i++)
            {
                currentPath = Path.Combine(currentPath, segments[i]).NormalizeSlashes();
                var folderId = $"{wsId}:{OntologyConstants.IdPrefixes.Folder}:{currentPath}";
                var intermediateFolder = new FolderNode(folderId, segments[i], currentPath);

                currentParent.Children.Add(intermediateFolder);
                folders.Add(intermediateFolder);
                currentParent = intermediateFolder;
            }

            await ScanDirectoryAsync(ctx.ScanPath, currentParent, workspaceNode, files, folders, gitignore, null, ctx);
        }

        ctx.Layer1Log.ScanCompleted(files.Count, folders.Count);
        return new Layer1Result(workspaceNode, filesStructureNode, files, folders);
    }

    private async Task ScanDirectoryAsync(
        string currentDir,
        IOntologyNode parentNode,
        WorkspaceNode workspaceNode,
        List<FileNode> files,
        List<FolderNode> folders,
        GitIgnoreMatcher gitignore,
        IReadOnlyList<IProjectParser>? inheritedParsers,
        ParsingContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

        var relativeDir = currentDir.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        if (relativeDir == ".") relativeDir = string.Empty;

        if (currentDir != ctx.AbsoluteWorkspacePath && ctx.FileFilter.IsExcludedDirectory(dirName, currentDir, inheritedParsers))
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
        var activeParsers = ctx.FileFilter.ResolveDirectoryProjectParsers(currentDir, dirFiles, inheritedParsers);
        var parsersToNotify = activeParsers is { Count: > 0 } ? activeParsers : ctx.FileFilter.ConnectedProjectParsers;

        for (int i = 0; i < parsersToNotify.Count; i++)
        {
            parsersToNotify[i].OnDirectoryScan(currentDir, relativeDir, gitignore, ctx.Layer1Log.DirectoryScanHookMessage);
            parsersToNotify[i].InspectDirectoryFiles(currentDir, dirFiles, workspaceNode);
        }

        var currentParentNode = parentNode;

        if (currentDir != ctx.AbsoluteWorkspacePath)
        {
            var absoluteFolderPath = currentDir.NormalizeFullPath();
            var folderId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Folder}:{absoluteFolderPath}";
            var folderNode = new FolderNode(folderId, dirName, absoluteFolderPath);

            parentNode.Children.Add(folderNode);
            folders.Add(folderNode);
            currentParentNode = folderNode;

            var gitPath = Path.Combine(currentDir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                var subGitNode = GitSettingsParser.Parse(ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, currentDir);
                if (subGitNode != null)
                {
                    folderNode.Children.Add(subGitNode);
                    ctx.RegisterGitRepository(currentDir, subGitNode);
                }
            }
        }

        await TryStreamVscodeTasksAsync(currentDir, ctx);

        // Recurse subdirectories with scoped active project parsers
        foreach (var subDir in dirInfo.GetDirectories())
        {
            await ScanDirectoryAsync(subDir.FullName, currentParentNode, workspaceNode, files, folders, gitignore, activeParsers, ctx);
        }

        // Process files
        PhysicalFileStore.SetDirectoryFiles(currentDir, dirFiles.Select(f => f.FullName).ToArray());

        foreach (var fileInfo in dirFiles)
        {
            await ProcessDirectoryFileAsync(fileInfo, currentParentNode, files, gitignore, ctx);
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
        var vscodeTasksPath = Path.Combine(currentDir, ".vscode", "tasks.json");
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

    private static async Task ProcessDirectoryFileAsync(
        FileInfo fileInfo,
        IOntologyNode currentParentNode,
        List<FileNode> files,
        GitIgnoreMatcher gitignore,
        ParsingContext ctx)
    {
        var file = fileInfo.FullName;
        var relativeFile = file.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);

        if (gitignore.IsIgnored(relativeFile, false))
        {
            return;
        }

        if (ctx.CustomFileExclusionPredicate?.Invoke(fileInfo.Name, relativeFile) == true)
        {
            return;
        }

        var fileName = fileInfo.Name;
        var isManifest = ctx.FileFilter.IsManifestFile(fileName);
        var isSupportedSourceOrConfig = ctx.FileFilter.IsSupportedSourceOrConfigFile(fileName);

        if (!isSupportedSourceOrConfig && !isManifest)
        {
            return;
        }

        if (isSupportedSourceOrConfig && ctx.FileFilter.ShouldSkipFileMetadata(fileInfo))
        {
            if (!isManifest) return;
            isSupportedSourceOrConfig = false;
        }

        var absoluteFilePath = file.NormalizeSlashes();
        var fileId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{relativeFile}";
        var sizeBytes = fileInfo.Length;
        int? lineCount = null;
        string? contentHash = null;
        DateTime? lastModifiedUtc = null;
        string? artifactRole = null;
        string? decodedText = null;
        var skipByContent = false;
        try
        {
            lastModifiedUtc = fileInfo.LastWriteTimeUtc;
            var streamed = await PhysicalFileStore.StreamAndAnalyzeFileAsync(
                absoluteFilePath,
                relativeFile,
                fileName,
                sizeBytes,
                isManifest,
                isSupportedSourceOrConfig,
                ctx.FileFilter,
                ctx.CancellationToken,
                computeHash: isManifest || !ctx.Clear);
            skipByContent = streamed.SkipByContent;
            contentHash = streamed.ContentHash;
            lineCount = streamed.LineCount;
            artifactRole = streamed.ArtifactRole;
            decodedText = streamed.DecodedText;
            if (isManifest && contentHash != null && lastModifiedUtc.HasValue)
            {
                ctx.DiscoveredManifestFiles[relativeFile] = (absoluteFilePath, contentHash, lastModifiedUtc.Value, sizeBytes);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(relativeFile, ex);
            if (isSupportedSourceOrConfig || isManifest)
            {
                var issueName = isManifest ? IssueManifestReadError : IssueFileReadError;
                ctx.RecordScanIssue(
                    Layer1Stage,
                    IssueCategoryIoError,
                    relativeFile,
                    ex.Message,
                    name: issueName,
                    severity: SeverityWarning,
                    exception: ex,
                    parentNodeId: isSupportedSourceOrConfig ? fileId : null,
                    isFileParent: isSupportedSourceOrConfig);
            }
        }

        if (!isSupportedSourceOrConfig || skipByContent)
        {
            return;
        }

        var fileNode = new FileNode(fileId, fileName, relativeFile, absoluteFilePath, false, null, null, lineCount, sizeBytes)
        {
            ContentHash = contentHash,
            LastModifiedUtc = lastModifiedUtc,
            ArtifactRole = artifactRole ?? ctx.FileFilter.ClassifyFileArtifactRole(relativeFile, fileName),
            PreloadedContent = decodedText
        };

        currentParentNode.Children.Add(fileNode);
        files.Add(fileNode);
    }

    public static HashSet<string> ExcludedDirectoryNames => new(WorkspaceFileFilter.ExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
    public static bool IsExcludedDirectory(string dirName) => WorkspaceFileFilter.Default.IsExcludedDirectory(dirName);
    public static bool IsSupportedFile(string fileName) => WorkspaceFileFilter.Default.IsSupportedSourceOrConfigFile(fileName);
    public static bool ShouldSkipFileName(string fileName) => WorkspaceFileFilter.Default.ShouldSkipFileName(fileName);
    public static bool ShouldSkipFile(FileInfo fileInfo) => WorkspaceFileFilter.Default.ShouldSkipFile(fileInfo);
    public static bool IsMinifiedContent(string filePath) => WorkspaceFileFilter.IsMinifiedContent(filePath);
}
