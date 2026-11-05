using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer1PhysicalParser
{
    public async Task<Layer1Result> ParseAsync(ParsingContext ctx)
    {
        ctx.Log("[Layer 1    ] Starting physical scan of directory topology...");

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

        var normalizedHostPath = ctx.HostWorkspacePath.Replace('\\', '/').TrimEnd('/');
        var folderName = Path.GetFileName(normalizedHostPath);
        if (string.IsNullOrEmpty(folderName)) folderName = normalizedHostPath;

        var workspaceName = folderName;
        if (ctx.PersistToGraphDb && ctx.DbClient != null)
        {
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

        if (!ctx.DiscoveredBinaryArtifacts.IsEmpty)
        {
            var hasNode = false;
            var hasNativeAddon = false;
            var hasDotNetDll = false;
            foreach (var (_, _, ext) in ctx.DiscoveredBinaryArtifacts)
            {
                if (ext.Equals(".node", StringComparison.OrdinalIgnoreCase))
                {
                    hasNode = true;
                    hasNativeAddon = true;
                }
                else if (ext.Equals(".wasm", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".dylib", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".so", StringComparison.OrdinalIgnoreCase))
                {
                    hasNativeAddon = true;
                }
                else if (ext.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    hasDotNetDll = true;
                }
            }
            const string ExtHasNativeNode = "has_native_node";
            const string ExtHasNativeAddon = "has_native_addon";
            const string ExtHasDotNetDll = "has_dotnet_dll";
            const string TrueVal = "true";
            if (hasNode) workspaceNode.SetExtension(ExtHasNativeNode, TrueVal);
            if (hasNativeAddon) workspaceNode.SetExtension(ExtHasNativeAddon, TrueVal);
            if (hasDotNetDll) workspaceNode.SetExtension(ExtHasDotNetDll, TrueVal);
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

        var vscodeTasksPath = Path.Combine(currentDir, ".vscode", "tasks.json");
        if (File.Exists(vscodeTasksPath))
        {
            try
            {
                var relTasks = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, vscodeTasksPath).Replace('\\', '/');
                var absTasks = Path.GetFullPath(vscodeTasksPath).Replace('\\', '/');
                var tasksInfo = new FileInfo(vscodeTasksPath);
                const string TasksJsonFileName = "tasks.json";
                var streamedTasks = await PhysicalFileStore.StreamAndAnalyzeFileAsync(absTasks, relTasks, TasksJsonFileName, tasksInfo.Length, isManifest: true, isSupportedSourceOrConfig: false, ctx.FileFilter, ctx.CancellationToken);
                if (streamedTasks.ContentHash != null) ctx.DiscoveredManifestFiles[relTasks] = (absTasks, streamedTasks.ContentHash, tasksInfo.LastWriteTimeUtc, tasksInfo.Length);
                // tasksHash handled by StreamAndAnalyzeFileAsync
                // tasksText handled by StreamAndAnalyzeFileAsync
                // SetFileText handled by StreamAndAnalyzeFileAsync
                // DiscoveredManifestFiles handled by StreamAndAnalyzeFileAsync
            }
            catch
            {
                // Best effort
            }
        }

        var dirInfo = new DirectoryInfo(currentDir);

        // Recurse subdirectories
        foreach (var subDir in dirInfo.GetDirectories())
        {
            await ScanDirectoryAsync(subDir.FullName, currentParentNode, files, folders, gitignore, ctx);
        }

        // Process files
        var dirFiles = dirInfo.GetFiles();
        PhysicalFileStore.SetDirectoryFiles(currentDir, dirFiles.Select(f => f.FullName).ToArray());

        foreach (var fileInfo in dirFiles)
        {
            var file = fileInfo.FullName;
            var relativeFile = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, file).Replace('\\', '/');

            if (gitignore.IsIgnored(relativeFile, false))
            {
                continue;
            }

            var ext = fileInfo.Extension;
            if (ext.Equals(".node", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".wasm", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".so", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".dylib", StringComparison.OrdinalIgnoreCase))
            {
                ctx.DiscoveredBinaryArtifacts.Add((relativeFile, file.Replace('\\', '/'), ext));
            }

            if (ctx.CustomFileExclusionPredicate?.Invoke(fileInfo.Name, relativeFile) == true)
            {
                continue;
            }

            var fileName = fileInfo.Name;
            var isManifest = ctx.FileFilter.IsManifestFile(fileName) || IsAuxiliaryMetadataFile(fileName);
            var isSupportedSourceOrConfig = ctx.FileFilter.IsSupportedSourceOrConfigFile(fileName);

            if (!isSupportedSourceOrConfig && !isManifest)
            {
                continue;
            }

            if (isSupportedSourceOrConfig && ctx.FileFilter.ShouldSkipFileMetadata(fileInfo))
            {
                if (!isManifest) continue;
                isSupportedSourceOrConfig = false;
            }

            var absoluteFilePath = fileInfo.FullName.Replace('\\', '/');
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
                    ctx.CancellationToken);
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
                if (isSupportedSourceOrConfig)
                {
                    ctx.RecordScanIssue("Layer1_Physical", "io_error", relativeFile, ex.Message, name: "FileReadError", severity: "warning", exception: ex, parentNodeId: fileId, isFileParent: true);
                }
            }

            if (!isSupportedSourceOrConfig || skipByContent)
            {
                continue;
            }

            var fileNode = new FileNode(fileId, fileName, relativeFile, absoluteFilePath, false, null, null, lineCount, sizeBytes)
            {
                ContentHash = contentHash,
                LastModifiedUtc = lastModifiedUtc,
                ArtifactRole = artifactRole ?? WorkspaceFileFilter.ClassifyArtifactRole(relativeFile, fileName),
                PreloadedContent = decodedText
            };

            currentParentNode.Children.Add(fileNode);
            files.Add(fileNode);
        }
    }

    private static bool IsAuxiliaryMetadataFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        if (fileName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("tsconfig.base.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("jsconfig.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("project.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("ng-package.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("angular.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("nest-cli.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("wrangler.toml", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("wrangler.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("wrangler.jsonc", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("settings.gradle", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("settings.gradle.kts", StringComparison.OrdinalIgnoreCase);
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
