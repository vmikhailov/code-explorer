namespace CodeExplorer.Options;

/// <summary>
/// Common interface for CLI options that accept a target parameter (e.g. view, configure, mcp, domain).
/// </summary>
public interface ITargetOption
{
    string? Target { get; }
}

/// <summary>
/// Extension methods for CLI option types implementing ITargetOption.
/// </summary>
public static class TargetOptionExtensions
{
    /// <summary>
    /// Checks whether the option's target matches any of the candidate names (case-insensitive).
    /// </summary>
    public static bool HasTarget(this ITargetOption? options, params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(options?.Target)) return false;
        var trimmed = options.Target.Trim();
        foreach (var c in candidates)
        {
            if (string.Equals(trimmed, c, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
