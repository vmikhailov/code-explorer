using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CodeExplorer.Core.Common;

public static class StringExtensions
{
    /// <summary>
    /// Case-insensitive StartsWith using StringComparison.OrdinalIgnoreCase.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartWithIC([NotNullWhen(true)] this string? str, string prefix) =>
        str != null && str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Case-insensitive StartsWith using StringComparison.OrdinalIgnoreCase.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartsWithIC([NotNullWhen(true)] this string? str, string prefix) =>
        str != null && str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes directory separators to forward slashes ('/') and trims trailing slashes.
    /// Returns the original string instance without allocation when already normalized.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NormalizePath(this string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        if (!path.Contains('\\') && !path.EndsWith('/')) return path;
        return path.Replace('\\', '/').TrimEnd('/');
    }

    /// <summary>
    /// Normalizes directory separators to forward slashes ('/') without trimming trailing slashes.
    /// Returns the original string instance without allocation when no backslashes are present.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NormalizeSlashes(this string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return path.Contains('\\') ? path.Replace('\\', '/') : path;
    }

    /// <summary>
    /// Resolves <see cref="Path.GetFullPath(string)"/> and normalizes separators to forward slashes ('/').
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NormalizeFullPath(this string path, bool trimTrailingSlash = false)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var full = Path.GetFullPath(path);
        return trimTrailingSlash ? full.NormalizePath() : full.NormalizeSlashes();
    }

    /// <summary>
    /// Computes <see cref="Path.GetRelativePath(string, string)"/> with forward slashes ('/'),
    /// returning <see cref="string.Empty"/> when the relative path is <c>"."</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetNormalizedRelativePath(this string path, string relativeTo)
    {
        var rel = Path.GetRelativePath(relativeTo, path).NormalizeSlashes();
        return rel == "." ? string.Empty : rel;
    }
}
