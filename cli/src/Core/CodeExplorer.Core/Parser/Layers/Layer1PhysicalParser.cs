using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer1PhysicalParser
{
    public async Task<Layer1Result> ParseAsync(ParsingContext ctx)
    {
        ctx.Log("[Layer1] Starting physical scan of directory topology...");

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
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
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
        var workspaceNode = new WorkspaceNode(wsId, workspaceName, hostPath);

        var filesNodeId = $"{wsId}:files_structure";
        var filesStructureNode = new FilesStructureNode(filesNodeId, "FilesStructure", hostPath);
        workspaceNode.Children.Add(filesStructureNode);

        var gitSettingsNode = GitSettingsParser.Parse(wsId, ctx.AbsoluteWorkspacePath);
        if (gitSettingsNode != null)
        {
            filesStructureNode.Children.Add(gitSettingsNode);
        }

        var files = new List<FileNode>();
        var folders = new List<FolderNode>();
        var gitignore = new GitIgnoreMatcher(ctx.AbsoluteWorkspacePath);

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
                var folderId = $"{wsId}:folder:{currentPath}";
                var intermediateFolder = new FolderNode(folderId, segments[i], currentPath);

                currentParent.Children.Add(intermediateFolder);
                folders.Add(intermediateFolder);
                currentParent = intermediateFolder;
            }

            await ScanDirectoryAsync(ctx.ScanPath, currentParent, files, folders, gitignore, ctx);
        }

        ctx.Log($"[Layer1] Physical topology scan complete. Found {files.Count} files, {folders.Count} folders.");
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
        var relativeDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        if (!string.IsNullOrEmpty(relativeDir))
        {
            var nestedGitIgnore = Path.Combine(currentDir, ".gitignore");
            if (File.Exists(nestedGitIgnore))
            {
                gitignore.LoadScopedFile(nestedGitIgnore, relativeDir);
            }
            var nestedCeIgnore = Path.Combine(currentDir, ".codeexplorerignore");
            if (File.Exists(nestedCeIgnore))
            {
                gitignore.LoadScopedFile(nestedCeIgnore, relativeDir);
            }
        }

        if (!string.IsNullOrEmpty(relativeDir) && gitignore.IsIgnored(relativeDir, true))
        {
            ctx.Log($"[Layer1] GitIgnore: Ignoring directory '{relativeDir}'");
            return;
        }

        var libmanPath = Path.Combine(currentDir, "libman.json");
        if (File.Exists(libmanPath))
        {
            RegisterLibManExclusions(libmanPath, currentDir, ctx.AbsoluteWorkspacePath, gitignore, ctx);
        }

        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;
        var dirNameLower = dirName.ToLowerInvariant();

        var genericExclusions = new HashSet<string>
        {
            ".git", ".github", ".vscode", ".idea", ".vs", ".go", "node_modules",
            "bin", "obj", "packages", "dist", "build", ".build", ".next", ".nuxt",
            ".turbo", ".cache", ".output", "out", "coverage", "scratch", "demo",
            "vendor", "bower_components", "third_party", "thirdparty", "3rdparty"
        };

        if (genericExclusions.Contains(dirNameLower))
        {
            return;
        }

        var currentParentNode = parentNode;

        if (currentDir != ctx.AbsoluteWorkspacePath)
        {
            var absoluteFolderPath = Path.GetFullPath(currentDir).Replace('\\', '/');
            var folderId = $"{ctx.WorkspaceId}:folder:{absoluteFolderPath}";
            var folderNode = new FolderNode(folderId, dirName, absoluteFolderPath);

            parentNode.Children.Add(folderNode);
            folders.Add(folderNode);
            currentParentNode = folderNode;
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
            var ext = fileInfo.Extension.ToLowerInvariant();
            var file = fileInfo.FullName;
            var relativeFile = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, file).Replace('\\', '/');

            if (gitignore.IsIgnored(relativeFile, false))
            {
                continue;
            }

            var hasParser = WorkspaceIndexer._fileParsers.Any(p => p.CanParse(ext));
            var isConfigFile = ConfigurationParser.IsConfigurationFile(fileInfo.Name);
            if (!hasParser && !isConfigFile)
            {
                continue;
            }

            if (ShouldSkipFile(fileInfo))
            {
                // we skip test files for now
                continue;
            }

            var absoluteFilePath = fileInfo.FullName.Replace('\\', '/');
            var fileId = $"{ctx.WorkspaceId}:file:{relativeFile}";
            var fileName = fileInfo.Name;
            var fileNode = new FileNode(fileId, fileName, relativeFile, absoluteFilePath);

            currentParentNode.Children.Add(fileNode);
            files.Add(fileNode);
        }
    }

    private const long MaxSourceFileSize = 1_048_576; // 1 MB limit for AST parsing

    private static bool ShouldSkipFile(FileInfo fileInfo)
    {
        var fileName = fileInfo.Name.ToLowerInvariant();

        // 1. Tests and mocks
        if (fileName.Contains("mock")) return true;
        if (fileName.EndsWith("tests.cs") || fileName.EndsWith("test.cs")) return true;
        if (fileName.EndsWith("_test.go")) return true;
        if (fileName.StartsWith("test_") && fileName.EndsWith(".py")) return true;
        if (fileName.EndsWith("_test.py")) return true;
        if (fileName.EndsWith(".test.ts") || fileName.EndsWith(".spec.ts") || fileName.EndsWith(".test.js") ||
            fileName.EndsWith(".spec.js")) return true;

        // 2. Scratch, temporary, playground, and debug scratch files
        if (fileName.StartsWith("scratch") || fileName.Contains(".scratch.") || fileName.Contains("_scratch.") ||
            fileName.StartsWith("temp_") || fileName.StartsWith("tmp_") ||
            fileName.EndsWith("_debug.ts") || fileName.EndsWith("_debug.js") ||
            fileName.StartsWith("debug_") || fileName.Contains("playground") || fileName.Contains("scratchpad"))
        {
            return true;
        }

        // 3. TypeScript Ambient Declaration files (no executable code/endpoints/calls)
        if (fileName.EndsWith(".d.ts")) return true;

        // 4. Minified, bundle, and vendor file conventions
        if (fileName.EndsWith(".min.js") || fileName.EndsWith(".min.mjs") || fileName.EndsWith(".min.cjs") ||
            fileName.EndsWith(".min.css") || fileName.EndsWith(".bundle.js") || fileName.EndsWith(".bundle.min.js")) return true;
        if (fileName.Contains(".min.")) return true;

        // 4. Oversized source files (> 1 MB are bundled distributions or generated data tables)
        // FileInfo.Length is populated from DirectoryInfo enumeration, avoiding per-file system calls
        try
        {
            if (fileInfo.Length > MaxSourceFileSize)
            {
                return true;
            }
        }
        catch
        {
            // Ignore file access errors
        }

        // 5. Minification heuristic: check first 4KB for extremely long lines (> 2000 chars)
        if (fileName.EndsWith(".js") || fileName.EndsWith(".ts") || fileName.EndsWith(".css"))
        {
            if (IsMinifiedContent(fileInfo.FullName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMinifiedContent(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var buffer = new char[8192];
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read <= 0) return false;

            var lineLen = 0;
            var newlines = 0;
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == '\n')
                {
                    newlines++;
                    lineLen = 0;
                }
                else
                {
                    lineLen++;
                    if (lineLen > 1000)
                    {
                        return true;
                    }
                }
            }
            if (lineLen > 1000) return true;

            // Average line length heuristic: if read >= 4096 and fewer than 4 newlines (avg line > 1000 chars)
            if (read >= 4096 && newlines <= 3) return true;
        }
        catch
        {
            // Fall through if file unreadable
        }
        return false;
    }

    private static void RegisterLibManExclusions(
        string libmanPath,
        string currentDir,
        string workspaceRoot,
        GitIgnoreMatcher gitignore,
        ParsingContext ctx)
    {
        try
        {
            var content = File.ReadAllText(libmanPath);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("libraries", out var libs) && libs.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var lib in libs.EnumerateArray())
                {
                    if (lib.TryGetProperty("destination", out var destProp) && destProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var dest = destProp.GetString();
                        if (!string.IsNullOrWhiteSpace(dest))
                        {
                            var fullDest = Path.GetFullPath(Path.Combine(currentDir, dest));
                            var relDest = Path.GetRelativePath(workspaceRoot, fullDest).Replace('\\', '/').Trim('/');
                            gitignore.AddPattern(relDest + "/");
                            ctx.Log($"[Layer1] LibMan: Excluded library destination '{relDest}'");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ctx.Log($"[Layer1] Error reading libman.json: {ex.Message}");
        }
    }
}
