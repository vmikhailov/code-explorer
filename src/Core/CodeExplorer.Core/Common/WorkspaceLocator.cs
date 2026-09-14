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

    public static WorkspaceInfo? FindWithFallbacks(string? explicitPath = null)
    {
        // 1. Explicit path
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Find(explicitPath);
        }

        // 2. Environment variables
        string[] envVars = ["WORKSPACE_ROOT", "CE_WORKSPACE", "VSCODE_WORKSPACE", "PROJECT_ROOT"];
        foreach (var envVar in envVars)
        {
            var envVal = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(envVal))
            {
                var ws = Find(envVal);
                if (ws != null) return ws;
            }
        }

        // 3. Current working directory and parents
        var cwdWs = Find(Directory.GetCurrentDirectory());
        if (cwdWs != null) return cwdWs;

        // 4. Global fallback registry in ~/.codeexplorer/last_workspace.txt
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var globalDir = Path.Combine(homeDir, FolderName);
            var lastWsFile = Path.Combine(globalDir, "last_workspace.txt");
            if (File.Exists(lastWsFile))
            {
                var lastPath = File.ReadAllText(lastWsFile).Trim();
                if (!string.IsNullOrEmpty(lastPath))
                {
                    var ws = Find(lastPath);
                    if (ws != null) return ws;
                }
            }
        }
        catch
        {
            // Best effort
        }

        return null;
    }

    public static WorkspaceInfo? FindFromDbPath(string dbPath)
    {
        var fullDb = Path.GetFullPath(dbPath);
        var dir = Path.GetDirectoryName(fullDb);
        return dir != null ? Find(dir) : null;
    }

    public static void RecordActiveWorkspace(string workspaceRoot)
    {
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var globalDir = Path.Combine(homeDir, FolderName);
            Directory.CreateDirectory(globalDir);
            File.WriteAllText(Path.Combine(globalDir, "last_workspace.txt"), workspaceRoot);
        }
        catch
        {
            // Best effort
        }
    }

    public static WorkspaceInfo Initialize(string targetDirectory, string? workspaceName = null)
    {
        var fullDir = Path.GetFullPath(targetDirectory);
        var ceDir = Path.Combine(fullDir, FolderName);
        var dbPath = Path.Combine(ceDir, DbFileName);
        var queriesDir = Path.Combine(ceDir, QueriesFolderName);

        Directory.CreateDirectory(ceDir);
        Directory.CreateDirectory(queriesDir);

        RecordActiveWorkspace(fullDir);

        return new WorkspaceInfo(fullDir, dbPath, queriesDir);
    }
}
