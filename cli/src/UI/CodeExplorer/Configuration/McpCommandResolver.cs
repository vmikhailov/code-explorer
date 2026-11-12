namespace CodeExplorer.Configuration;

public static class McpCommandResolver
{
    public const string DefaultCommand = "ce";

    public static string ResolveCommand(ConfigScope scope, string? explicitCommandPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitCommandPath))
        {
            return explicitCommandPath.Trim();
        }

        if (scope == ConfigScope.Workspace)
        {
            return DefaultCommand;
        }

        var procPath = Environment.ProcessPath;
        if (!ToolDetector.IsExecutableInPath(DefaultCommand) &&
            !string.IsNullOrEmpty(procPath) &&
            string.Equals(Path.GetFileNameWithoutExtension(procPath), DefaultCommand, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(procPath))
        {
            return procPath;
        }

        return DefaultCommand;
    }
}
