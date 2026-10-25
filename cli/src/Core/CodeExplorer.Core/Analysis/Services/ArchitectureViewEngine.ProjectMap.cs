using System.Text.Json;
using CodeExplorer.Core.Analysis.ProjectMap;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Project Map (C3 internal module view). A pure view-layer projection over existing ontology nodes/edges
/// (Project, File, Function, Type, Table, Topic, ExternalService, CALLS, IMPLEMENTS, ...). Nothing is persisted.
/// </summary>
public partial class ArchitectureViewEngine
{
    private const int ProjectMapEdgeRowLimit = 200_000;

    private static readonly string[] ProjectMapLiftedKinds = ["CALLS", "USES_TYPE", "IMPLEMENTS", "INHERITS_FROM", "TRIGGERS"];

    private static readonly string[] ProjectMapResourceLabels =
        ["Table", "Topic", "ExternalService", "Database", "CloudService", "DataSet", "Procedure"];

    private sealed record ProjectMapProjectInfo(string Id, string Name, string Path, bool IsTest, int FileCount);

    public async Task<GraphDataDto> GetProjectMapViewAsync(string? projectScope, CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var projects = await LoadProjectMapProjectsAsync(ct);
        var selected = SelectProjectMapProject(projects, projectScope);

        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "ProjectMap",
                ["level"] = "C3",
                ["graphType"] = "projectmap",
                ["scope"] = projectScope ?? "",
                ["projects"] = JsonSerializer.Serialize(projects
                    .Where(p => !p.IsTest)
                    .OrderByDescending(p => p.FileCount)
                    .Select(p => new { id = p.Id, name = p.Name, path = p.Path, files = p.FileCount }))
            }
        };

        if (selected == null)
        {
            graph.Metadata["error"] = string.IsNullOrWhiteSpace(projectScope)
                ? "No indexed projects found."
                : $"Project '{projectScope}' not found.";
            return graph;
        }

        var prefix = string.IsNullOrEmpty(selected.Path) ? "" : selected.Path.Replace('\\', '/').TrimEnd('/') + "/";
        var input = new ProjectMapInput
        {
            ProjectId = selected.Id,
            ProjectName = selected.Name,
            ProjectPath = selected.Path,
            Files = await LoadProjectMapFilesAsync(selected.Id, ct),
            SymbolEdges = await LoadProjectMapSymbolEdgesAsync(prefix, warnings, ct),
            ResourceLinks = await LoadProjectMapResourceLinksAsync(prefix, warnings, ct),
            EntryPointsByFile = await LoadProjectMapEntryPointsAsync(prefix, warnings, ct)
        };

        var result = ProjectMapBuilder.Build(input);
        ProjectMapGraphMapper.Populate(graph, result);
        if (warnings.Count > 0) graph.Metadata["warnings"] = JsonSerializer.Serialize(warnings);
        return graph;
    }

    private async Task<List<ProjectMapProjectInfo>> LoadProjectMapProjectsAsync(CancellationToken ct)
    {
        const string query =
            "MATCH (p:Project)-[:CONTAINS]->(f:File) RETURN p.id AS id, p.name AS name, p.path AS path, p.is_test AS is_test, count(f) AS files";
        var json = await db.ExecuteQueryAsync(query, null, ct);
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray()
            .Select(e => new ProjectMapProjectInfo(
                e.GetStringProp("id"),
                e.GetStringProp("name"),
                e.GetStringProp("path"),
                ReadBool(e, "is_test"),
                e.GetNullableIntProp("files") ?? 0))
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .ToList();
    }

    private static ProjectMapProjectInfo? SelectProjectMapProject(List<ProjectMapProjectInfo> projects, string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return projects.Where(p => !p.IsTest).OrderByDescending(p => p.FileCount).FirstOrDefault()
                   ?? projects.OrderByDescending(p => p.FileCount).FirstOrDefault();
        }

        var s = scope.Trim().Replace('\\', '/').TrimEnd('/');
        return projects.FirstOrDefault(p => p.Id.Equals(scope, StringComparison.OrdinalIgnoreCase))
               ?? projects.FirstOrDefault(p => p.Name.Equals(s, StringComparison.OrdinalIgnoreCase))
               ?? projects.FirstOrDefault(p => p.Path.Replace('\\', '/').TrimEnd('/').Equals(s, StringComparison.OrdinalIgnoreCase))
               ?? projects.Where(p => !p.IsTest).FirstOrDefault(p =>
                   p.Path.Replace('\\', '/').TrimEnd('/').Split('/').Last().Equals(s, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<ProjectMapFile>> LoadProjectMapFilesAsync(string projectId, CancellationToken ct)
    {
        const string query =
            "MATCH (p:Project)-[:CONTAINS]->(f:File) WHERE p.id = $pid RETURN f.path AS path, f.is_test AS is_test, f.line_count AS line_count";
        var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["pid"] = projectId }, ct);
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray()
            .Select(e => new ProjectMapFile(e.GetStringProp("path").Replace('\\', '/'), ReadBool(e, "is_test"), e.GetNullableIntProp("line_count")))
            .Where(f => f.Path.Length > 0)
            .ToList();
    }

    private async Task<List<ProjectMapSymbolEdge>> LoadProjectMapSymbolEdgesAsync(string prefix, List<string> warnings, CancellationToken ct)
    {
        var kinds = string.Join(", ", ProjectMapLiftedKinds.Select(EscapeCypherStringLiteral));
        var query =
            $"MATCH (a)-[r]->(b) WHERE r.kind IN [{kinds}] " +
            "AND (a.file_path STARTS WITH $prefix OR a.file_path CONTAINS $slashPrefix) " +
            "AND (b.file_path STARTS WITH $prefix OR b.file_path CONTAINS $slashPrefix) " +
            "AND a.file_path <> b.file_path " +
            $"RETURN a.file_path AS from_file, b.file_path AS to_file, r.kind AS kind, a.name AS from_name, b.name AS to_name, a.start_line AS line LIMIT {ProjectMapEdgeRowLimit}";

        var rows = await QueryProjectMapRowsAsync(query, prefix, "symbol edges", warnings, ct);
        if (rows.Count >= ProjectMapEdgeRowLimit)
        {
            warnings.Add($"Symbol edges truncated at {ProjectMapEdgeRowLimit} rows.");
        }

        var edges = rows.Select(e => new ProjectMapSymbolEdge(
                NormalizeToProjectPath(e.GetStringProp("from_file"), prefix),
                NormalizeToProjectPath(e.GetStringProp("to_file"), prefix),
                e.GetStringProp("kind"),
                e.GetStringProp("from_name"),
                e.GetStringProp("to_name"),
                e.GetNullableIntProp("line")))
            .Where(e => e.FromFile.Length > 0 && e.ToFile.Length > 0 && !e.FromFile.Equals(e.ToFile, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Synthesize USES_TYPE edges from Type->Member data_type and EntryPoint->Type handler contracts within the project
        const string typesQuery =
            "MATCH (t:Type) WHERE t.file_path STARTS WITH $prefix OR t.file_path CONTAINS $slashPrefix " +
            "RETURN t.name AS name, t.file_path AS file";
        var typeRows = await QueryProjectMapRowsAsync(typesQuery, prefix, "project types", warnings, ct);
        var typeToFiles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var tr in typeRows)
        {
            var tName = StripGenericArity(tr.GetStringProp("name"));
            var tFile = NormalizeToProjectPath(tr.GetStringProp("file"), prefix);
            if (tName.Length == 0 || tFile.Length == 0) continue;
            if (!typeToFiles.TryGetValue(tName, out var list))
            {
                list = [];
                typeToFiles[tName] = list;
            }
            if (!list.Contains(tFile, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(tFile);
            }
        }

        if (typeToFiles.Count > 0)
        {
            const string membersQuery =
                "MATCH (t:Type)-[:HAS_MEMBER]->(m:Member) " +
                "WHERE (t.file_path STARTS WITH $prefix OR t.file_path CONTAINS $slashPrefix) AND m.data_type <> '' " +
                "RETURN t.name AS from_name, t.file_path AS from_file, m.data_type AS dtype, m.start_line AS line";
            var memberRows = await QueryProjectMapRowsAsync(membersQuery, prefix, "member type references", warnings, ct);
            var seenMemberTypeEdges = new HashSet<(string FromFile, string ToFile, string FromSym, string ToSym)>();

            foreach (var mr in memberRows)
            {
                var fromFile = NormalizeToProjectPath(mr.GetStringProp("from_file"), prefix);
                var fromName = mr.GetStringProp("from_name");
                var dtype = mr.GetStringProp("dtype");
                var line = mr.GetNullableIntProp("line");
                if (fromFile.Length == 0 || dtype.Length == 0) continue;

                foreach (var token in ExtractTypeIdentifiers(dtype))
                {
                    if (!typeToFiles.TryGetValue(token, out var targetFiles)) continue;
                    foreach (var toFile in targetFiles)
                    {
                        if (fromFile.Equals(toFile, StringComparison.OrdinalIgnoreCase)) continue;
                        if (seenMemberTypeEdges.Add((fromFile, toFile, fromName, token)))
                        {
                            edges.Add(new ProjectMapSymbolEdge(fromFile, toFile, "USES_TYPE", fromName, token, line));
                        }
                    }
                }
            }
        }

        return edges;
    }

    private async Task<List<ProjectMapResourceLink>> LoadProjectMapResourceLinksAsync(string prefix, List<string> warnings, CancellationToken ct)
    {
        var labels = string.Join(", ", ProjectMapResourceLabels.Select(EscapeCypherStringLiteral));
        const string projection =
            "RETURN res.id AS res_id, labels(res)[0] AS res_kind, res.name AS res_name, r.kind AS kind, coalesce(s.file_path, s.path) AS file, s.name AS sym, s.start_line AS line";

        var queries = new (string Query, string Context)[]
        {
            ($"MATCH (res)-[r]->(s) WHERE labels(res)[0] IN [{labels}] AND NOT (res.id STARTS WITH 'ws:top:mediatr:') AND NOT (labels(s)[0] IN ['Project', 'Database', 'Domain']) AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) {projection}", "resource links (inbound)"),
            ($"MATCH (s)-[r]->(res) WHERE labels(res)[0] IN [{labels}] AND NOT (res.id STARTS WITH 'ws:top:mediatr:') AND NOT (labels(s)[0] IN ['Project', 'Database', 'Domain']) AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) {projection}", "resource links (outbound)"),
            ("MATCH (res:Table)<-[r]-(q:Query)-[a]->(s) WHERE a.kind = 'ACCESSED_BY' AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix) " + projection, "query tables (accessed by)"),
            ("MATCH (s)-[a]->(q:Query)-[r]->(res:Table) WHERE (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix) " + projection, "query tables (declared)")
        };

        var links = new List<ProjectMapResourceLink>();
        foreach (var (query, context) in queries)
        {
            var rows = await QueryProjectMapRowsAsync(query, prefix, context, warnings, ct);
            foreach (var e in rows)
            {
                var normFile = NormalizeToProjectPath(e.GetStringProp("file"), prefix);
                if (normFile.Length == 0 || ProjectMapPartitioner.IsIgnoredProjectMapFile(normFile)) continue;
                links.Add(new ProjectMapResourceLink(
                    normFile,
                    e.GetStringProp("res_id"),
                    e.GetStringProp("res_kind", "Table"),
                    e.GetStringProp("res_name", e.GetStringProp("res_id")),
                    e.GetStringProp("kind"),
                    e.GetStringProp("sym"),
                    e.GetNullableIntProp("line")));
            }
        }

        return links;
    }

    private async Task<Dictionary<string, int>> LoadProjectMapEntryPointsAsync(string prefix, List<string> warnings, CancellationToken ct)
    {
        const string edgeQuery =
            "MATCH (e)-[r]->(s) WHERE labels(e)[0] IN ['Endpoint', 'EntryPoint'] AND r.kind IN ['TRIGGERS', 'EXPOSED_BY'] " +
            "AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) " +
            "RETURN e.id AS eid, coalesce(s.file_path, s.path) AS file";
        const string directQuery =
            "MATCH (e) WHERE labels(e)[0] IN ['Endpoint', 'EntryPoint'] " +
            "AND (e.file_path STARTS WITH $prefix OR e.file_path CONTAINS $slashPrefix OR e.path STARTS WITH $prefix OR e.path CONTAINS $slashPrefix) " +
            "RETURN e.id AS eid, coalesce(e.file_path, e.path) AS file";

        var edgeRows = await QueryProjectMapRowsAsync(edgeQuery, prefix, "entry points (edges)", warnings, ct);
        var directRows = await QueryProjectMapRowsAsync(directQuery, prefix, "entry points (direct)", warnings, ct);

        return edgeRows.Concat(directRows)
            .Select(e => (Id: e.GetStringProp("eid"), File: NormalizeToProjectPath(e.GetStringProp("file"), prefix)))
            .Where(x => x.Id.Length > 0 && x.File.Length > 0)
            .Distinct()
            .GroupBy(x => x.File, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().Count(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<JsonElement>> QueryProjectMapRowsAsync(
        string query, string prefix, string context, List<string> warnings, CancellationToken ct)
    {
        try
        {
            var slashPrefix = "/" + prefix.TrimStart('/');
            var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object?>
            {
                ["prefix"] = prefix,
                ["slashPrefix"] = slashPrefix
            }, ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "[ProjectMap] {Context} query failed: {Message}", context, ex.Message);
            warnings.Add($"{context}: {ex.Message}");
            return [];
        }
    }

    private static string NormalizeToProjectPath(string? rawPath, string prefix)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return "";
        var norm = rawPath.Replace('\\', '/').TrimStart('/');
        if (prefix.Length == 0 || norm.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return norm;
        }
        var slashPrefix = "/" + prefix;
        var idx = norm.IndexOf(slashPrefix, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return norm[(idx + 1)..];
        }
        return norm;
    }

    private static string StripGenericArity(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return "";
        var angleIdx = typeName.IndexOf('<');
        if (angleIdx > 0) typeName = typeName[..angleIdx];
        var tickIdx = typeName.IndexOf('`');
        if (tickIdx > 0) typeName = typeName[..tickIdx];
        return typeName.Trim();
    }

    private static IEnumerable<string> ExtractTypeIdentifiers(string rawTypeExpr)
    {
        var start = -1;
        for (var i = 0; i <= rawTypeExpr.Length; i++)
        {
            var ch = i < rawTypeExpr.Length ? rawTypeExpr[i] : '\0';
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                var token = rawTypeExpr[start..i];
                start = -1;
                if (token.Length >= 2 && char.IsUpper(token[0]))
                {
                    yield return token;
                }
            }
        }
    }

    private static bool ReadBool(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v)) return false;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => v.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => v.GetString() is "true" or "True" or "1",
            _ => false
        };
    }
}
