using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public enum ArchitectureViewType
{
    SystemContext, // C1: Macro System Context view (Services, Databases, Topics, ExternalServices)
    ServiceFlow,   // C2: Container & Service Flow view (Interactions, Messaging, Shared Libraries)
    Component      // C3: Component Drill-down for a selected container/project
}

public class ArchitectureViewRequest
{
    public ArchitectureViewType ViewType { get; set; } = ArchitectureViewType.SystemContext;
    public string? Scope { get; set; }
    public bool IncludeLibraries { get; set; } = false;
}

/// <summary>
/// Executes single-query architectural projections directly against the graph database,
/// bypassing runtime BFS lifting and heuristics.
/// </summary>
public class ArchitectureViewEngine(IGraphClient db)
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GraphDataDto> SystemContextCache = new();

    public static void InvalidateCache()
    {
        SystemContextCache.Clear();
    }

    public async Task<GraphDataDto> GetViewAsync(ArchitectureViewRequest request, CancellationToken ct = default)
    {
        return request.ViewType switch
        {
            ArchitectureViewType.SystemContext => await GetSystemContextViewAsync(request.IncludeLibraries, ct),
            ArchitectureViewType.ServiceFlow => await GetServiceFlowViewAsync(request.Scope, request.IncludeLibraries, ct),
            ArchitectureViewType.Component => await GetComponentViewAsync(request.Scope, ct),
            _ => await GetSystemContextViewAsync(request.IncludeLibraries, ct)
        };
    }

    /// <summary>
    /// C1: System Context View.
    /// Retrieves top-level executable services, external APIs, databases, and message topics with materialized macro-edges.
    /// </summary>
    public async Task<GraphDataDto> GetSystemContextViewAsync(bool includeLibraries = false, CancellationToken ct = default)
    {
        var cacheKey = $"c1:libs={includeLibraries}";
        if (SystemContextCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "SystemContext",
                ["level"] = "C1",
                ["graphType"] = "architecture"
            }
        };

        // Query macro nodes: Services, Databases, Topics, ExternalServices
        var nodesQuery = includeLibraries
            ? "MATCH (n) WHERE labels(n)[0] IN ['Project', 'Database', 'Topic', 'ExternalService'] RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.path AS path, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type"
            : "MATCH (n) WHERE labels(n)[0] IN ['Database', 'Topic', 'ExternalService'] OR (labels(n)[0] = 'Project' AND (n.is_library <> 'true' OR n.is_library IS NULL) AND (n.role <> 'SharedLibrary' AND n.role <> 'Test' OR n.role IS NULL)) RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.path AS path, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type";

        var nodesJson = await db.ExecuteQueryAsync(nodesQuery, null, ct);
        using var nodesDoc = JsonDocument.Parse(nodesJson);
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var elem in nodesDoc.RootElement.EnumerateArray())
        {
            var id = elem.GetProperty("id").GetString() ?? "";
            var kind = elem.GetProperty("kind").GetString() ?? "";
            var name = elem.TryGetProperty("name", out var np) && np.ValueKind == JsonValueKind.String ? np.GetString() ?? id : id;
            var path = elem.TryGetProperty("path", out var pp) && pp.ValueKind == JsonValueKind.String ? pp.GetString() : null;

            if (string.IsNullOrEmpty(id) || !nodeIds.Add(id)) continue;

            var props = new Dictionary<string, string>();
            if (elem.TryGetProperty("role", out var rp) && rp.ValueKind == JsonValueKind.String) props["role"] = rp.GetString()!;
            if (elem.TryGetProperty("is_library", out var lp) && lp.ValueKind == JsonValueKind.String) props["is_library"] = lp.GetString()!;
            if (elem.TryGetProperty("db_type", out var dtp) && dtp.ValueKind == JsonValueKind.String) props["db_type"] = dtp.GetString()!;
            if (elem.TryGetProperty("project_type", out var ptp) && ptp.ValueKind == JsonValueKind.String) props["project_type"] = ptp.GetString()!;

            graph.Nodes.Add(new GraphNodeDto
            {
                Id = id,
                Name = name,
                Kind = kind,
                FilePath = path,
                Properties = props
            });
        }

        // Query macro relationships: INTEGRATES_WITH, USES_DB, TRIGGERS, PUBLISHES_TO
        var edgesQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['INTEGRATES_WITH', 'USES_DB', 'TRIGGERS', 'PUBLISHES_TO', 'DEPENDS_ON'] RETURN src.id AS from_id, tgt.id AS to_id, r.kind AS kind, r.properties AS properties";
        var edgesJson = await db.ExecuteQueryAsync(edgesQuery, null, ct);
        using var edgesDoc = JsonDocument.Parse(edgesJson);
        var edgeSet = new HashSet<(string From, string To, string Kind)>();

        foreach (var elem in edgesDoc.RootElement.EnumerateArray())
        {
            var fromId = elem.GetProperty("from_id").GetString() ?? "";
            var toId = elem.GetProperty("to_id").GetString() ?? "";
            var kind = elem.GetProperty("kind").GetString() ?? "";

            if (!nodeIds.Contains(fromId) || !nodeIds.Contains(toId)) continue;
            if (!edgeSet.Add((fromId, toId, kind))) continue;

            var edgeProps = new Dictionary<string, string>();
            if (elem.TryGetProperty("properties", out var propsElem) && propsElem.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in propsElem.EnumerateObject())
                {
                    edgeProps[prop.Name] = prop.Value.ToString();
                }
            }

            var category = kind switch
            {
                OntologyConstants.Relationships.UsesDb => "database",
                OntologyConstants.Relationships.Triggers or OntologyConstants.Relationships.PublishesTo => "messaging",
                OntologyConstants.Relationships.IntegratesWith => "service_call",
                _ => "dependency"
            };

            graph.Edges.Add(new GraphEdgeDto
            {
                Id = $"{fromId}->{toId}:{kind}",
                Source = fromId,
                Target = toId,
                Kind = kind,
                Category = category,
                Properties = edgeProps
            });
        }

        SystemContextCache[cacheKey] = graph;
        return graph;
    }

    /// <summary>
    /// C2: Service Flow View.
    /// Focuses on service-to-service interactions, data persistence, and shared library dependencies for a container or workspace.
    /// </summary>
    public async Task<GraphDataDto> GetServiceFlowViewAsync(string? scope, bool includeLibraries = true, CancellationToken ct = default)
    {
        // When no scope is passed, return full system context with libraries enabled
        if (string.IsNullOrWhiteSpace(scope))
        {
            var fullGraph = await GetSystemContextViewAsync(includeLibraries: true, ct);
            fullGraph.Metadata ??= new Dictionary<string, string>();
            fullGraph.Metadata["view"] = "ServiceFlow";
            fullGraph.Metadata["level"] = "C2";
            return fullGraph;
        }

        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "ServiceFlow",
                ["level"] = "C2",
                ["scope"] = scope,
                ["graphType"] = "flow"
            }
        };

        // Resolve scope node ID
        var scopeQuery = "MATCH (p:Project) WHERE p.id = $scope OR p.name = $scope RETURN p.id AS id, p.name AS name, p.path AS path, p.role AS role, p.is_library AS is_library LIMIT 1";
        var scopeJson = await db.ExecuteQueryAsync(scopeQuery, new Dictionary<string, object?> { ["scope"] = scope }, ct);
        using var scopeDoc = JsonDocument.Parse(scopeJson);
        if (scopeDoc.RootElement.GetArrayLength() == 0)
        {
            return graph;
        }

        var centerElem = scopeDoc.RootElement[0];
        var centerId = centerElem.GetProperty("id").GetString()!;
        var centerName = centerElem.GetProperty("name").GetString()!;

        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { centerId };
        graph.Nodes.Add(new GraphNodeDto
        {
            Id = centerId,
            Name = centerName,
            Kind = OntologyConstants.NodeLabels.Project,
            FilePath = centerElem.TryGetProperty("path", out var pp) ? pp.GetString() : null,
            Properties = new Dictionary<string, string> { ["column"] = "center" }
        });

        // Query 1-hop neighborhood of centerId using materialized edges
        var hoodQuery = "MATCH (src)-[r]->(tgt) WHERE src.id = $centerId OR tgt.id = $centerId RETURN src.id AS from_id, labels(src)[0] AS from_kind, src.name AS from_name, tgt.id AS to_id, labels(tgt)[0] AS to_kind, tgt.name AS to_name, r.kind AS kind, r.properties AS properties";
        var hoodJson = await db.ExecuteQueryAsync(hoodQuery, new Dictionary<string, object?> { ["centerId"] = centerId }, ct);
        using var hoodDoc = JsonDocument.Parse(hoodJson);

        foreach (var elem in hoodDoc.RootElement.EnumerateArray())
        {
            var fromId = elem.GetProperty("from_id").GetString()!;
            var toId = elem.GetProperty("to_id").GetString()!;
            var kind = elem.GetProperty("kind").GetString()!;

            var isOutbound = fromId.Equals(centerId, StringComparison.OrdinalIgnoreCase);
            var otherId = isOutbound ? toId : fromId;
            var otherKind = isOutbound ? elem.GetProperty("to_kind").GetString()! : elem.GetProperty("from_kind").GetString()!;
            var otherName = isOutbound ? elem.GetProperty("to_name").GetString() ?? otherId : elem.GetProperty("from_name").GetString() ?? otherId;

            if (nodeIds.Add(otherId))
            {
                var column = isOutbound ? "right" : "left";
                graph.Nodes.Add(new GraphNodeDto
                {
                    Id = otherId,
                    Name = otherName,
                    Kind = otherKind,
                    Properties = new Dictionary<string, string> { ["column"] = column }
                });
            }

            var edgeProps = new Dictionary<string, string>();
            if (elem.TryGetProperty("properties", out var propsElem) && propsElem.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in propsElem.EnumerateObject())
                {
                    edgeProps[prop.Name] = prop.Value.ToString();
                }
            }

            graph.Edges.Add(new GraphEdgeDto
            {
                Id = $"{fromId}->{toId}:{kind}",
                Source = fromId,
                Target = toId,
                Kind = kind,
                Category = isOutbound ? "outbound" : "inbound",
                Properties = edgeProps
            });
        }

        return graph;
    }

    /// <summary>
    /// C3: Component Drill-down View.
    /// Returns internal controllers, entities, queries, and tables within the specified project container.
    /// </summary>
    public async Task<GraphDataDto> GetComponentViewAsync(string? projectScope, CancellationToken ct = default)
    {
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "Component",
                ["level"] = "C3",
                ["scope"] = projectScope ?? "",
                ["graphType"] = "component"
            }
        };

        if (string.IsNullOrWhiteSpace(projectScope)) return graph;

        // Query all components declared in or persisted in this project
        var query = "MATCH (p:Project)-[:CONTAINS*1..3]->(c) WHERE (p.id = $scope OR p.name = $scope) AND labels(c)[0] IN ['Endpoint', 'EntryPoint', 'Type', 'Table', 'Query'] RETURN c.id AS id, labels(c)[0] AS kind, c.name AS name, c.path AS path";
        var resJson = await db.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["scope"] = projectScope }, ct);
        using var doc = JsonDocument.Parse(resJson);
        var compIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var id = elem.GetProperty("id").GetString()!;
            var kind = elem.GetProperty("kind").GetString()!;
            var name = elem.GetProperty("name").GetString() ?? id;
            var path = elem.TryGetProperty("path", out var pp) ? pp.GetString() : null;

            if (compIds.Add(id))
            {
                graph.Nodes.Add(new GraphNodeDto
                {
                    Id = id,
                    Name = name,
                    Kind = kind,
                    FilePath = path
                });
            }
        }

        // Query internal component relationships: CALLS, EXPOSES_ENDPOINT, PERSISTED_IN, QUERIES_DB
        if (compIds.Count > 0)
        {
            var inListStr = string.Join(", ", compIds.Select(id => $"'{id.Replace("'", "''")}'"));
            var relsQuery = $"MATCH (src)-[r]->(tgt) WHERE src.id IN [{inListStr}] AND tgt.id IN [{inListStr}] RETURN src.id AS from_id, tgt.id AS to_id, r.kind AS kind";
            var relsJson = await db.ExecuteQueryAsync(relsQuery, null, ct);
            using var relsDoc = JsonDocument.Parse(relsJson);

            foreach (var elem in relsDoc.RootElement.EnumerateArray())
            {
                var fromId = elem.GetProperty("from_id").GetString()!;
                var toId = elem.GetProperty("to_id").GetString()!;
                var kind = elem.GetProperty("kind").GetString()!;

                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{fromId}->{toId}:{kind}",
                    Source = fromId,
                    Target = toId,
                    Kind = kind
                });
            }
        }

        return graph;
    }
}
