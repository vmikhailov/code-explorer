using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer1PhysicalParser
{
    public async Task<Layer1Result> ParseAsync(ParsingContext ctx)
    {
        ctx.Log("[Layer1PhysicalParser] Starting physical scan of directory topology...");

        // 0. Get or create Workspace ID from database (auto-incremented)
        var wsId = await ctx.DbClient.GetOrCreateWorkspaceIdAsync(ctx.HostWorkspacePath);
        ctx.WorkspaceId = wsId;
        ctx.CancellationToken.ThrowIfCancellationRequested();

        await ctx.DbClient.SaveEmptyWorkspaceNodeAsync(wsId, ctx.HostWorkspacePath);
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var normalizedHostPath = ctx.HostWorkspacePath.Replace('\\', '/').TrimEnd('/');
        var folderName = Path.GetFileName(normalizedHostPath);
        if (string.IsNullOrEmpty(folderName)) folderName = normalizedHostPath;

        var hostPath = PathTools.NormalizeToHostPath(ctx.HostWorkspacePath);
        var workspaceNode = new WorkspaceNode(wsId, folderName, hostPath);

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

        await ScanDirectoryAsync(ctx.AbsoluteWorkspacePath, filesStructureNode, files, folders, gitignore, ctx);

        ctx.Log($"[Layer1PhysicalParser] Physical topology scan complete. Found {files.Count} files, {folders.Count} folders.");
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

        if (!string.IsNullOrEmpty(relativeDir) && gitignore.IsIgnored(relativeDir, true))
        {
            ctx.Log($"[Layer1PhysicalParser] GitIgnore: Ignoring directory '{relativeDir}'");
            return;
        }

        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;
        var dirNameLower = dirName.ToLowerInvariant();

        var genericExclusions = new HashSet<string>
        {
            ".git", ".github", ".vscode", ".idea", ".vs", ".go", "node_modules",
            "bin", "obj", "packages", "dist", "build", "scratch", "demo"
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

        // Recurse subdirectories
        foreach (var subDir in Directory.GetDirectories(currentDir))
        {
            await ScanDirectoryAsync(subDir, currentParentNode, files, folders, gitignore, ctx);
        }

        // Process files
        foreach (var file in Directory.GetFiles(currentDir))
        {
            var ext = Path.GetExtension(file).ToLower();
            var relativeFile = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, file).Replace('\\', '/');

            if (gitignore.IsIgnored(relativeFile, false))
            {
                continue;
            }

            var hasParser = WorkspaceIndexer._fileParsers.Any(p => p.CanParse(ext));
            if (!hasParser)
            {
                continue;
            }

            if (IsTestOrMockFile(file))
            {
                // we skip test files for now
                continue;
            }

            var absoluteFilePath = Path.GetFullPath(file).Replace('\\', '/');
            var fileId = $"{ctx.WorkspaceId}:file:{relativeFile}";
            var fileName = Path.GetFileName(file);
            var fileNode = new FileNode(fileId, fileName, relativeFile, absoluteFilePath);

            currentParentNode.Children.Add(fileNode);
            files.Add(fileNode);
        }
    }

    private static bool IsTestOrMockFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        if (fileName.Contains("mock")) return true;
        if (fileName.EndsWith("tests.cs") || fileName.EndsWith("test.cs")) return true;
        if (fileName.EndsWith("_test.go")) return true;
        if (fileName.StartsWith("test_") && fileName.EndsWith(".py")) return true;
        if (fileName.EndsWith("_test.py")) return true;
        if (fileName.EndsWith(".test.ts") || fileName.EndsWith(".spec.ts") || fileName.EndsWith(".test.js") ||
            fileName.EndsWith(".spec.js")) return true;

        return false;
    }
}
