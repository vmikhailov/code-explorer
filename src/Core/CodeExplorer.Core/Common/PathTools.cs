using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Common;

public static class PathTools
{

    /// <summary>
    /// Gets the relative path of a file relative to the workspace, normalizing path separators
    /// and stripping drive letters if present.
    /// </summary>
    public static string GetRelativePath(string filePath, string? hostWorkspacePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return filePath;
        }

        var normalizedFilePath = filePath.Replace('\\', '/');
        var normalizedHostPath = hostWorkspacePath?.Replace('\\', '/') ?? string.Empty;

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
