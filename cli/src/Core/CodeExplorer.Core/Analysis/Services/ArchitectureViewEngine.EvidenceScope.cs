using System.Text.Json;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Defines which files and symbols belong to a service so evidence can be attributed to it.
/// Membership is decided on directory boundaries, never by substring.
/// </summary>
internal sealed class EvidenceScope(string id, string name, string? directory)
{
    private static readonly HashSet<string> ManifestExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj", ".fsproj", ".vbproj", ".sln", ".slnx", ".props", ".json", ".toml", ".xml",
        ".yaml", ".yml", ".gradle", ".kts", ".mod", ".txt"
    };

    public string Id { get; } = id;
    public string Name { get; } = name;
    public string? Directory { get; } = NormalizeDirectory(directory);

    /// <summary>Project nodes may point at a manifest (e.g. Foo.csproj); the scope is its folder.</summary>
    public static string? NormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var p = path.Replace('\\', '/').Trim();
        while (p.StartsWith("./", StringComparison.Ordinal)) p = p[2..];
        p = p.TrimEnd('/');

        var lastSlash = p.LastIndexOf('/');
        var leaf = lastSlash >= 0 ? p[(lastSlash + 1)..] : p;
        if (ManifestExtensions.Contains(Path.GetExtension(leaf)))
        {
            p = lastSlash >= 0 ? p[..lastSlash] : string.Empty;
        }

        return p is "" or "." ? null : p;
    }

    public bool Matches(string? filePath, string? itemService = null)
    {
        if (!string.IsNullOrEmpty(itemService) &&
            (itemService.Equals(Name, StringComparison.OrdinalIgnoreCase) || itemService.Equals(Id, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.IsNullOrEmpty(filePath) || Directory is null) return false;

        var f = filePath.Replace('\\', '/');
        if (f.Equals(Directory, StringComparison.OrdinalIgnoreCase) ||
            f.StartsWith(Directory + "/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Absolute paths: the project directory must appear as whole path segments.
        return f.Contains("/" + Directory + "/", StringComparison.OrdinalIgnoreCase);
    }
}

public partial class ArchitectureViewEngine
{
    private static readonly string[] KnownEntityIdPrefixes =
    [
        "service:", "domain:", "dom:", "ws:s:", "ws:app:", "ws:w:", "ws:cli:", "ws:lib:",
        "ws:p:", "ws:project:", "project:", "app:", "worker:", "cli:", "lib:"
    ];

    internal static string StripEntityIdPrefix(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return string.Empty;
        var clean = rawId.Trim();
        foreach (var p in KnownEntityIdPrefixes)
        {
            if (clean.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean[p.Length..];
                break;
            }
        }
        return clean.TrimEnd(':');
    }

    internal async Task<EvidenceScope?> ResolveServiceScopeAsync(string serviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serviceId)) return null;

        var cleanId = StripEntityIdPrefix(serviceId);
        if (string.IsNullOrWhiteSpace(cleanId)) cleanId = serviceId;

        try
        {
            const string query = """
                MATCH (s)
                WHERE (s:Project OR s:Service OR s:Worker OR s:App OR s:CliTool OR s:Library OR s:TestSuite)
                  AND (s.id = $sId OR s.name = $sId OR s.id = $cleanId OR s.name = $cleanId
                       OR s.id ENDS WITH (':' + $cleanId) OR s.id ENDS WITH (':' + $cleanId + ':'))
                RETURN s.id AS id, s.name AS name, coalesce(s.file_path, s.path) AS path, labels(s)[0] AS kind
                """;
            var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object>
            {
                ["sId"] = serviceId,
                ["cleanId"] = cleanId
            }, ct);
            using var doc = JsonDocument.Parse(json);
            string? bestId = null;
            string? bestName = null;
            string? bestPath = null;

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var rId = row.GetStringProp("id", serviceId);
                var rName = row.GetStringProp("name", cleanId);
                var rPath = row.GetStringProp("path");
                var rKind = row.GetStringProp("kind");

                if (bestId is null || !string.IsNullOrEmpty(rPath) && (string.IsNullOrEmpty(bestPath) || rKind == "Project"))
                {
                    bestId = rId;
                    bestName = rName;
                    if (!string.IsNullOrEmpty(rPath)) bestPath = rPath;
                }
            }

            if (bestId is not null)
            {
                return new EvidenceScope(bestId, bestName ?? cleanId, bestPath ?? cleanId);
            }
        }
        catch
        {
            // Fall through to an unresolved scope that matches by cleanId directory/name.
        }

        return new EvidenceScope(serviceId, cleanId, cleanId);
    }
}
