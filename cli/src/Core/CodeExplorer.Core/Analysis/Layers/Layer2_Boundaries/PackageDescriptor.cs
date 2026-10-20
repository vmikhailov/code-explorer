namespace CodeExplorer.Core.Parser;

/// <summary>
/// Declarative descriptor of a package dependency, framework, or library for ontology classification.
/// Decouples package identification and role assignment from AST traversal.
/// </summary>
public record PackageDescriptor(
    string Id,
    string Name,
    LibraryRole Role = LibraryRole.General,
    string Type = "library",
    IReadOnlyList<string>? SupportedPatterns = null,
    Func<ProjectContext, bool>? CustomMatch = null
)
{
    public IReadOnlyList<string> SupportedPatterns { get; init; } = SupportedPatterns ?? [];

    /// <summary>
    /// Checks whether this package descriptor matches the given project context.
    /// Evaluates custom match predicates (e.g. SDK checks) or scans project dependencies against supported patterns.
    /// </summary>
    public bool Matches(ProjectContext context)
    {
        if (CustomMatch != null && CustomMatch(context))
        {
            return true;
        }

        if (context.Dependencies == null || context.Dependencies.Count == 0 || SupportedPatterns.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < context.Dependencies.Count; i++)
        {
            var dep = context.Dependencies[i];
            for (int j = 0; j < SupportedPatterns.Count; j++)
            {
                if (IsLibraryMatch(dep, SupportedPatterns[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Performs a part-based matching of library names, splitting by '.' and '/' and comparing segments,
    /// supporting wildcard '*' segments and prefixes.
    /// </summary>
    public static bool IsLibraryMatch(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;

        var aParts = a.Split(['.', '/'], StringSplitOptions.RemoveEmptyEntries);
        var bParts = b.Split(['.', '/'], StringSplitOptions.RemoveEmptyEntries);

        if (aParts.Length < bParts.Length) return false;

        for (var i = 0; i < bParts.Length; i++)
        {
            if (bParts[i] == "*") continue;
            if (bParts[i].EndsWith('*') && bParts[i].Length > 1)
            {
                var prefix = bParts[i][..^1];
                if (!aParts[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return false;
                continue;
            }

            if (!aParts[i].Equals(bParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
