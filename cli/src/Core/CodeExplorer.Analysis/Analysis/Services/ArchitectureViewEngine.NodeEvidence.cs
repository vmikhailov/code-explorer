using System.Text.Json;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
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
            NodeKind = EnumExtensions.ParseNodeKind(nodeKind)
        };

        var warnings = new List<string>();
        var scopeCache = new Dictionary<string, EvidenceScope?>(StringComparer.OrdinalIgnoreCase);
        var resolvedNodes = await ResolveEvidenceNodesAsync(nodeIds, ct, warnings);
        await PreloadServiceScopesAsync(resolvedNodes, scopeCache, warnings, ct);
        if (resolvedNodes.Count > 0)
        {
            response.NodeName = resolvedNodes[0].Name;
            if (response.NodeKind == NodeKind.Unspecified)
            {
                response.NodeKind = EnumExtensions.ParseNodeKind(resolvedNodes[0].Label);
            }
        }

        var items = new List<ArchitectureEvidenceItemDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in resolvedNodes)
        {
            var nodeItems = await CollectSingleNodeEvidenceAsync(node, scopeCache, warnings, ct);
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
            .ThenBy(i => i.LineStart ?? int.MaxValue)
            .Take(150)
            .ToList();
        response.TotalCount = response.Items.Count;
        if (warnings.Count > 0)
        {
            response.Warnings = warnings;
        }
        return response;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectSingleNodeEvidenceAsync(
        EvidenceNodeRef node,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string> warnings,
        CancellationToken ct)
    {
        if (node.Label == OntologyConstants.NodeLabels.Database)
        {
            var dbRes = await GetDatabaseEvidenceCoreAsync(node.Id, null, ct, scopeCache);
            if (dbRes.Warnings != null) warnings.AddRange(dbRes.Warnings.Where(w => !warnings.Contains(w)));
            return dbRes.Items;
        }

        if (node.Label == OntologyConstants.NodeLabels.Topic)
        {
            return await CollectAllTopicNodeEvidenceAsync(node, warnings, ct);
        }

        if (node.Label is OntologyConstants.NodeLabels.ExternalService or OntologyConstants.NodeLabels.CloudService)
        {
            return await CollectAllExternalNodeEvidenceAsync(node, warnings, ct);
        }

        return await CollectServiceOrProjectNodeEvidenceAsync(node, scopeCache, warnings, ct);
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectAllTopicNodeEvidenceAsync(
        EvidenceNodeRef topic,
        List<string> warnings,
        CancellationToken ct)
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
                RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind,
                       coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line,
                       coalesce(sym.end_line, sym.line_end) AS line_end,
                       r.properties AS properties,
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
                    var rProps = row.ExtractProperties();
                    var path = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                        ? rfp
                        : row.GetStringProp("path");
                    if (string.IsNullOrEmpty(path)) continue;
                    int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                    int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                    var symName = row.GetStringProp("name");
                    var svc = row.GetStringProp("service");
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = row.GetStringProp("id"),
                        Kind = publish ? OntologyConstants.EvidenceKinds.Publish : OntologyConstants.EvidenceKinds.Subscribe,
                        Operation = publish ? "PUBLISH" : "SUBSCRIBE",
                        Title = $"{(publish ? "Publishes" : "Subscribes")} {topic.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = lineEnd,
                        Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                        CallerSymbol = symName,
                        TargetSymbol = topic.Name,
                        ServiceName = string.IsNullOrEmpty(svc) ? null : svc,
                        Reason = $"{row.GetStringProp("kind")} '{symName}' {(publish ? "publishes to" : "consumes from")} topic '{topic.Name}'"
                    });
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, $"Topic node evidence query for '{topic.Name}'", ex);
            }
        }
        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectAllExternalNodeEvidenceAsync(
        EvidenceNodeRef external,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var outgoingKinds = string.Join(", ", ExternalCallKinds.Append("TRANSITIVELY_CALLS").Distinct().Select(k => $"'{k}'"));
        foreach (var (pattern, kinds) in new[] { ("(sym)-[r]->(ext)", outgoingKinds), ("(ext)-[r]->(sym)", "'CALLED_BY', 'ACCESSED_BY', 'DECLARED_IN'") })
        {
            var query = $"""
                MATCH {pattern}
                WHERE (ext.id = $extId OR ext.name = $extName)
                  AND r.kind IN [{kinds}]
                  AND {string.Format(NotAContainer, "sym")}
                RETURN sym.id AS id, sym.name AS name, labels(sym)[0] AS kind,
                       coalesce(sym.file_path, sym.path) AS path,
                       coalesce(sym.start_line, sym.line) AS line,
                       coalesce(sym.end_line, sym.line_end) AS line_end,
                       r.kind AS rel_kind, r.properties AS properties,
                       json_extract(sym.properties, '$.service') AS service
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
                    var rProps = row.ExtractProperties();
                    var path = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                        ? rfp
                        : row.GetStringProp("path");
                    if (string.IsNullOrEmpty(path)) continue;
                    var symKind = row.GetStringProp("kind");
                    if (symKind is "Endpoint" or "EntryPoint" && row.GetStringProp("rel_kind") == "ATTRIBUTED_TO") continue;

                    int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                    int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                    var symName = row.GetStringProp("name");
                    var svc = row.GetStringProp("service");
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = row.GetStringProp("id"),
                        Kind = OntologyConstants.EvidenceKinds.HttpCall,
                        Operation = "CALL",
                        Title = $"Calls {external.Name}",
                        FilePath = path,
                        LineStart = line,
                        LineEnd = lineEnd,
                        Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                        CallerSymbol = symName,
                        TargetSymbol = external.Name,
                        ServiceName = string.IsNullOrEmpty(svc) ? null : svc,
                        Reason = $"Invokes external service '{external.Name}'"
                    });
                }
            }
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, $"External node evidence query for '{external.Name}'", ex);
            }
        }
        return items;
    }

    private async Task<List<ArchitectureEvidenceItemDto>> CollectServiceOrProjectNodeEvidenceAsync(
        EvidenceNodeRef serviceNode,
        Dictionary<string, EvidenceScope?> scopeCache,
        List<string> warnings,
        CancellationToken ct)
    {
        var items = new List<ArchitectureEvidenceItemDto>();
        var scope = await ResolveServiceScopeAsync(serviceNode.Id, ct, scopeCache, warnings);
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
                var epId = row.GetStringProp("id");
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"), epId)) continue;

                var method = row.GetStringProp("http_method");
                var route = row.GetStringProp("route");
                var epName = row.GetStringProp("name");
                int? line = row.GetNullableIntProp("line");
                int? lineEnd = row.GetNullableIntProp("line_end");
                if (line <= 0) line = null;
                if (lineEnd <= 0) lineEnd = line;

                var op = string.IsNullOrEmpty(method) || method.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? "ENDPOINT" : method.ToUpperInvariant();

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = epId,
                    Kind = OntologyConstants.EvidenceKinds.Endpoint,
                    Operation = op,
                    Title = string.IsNullOrEmpty(route) ? epName : $"{op} {route}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = lineEnd,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    TargetSymbol = epName,
                    ServiceName = scope.Name,
                    Reason = $"HTTP/RPC endpoint exposed by '{scope.Name}'"
                });
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Service endpoint evidence query for '{scope.Name}'", ex);
        }

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
                var entryId = row.GetStringProp("id");
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"), entryId)) continue;

                var name = row.GetStringProp("name");
                int? line = row.GetNullableIntProp("line");
                int? lineEnd = row.GetNullableIntProp("line_end");
                if (line <= 0) line = null;
                if (lineEnd <= 0) lineEnd = line;

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = entryId,
                    Kind = OntologyConstants.EvidenceKinds.EntryPoint,
                    Operation = "ENTRY",
                    Title = name,
                    FilePath = path,
                    LineStart = line,
                    LineEnd = lineEnd,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    CallerSymbol = name,
                    ServiceName = scope.Name,
                    Reason = $"Application/worker entry point in '{scope.Name}'"
                });
                if (++entryCount >= 25) break;
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Service entrypoint evidence query for '{scope.Name}'", ex);
        }

        // 3. ORM Entities (Table -[:ACCESSED_BY|DECLARED_IN]-> Type or legacy Type -[:PERSISTED_IN]-> Table) in this service
        var ormQuery = """
            MATCH (t:Type)-[r]-(tbl:Table)
            WHERE r.kind IN ['PERSISTED_IN', 'ACCESSED_BY', 'DECLARED_IN']
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
                var typeId = row.GetStringProp("id");
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path) || !scope.Matches(path, row.GetStringProp("service"), typeId)) continue;

                var name = row.GetStringProp("name");
                var tblName = row.GetStringProp("tbl_name");
                int? line = row.GetNullableIntProp("line");
                int? lineEnd = row.GetNullableIntProp("line_end");
                if (line <= 0) line = null;
                if (lineEnd <= 0) lineEnd = line;

                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = typeId,
                    Kind = OntologyConstants.EvidenceKinds.Orm,
                    Operation = "ORM",
                    Title = $"{name} → {tblName}",
                    FilePath = path,
                    LineStart = line,
                    LineEnd = lineEnd,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    CallerSymbol = name,
                    TargetSymbol = tblName,
                    ServiceName = scope.Name,
                    Reason = $"ORM entity '{name}' persisted in table '{tblName}'"
                });
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Service ORM entity evidence query for '{scope.Name}'", ex);
        }

        // 4. Primary project manifest / declaration fallback
        if (!string.IsNullOrEmpty(scope.Directory))
        {
            var manifestPath = serviceNode.Path ?? scope.Directory;
            if (!manifestPath.Contains('.')) manifestPath = $"{manifestPath.TrimEnd('/')}/package.json";
            items.Add(new ArchitectureEvidenceItemDto
            {
                Id = serviceNode.Id,
                Kind = OntologyConstants.EvidenceKinds.Config,
                Operation = "MANIFEST",
                Title = $"{scope.Name} ({serviceNode.Label})",
                FilePath = manifestPath,
                LineStart = null,
                LineEnd = null,
                Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                ServiceName = scope.Name,
                Reason = $"Project / service manifest for '{scope.Name}'"
            });
        }

        return items;
    }
}
