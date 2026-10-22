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

    private static readonly string[] ProjectMapLiftedKinds = ["CALLS", "USES_TYPE", "IMPLEMENTS", "INHERITS_FROM"];

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
            $"MATCH (a)-[r]->(b) WHERE r.kind IN [{kinds}] AND a.file_path STARTS WITH $prefix AND b.file_path STARTS WITH $prefix AND a.file_path <> b.file_path " +
            $"RETURN a.file_path AS from_file, b.file_path AS to_file, r.kind AS kind, a.name AS from_name, b.name AS to_name, a.start_line AS line LIMIT {ProjectMapEdgeRowLimit}";

        var rows = await QueryProjectMapRowsAsync(query, prefix, "symbol edges", warnings, ct);
        if (rows.Count >= ProjectMapEdgeRowLimit)
        {
            warnings.Add($"Symbol edges truncated at {ProjectMapEdgeRowLimit} rows.");
        }

        return rows.Select(e => new ProjectMapSymbolEdge(
                e.GetStringProp("from_file"),
                e.GetStringProp("to_file"),
                e.GetStringProp("kind"),
                e.GetStringProp("from_name"),
                e.GetStringProp("to_name"),
                e.GetNullableIntProp("line")))
            .ToList();
    }

    private async Task<List<ProjectMapResourceLink>> LoadProjectMapResourceLinksAsync(string prefix, List<string> warnings, CancellationToken ct)
    {
        var labels = string.Join(", ", ProjectMapResourceLabels.Select(EscapeCypherStringLiteral));
        const string projection =
            "RETURN res.id AS res_id, labels(res)[0] AS res_kind, res.name AS res_name, r.kind AS kind, s.file_path AS file, s.name AS sym, s.start_line AS line";

        var queries = new (string Query, string Context)[]
        {
            ($"MATCH (res)-[r]->(s) WHERE labels(res)[0] IN [{labels}] AND s.file_path STARTS WITH $prefix {projection}", "resource links (inbound)"),
            ($"MATCH (s)-[r]->(res) WHERE labels(res)[0] IN [{labels}] AND s.file_path STARTS WITH $prefix {projection}", "resource links (outbound)"),
            ("MATCH (res:Table)<-[r]-(q:Query)-[a]->(s) WHERE a.kind = 'ACCESSED_BY' AND s.file_path STARTS WITH $prefix " + projection, "query tables (accessed by)"),
            ("MATCH (s)-[a]->(q:Query)-[r]->(res:Table) WHERE s.file_path STARTS WITH $prefix " + projection, "query tables (declared)")
        };

        var links = new List<ProjectMapResourceLink>();
        foreach (var (query, context) in queries)
        {
            var rows = await QueryProjectMapRowsAsync(query, prefix, context, warnings, ct);
            links.AddRange(rows.Select(e => new ProjectMapResourceLink(
                e.GetStringProp("file"),
                e.GetStringProp("res_id"),
                e.GetStringProp("res_kind", "Table"),
                e.GetStringProp("res_name", e.GetStringProp("res_id")),
                e.GetStringProp("kind"),
                e.GetStringProp("sym"),
                e.GetNullableIntProp("line"))));
        }

        return links;
    }

    private async Task<Dictionary<string, int>> LoadProjectMapEntryPointsAsync(string prefix, List<string> warnings, CancellationToken ct)
    {
        const string query =
            "MATCH (e)-[r]->(s) WHERE labels(e)[0] IN ['Endpoint', 'EntryPoint'] AND r.kind IN ['TRIGGERS', 'EXPOSED_BY'] AND s.file_path STARTS WITH $prefix " +
            "RETURN e.id AS eid, s.file_path AS file";
        var rows = await QueryProjectMapRowsAsync(query, prefix, "entry points", warnings, ct);

        return rows
            .Select(e => (Id: e.GetStringProp("eid"), File: e.GetStringProp("file")))
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
            var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["prefix"] = prefix }, ct);
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
