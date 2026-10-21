using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private sealed record EvidenceNodeRef(string Id, string Name, string Label, string? Path);

    private static readonly string[] PublishOutgoingKinds = [OntologyConstants.Relationships.PublishesTo];
    private static readonly string[] PublishIncomingKinds = [OntologyConstants.Relationships.PublishedBy];
    private static readonly string[] SubscribeOutgoingKinds = [OntologyConstants.Relationships.SubscribesTo, "HANDLES"];
    private static readonly string[] SubscribeIncomingKinds = [OntologyConstants.Relationships.Triggers, OntologyConstants.Relationships.SubscribedBy];
    private static readonly string[] ExternalCallKinds =
    [
        OntologyConstants.Relationships.ServiceCall, OntologyConstants.Relationships.CallsEndpoint, OntologyConstants.Relationships.Calls,
        OntologyConstants.Relationships.UsesApi, OntologyConstants.Relationships.UsesCloud
    ];

    private const string NotAContainer = "NOT ({0}:Project OR {0}:Service OR {0}:Workspace OR {0}:SemanticStructure)";

    /// <summary>
    /// Resolves the source-code evidence behind a rendered edge. Group edges pass every member id on
    /// each side; evidence is aggregated and de-duplicated across all member pairs.
    /// </summary>
    public async Task<EdgeEvidenceResponseDto> GetEdgeEvidenceAsync(
        IReadOnlyList<string> sourceIds,
        IReadOnlyList<string> targetIds,
        string? edgeKind,
        CancellationToken ct = default)
    {
        var response = new EdgeEvidenceResponseDto
        {
            SourceId = sourceIds.FirstOrDefault() ?? string.Empty,
            TargetId = targetIds.FirstOrDefault() ?? string.Empty,
            EdgeKind = edgeKind ?? string.Empty
        };

        var sources = await ResolveEvidenceNodesAsync(sourceIds, ct);
        var targets = await ResolveEvidenceNodesAsync(targetIds, ct);
        var items = new List<ArchitectureEvidenceItemDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in sources)
        {
            foreach (var t in targets)
            {
                if (s.Id == t.Id) continue;
                var pair = await CollectPairEvidenceAsync(s, t, ct);
                if (pair.Count == 0)
                {
                    // Bidirectional edges are rendered with an arbitrary orientation.
                    pair = await CollectPairEvidenceAsync(t, s, ct);
                }

                foreach (var item in pair)
                {
                    if (seen.Add($"{item.Kind}:{item.FilePath}:{item.LineStart}:{item.TargetSymbol}:{item.Operation}"))
                    {
                        items.Add(item);
                    }
                }
            }
        }

        response.Items = items
            .OrderBy(i => EvidenceKindOrder(i.Kind))
            .ThenBy(i => i.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.LineStart)
            .ToList();
        response.TotalCount = response.Items.Count;
        return response;
    }

    private static int EvidenceKindOrder(string kind) => kind switch
    {
        "http_call" or "grpc_call" or "wcf_call" or "publish" or "subscribe" => 0,
        "query" or "procedure" => 1,
        "orm" or "table" => 2,
        "endpoint" => 3,
        "config" => 4,
        "migration" => 5,
        _ => 6
    };

    private async Task<List<EvidenceNodeRef>> ResolveEvidenceNodesAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var result = new List<EvidenceNodeRef>();
        foreach (var id in ids.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                const string query = """
                    MATCH (n)
                    WHERE n.id = $id OR n.name = $id
                    RETURN n.id AS id, n.name AS name, labels(n)[0] AS kind, coalesce(n.file_path, n.path) AS path
                    LIMIT 1
                    """;
                var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["id"] = id }, ct);
                using var doc = JsonDocument.Parse(json);
                var row = doc.RootElement.EnumerateArray().FirstOrDefault();
                if (row.ValueKind == JsonValueKind.Object)
                {
                    result.Add(new EvidenceNodeRef(row.GetStringProp("id", id), row.GetStringProp("name", id), row.GetStringProp("kind"), row.GetStringProp("path")));
                }
            }
            catch
            {
                // An unresolvable member simply contributes no evidence.
            }
        }

        return result;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectPairEvidenceAsync(EvidenceNodeRef source, EvidenceNodeRef target, CancellationToken ct)
    {
        if (target.Label == OntologyConstants.NodeLabels.Database)
        {
            return (await GetDatabaseEvidenceAsync(target.Id, source.Id, ct)).Items;
        }

        if (source.Label == OntologyConstants.NodeLabels.Database)
        {
            return (await GetDatabaseEvidenceAsync(source.Id, target.Id, ct)).Items;
        }

        if (target.Label == OntologyConstants.NodeLabels.Topic)
        {
            return await CollectTopicEvidenceAsync(target, source, publish: true, ct);
        }

        if (source.Label == OntologyConstants.NodeLabels.Topic)
        {
            return await CollectTopicEvidenceAsync(source, target, publish: false, ct);
        }

        if (target.Label == OntologyConstants.NodeLabels.ExternalService)
        {
            return await CollectExternalCallEvidenceAsync(source, target, ct);
        }

        return await CollectEndpointCallEvidenceAsync(source, target, ct);
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectTopicEvidenceAsync(
        EvidenceNodeRef topic, EvidenceNodeRef service, bool publish, CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var scope = await ResolveServiceScopeAsync(service.Id, ct);
        if (scope is null) return items;

        var outgoing = publish ? PublishOutgoingKinds : SubscribeOutgoingKinds;
        var incoming = publish ? PublishIncomingKinds : SubscribeIncomingKinds;

        foreach (var (pattern, kinds) in new[] { ("(sym)-[r]->(t)", outgoing), ("(t)-[r]->(sym)", incoming) })
        {
            var query = $"""
                MATCH {pattern}
                WHERE t.id = $topicId AND r.kind IN [{string.Join(", ", kinds.Select(k => $"'{k}'"))}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind, coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line, coalesce(sym.end_line, sym.line_end) AS line_end,
                       json_extract(sym.properties, '$.service') AS service
                """;

            try
            {
                var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["topicId"] = topic.Id }, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var path = row.GetStringProp("path");
                    if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"))) continue;

                    var line = row.GetNullableIntProp("line") ?? 1;
                    var symName = row.GetStringProp("name");
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = row.GetStringProp("id"),
                        Kind = publish ? "publish" : "subscribe",
                        Operation = publish ? "PUBLISH" : "SUBSCRIBE",
                        Title = $"{(publish ? "Publishes" : "Subscribes")} {topic.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = row.GetNullableIntProp("line_end") ?? line,
                        CallerSymbol = symName,
                        TargetSymbol = topic.Name,
                        ServiceName = scope.Name,
                        Reason = $"{row.GetStringProp("kind")} '{symName}' {(publish ? "publishes to" : "consumes from")} topic '{topic.Name}'"
                    });
                }
            }
            catch
            {
                // A failing direction must not hide evidence found in the other one.
            }
        }

        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectExternalCallEvidenceAsync(
        EvidenceNodeRef service, EvidenceNodeRef external, CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var scope = await ResolveServiceScopeAsync(service.Id, ct);
        if (scope is null) return items;

        var query = $"""
            MATCH (sym)-[r]->(ext)
            WHERE ext.id = $extId AND r.kind IN [{string.Join(", ", ExternalCallKinds.Select(k => $"'{k}'"))}]
              AND {string.Format(NotAContainer, "sym")}
            RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind, coalesce(sym.file_path, sym.path) AS path,
                   coalesce(sym.start_line, sym.line) AS line, coalesce(sym.end_line, sym.line_end) AS line_end
            """;

        try
        {
            var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["extId"] = external.Id }, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path)) continue;

                var line = row.GetNullableIntProp("line") ?? 1;
                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = row.GetStringProp("id"),
                    Kind = "http_call",
                    Operation = "CALL",
                    Title = $"Calls {external.Name}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = row.GetNullableIntProp("line_end") ?? line,
                    CallerSymbol = row.GetStringProp("name"),
                    TargetSymbol = external.Name,
                    ServiceName = scope.Name,
                    Reason = $"Invokes external service '{external.Name}'"
                });
            }
        }
        catch
        {
            // No evidence available for this pair.
        }

        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectEndpointCallEvidenceAsync(
        EvidenceNodeRef caller, EvidenceNodeRef callee, CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var callerScope = await ResolveServiceScopeAsync(caller.Id, ct);
        var calleeScope = await ResolveServiceScopeAsync(callee.Id, ct);
        if (callerScope is null || calleeScope is null) return items;

        var query = $"""
            MATCH (sym)-[r]->(ep:Endpoint)
            WHERE r.kind = 'CALLS_ENDPOINT' AND {string.Format(NotAContainer, "sym")}
            RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind, coalesce(sym.file_path, sym.path) AS path,
                   coalesce(sym.start_line, sym.line) AS line, coalesce(sym.end_line, sym.line_end) AS line_end,
                   ep.name AS ep_name, coalesce(ep.file_path, ep.path) AS ep_path,
                   ep.http_method AS http_method, ep.route_template AS route, ep.protocol AS protocol
            """;

        try
        {
            var json = await db.ExecuteQueryAsync(query, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !callerScope.Matches(path)) continue;
                if (!calleeScope.Matches(row.GetStringProp("ep_path"))) continue;

                var protocol = row.GetStringProp("protocol");
                var method = row.GetStringProp("http_method");
                var route = row.GetStringProp("route");
                var epName = row.GetStringProp("ep_name");
                var line = row.GetNullableIntProp("line") ?? 1;

                var kind = protocol.Equals("grpc", StringComparison.OrdinalIgnoreCase) ? "grpc_call"
                         : protocol.Equals("wcf", StringComparison.OrdinalIgnoreCase) ? "wcf_call"
                         : "http_call";
                var operation = string.IsNullOrEmpty(method) || method.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    ? (kind == "http_call" ? "CALL" : kind[..4].ToUpperInvariant())
                    : method.ToUpperInvariant();

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = row.GetStringProp("id"),
                    Kind = kind,
                    Operation = operation,
                    Title = string.IsNullOrEmpty(route) ? epName : $"{operation} {route}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = row.GetNullableIntProp("line_end") ?? line,
                    CallerSymbol = row.GetStringProp("name"),
                    TargetSymbol = epName,
                    ServiceName = callerScope.Name,
                    Reason = $"Calls endpoint '{epName}' exposed by '{calleeScope.Name}'"
                });
            }
        }
        catch
        {
            // No evidence available for this pair.
        }

        return items;
    }

    private sealed record DatabaseObjectRef(string Id, string Name, string Label);

    /// <summary>Tables and procedures that belong to the database, in either relationship direction.</summary>
    private async Task<List<DatabaseObjectRef>> FindDatabaseObjectsAsync(string dbId, string dbName, string rawId, CancellationToken ct)
    {
        var found = new Dictionary<string, DatabaseObjectRef>(StringComparer.OrdinalIgnoreCase);
        var parameters = new Dictionary<string, object> { ["id"] = dbId, ["name"] = dbName, ["rawId"] = rawId };

        foreach (var pattern in new[] { "(obj)-[r]->(tgt)", "(tgt)-[r]->(obj)" })
        {
            var query = $"""
                MATCH {pattern}
                WHERE (obj:Table OR obj:Procedure) AND (tgt.id = $id OR tgt.name = $name OR tgt.id = $rawId)
                RETURN obj.id AS id, obj.name AS name, labels(obj)[0] AS kind
                """;
            var json = await db.ExecuteQueryAsync(query, parameters, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                if (!string.IsNullOrEmpty(id))
                {
                    found.TryAdd(id, new DatabaseObjectRef(id, row.GetStringProp("name", id), row.GetStringProp("kind")));
                }
            }
        }

        return [.. found.Values];
    }
}
