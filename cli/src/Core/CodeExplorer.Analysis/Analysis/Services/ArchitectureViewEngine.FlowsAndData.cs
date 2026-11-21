using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private const string ViewKeyCallFlow = "call-flow";
    private const string ViewKeyDatabaseMap = "database-map";
    private const string ViewKeyDataFlow = "data-flow";
    private const string FlowMetaView = "view";
    private const string FlowMetaScope = "scope";
    private const string FlowMetaSharedCount = "shared_db_count";
    private const string PropIsShared = "is_shared";
    private const string PropConsumerCount = "consumer_count";
    private const string PropEngine = "engine";
    private const string PropOperation = "operation";
    private const string PropDirection = "direction";
    private const string DirectionWrite = "write";
    private const string DirectionRead = "read";
    private const string ParamScope = "scope";
    private const int MaxCallFlowHops = 5;
    private const int MaxCallFlowNodes = 120;

    private const string QueryCallFlowAnchorsScoped =
        "MATCH (n) WHERE (n:Endpoint OR n:EntryPoint OR n:Function) " +
        "AND (n.id = $scope OR n.name = $scope OR n.symbol = $scope OR n.name CONTAINS $scope) " +
        "RETURN n.id AS id, labels(n)[0] AS kind, coalesce(n.name, n.id) AS name, " +
        "coalesce(n.file_path, n.path, '') AS filePath, coalesce(n.start_line, n.line, 0) AS startLine " +
        "LIMIT 10";

    private const string QueryCallFlowAnchorsDefault =
        "MATCH (n) WHERE n:Endpoint OR n:EntryPoint " +
        "RETURN n.id AS id, labels(n)[0] AS kind, coalesce(n.name, n.id) AS name, " +
        "coalesce(n.file_path, n.path, '') AS filePath, coalesce(n.start_line, n.line, 0) AS startLine " +
        "LIMIT 10";

    private const string QueryCallFlowStep =
        "MATCH (src)-[r:TRIGGERS|EXPOSED_BY|IMPLEMENTED_BY|CALLS|READS_FROM|WRITES_TO|PUBLISHES_TO|SERVICE_CALL]->(tgt) " +
        "WHERE src.id = $srcId " +
        "RETURN tgt.id AS tgtId, labels(tgt)[0] AS tgtKind, coalesce(tgt.name, tgt.id) AS tgtName, " +
        "coalesce(tgt.file_path, tgt.path, '') AS filePath, coalesce(tgt.start_line, tgt.line, 0) AS startLine, " +
        "type(r) AS relType, coalesce(r.line, 0) AS relLine " +
        "LIMIT 25";

    private const string QueryDatabaseMapEdges =
        "MATCH (src)-[r:USES_DB|PUBLISHES_TO|SUBSCRIBES_TO|READS_FROM|WRITES_TO]->(res) " +
        "WHERE (src:Service OR src:App OR src:Worker OR src:Project OR src:CliTool) " +
        "AND (res:Database OR res:DataSet OR res:Topic) " +
        "RETURN src.id AS srcId, labels(src)[0] AS srcKind, coalesce(src.name, src.id) AS srcName, " +
        "res.id AS resId, labels(res)[0] AS resKind, coalesce(res.name, res.id) AS resName, " +
        "coalesce(res.engine, res.broker_type, '') AS engine, type(r) AS relType";

    private const string QueryDataFlowEdgesAll =
        "MATCH (actor)-[r:READS_FROM|WRITES_TO|ACCESSED_BY|PUBLISHES_TO|SUBSCRIBES_TO]->(target) " +
        "WHERE target:Table OR target:Topic OR target:Database " +
        "RETURN actor.id AS actorId, labels(actor)[0] AS actorKind, coalesce(actor.name, actor.id) AS actorName, " +
        "coalesce(actor.file_path, actor.path, '') AS actorFile, coalesce(actor.start_line, actor.line, 0) AS actorLine, " +
        "target.id AS targetId, labels(target)[0] AS targetKind, coalesce(target.name, target.id) AS targetName, " +
        "type(r) AS relType, coalesce(r.operation, '') AS operation " +
        "LIMIT 250";

    private const string QueryDataFlowEdgesScoped =
        "MATCH (actor)-[r:READS_FROM|WRITES_TO|ACCESSED_BY|PUBLISHES_TO|SUBSCRIBES_TO]->(target) " +
        "WHERE (target:Table OR target:Topic OR target:Database) " +
        "AND (target.id = $scope OR target.name = $scope OR target.name CONTAINS $scope) " +
        "RETURN actor.id AS actorId, labels(actor)[0] AS actorKind, coalesce(actor.name, actor.id) AS actorName, " +
        "coalesce(actor.file_path, actor.path, '') AS actorFile, coalesce(actor.start_line, actor.line, 0) AS actorLine, " +
        "target.id AS targetId, labels(target)[0] AS targetKind, coalesce(target.name, target.id) AS targetName, " +
        "type(r) AS relType, coalesce(r.operation, '') AS operation " +
        "LIMIT 250";

    private const string QueryCatalogEntryPoints =
        "MATCH (n) WHERE n:Endpoint OR n:EntryPoint " +
        "RETURN n.id AS id, labels(n)[0] AS kind, coalesce(n.name, n.id) AS name, " +
        "coalesce(n.file_path, n.path, '') AS filePath, coalesce(n.start_line, n.line, 0) AS startLine " +
        "ORDER BY name LIMIT 200";

    private const string QueryCatalogDatabases =
        "MATCH (n) WHERE n:Database OR n:Table OR n:Topic " +
        "RETURN n.id AS id, labels(n)[0] AS kind, coalesce(n.name, n.id) AS name, " +
        "coalesce(n.engine, n.broker_type, '') AS engine " +
        "ORDER BY kind, name LIMIT 300";

    /// <summary>
    /// Builds the Code -> Call Flow directed trace from entry points / endpoints / functions down to DB and egress.
    /// </summary>
    public async Task<GraphDataDto> GetCallFlowViewAsync(string? scope = null, CancellationToken ct = default)
    {
        var anchorRes = string.IsNullOrWhiteSpace(scope)
            ? await db.ExecuteCypherAsync(QueryCallFlowAnchorsDefault, null, ct)
            : await db.ExecuteCypherAsync(QueryCallFlowAnchorsScoped, new Dictionary<string, string> { [ParamScope] = scope.Trim() }, ct);

        var nodesById = new Dictionary<string, GraphNodeDto>(StringComparer.Ordinal);
        var edges = new List<GraphEdgeDto>();
        var edgeKeys = new HashSet<(string Src, string Tgt, RelationshipKind Kind)>();
        var frontier = new Queue<(string NodeId, int Depth)>();

        foreach (var row in anchorRes.Rows)
        {
            var id = row.GetStringProp("id");
            if (string.IsNullOrEmpty(id) || nodesById.ContainsKey(id)) continue;

            nodesById[id] = CreateFlowNode(
                id,
                row.GetStringProp("kind"),
                row.GetStringProp("name"),
                row.GetStringProp("filePath"),
                row.GetIntProp("startLine"));
            frontier.Enqueue((id, 0));
        }

        while (frontier.Count > 0 && nodesById.Count < MaxCallFlowNodes)
        {
            var (currentId, depth) = frontier.Dequeue();
            if (depth >= MaxCallFlowHops) continue;

            var stepRes = await db.ExecuteCypherAsync(
                QueryCallFlowStep,
                new Dictionary<string, string> { ["srcId"] = currentId },
                ct);

            foreach (var row in stepRes.Rows)
            {
                var tgtId = row.GetStringProp("tgtId");
                if (string.IsNullOrEmpty(tgtId)) continue;

                var relKind = EnumExtensions.ParseRelationshipKind(row.GetStringProp("relType"));
                if (!nodesById.ContainsKey(tgtId))
                {
                    if (nodesById.Count >= MaxCallFlowNodes) break;
                    nodesById[tgtId] = CreateFlowNode(
                        tgtId,
                        row.GetStringProp("tgtKind"),
                        row.GetStringProp("tgtName"),
                        row.GetStringProp("filePath"),
                        row.GetIntProp("startLine"));
                    frontier.Enqueue((tgtId, depth + 1));
                }

                if (edgeKeys.Add((currentId, tgtId, relKind)))
                {
                    var relLine = row.GetIntProp("relLine");
                    var props = relLine > 0
                        ? new Dictionary<string, string>(StringComparer.Ordinal) { ["line"] = relLine.ToString() }
                        : null;
                    edges.Add(new GraphEdgeDto
                    {
                        Id = $"{currentId}->{tgtId}:{relKind}",
                        Source = currentId,
                        Target = tgtId,
                        Kind = relKind,
                        Category = ClassifyFlowEdgeCategory(relKind),
                        Properties = props
                    });
                }
            }
        }

        return new GraphDataDto
        {
            Nodes = [.. nodesById.Values],
            Edges = edges,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FlowMetaView] = ViewKeyCallFlow,
                [FlowMetaScope] = scope ?? string.Empty
            }
        };
    }

    /// <summary>
    /// Builds the Data -> Database Map projection highlighting shared databases and topics.
    /// </summary>
    public async Task<GraphDataDto> GetDatabaseMapViewAsync(bool sharedDatabasesOnly = false, CancellationToken ct = default)
    {
        var res = await db.ExecuteCypherAsync(QueryDatabaseMapEdges, null, ct);

        var nodesById = new Dictionary<string, GraphNodeDto>(StringComparer.Ordinal);
        var consumersByResource = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var edges = new List<GraphEdgeDto>();
        var edgeKeys = new HashSet<(string Src, string Tgt, RelationshipKind Kind)>();

        foreach (var row in res.Rows)
        {
            var srcId = row.GetStringProp("srcId");
            var resId = row.GetStringProp("resId");
            if (string.IsNullOrEmpty(srcId) || string.IsNullOrEmpty(resId)) continue;

            if (!nodesById.ContainsKey(srcId))
            {
                nodesById[srcId] = CreateFlowNode(srcId, row.GetStringProp("srcKind"), row.GetStringProp("srcName"), null, 0);
            }

            if (!nodesById.ContainsKey(resId))
            {
                var resNode = CreateFlowNode(resId, row.GetStringProp("resKind"), row.GetStringProp("resName"), null, 0);
                var engine = row.GetStringProp(PropEngine);
                if (!string.IsNullOrEmpty(engine))
                {
                    resNode.Properties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                    resNode.Properties[PropEngine] = engine;
                }
                nodesById[resId] = resNode;
            }

            if (!consumersByResource.TryGetValue(resId, out var consumers))
            {
                consumers = new HashSet<string>(StringComparer.Ordinal);
                consumersByResource[resId] = consumers;
            }
            consumers.Add(srcId);

            var relKind = EnumExtensions.ParseRelationshipKind(row.GetStringProp("relType"));
            if (edgeKeys.Add((srcId, resId, relKind)))
            {
                edges.Add(new GraphEdgeDto
                {
                    Id = $"{srcId}->{resId}:{relKind}",
                    Source = srcId,
                    Target = resId,
                    Kind = relKind,
                    Category = ClassifyFlowEdgeCategory(relKind)
                });
            }
        }

        var sharedCount = 0;
        var sharedResourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (resId, consumers) in consumersByResource)
        {
            var isShared = consumers.Count > 1;
            if (isShared)
            {
                sharedCount++;
                sharedResourceIds.Add(resId);
            }

            if (nodesById.TryGetValue(resId, out var resNode))
            {
                resNode.Properties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                resNode.Properties[PropConsumerCount] = consumers.Count.ToString();
                resNode.Properties[PropIsShared] = isShared ? ProjDepsBoolTrue : ProjDepsBoolFalse;
            }
        }

        if (sharedDatabasesOnly)
        {
            edges = edges.Where(e => sharedResourceIds.Contains(e.Target)).ToList();
            var keepIds = new HashSet<string>(sharedResourceIds, StringComparer.Ordinal);
            foreach (var edge in edges)
            {
                keepIds.Add(edge.Source);
            }
            foreach (var id in nodesById.Keys.Where(k => !keepIds.Contains(k)).ToList())
            {
                nodesById.Remove(id);
            }
        }

        return new GraphDataDto
        {
            Nodes = [.. nodesById.Values.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)],
            Edges = edges,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FlowMetaView] = ViewKeyDatabaseMap,
                [FlowMetaSharedCount] = sharedCount.ToString()
            }
        };
    }

    /// <summary>
    /// Builds the Data -> Data Flow projection oriented as Writers -> Table / Topic -> Readers.
    /// </summary>
    public async Task<GraphDataDto> GetDataFlowViewAsync(string? scope = null, CancellationToken ct = default)
    {
        var res = string.IsNullOrWhiteSpace(scope)
            ? await db.ExecuteCypherAsync(QueryDataFlowEdgesAll, null, ct)
            : await db.ExecuteCypherAsync(QueryDataFlowEdgesScoped, new Dictionary<string, string> { [ParamScope] = scope.Trim() }, ct);

        var nodesById = new Dictionary<string, GraphNodeDto>(StringComparer.Ordinal);
        var edges = new List<GraphEdgeDto>();
        var edgeKeys = new HashSet<(string Src, string Tgt, RelationshipKind Kind)>();

        foreach (var row in res.Rows)
        {
            var actorId = row.GetStringProp("actorId");
            var targetId = row.GetStringProp("targetId");
            if (string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(targetId)) continue;

            if (!nodesById.ContainsKey(actorId))
            {
                nodesById[actorId] = CreateFlowNode(
                    actorId,
                    row.GetStringProp("actorKind"),
                    row.GetStringProp("actorName"),
                    row.GetStringProp("actorFile"),
                    row.GetIntProp("actorLine"));
            }

            if (!nodesById.ContainsKey(targetId))
            {
                nodesById[targetId] = CreateFlowNode(
                    targetId,
                    row.GetStringProp("targetKind"),
                    row.GetStringProp("targetName"),
                    null,
                    0);
            }

            var relKind = EnumExtensions.ParseRelationshipKind(row.GetStringProp("relType"));
            var operation = row.GetStringProp(PropOperation);
            var isWrite = relKind is RelationshipKind.WritesTo or RelationshipKind.PublishesTo ||
                          operation.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
                          operation.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                          operation.Contains("DELETE", StringComparison.OrdinalIgnoreCase);

            // Orient graph left-to-right: Writers -> DataAsset -> Readers
            var edgeSource = isWrite ? actorId : targetId;
            var edgeTarget = isWrite ? targetId : actorId;

            if (edgeKeys.Add((edgeSource, edgeTarget, relKind)))
            {
                var props = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [PropDirection] = isWrite ? DirectionWrite : DirectionRead
                };
                if (!string.IsNullOrEmpty(operation))
                {
                    props[PropOperation] = operation;
                }

                edges.Add(new GraphEdgeDto
                {
                    Id = $"{edgeSource}->{edgeTarget}:{relKind}",
                    Source = edgeSource,
                    Target = edgeTarget,
                    Kind = relKind,
                    Category = ClassifyFlowEdgeCategory(relKind),
                    Properties = props
                });
            }
        }

        return new GraphDataDto
        {
            Nodes = [.. nodesById.Values.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)],
            Edges = edges,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FlowMetaView] = ViewKeyDataFlow,
                [FlowMetaScope] = scope ?? string.Empty
            }
        };
    }

    public async Task<List<GraphNodeDto>> GetCatalogEntryPointsAsync(CancellationToken ct = default)
    {
        var res = await db.ExecuteCypherAsync(QueryCatalogEntryPoints, null, ct);
        return res.Rows
            .Select(r => CreateFlowNode(
                r.GetStringProp("id"),
                r.GetStringProp("kind"),
                r.GetStringProp("name"),
                r.GetStringProp("filePath"),
                r.GetIntProp("startLine")))
            .Where(n => !string.IsNullOrEmpty(n.Id))
            .ToList();
    }

    public async Task<List<GraphNodeDto>> GetCatalogDatabasesAsync(CancellationToken ct = default)
    {
        var res = await db.ExecuteCypherAsync(QueryCatalogDatabases, null, ct);
        var list = new List<GraphNodeDto>(res.Count);
        foreach (var r in res.Rows)
        {
            var id = r.GetStringProp("id");
            if (string.IsNullOrEmpty(id)) continue;
            var node = CreateFlowNode(id, r.GetStringProp("kind"), r.GetStringProp("name"), null, 0);
            var engine = r.GetStringProp(PropEngine);
            if (!string.IsNullOrEmpty(engine))
            {
                node.Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [PropEngine] = engine };
            }
            list.Add(node);
        }
        return list;
    }

    private static GraphNodeDto CreateFlowNode(string id, string kindStr, string name, string? filePath, int startLine)
    {
        return new GraphNodeDto
        {
            Id = id,
            Kind = EnumExtensions.ParseNodeKind(kindStr),
            Name = name,
            DisplayName = name,
            FilePath = string.IsNullOrEmpty(filePath) ? null : filePath,
            LineStart = startLine > 0 ? startLine : null
        };
    }

    private static GraphEdgeCategory ClassifyFlowEdgeCategory(RelationshipKind kind) => kind switch
    {
        RelationshipKind.UsesDb or RelationshipKind.ReadsFrom or RelationshipKind.WritesTo or RelationshipKind.AccessedBy => GraphEdgeCategory.Database,
        RelationshipKind.PublishesTo or RelationshipKind.SubscribesTo => GraphEdgeCategory.Messaging,
        RelationshipKind.ServiceCall => GraphEdgeCategory.ServiceCall,
        _ => GraphEdgeCategory.Library
    };
}
