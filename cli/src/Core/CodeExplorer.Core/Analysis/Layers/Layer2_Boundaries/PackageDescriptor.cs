namespace CodeExplorer.Core.Parser;

/// <summary>
/// Declarative descriptor of a package dependency, framework, or library for ontology classification.
/// Decouples package identification and role assignment from AST traversal.
/// </summary>
public record PackageDescriptor(
    string Id,
    string Name,
    LibraryRole Role = LibraryRole.General,
    SemanticExtensionType Type = SemanticExtensionType.Library,
    IReadOnlyList<string>? SupportedPatterns = null,
    Func<ProjectContext, bool>? CustomMatch = null
)
{
    public PackageDescriptor(
        string id,
        string name,
        LibraryRole role,
        string type,
        IReadOnlyList<string>? supportedPatterns = null,
        Func<ProjectContext, bool>? customMatch = null)
        : this(id, name, role, ParseExtensionType(type), supportedPatterns, customMatch)
    {
    }

    private static SemanticExtensionType ParseExtensionType(string? type) => type?.ToLowerInvariant() switch
    {
        "framework" => SemanticExtensionType.Framework,
        "db:relational" or "relational" or "orm" => SemanticExtensionType.RelationalDb,
        "db:document" or "document" => SemanticExtensionType.DocumentDb,
        "db:keyvalue" or "keyvalue" => SemanticExtensionType.KeyValueDb,
        "db:analytics" or "analytics" => SemanticExtensionType.AnalyticsDb,
        "db:search" or "search" => SemanticExtensionType.SearchDb,
        "db:vector" or "vector" => SemanticExtensionType.VectorDb,
        "db:graph" or "graph" => SemanticExtensionType.GraphDb,
        "db:timeseries" or "timeseries" => SemanticExtensionType.TimeSeriesDb,
        "queue" or "messaging" => SemanticExtensionType.Queue,
        "cloud" => SemanticExtensionType.Cloud,
        "http-client" => SemanticExtensionType.HttpClient,
        "api" or "api:client" => SemanticExtensionType.ApiClient,
        "framework:grpc" => SemanticExtensionType.GrpcFramework,
        "framework:graphql" => SemanticExtensionType.GraphqlFramework,
        _ => SemanticExtensionType.Library
    };

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

        return MatchesAnyDependency(context.Dependencies);
    }

    public bool MatchesDependency(string dependencyName)
    {
        if (string.IsNullOrEmpty(dependencyName) || SupportedPatterns.Count == 0)
            return false;

        for (int j = 0; j < SupportedPatterns.Count; j++)
        {
            if (IsLibraryMatch(dependencyName, SupportedPatterns[j]))
            {
                return true;
            }
        }

        return false;
    }

    public bool MatchesAnyDependency(IEnumerable<string>? dependencies)
    {
        if (dependencies == null || SupportedPatterns.Count == 0)
            return false;

        foreach (var dep in dependencies)
        {
            if (MatchesDependency(dep))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Performs a part-based matching of library names, splitting by '.', '/', and ':' and comparing segments,
    /// supporting wildcard '*' segments and prefixes.
    /// </summary>
    public static bool IsLibraryMatch(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;

        var aParts = a.Split(['.', '/', ':'], StringSplitOptions.RemoveEmptyEntries);
        var bParts = b.Split(['.', '/', ':'], StringSplitOptions.RemoveEmptyEntries);

        if (bParts.Length == 1 && bParts[0].StartsWith('*') && bParts[0].EndsWith('*') && bParts[0].Length > 2)
        {
            var sub = bParts[0][1..^1];
            return a.Contains(sub, StringComparison.OrdinalIgnoreCase);
        }

        if (aParts.Length < bParts.Length) return false;

        for (var i = 0; i < bParts.Length; i++)
        {
            if (bParts[i] == "*") continue;
            if (bParts[i].StartsWith('*') && bParts[i].EndsWith('*') && bParts[i].Length > 2)
            {
                var sub = bParts[i][1..^1];
                if (!aParts[i].Contains(sub, StringComparison.OrdinalIgnoreCase))
                    return false;
                continue;
            }
            if (bParts[i].EndsWith('*') && bParts[i].Length > 1)
            {
                var prefix = bParts[i][..^1];
                if (!aParts[i].StartWithIC(prefix))
                    return false;
                continue;
            }

            if (!aParts[i].Equals(bParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
