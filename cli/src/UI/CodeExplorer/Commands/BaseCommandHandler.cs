using System.Diagnostics.CodeAnalysis;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Commands;

/// <summary>
/// Base class providing unified workspace initialization checks and console reporting for CLI command handlers.
/// </summary>
public abstract class BaseCommandHandler
{
    /// <summary>
    /// Locates the active workspace directory, ensuring it has been initialized with 'ce init'.
    /// Optionally verifies that the graph database exists (has been indexed with 'ce scan').
    /// Prints a formatted error to the console and returns null if validation fails.
    /// </summary>
    public static WorkspaceInfo? EnsureInitialized(string? dir, bool requireIndexed = false)
    {
        var targetDir = Path.GetFullPath(dir ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null)
        {
            PrintError($"Error: No CodeExplorer workspace found at '{targetDir}'. Run 'ce init' and 'ce scan' first.");
            return null;
        }

        if (requireIndexed && !File.Exists(ws.DbPath))
        {
            PrintError($"Error: Workspace found at '{ws.RootDirectory}', but database has not been initialized yet. Run 'ce scan' first.");
            return null;
        }

        return ws;
    }

    /// <summary>
    /// Locates the active workspace directory, returning true if found (and indexed if required).
    /// </summary>
    public static bool EnsureInitialized(string? dir, [NotNullWhen(true)] out WorkspaceInfo? ws, bool requireIndexed = false)
    {
        ws = EnsureInitialized(dir, requireIndexed);
        return ws != null;
    }

    public static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    public static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    /// <summary>
    /// Checks whether the target matches any of the candidate target names (case-insensitive).
    /// </summary>
    public static bool HasTarget(string? target, params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        var trimmed = target.Trim();
        foreach (var candidate in candidates)
        {
            if (string.Equals(trimmed, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks whether the target matches any of the candidate target names (case-insensitive).
    /// </summary>
    public static bool HasTarget(string? target, ReadOnlySpan<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        var trimmed = target.Trim();
        foreach (var candidate in candidates)
        {
            if (string.Equals(trimmed, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }


    /// <summary>
    /// General-purpose case-insensitive check whether a string value matches any candidate.
    /// </summary>
    public static bool IsOneOf(string? value, params string[] candidates) =>
        HasTarget(value, candidates);

    /// <summary>
    /// Checks whether the format string matches any of the candidate formats (case-insensitive).
    /// </summary>
    public static bool IsFormat(string? format, params string[] candidates) =>
        HasTarget(format, candidates);

    /// <summary>
    /// Checks whether the format is JSON, either explicitly specified or via boolean flag.
    /// </summary>
    public static bool IsJsonFormat(string? format, bool jsonFlag = false) =>
        jsonFlag || IsFormat(format, "json");

    /// <summary>
    /// Parses a delimited string (e.g. comma or semicolon separated list of files, symbols, etc.)
    /// into a cleaned list of trimmed non-empty tokens, or null if input is empty/whitespace.
    /// </summary>
    public static List<string>? ParseList(string? rawList)
    {
        if (string.IsNullOrWhiteSpace(rawList)) return null;
        var items = rawList.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        return items.Count > 0 ? items : null;
    }

    /// <summary>
    /// Checks whether any item in the collection matches any of the candidates (case-insensitive).
    /// </summary>
    public static bool ContainsAny(IEnumerable<string>? items, params string[] candidates)
    {
        if (items == null) return false;
        var set = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
        return items.Any(item => set.Contains(item.Trim()));
    }

    /// <summary>
    /// Checks whether the specified file path ends with any of the candidate extensions.
    /// Extension dots are handled automatically (e.g. "cs" or ".cs").
    /// </summary>
    public static bool HasExtension(string? filePath, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var fileExt = Path.GetExtension(filePath);
        foreach (var ext in extensions)
        {
            var normalized = ext.StartsWith('.') ? ext : "." + ext;
            if (string.Equals(fileExt, normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks whether any file in the collection has any of the specified extensions.
    /// </summary>
    public static bool HasAnyWithExtension(IEnumerable<string>? filePaths, params string[] extensions)
    {
        if (filePaths == null) return false;
        return filePaths.Any(f => HasExtension(f, extensions));
    }

    /// <summary>
    /// Checks whether the file name of filePath matches any of the candidate file names (case-insensitive).
    /// </summary>
    public static bool HasFileName(string? filePath, params string[] candidateFileNames)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var fileName = Path.GetFileName(filePath);
        return HasTarget(fileName, candidateFileNames);
    }
}
