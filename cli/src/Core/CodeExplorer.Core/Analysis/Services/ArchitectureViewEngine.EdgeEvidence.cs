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
        "endpoint" or "entrypoint" => 3,
        "library_call" or "test_call" => 4,
        "config" => 5,
        "migration" => 6,
        _ => 7
    };

    private static int NodeKindResolutionRank(string kind, bool rawHadServicePrefix)
    {
        if (rawHadServicePrefix)
        {
            return kind switch
            {
                "Service" or "App" or "Worker" or "CliTool" or "Library" or "TestSuite" => 100,
                "Project" => 90,
                "Database" or "Topic" or "ExternalService" => 20,
                _ => 0
            };
        }

        return kind switch
        {
            "Database" or "Topic" or "ExternalService" => 100,
            "Service" or "App" or "Worker" or "CliTool" or "Library" or "TestSuite" => 95,
            "Project" => 90,
            "Endpoint" or "EntryPoint" or "Table" or "Procedure" => 50,
            _ => 0
        };
    }

    private async Task<List<EvidenceNodeRef>> ResolveEvidenceNodesAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var result = new List<EvidenceNodeRef>();
        foreach (var rawId in ids.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var id = rawId.Trim();
            var cleanId = StripEntityIdPrefix(id);
            if (string.IsNullOrWhiteSpace(cleanId)) cleanId = id;
            var rawHadServicePrefix = !id.Equals(cleanId, StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:db:", StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:top:", StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:es:", StringComparison.OrdinalIgnoreCase) &&
                                      !id.StartsWith("ws:res:", StringComparison.OrdinalIgnoreCase);

            try
            {
                const string query = """
                    MATCH (n)
                    WHERE n.id = $id OR n.name = $id OR n.id = $cleanId OR n.name = $cleanId
                       OR n.id ENDS WITH (':' + $cleanId) OR n.id ENDS WITH (':' + $cleanId + ':')
                    RETURN n.id AS id, n.name AS name, labels(n)[0] AS kind, coalesce(n.file_path, n.path) AS path
                    """;
                var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["cleanId"] = cleanId
                }, ct);
                using var doc = JsonDocument.Parse(json);
                EvidenceNodeRef? best = null;
                var bestScore = -1;
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var rId = row.GetStringProp("id", id);
                    var rName = row.GetStringProp("name", cleanId);
                    var rKind = row.GetStringProp("kind");
                    var rPath = row.GetStringProp("path");
                    var rank = NodeKindResolutionRank(rKind, rawHadServicePrefix);
                    if (rank == 0 && !rId.Equals(id, StringComparison.OrdinalIgnoreCase)) continue;

                    var exactBonus = rId.Equals(id, StringComparison.OrdinalIgnoreCase) ? 1000
                                   : rName.Equals(cleanId, StringComparison.OrdinalIgnoreCase) ? 200
                                   : 0;
                    var score = exactBonus + rank;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new EvidenceNodeRef(rId, rName, rKind, rPath);
                    }
                }
                if (best is not null)
                {
                    result.Add(best);
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
            var pub = await CollectTopicEvidenceAsync(target, source, publish: true, ct);
            if (pub.Count > 0) return pub;
            return await CollectTopicEvidenceAsync(target, source, publish: false, ct);
        }

        if (source.Label == OntologyConstants.NodeLabels.Topic)
        {
            var sub = await CollectTopicEvidenceAsync(source, target, publish: false, ct);
            if (sub.Count > 0) return sub;
            return await CollectTopicEvidenceAsync(source, target, publish: true, ct);
        }

        if (target.Label == OntologyConstants.NodeLabels.ExternalService)
        {
            return await CollectExternalCallEvidenceAsync(source, target, ct);
        }

        if (source.Label == OntologyConstants.NodeLabels.ExternalService)
        {
            return await CollectExternalCallEvidenceAsync(target, source, ct);
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

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outgoingKinds = string.Join(", ", ExternalCallKinds.Append("TRANSITIVELY_CALLS").Distinct().Select(k => $"'{k}'"));
        var incomingKinds = "'CALLED_BY'";

        foreach (var (pattern, kinds) in new[] { ("(sym)-[r]->(ext)", outgoingKinds), ("(ext)-[r]->(sym)", incomingKinds) })
        {
            var query = $"""
                MATCH {pattern}
                WHERE ext.id = $extId AND r.kind IN [{kinds}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind, coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line, coalesce(sym.end_line, sym.line_end) AS line_end,
                       r.kind AS rel_kind, json_extract(sym.properties, '$.service') AS service
                """;

            try
            {
                var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["extId"] = external.Id }, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var path = row.GetStringProp("path");
                    if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"))) continue;

                    var symKind = row.GetStringProp("kind");
                    if (symKind is "Endpoint" or "EntryPoint" && row.GetStringProp("rel_kind") == "ATTRIBUTED_TO") continue;

                    var line = row.GetNullableIntProp("line") ?? 1;
                    var symName = row.GetStringProp("name");
                    if (!seen.Add($"{path}:{line}:{symName}")) continue;

                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = row.GetStringProp("id"),
                        Kind = "http_call",
                        Operation = "CALL",
                        Title = $"Calls {external.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = row.GetNullableIntProp("line_end") ?? line,
                        CallerSymbol = symName,
                        TargetSymbol = external.Name,
                        ServiceName = scope.Name,
                        Reason = $"Invokes external service '{external.Name}'"
                    });
                }
            }
            catch
            {
                // Continue to next pattern.
            }
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

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Direct Endpoint calls: (sym)-[:CALLS_ENDPOINT]->(ep:Endpoint)
        var epQuery = $"""
            MATCH (sym)-[r]->(ep:Endpoint)
            WHERE r.kind = 'CALLS_ENDPOINT' AND {string.Format(NotAContainer, "sym")}
            RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind, coalesce(sym.file_path, sym.path) AS path,
                   coalesce(sym.start_line, sym.line) AS line, coalesce(sym.end_line, sym.line_end) AS line_end,
                   ep.name AS ep_name, coalesce(ep.file_path, ep.path) AS ep_path,
                   ep.http_method AS http_method, ep.route_template AS route, ep.protocol AS protocol,
                   json_extract(sym.properties, '$.service') AS caller_service,
                   json_extract(ep.properties, '$.service') AS ep_service
            """;

        try
        {
            var json = await db.ExecuteQueryAsync(epQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !callerScope.Matches(path, row.GetStringProp("caller_service"))) continue;
                if (!calleeScope.Matches(row.GetStringProp("ep_path"), row.GetStringProp("ep_service"))) continue;

                var protocol = row.GetStringProp("protocol");
                var method = row.GetStringProp("http_method");
                var route = row.GetStringProp("route");
                var epName = row.GetStringProp("ep_name");
                var line = row.GetNullableIntProp("line") ?? 1;
                var callerName = row.GetStringProp("name");

                var kind = protocol.Equals("grpc", StringComparison.OrdinalIgnoreCase) ? "grpc_call"
                         : protocol.Equals("wcf", StringComparison.OrdinalIgnoreCase) ? "wcf_call"
                         : "http_call";
                var operation = string.IsNullOrEmpty(method) || method.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    ? (kind == "http_call" ? "CALL" : kind[..4].ToUpperInvariant())
                    : method.ToUpperInvariant();

                if (!seen.Add($"{path}:{line}:{epName}")) continue;

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = row.GetStringProp("id"),
                    Kind = kind,
                    Operation = operation,
                    Title = string.IsNullOrEmpty(route) ? epName : $"{operation} {route}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = row.GetNullableIntProp("line_end") ?? line,
                    CallerSymbol = callerName,
                    TargetSymbol = epName,
                    ServiceName = callerScope.Name,
                    Reason = $"Calls endpoint '{epName}' exposed by '{calleeScope.Name}'"
                });
            }
        }
        catch { }

        // 2. Cross-project symbol calls / inheritance / type usages: (sym1)-[:CALLS|INHERITS_FROM|IMPLEMENTS|USES_API]->(sym2)
        if (items.Count == 0 && callerScope.Directory is not null && calleeScope.Directory is not null)
        {
            var symQuery = """
                MATCH (sym1)-[r]->(sym2)
                WHERE r.kind IN ['CALLS', 'INHERITS_FROM', 'IMPLEMENTS', 'USES_API']
                  AND (sym1:Function OR sym1:Type OR sym1:Member OR sym1:File)
                  AND (sym2:Function OR sym2:Type OR sym2:Member OR sym2:Endpoint)
                RETURN sym1.id AS id, sym1.name AS caller_name, labels(sym1)[0] AS caller_kind,
                       coalesce(sym1.file_path, sym1.path) AS caller_path,
                       coalesce(sym1.start_line, sym1.line) AS line, coalesce(sym1.end_line, sym1.line_end) AS line_end,
                       sym2.name AS target_name, labels(sym2)[0] AS target_kind,
                       coalesce(sym2.file_path, sym2.path) AS target_path,
                       r.kind AS rel_kind
                """;
            try
            {
                var json = await db.ExecuteQueryAsync(symQuery, null, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var cPath = row.GetStringProp("caller_path");
                    var tPath = row.GetStringProp("target_path");
                    if (string.IsNullOrEmpty(cPath) || string.IsNullOrEmpty(tPath)) continue;
                    if (!callerScope.Matches(cPath) || !calleeScope.Matches(tPath)) continue;

                    var line = row.GetNullableIntProp("line") ?? 1;
                    var callerName = row.GetStringProp("caller_name");
                    var targetName = row.GetStringProp("target_name");
                    var relKind = row.GetStringProp("rel_kind", "CALLS");
                    if (!seen.Add($"{cPath}:{line}:{targetName}")) continue;

                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = row.GetStringProp("id"),
                        Kind = "http_call",
                        Operation = relKind == "CALLS" ? "CALL" : relKind,
                        Title = $"{callerName} → {targetName}",
                        FilePath = cPath,
                        LineStart = line,
                        LineEnd = row.GetNullableIntProp("line_end") ?? line,
                        CallerSymbol = callerName,
                        TargetSymbol = targetName,
                        ServiceName = callerScope.Name,
                        Reason = $"Cross-service reference from '{callerScope.Name}' to '{targetName}' in '{calleeScope.Name}'"
                    });
                    if (items.Count >= 100) break;
                }
            }
            catch { }
        }

        // 3. Package / Library / Manifest dependency fallback: (p1:Project)-[:DEPENDS_ON]->(pkg:Package)-[:IMPLEMENTED_BY]->(p2:Project)
        // or direct project-to-project (s1)-[:SERVICE_CALL|DEPENDS_ON|TESTS]->(s2)
        if (items.Count == 0)
        {
            var pkgQuery = """
                MATCH (p1:Project)-[r:DEPENDS_ON]->(pkg:Package)-[:IMPLEMENTED_BY]->(p2:Project)
                RETURN p1.id AS p1_id, p1.name AS p1_name, coalesce(p1.file_path, p1.path) AS p1_path,
                       pkg.name AS pkg_name,
                       p2.id AS p2_id, p2.name AS p2_name, coalesce(p2.file_path, p2.path) AS p2_path
                """;
            try
            {
                var json = await db.ExecuteQueryAsync(pkgQuery, null, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var p1Path = row.GetStringProp("p1_path");
                    var p2Path = row.GetStringProp("p2_path");
                    var p1Name = row.GetStringProp("p1_name");
                    var p2Name = row.GetStringProp("p2_name");
                    if (!callerScope.Matches(p1Path, p1Name) || !calleeScope.Matches(p2Path, p2Name)) continue;

                    var pkgName = row.GetStringProp("pkg_name");
                    var manifestPath = string.IsNullOrEmpty(p1Path) ? $"{callerScope.Directory}/package.json" : p1Path;
                    if (!manifestPath.Contains('.')) manifestPath = $"{manifestPath.TrimEnd('/')}/package.json";

                    if (seen.Add($"pkg:{manifestPath}:{pkgName}"))
                    {
                        items.Add(new ArchitectureEvidenceItemDto
                        {
                            Id = row.GetStringProp("p1_id"),
                            Kind = "library_call",
                            Operation = "DEPENDS_ON",
                            Title = $"Depends on package {pkgName}",
                            FilePath = manifestPath,
                            LineStart = 1,
                            LineEnd = 1,
                            CallerSymbol = callerScope.Name,
                            TargetSymbol = pkgName,
                            ServiceName = callerScope.Name,
                            Reason = $"Project '{callerScope.Name}' declares dependency on workspace library '{pkgName}' ('{calleeScope.Name}')"
                        });
                    }
                }
            }
            catch { }
        }

        // 4. Direct project/service macro-relationship fallback if still empty and an actual graph edge exists between caller and callee
        if (items.Count == 0 && !string.IsNullOrEmpty(callerScope.Directory))
        {
            var directEdgeQuery = """
                MATCH (s1)-[r]->(s2)
                WHERE (s1.id = $cId OR s1.name = $cName)
                  AND (s2.id = $tId OR s2.name = $tName)
                  AND r.kind IN ['SERVICE_CALL', 'DEPENDS_ON', 'TESTS', 'USES_API']
                RETURN r.kind AS rel_kind
                LIMIT 1
                """;
            try
            {
                var json = await db.ExecuteQueryAsync(directEdgeQuery, new Dictionary<string, object>
                {
                    ["cId"] = caller.Id,
                    ["cName"] = caller.Name,
                    ["tId"] = callee.Id,
                    ["tName"] = callee.Name
                }, ct);
                using var doc = JsonDocument.Parse(json);
                var row = doc.RootElement.EnumerateArray().FirstOrDefault();
                if (row.ValueKind == JsonValueKind.Object)
                {
                    var relKind = row.GetStringProp("rel_kind", "CONFIG");
                    var manifestPath = caller.Path ?? callerScope.Directory;
                    if (!manifestPath.Contains('.')) manifestPath = $"{manifestPath.TrimEnd('/')}/package.json";
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = caller.Id,
                        Kind = "config",
                        Operation = relKind,
                        Title = $"{callerScope.Name} → {calleeScope.Name}",
                        FilePath = manifestPath,
                        LineStart = 1,
                        LineEnd = 1,
                        CallerSymbol = callerScope.Name,
                        TargetSymbol = calleeScope.Name,
                        ServiceName = callerScope.Name,
                        Reason = $"Project/service dependency ({relKind}) from '{callerScope.Name}' to '{calleeScope.Name}'"
                    });
                }
            }
            catch { }
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

    /// <summary>
    /// Resolves source-code evidence proving a rendered node's role and capabilities
    /// (Endpoints, EntryPoints, ORM entities, Queries, Topic publishers/consumers, External calls, Configs).
    /// Grouped nodes pass every member id.
    /// </summary>
    public async Task<NodeEvidenceResponseDto> GetNodeEvidenceAsync(
        IReadOnlyList<string> nodeIds,
        string? nodeKind = null,
        CancellationToken ct = default)
    {
        var response = new NodeEvidenceResponseDto
        {
            NodeId = nodeIds.FirstOrDefault() ?? string.Empty,
            NodeKind = nodeKind ?? string.Empty
        };

        var resolvedNodes = await ResolveEvidenceNodesAsync(nodeIds, ct);
        if (resolvedNodes.Count > 0)
        {
            response.NodeName = resolvedNodes[0].Name;
            if (string.IsNullOrEmpty(response.NodeKind))
            {
                response.NodeKind = resolvedNodes[0].Label;
            }
        }

        var items = new List<ArchitectureEvidenceItemDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in resolvedNodes)
        {
            var nodeItems = await CollectSingleNodeEvidenceAsync(node, ct);
            foreach (var item in nodeItems)
            {
                if (seen.Add($"{item.Kind}:{item.FilePath}:{item.LineStart}:{item.Title}:{item.Operation}"))
                {
                    items.Add(item);
                }
            }
        }

        response.Items = items
            .OrderBy(i => EvidenceKindOrder(i.Kind))
            .ThenBy(i => i.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.LineStart)
            .Take(150)
            .ToList();
        response.TotalCount = response.Items.Count;
        return response;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectSingleNodeEvidenceAsync(EvidenceNodeRef node, CancellationToken ct)
    {
        if (node.Label == OntologyConstants.NodeLabels.Database)
        {
            return (await GetDatabaseEvidenceAsync(node.Id, null, ct)).Items;
        }

        if (node.Label == OntologyConstants.NodeLabels.Topic)
        {
            return await CollectAllTopicNodeEvidenceAsync(node, ct);
        }

        if (node.Label == OntologyConstants.NodeLabels.ExternalService)
        {
            return await CollectAllExternalNodeEvidenceAsync(node, ct);
        }

        return await CollectServiceOrProjectNodeEvidenceAsync(node, ct);
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectAllTopicNodeEvidenceAsync(EvidenceNodeRef topic, CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        foreach (var (publish, kinds, dir) in new[]
        {
            (true, PublishOutgoingKinds, "(sym)-[r]->(t)"),
            (true, PublishIncomingKinds, "(t)-[r]->(sym)"),
            (false, SubscribeOutgoingKinds, "(sym)-[r]->(t)"),
            (false, SubscribeIncomingKinds, "(t)-[r]->(sym)")
        })
        {
            var query = $"""
                MATCH {dir}
                WHERE (t.id = $topicId OR t.name = $topicName)
                  AND r.kind IN [{string.Join(", ", kinds.Select(k => $"'{k}'"))}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind, coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line, coalesce(sym.end_line, sym.line_end) AS line_end,
                       json_extract(sym.properties, '$.service') AS service
                """;
            try
            {
                var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object>
                {
                    ["topicId"] = topic.Id,
                    ["topicName"] = topic.Name
                }, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var path = row.GetStringProp("path");
                    if (string.IsNullOrEmpty(path)) continue;
                    var line = row.GetNullableIntProp("line") ?? 1;
                    var symName = row.GetStringProp("name");
                    var svc = row.GetStringProp("service");
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
                        ServiceName = string.IsNullOrEmpty(svc) ? null : svc,
                        Reason = $"{row.GetStringProp("kind")} '{symName}' {(publish ? "publishes to" : "consumes from")} topic '{topic.Name}'"
                    });
                }
            }
            catch { }
        }
        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectAllExternalNodeEvidenceAsync(EvidenceNodeRef external, CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var outgoingKinds = string.Join(", ", ExternalCallKinds.Append("TRANSITIVELY_CALLS").Distinct().Select(k => $"'{k}'"));
        foreach (var (pattern, kinds) in new[] { ("(sym)-[r]->(ext)", outgoingKinds), ("(ext)-[r]->(sym)", "'CALLED_BY'") })
        {
            var query = $"""
                MATCH {pattern}
                WHERE (ext.id = $extId OR ext.name = $extName)
                  AND r.kind IN [{kinds}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind, coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line, coalesce(sym.end_line, sym.line_end) AS line_end,
                       r.kind AS rel_kind, json_extract(sym.properties, '$.service') AS service
                """;
            try
            {
                var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object>
                {
                    ["extId"] = external.Id,
                    ["extName"] = external.Name
                }, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var path = row.GetStringProp("path");
                    if (string.IsNullOrEmpty(path)) continue;
                    var symKind = row.GetStringProp("kind");
                    if (symKind is "Endpoint" or "EntryPoint" && row.GetStringProp("rel_kind") == "ATTRIBUTED_TO") continue;

                    var line = row.GetNullableIntProp("line") ?? 1;
                    var symName = row.GetStringProp("name");
                    var svc = row.GetStringProp("service");
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = row.GetStringProp("id"),
                        Kind = "http_call",
                        Operation = "CALL",
                        Title = $"Calls {external.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = row.GetNullableIntProp("line_end") ?? line,
                        CallerSymbol = symName,
                        TargetSymbol = external.Name,
                        ServiceName = string.IsNullOrEmpty(svc) ? null : svc,
                        Reason = $"Invokes external service '{external.Name}'"
                    });
                }
            }
            catch { }
        }
        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectServiceOrProjectNodeEvidenceAsync(EvidenceNodeRef serviceNode, CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var scope = await ResolveServiceScopeAsync(serviceNode.Id, ct);
        if (scope is null) return items;

        // 1. Endpoints exposed by this service
        var epQuery = """
            MATCH (ep:Endpoint)
            RETURN ep.id AS id, ep.name AS name, coalesce(ep.file_path, ep.path) AS path,
                   coalesce(ep.start_line, ep.line) AS line, coalesce(ep.end_line, ep.line_end) AS line_end,
                   ep.http_method AS http_method, ep.route_template AS route, ep.protocol AS protocol,
                   json_extract(ep.properties, '$.service') AS service
            """;
        try
        {
            var json = await db.ExecuteQueryAsync(epQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"))) continue;

                var method = row.GetStringProp("http_method");
                var route = row.GetStringProp("route");
                var epName = row.GetStringProp("name");
                var line = row.GetNullableIntProp("line") ?? 1;
                var op = string.IsNullOrEmpty(method) || method.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? "ENDPOINT" : method.ToUpperInvariant();

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = row.GetStringProp("id"),
                    Kind = "endpoint",
                    Operation = op,
                    Title = string.IsNullOrEmpty(route) ? epName : $"{op} {route}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = row.GetNullableIntProp("line_end") ?? line,
                    TargetSymbol = epName,
                    ServiceName = scope.Name,
                    Reason = $"HTTP/RPC endpoint exposed by '{scope.Name}'"
                });
            }
        }
        catch { }

        // 2. EntryPoints (bootstrap / workers / CLI / UI components) in this service
        var entryQuery = """
            MATCH (ep:EntryPoint)
            RETURN ep.id AS id, ep.name AS name, coalesce(ep.file_path, ep.path) AS path,
                   coalesce(ep.start_line, ep.line) AS line, coalesce(ep.end_line, ep.line_end) AS line_end,
                   json_extract(ep.properties, '$.kind') AS ep_kind,
                   json_extract(ep.properties, '$.service') AS service
            """;
        try
        {
            var json = await db.ExecuteQueryAsync(entryQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            var entryCount = 0;
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"))) continue;

                var name = row.GetStringProp("name");
                var line = row.GetNullableIntProp("line") ?? 1;
                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = row.GetStringProp("id"),
                    Kind = "entrypoint",
                    Operation = "ENTRY",
                    Title = name,
                    FilePath = path,
                    LineStart = line,
                    LineEnd = row.GetNullableIntProp("line_end") ?? line,
                    CallerSymbol = name,
                    ServiceName = scope.Name,
                    Reason = $"Application/worker entry point in '{scope.Name}'"
                });
                if (++entryCount >= 25) break;
            }
        }
        catch { }

        // 3. ORM Entities (PERSISTED_IN) in this service
        var ormQuery = """
            MATCH (t:Type)-[:PERSISTED_IN]->(tbl)
            RETURN t.id AS id, t.name AS name, coalesce(t.file_path, t.path) AS path,
                   coalesce(t.start_line, t.line) AS line, coalesce(t.end_line, t.line_end) AS line_end,
                   tbl.name AS tbl_name, json_extract(t.properties, '$.service') AS service
            """;
        try
        {
            var json = await db.ExecuteQueryAsync(ormQuery, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"))) continue;

                var name = row.GetStringProp("name");
                var tblName = row.GetStringProp("tbl_name");
                var line = row.GetNullableIntProp("line") ?? 1;
                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = row.GetStringProp("id"),
                    Kind = "orm",
                    Operation = "ORM",
                    Title = $"{name} → {tblName}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = row.GetNullableIntProp("line_end") ?? line,
                    CallerSymbol = name,
                    TargetSymbol = tblName,
                    ServiceName = scope.Name,
                    Reason = $"ORM entity '{name}' persisted in table '{tblName}'"
                });
            }
        }
        catch { }

        // 4. Primary project manifest / declaration fallback
        if (!string.IsNullOrEmpty(scope.Directory))
        {
            var manifestPath = serviceNode.Path ?? scope.Directory;
            if (!manifestPath.Contains('.')) manifestPath = $"{manifestPath.TrimEnd('/')}/package.json";
            items.Add(new ArchitectureEvidenceItemDto
            {
                Id = serviceNode.Id,
                Kind = "config",
                Operation = "MANIFEST",
                Title = $"{scope.Name} ({serviceNode.Label})",
                FilePath = manifestPath,
                LineStart = 1,
                LineEnd = 1,
                ServiceName = scope.Name,
                Reason = $"Project / service manifest for '{scope.Name}'"
            });
        }

        return items;
    }
}
