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
    internal async Task<EvidenceScope?> ResolveServiceScopeAsync(string serviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serviceId)) return null;

        try
        {
            const string query = """
                MATCH (s)
                WHERE (s:Project OR s:Service OR s:Worker OR s:App)
                  AND (s.id = $sId OR s.name = $sId OR s.id ENDS WITH (':' + $sId))
                RETURN s.id AS id, s.name AS name, coalesce(s.file_path, s.path) AS path
                LIMIT 1
                """;
            var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["sId"] = serviceId }, ct);
            using var doc = JsonDocument.Parse(json);
            var row = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (row.ValueKind == JsonValueKind.Object)
            {
                return new EvidenceScope(
                    row.GetStringProp("id", serviceId),
                    row.GetStringProp("name", serviceId),
                    row.GetStringProp("path"));
            }
        }
        catch
        {
            // Fall through to an unresolved scope that only matches by service name.
        }

        return new EvidenceScope(serviceId, serviceId, null);
    }
}
