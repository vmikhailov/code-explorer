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
        var dirStr = Path.TrimEndingDirectorySeparator(
            string.IsNullOrWhiteSpace(startDirectory)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(startDirectory));

        var current = new DirectoryInfo(dirStr);
        while (current != null)
        {
            var currentFull = Path.TrimEndingDirectorySeparator(current.FullName);
            var ceDir = Path.Combine(current.FullName, FolderName);
            if (Directory.Exists(ceDir))
            {
                if (IsUserHomeDirectory(currentFull) &&
                    !dirStr.Equals(currentFull, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (IsGlobalStateOnlyDirectory(ceDir))
                {
                    if (IsUserHomeDirectory(currentFull))
                    {
                        break;
                    }
                    current = current.Parent;
                    continue;
                }

                EnsureGitIgnore(ceDir);

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

    private static bool IsUserHomeDirectory(string fullPath)
    {
        var normalized = Path.TrimEndingDirectorySeparator(fullPath);
        string?[] candidates =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable("USERPROFILE"),
            Environment.GetEnvironmentVariable("HOME")
        ];

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                var normCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
                if (normalized.Equals(normCandidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore malformed environment paths
            }
        }

        return false;
    }

    private static bool IsGlobalStateOnlyDirectory(string ceDir)
    {
        if (File.Exists(Path.Combine(ceDir, DbFileName)) ||
            Directory.Exists(Path.Combine(ceDir, QueriesFolderName)) ||
            File.Exists(Path.Combine(ceDir, "settings.json")) ||
            Directory.Exists(Path.Combine(ceDir, "gates")))
        {
            return false;
        }

        return File.Exists(Path.Combine(ceDir, "last_workspace.txt")) ||
               Directory.Exists(Path.Combine(ceDir, "bin"));
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

    private const string UnexpandedVarPrefix = "${";
    private static readonly string[] WorkspaceEnvVars = ["WORKSPACE_ROOT", "CE_WORKSPACE", "VSCODE_WORKSPACE", "PROJECT_ROOT"];

    public static WorkspaceInfo? FindExplicitOnly(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Find(explicitPath);
        }

        foreach (var envVar in WorkspaceEnvVars)
        {
            var envVal = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(envVal) && !envVal.Contains(UnexpandedVarPrefix, StringComparison.Ordinal))
            {
                var ws = Find(envVal);
                if (ws != null) return ws;
            }
        }

        return null;
    }

    public static WorkspaceInfo? FindWithFallbacks(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Find(explicitPath);
        }

        var explicitWs = FindExplicitOnly();
        if (explicitWs != null)
        {
            return explicitWs;
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
        EnsureGitIgnore(ceDir);
        WorkspaceSettings.EnsureDefaultSettingsFile(fullDir);

        RecordActiveWorkspace(fullDir);

        return new WorkspaceInfo(fullDir, dbPath, queriesDir);
    }

    /// <summary>
    /// Ensures that local SQLite databases, WAL files, and concurrency locks inside
    /// the workspace directory (.codeexplorer) are ignored by git.
    /// </summary>
    public static void EnsureGitIgnore(string ceDirectory)
    {
        try
        {
            var gitIgnorePath = Path.Combine(ceDirectory, ".gitignore");
            if (!File.Exists(gitIgnorePath))
            {
                const string content = """
                    # Ignore local SQLite graph database, journal/WAL files, and locks
                    *.db
                    *.db-*
                    *.lock
                    *.tmp

                    """;
                File.WriteAllText(gitIgnorePath, content);
            }
            else
            {
                var existing = File.ReadAllText(gitIgnorePath);
                if (!existing.Contains("*.db"))
                {
                    File.AppendAllText(gitIgnorePath, "\n*.db\n*.db-*\n*.lock\n*.tmp\n");
                }
            }
        }
        catch
        {
            // Best effort
        }
    }
}
