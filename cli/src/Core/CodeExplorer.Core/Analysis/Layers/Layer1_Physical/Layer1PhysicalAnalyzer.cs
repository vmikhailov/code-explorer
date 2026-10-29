using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer1PhysicalParser
{
    public async Task<Layer1Result> ParseAsync(ParsingContext ctx)
    {
        ctx.Log("[Layer 1    ] Starting physical scan of directory topology...");

        // 0. Get or create Workspace ID from database (auto-incremented)
        var wsId = await ctx.DbClient.GetOrCreateWorkspaceIdAsync(ctx.HostWorkspacePath);
        ctx.WorkspaceId = wsId;
        ctx.CancellationToken.ThrowIfCancellationRequested();

        await ctx.DbClient.SaveEmptyWorkspaceNodeAsync(wsId, ctx.HostWorkspacePath);
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var normalizedHostPath = ctx.HostWorkspacePath.Replace('\\', '/').TrimEnd('/');
        var folderName = Path.GetFileName(normalizedHostPath);
        if (string.IsNullOrEmpty(folderName)) folderName = normalizedHostPath;

        var workspaceName = folderName;
        try
        {
            var wsNameResult = await ctx.DbClient.ExecuteQueryAsync(
                "MATCH (w:Workspace) RETURN w.name AS name LIMIT 1");

            using var doc = System.Text.Json.JsonDocument.Parse(wsNameResult);

            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                doc.RootElement.GetArrayLength() > 0)
            {
                var row = doc.RootElement[0];

                if (row.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var customName = n.GetString();
                    if (!string.IsNullOrWhiteSpace(customName)) workspaceName = customName;
                }
            }
        }
        catch
        {
            // Fallback to folderName
        }

        var hostPath = PathTools.NormalizeToHostPath(ctx.HostWorkspacePath);
        var wsExtensions = new Dictionary<string, string>
        {
            ["indexed_at"] = DateTime.UtcNow.ToString("o")
        };
        var workspaceNode = new WorkspaceNode(wsId, workspaceName, hostPath, wsExtensions);

        var filesNodeId = $"{wsId}:{OntologyConstants.IdPrefixes.FilesStructure}";
        var filesStructureNode = new FilesStructureNode(filesNodeId, "FilesStructure", hostPath);
        workspaceNode.Children.Add(filesStructureNode);

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

        var files = new List<FileNode>();
        var folders = new List<FolderNode>();
        var gitignore = new GitIgnoreMatcher(ctx.AbsoluteWorkspacePath, ctx.FileFilter);

        if (!ctx.IsSubtreeScan)
        {
            await ScanDirectoryAsync(ctx.AbsoluteWorkspacePath, filesStructureNode, files, folders, gitignore, ctx);
        }
        else
        {
            var relativeSubtree = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, ctx.ScanPath).Replace('\\', '/');
            var segments = relativeSubtree.Split('/', StringSplitOptions.RemoveEmptyEntries);

            IOntologyNode currentParent = filesStructureNode;
            var currentPath = ctx.AbsoluteWorkspacePath;

            // Build intermediate folders up to the parent of ctx.ScanPath
            for (int i = 0; i < segments.Length - 1; i++)
            {
                currentPath = Path.Combine(currentPath, segments[i]).Replace('\\', '/');
                var folderId = $"{wsId}:{OntologyConstants.IdPrefixes.Folder}:{currentPath}";
                var intermediateFolder = new FolderNode(folderId, segments[i], currentPath);

                currentParent.Children.Add(intermediateFolder);
                folders.Add(intermediateFolder);
                currentParent = intermediateFolder;
            }

            await ScanDirectoryAsync(ctx.ScanPath, currentParent, files, folders, gitignore, ctx);
        }

        ctx.Log($"[Layer 1    ] Physical topology scan complete. Found {files.Count} files, {folders.Count} folders.");
        return new Layer1Result(workspaceNode, filesStructureNode, files, folders);
    }

    private async Task ScanDirectoryAsync(
        string currentDir,
        IOntologyNode parentNode,
        List<FileNode> files,
        List<FolderNode> folders,
        GitIgnoreMatcher gitignore,
        ParsingContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

        var relativeDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        if (currentDir != ctx.AbsoluteWorkspacePath && ctx.FileFilter.IsExcludedDirectory(dirName, currentDir))
        {
            ctx.LogDebug($"[Layer 1    ] Skipping excluded directory: '{relativeDir}'");
            return;
        }

        if (!string.IsNullOrEmpty(relativeDir))
        {
            gitignore.LoadScopedDirectory(currentDir, relativeDir);
        }

        if (!string.IsNullOrEmpty(relativeDir) && gitignore.IsIgnored(relativeDir, true))
        {
            ctx.LogDebug($"[Layer 1    ] GitIgnore: Skipping ignored directory: '{relativeDir}'");
            return;
        }

        foreach (var parser in ctx.FileFilter.ConnectedProjectParsers)
        {
            parser.OnDirectoryScan(currentDir, relativeDir, gitignore, ctx.LogDebug);
        }

        var currentParentNode = parentNode;

        if (currentDir != ctx.AbsoluteWorkspacePath)
        {
            var absoluteFolderPath = Path.GetFullPath(currentDir).Replace('\\', '/');
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

        var dirInfo = new DirectoryInfo(currentDir);

        // Recurse subdirectories
        foreach (var subDir in dirInfo.GetDirectories())
        {
            await ScanDirectoryAsync(subDir.FullName, currentParentNode, files, folders, gitignore, ctx);
        }

        // Process files
        foreach (var fileInfo in dirInfo.GetFiles())
        {
            var file = fileInfo.FullName;
            var relativeFile = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, file).Replace('\\', '/');

            if (gitignore.IsIgnored(relativeFile, false))
            {
                continue;
            }

            if (!ctx.FileFilter.IsSupportedSourceOrConfigFile(fileInfo.Name))
            {
                continue;
            }

            if (ctx.FileFilter.ShouldSkipFile(fileInfo))
            {
                continue;
            }

            var absoluteFilePath = fileInfo.FullName.Replace('\\', '/');
            var fileId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{relativeFile}";
            var fileName = fileInfo.Name;
            var sizeBytes = fileInfo.Length;
            int? lineCount = null;
            string? contentHash = null;
            DateTime? lastModifiedUtc = null;
            string? artifactRole = null;
            try
            {
                lastModifiedUtc = fileInfo.LastWriteTimeUtc;
                var bytes = await File.ReadAllBytesAsync(fileInfo.FullName, ctx.CancellationToken);
                lineCount = CountLines(bytes);
                contentHash = Incremental.HashUtility.ComputeSha256(bytes);
                artifactRole = WorkspaceFileFilter.ClassifyArtifactRole(relativeFile, fileName, bytes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.RecordScanIssue("Layer1_Physical", "io_error", relativeFile, ex.Message, name: "FileReadError", severity: "warning", exception: ex, parentNodeId: fileId, isFileParent: true);
            }
            var fileNode = new FileNode(fileId, fileName, relativeFile, absoluteFilePath, false, null, null, lineCount, sizeBytes)
            {
                ContentHash = contentHash,
                LastModifiedUtc = lastModifiedUtc,
                ArtifactRole = artifactRole ?? WorkspaceFileFilter.ClassifyArtifactRole(relativeFile, fileName)
            };

            currentParentNode.Children.Add(fileNode);
            files.Add(fileNode);
        }
    }

    private static int CountLines(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return 0;
        var newlines = bytes.Count((byte)'\n');
        return bytes[^1] == (byte)'\n' ? newlines : newlines + 1;
    }

    public static HashSet<string> ExcludedDirectoryNames => new(WorkspaceFileFilter.ExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
    public static bool IsExcludedDirectory(string dirName) => WorkspaceFileFilter.Default.IsExcludedDirectory(dirName);
    public static bool IsSupportedFile(string fileName) => WorkspaceFileFilter.Default.IsSupportedSourceOrConfigFile(fileName);
    public static bool ShouldSkipFileName(string fileName) => WorkspaceFileFilter.Default.ShouldSkipFileName(fileName);
    public static bool ShouldSkipFile(FileInfo fileInfo) => WorkspaceFileFilter.Default.ShouldSkipFile(fileInfo);
    public static bool IsMinifiedContent(string filePath) => WorkspaceFileFilter.IsMinifiedContent(filePath);
}
