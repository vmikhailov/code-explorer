using System.Text.Json;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Defines which files and symbols belong to a service so evidence can be attributed to it.
/// Prefers structural CONTAINS ownership in the graph and falls back to directory boundaries
/// while excluding any nested child project directories.
/// </summary>
internal sealed class EvidenceScope(
    string id,
    string name,
    string? directory,
    IReadOnlySet<string>? containedNodeIds = null,
    IReadOnlyList<string>? excludedSubdirectories = null)
{
    private static readonly HashSet<string> ManifestExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj", ".fsproj", ".vbproj", ".sln", ".slnx", ".props", ".json", ".toml", ".xml",
        ".yaml", ".yml", ".gradle", ".kts", ".mod", ".txt"
    };

    public string Id { get; } = id;
    public string Name { get; } = name;
    public string? Directory { get; } = NormalizeDirectory(directory);
    public IReadOnlySet<string> ContainedNodeIds { get; } = containedNodeIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> ExcludedSubdirectories { get; } =
        excludedSubdirectories?
            .Select(NormalizeDirectory)
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => d!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];

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

    public bool IsStructuralMember(string? nodeId) =>
        !string.IsNullOrEmpty(nodeId) && ContainedNodeIds.Contains(nodeId);

    public bool Matches(string? filePath, string? itemService = null, string? nodeId = null)
    {
        if (!string.IsNullOrEmpty(nodeId) && ContainedNodeIds.Count > 0)
        {
            if (ContainedNodeIds.Contains(nodeId)) return true;
        }

        if (!string.IsNullOrEmpty(itemService) &&
            (itemService.Equals(Name, StringComparison.OrdinalIgnoreCase) || itemService.Equals(Id, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.IsNullOrEmpty(filePath) || Directory is null) return false;

        var f = filePath.Replace('\\', '/');
        foreach (var childDir in ExcludedSubdirectories)
        {
            if (f.Equals(childDir, StringComparison.OrdinalIgnoreCase) ||
                f.StartsWith(childDir + "/", StringComparison.OrdinalIgnoreCase) ||
                f.Contains("/" + childDir + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

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

    internal async Task<EvidenceScope?> ResolveServiceScopeAsync(
        string serviceId,
        CancellationToken ct,
        Dictionary<string, EvidenceScope?>? scopeCache = null,
        List<string>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(serviceId)) return null;
        if (scopeCache != null && scopeCache.TryGetValue(serviceId, out var cachedScope))
        {
            return cachedScope;
        }

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
            var candidateServiceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var rId = row.GetStringProp("id", serviceId);
                var rName = row.GetStringProp("name", cleanId);
                var rPath = row.GetStringProp("path");
                var rKind = row.GetStringProp("kind");
                if (!string.IsNullOrEmpty(rId)) candidateServiceIds.Add(rId);

                if (bestId is null || !string.IsNullOrEmpty(rPath) && (string.IsNullOrEmpty(bestPath) || rKind == "Project"))
                {
                    bestId = rId;
                    bestName = rName;
                    if (!string.IsNullOrEmpty(rPath)) bestPath = rPath;
                }
            }

            if (bestId is not null)
            {
                var normBestDir = EvidenceScope.NormalizeDirectory(bestPath ?? cleanId);
                var containedIds = new HashSet<string>(candidateServiceIds, StringComparer.OrdinalIgnoreCase);
                var excludedSubdirs = new List<string>();

                // 1. Structural containment: (s)-[:CONTAINS|DECLARES|DECLARES_TYPE|HAS_METHOD|HAS_MEMBER*1..3]->(sym)
                // and (sym)-[:DECLARED_IN]->(f) where (s)-[:CONTAINS]->(f)
                try
                {
                    var svcIdLiterals = string.Join(", ", candidateServiceIds.Select(EscapeCypherStringLiteral));
                    var containsQuery = $"""
                        MATCH (s)-[:CONTAINS|DECLARES|DECLARES_TYPE|HAS_METHOD|HAS_MEMBER*1..3]->(sym)
                        WHERE s.id IN [{svcIdLiterals}]
                        RETURN sym.id AS sym_id
                        """;
                    var cJson = await db.ExecuteQueryAsync(containsQuery, null, ct);
                    using var cDoc = JsonDocument.Parse(cJson);
                    foreach (var row in cDoc.RootElement.EnumerateArray())
                    {
                        var symId = row.GetStringProp("sym_id");
                        if (!string.IsNullOrEmpty(symId)) containedIds.Add(symId);
                    }
                }
                catch (Exception ex)
                {
                    RecordEvidenceWarning(warnings, $"Structural containment query for '{bestId}'", ex);
                }

                // 2. Discover nested child projects so parent project never claims a child project's files/evidence
                if (!string.IsNullOrEmpty(normBestDir))
                {
                    try
                    {
                        const string allProjectsQuery = """
                            MATCH (p:Project)
                            RETURN p.id AS id, coalesce(p.file_path, p.path) AS path
                            """;
                        var pJson = await db.ExecuteQueryAsync(allProjectsQuery, null, ct);
                        using var pDoc = JsonDocument.Parse(pJson);
                        foreach (var row in pDoc.RootElement.EnumerateArray())
                        {
                            var pId = row.GetStringProp("id");
                            if (string.IsNullOrEmpty(pId) || candidateServiceIds.Contains(pId)) continue;
                            var otherDir = EvidenceScope.NormalizeDirectory(row.GetStringProp("path"));
                            if (!string.IsNullOrEmpty(otherDir) &&
                                !otherDir.Equals(normBestDir, StringComparison.OrdinalIgnoreCase) &&
                                otherDir.StartsWith(normBestDir + "/", StringComparison.OrdinalIgnoreCase))
                            {
                                excludedSubdirs.Add(otherDir);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        RecordEvidenceWarning(warnings, $"Nested project discovery for '{bestId}'", ex);
                    }
                }

                var resolvedScope = new EvidenceScope(bestId, bestName ?? cleanId, bestPath ?? cleanId, containedIds, excludedSubdirs);
                if (scopeCache != null)
                {
                    scopeCache[serviceId] = resolvedScope;
                    scopeCache[bestId] = resolvedScope;
                }
                return resolvedScope;
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Failed to resolve service scope '{serviceId}'", ex);
        }

        var fallbackScope = new EvidenceScope(serviceId, cleanId, cleanId);
        if (scopeCache != null)
        {
            scopeCache[serviceId] = fallbackScope;
        }
        return fallbackScope;
    }

    private async Task PreloadServiceScopesAsync(
        IEnumerable<EvidenceNodeRef> serviceNodes,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string>? warnings,
        CancellationToken ct)
    {
        var pending = serviceNodes
            .Where(n => !string.IsNullOrEmpty(n.Id) && !scopeCache.ContainsKey(n.Id) &&
                        n.Label is not (OntologyConstants.NodeLabels.Database or OntologyConstants.NodeLabels.Topic or OntologyConstants.NodeLabels.ExternalService))
            .ToList();
        if (pending.Count == 0) return;

        var containedByService = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in pending)
        {
            containedByService[s.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { s.Id };
        }

        try
        {
            var svcLiterals = string.Join(", ", pending.Select(s => EscapeCypherStringLiteral(s.Id)).Distinct());
            var containsQuery = $"""
                MATCH (s)-[:CONTAINS|DECLARES|DECLARES_TYPE|HAS_METHOD|HAS_MEMBER*1..3]->(sym)
                WHERE s.id IN [{svcLiterals}]
                RETURN s.id AS svc_id, sym.id AS sym_id
                """;
            var cJson = await db.ExecuteQueryAsync(containsQuery, null, ct);
            using var cDoc = JsonDocument.Parse(cJson);
            foreach (var row in cDoc.RootElement.EnumerateArray())
            {
                var svcId = row.GetStringProp("svc_id");
                var symId = row.GetStringProp("sym_id");
                if (!string.IsNullOrEmpty(svcId) && !string.IsNullOrEmpty(symId) &&
                    containedByService.TryGetValue(svcId, out var set))
                {
                    set.Add(symId);
                }
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, "Batch structural containment query", ex);
        }

        var allProjectDirs = new List<(string Id, string Dir)>();
        try
        {
            const string allProjectsQuery = """
                MATCH (p:Project)
                RETURN p.id AS id, coalesce(p.file_path, p.path) AS path
                """;
            var pJson = await db.ExecuteQueryAsync(allProjectsQuery, null, ct);
            using var pDoc = JsonDocument.Parse(pJson);
            foreach (var row in pDoc.RootElement.EnumerateArray())
            {
                var pId = row.GetStringProp("id");
                var dir = EvidenceScope.NormalizeDirectory(row.GetStringProp("path"));
                if (!string.IsNullOrEmpty(pId) && !string.IsNullOrEmpty(dir))
                {
                    allProjectDirs.Add((pId, dir));
                }
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, "Batch nested project discovery", ex);
        }

        foreach (var s in pending)
        {
            var normDir = EvidenceScope.NormalizeDirectory(s.Path ?? s.Name);
            var excluded = new List<string>();
            if (!string.IsNullOrEmpty(normDir))
            {
                foreach (var (otherId, otherDir) in allProjectDirs)
                {
                    if (otherId.Equals(s.Id, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!otherDir.Equals(normDir, StringComparison.OrdinalIgnoreCase) &&
                        otherDir.StartsWith(normDir + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        excluded.Add(otherDir);
                    }
                }
            }

            var scope = new EvidenceScope(
                s.Id,
                s.Name,
                s.Path ?? s.Name,
                containedByService.GetValueOrDefault(s.Id),
                excluded);
            scopeCache[s.Id] = scope;
        }
    }
}
