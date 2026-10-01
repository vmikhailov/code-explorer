using CodeExplorer.Configuration.Adapters;

namespace CodeExplorer.Configuration;

public static class ToolDetector
{
    private static readonly IMcpTargetAdapter[] AllAdapters =
    [
        new AntigravityAdapter(),
        new CursorAdapter(),
        new ClaudeDesktopAdapter(),
        new ClaudeCodeAdapter(),
        new WindsurfAdapter(),
        new VsCodeAdapter(),
        new ClineAdapter(),
        new ZedAdapter()
    ];

    public static IReadOnlyList<IMcpTargetAdapter> GetAllAdapters() => AllAdapters;

    public static IMcpTargetAdapter? FindAdapter(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return null;

        var normalized = targetName.Trim().ToLowerInvariant();
        return AllAdapters.FirstOrDefault(a =>
            a.TargetName.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            a.Aliases.Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    public static IReadOnlyList<IMcpTargetAdapter> DetectInstalledTools(string workspaceRoot)
    {
        return AllAdapters.Where(a => a.IsInstalled(workspaceRoot)).ToList();
    }

    public static bool IsExecutableInPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return false;

        string[] extensions = OperatingSystem.IsWindows()
            ? [".exe", ".cmd", ".bat", ""]
            : [""];

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(path, exeName + ext);
                if (File.Exists(fullPath)) return true;
            }
        }
        return false;
    }
}
