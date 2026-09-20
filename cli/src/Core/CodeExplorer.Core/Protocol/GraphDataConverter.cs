using System.Text.Json;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Protocol;

public static class GraphDataConverter
{
    public static async Task<GraphDataDto> GetArchitectureGraphAsync(
        IGraphClient client,
        string? projectFilter = null,
        CancellationToken cancellationToken = default)
    {
        var graph = new GraphDataDto();
        var nodeMap = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);

        // 1. Projects
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.framework AS framework, p.path AS path";
        var projJson = await client.ExecuteQueryAsync(projQuery, null, cancellationToken);
        using var projDoc = JsonDocument.Parse(projJson);

        foreach (var row in projDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

            if (!string.IsNullOrWhiteSpace(projectFilter) && !name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var node = new GraphNodeDto
            {
                Id = id,
                Kind = "Project",
                Name = name,
                DisplayName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})",
                FilePath = path,
                Properties = new Dictionary<string, string>()
            };
            if (!string.IsNullOrEmpty(framework)) node.Properties["framework"] = framework;
            if (!string.IsNullOrEmpty(path)) node.Properties["path"] = path;

            nodeMap[id] = node;
            graph.Nodes.Add(node);
        }

        // 2. Databases
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type";
        var dbJson = await client.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        using var dbDoc = JsonDocument.Parse(dbJson);

        foreach (var row in dbDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
            var dbType = row.TryGetProperty("db_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Database") : "Database";

            var node = new GraphNodeDto
            {
                Id = id,
                Kind = "Database",
                Name = name,
                DisplayName = $"{name} [{dbType}]",
                Properties = new Dictionary<string, string> { ["db_type"] = dbType }
            };

            nodeMap[id] = node;
            graph.Nodes.Add(node);
        }

        // 3. External Services / Message Brokers
        var svcQuery = "MATCH (s:ExternalService) RETURN s.id AS id, s.name AS name, s.service_type AS service_type";
        var svcJson = await client.ExecuteQueryAsync(svcQuery, null, cancellationToken);
        using var svcDoc = JsonDocument.Parse(svcJson);

        foreach (var row in svcDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
            var st = row.TryGetProperty("service_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Service") : "Service";

            if (!nodeMap.ContainsKey(id))
            {
                var node = new GraphNodeDto
                {
                    Id = id,
                    Kind = "ExternalService",
                    Name = name,
                    DisplayName = $"{name} [{st}]",
                    Properties = new Dictionary<string, string> { ["service_type"] = st }
                };
                nodeMap[id] = node;
                graph.Nodes.Add(node);
            }
        }

        // 4. Project -> Project Dependencies
        var depQuery = "MATCH (p1:Project)-[r:DEPENDS_ON]->(p2:Project) RETURN p1.id AS source, p2.id AS target, r.kind AS kind";
        var depJson = await client.ExecuteQueryAsync(depQuery, null, cancellationToken);
        using var depDoc = JsonDocument.Parse(depJson);

        foreach (var row in depDoc.RootElement.EnumerateArray())
        {
            var src = row.GetProperty("source").GetString() ?? "";
            var tgt = row.GetProperty("target").GetString() ?? "";
            var kind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "DEPENDS_ON") : "DEPENDS_ON";

            if (nodeMap.ContainsKey(src) && nodeMap.ContainsKey(tgt))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{src}->{tgt}:{kind}",
                    Source = src,
                    Target = tgt,
                    Kind = kind
                });
            }
        }

        // 5. Project -> Database
        var usesDbQuery = "MATCH (p:Project)-[r:USES_DB]->(d:Database) RETURN p.id AS source, d.id AS target, r.kind AS kind";
        var usesDbJson = await client.ExecuteQueryAsync(usesDbQuery, null, cancellationToken);
        using var usesDbDoc = JsonDocument.Parse(usesDbJson);

        foreach (var row in usesDbDoc.RootElement.EnumerateArray())
        {
            var src = row.GetProperty("source").GetString() ?? "";
            var tgt = row.GetProperty("target").GetString() ?? "";
            var kind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "USES_DB") : "USES_DB";

            if (nodeMap.ContainsKey(src) && nodeMap.ContainsKey(tgt))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{src}->{tgt}:{kind}",
                    Source = src,
                    Target = tgt,
                    Kind = kind
                });
            }
        }

        return graph;
    }
}
