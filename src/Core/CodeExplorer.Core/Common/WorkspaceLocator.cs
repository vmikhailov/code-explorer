namespace CodeExplorer.Core.Common;

public record WorkspaceInfo(
    string RootDirectory,
    string DbPath,
    string QueriesDirectory
);

public static class WorkspaceLocator
{
    public const string FolderName = ".codeexplorer";
    public const string DbFileName = "graph.db";
    public const string QueriesFolderName = "queries";

    public static WorkspaceInfo? Find(string? startDirectory = null)
    {
        var dirStr = string.IsNullOrWhiteSpace(startDirectory)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(startDirectory);

        var current = new DirectoryInfo(dirStr);
        while (current != null)
        {
            var ceDir = Path.Combine(current.FullName, FolderName);
            if (Directory.Exists(ceDir))
            {
                return new WorkspaceInfo(
                    RootDirectory: current.FullName,
                    DbPath: Path.Combine(ceDir, DbFileName),
                    QueriesDirectory: Path.Combine(ceDir, QueriesFolderName)
                );
            }
            current = current.Parent;
        }

        return null;
    }

    public static WorkspaceInfo FindOrThrow(string? startDirectory = null)
    {
        var ws = Find(startDirectory);
        if (ws == null)
        {
            var searched = string.IsNullOrWhiteSpace(startDirectory) ? Directory.GetCurrentDirectory() : startDirectory;
            throw new InvalidOperationException(
                $"No '{FolderName}' workspace found in '{searched}' or any parent directory.\n" +
                $"Run 'ce init [name]' to initialize a workspace.");
        }
        return ws;
    }

    public static WorkspaceInfo Initialize(string targetDirectory, string? workspaceName = null)
    {
        var fullDir = Path.GetFullPath(targetDirectory);
        var ceDir = Path.Combine(fullDir, FolderName);
        var dbPath = Path.Combine(ceDir, DbFileName);
        var queriesDir = Path.Combine(ceDir, QueriesFolderName);

        Directory.CreateDirectory(ceDir);
        Directory.CreateDirectory(queriesDir);

        return new WorkspaceInfo(fullDir, dbPath, queriesDir);
    }
}
