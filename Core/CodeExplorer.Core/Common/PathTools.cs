using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Common;

public static class PathTools
{
    public static bool InContainer { get; } = 
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
        File.Exists("/.dockerenv");

    /// <summary>
    /// Translates a host file path (e.g. C:\Work\...) to the corresponding path inside the container
    /// (e.g. /host/Work/...) if running inside a container where the "/host" directory is mounted.
    /// </summary>
    public static string TranslateHostPathToContainerPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (InContainer && Directory.Exists("/host"))
        {
            var normalized = path.Replace('\\', '/');
            
            // Check for drive letter e.g., "C:", "C:/", "c:/foo"
            var driveMatch = Regex.Match(normalized, @"^[A-Za-z]:");
            if (driveMatch.Success)
            {
                // Strip the drive letter and any leading slash after it
                var sub = normalized.Substring(driveMatch.Length).TrimStart('/');
                return "/host/" + sub;
            }
            
            // For macOS / Linux hosts running inside container
            if (normalized.StartsWith('/') && !normalized.StartsWith("/host"))
            {
                return "/host/" + normalized.TrimStart('/');
            }
        }

        return path;
    }

    /// <summary>
    /// Gets the relative path of a file relative to the workspace, normalizing path separators
    /// and stripping drive letters if present.
    /// </summary>
    public static string GetRelativePath(string filePath, string hostWorkspacePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return filePath;
        }

        var normalizedFilePath = filePath.Replace('\\', '/');
        var normalizedHostPath = hostWorkspacePath.Replace('\\', '/');

        if (!string.IsNullOrEmpty(normalizedHostPath) && normalizedFilePath.StartsWith(normalizedHostPath, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedFilePath.Substring(normalizedHostPath.Length).TrimStart('/');
        }

        var driveMatch = Regex.Match(normalizedFilePath, @"^[A-Za-z]:");
        if (driveMatch.Success)
        {
            return normalizedFilePath.Substring(driveMatch.Length).TrimStart('/');
        }

        return normalizedFilePath;
    }

    /// <summary>
    /// Normalizes a path to the host format if it looks like a Windows path (e.g. starts with C:).
    /// </summary>
    public static string NormalizeToHostPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (Regex.IsMatch(path, @"^[A-Za-z]:"))
        {
            return path.Replace('/', '\\');
        }

        return path;
    }
}
