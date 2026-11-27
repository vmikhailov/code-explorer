using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

internal static class ServiceContractBuilder
{
    private const string PropFramework = "framework";
    private const string PropLanguage = "language";
    private const string PropProjectType = "project_type";
    private const string PropIsInternal = "is_internal";
    private const string PropBrokerType = "broker_type";
    private const string PropProtocol = "protocol";
    private const string PropViaLibrary = "via_library";
    private const string TrueLiteral = "true";
    private const string MediatrBroker = "mediatr";
    private const string MediatrMarker = ":mediatr:";
    private const string GoLanguage = "go";
    private const string GoNetHttpFramework = "net/http";
    private const string DirectionAll = "all";
    private const string DirectionIngress = "ingress";
    private const string DirectionEgress = "egress";

    private static readonly string[] IngressEndpointQueries =
    [
        "MATCH (s)-[:CONTAINS|EXPOSES]->(ep:Endpoint) WHERE (s.id = $id OR s.name = $name) AND (ep.is_test IS NULL OR ep.is_test = false) RETURN ep.name AS epName",
        "MATCH (ep:Endpoint)-[:DECLARED_IN]->(p) WHERE (p.id = $id OR p.name = $name) AND (ep.is_test IS NULL OR ep.is_test = false) RETURN ep.name AS epName",
        "MATCH (p)-[:CONTAINS]->(f:File)<-[:DECLARED_IN|CONTAINS]-(ep:Endpoint) WHERE (p.id = $id OR p.name = $name) AND (ep.is_test IS NULL OR ep.is_test = false) RETURN ep.name AS epName",
        "MATCH (s)-[:IMPLEMENTED_BY]->(p:Project)-[:CONTAINS]->(f:File)<-[:DECLARED_IN|CONTAINS]-(ep:Endpoint) WHERE (s.id = $id OR s.name = $name) AND (ep.is_test IS NULL OR ep.is_test = false) RETURN ep.name AS epName"
    ];

    internal static async Task<ServiceContractDto> BuildServiceContractsAsync(
        IGraphClient db,
        GraphDataDto archGraph,
        string serviceName,
        string direction,
        CancellationToken ct)
    {
        var targetNode = archGraph.Nodes.FirstOrDefault(n =>
            n.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase) ||
            n.Id.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        var contract = new ServiceContractDto
        {
            ServiceName = targetNode?.Name ?? serviceName,
            Kind = targetNode?.Kind ?? NodeKind.Service,
            Framework = targetNode?.Properties?.GetValueOrDefault(PropFramework),
            Language = targetNode?.Properties?.GetValueOrDefault(PropLanguage) ?? targetNode?.Properties?.GetValueOrDefault(PropProjectType)
        };

        if (targetNode == null)
        {
            return contract;
        }

        var targetId = targetNode.Id;

        if (direction is DirectionAll or DirectionIngress)
        {
            await PopulateIngressContractsAsync(db, archGraph, targetId, serviceName, contract, ct);
        }

        if (direction is DirectionAll or DirectionEgress)
        {
            PopulateEgressContracts(archGraph, targetId, contract);
        }

        return contract;
    }

    private static async Task PopulateIngressContractsAsync(
        IGraphClient db,
        GraphDataDto archGraph,
        string targetId,
        string serviceName,
        ServiceContractDto contract,
        CancellationToken ct)
    {
        foreach (var epQuery in IngressEndpointQueries)
        {
            try
            {
                var epNames = await db.QueryAsync<string>(epQuery, new { id = targetId, name = serviceName }, ct);
                foreach (var epName in epNames)
                {
                    if (!string.IsNullOrEmpty(epName) && !contract.IngressEndpoints.Contains(epName))
                    {
                        contract.IngressEndpoints.Add(epName);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Continue across fallback endpoint attachment patterns
                _ = ex.Message;
            }
        }

        if (string.IsNullOrWhiteSpace(contract.Framework) &&
            contract.IngressEndpoints.Count > 0 &&
            string.Equals(contract.Language, GoLanguage, StringComparison.OrdinalIgnoreCase))
        {
            contract.Framework = GoNetHttpFramework;
        }

        foreach (var edge in archGraph.Edges)
        {
            if (edge.Target.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            {
                if (edge.Category == GraphEdgeCategory.Messaging || edge.Kind is RelationshipKind.SubscribesTo or RelationshipKind.SubscribedBy or RelationshipKind.Triggers)
                {
                    var srcNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Source, StringComparison.OrdinalIgnoreCase));
                    var topicName = srcNode?.Name ?? edge.Source;
                    if (!IsInternalTopic(edge, srcNode, edge.Source) && !contract.SubscribedTopics.Contains(topicName))
                    {
                        contract.SubscribedTopics.Add(topicName);
                    }
                }
            }
            else if (edge.Source.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            {
                var tgtNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Target, StringComparison.OrdinalIgnoreCase));
                var tgtName = tgtNode?.Name ?? edge.Target;
                var isTopic = tgtNode?.Kind == NodeKind.Topic || edge.Category == GraphEdgeCategory.Messaging;

                if (isTopic && edge.Kind is RelationshipKind.SubscribesTo or RelationshipKind.SubscribedBy or RelationshipKind.Subscribes or RelationshipKind.Consumes)
                {
                    if (!IsInternalTopic(edge, tgtNode, edge.Target) && !contract.SubscribedTopics.Contains(tgtName))
                    {
                        contract.SubscribedTopics.Add(tgtName);
                    }
                }
            }
        }
    }

    private static void PopulateEgressContracts(GraphDataDto archGraph, string targetId, ServiceContractDto contract)
    {
        foreach (var edge in archGraph.Edges)
        {
            if (!edge.Source.Equals(targetId, StringComparison.OrdinalIgnoreCase)) continue;

            var tgtNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Target, StringComparison.OrdinalIgnoreCase));
            var tgtName = tgtNode?.Name ?? edge.Target;

            if (edge.Category == GraphEdgeCategory.Database || edge.Kind == RelationshipKind.UsesDb || tgtNode?.Kind == NodeKind.Database)
            {
                if (!contract.Databases.Contains(tgtName)) contract.Databases.Add(tgtName);
            }
            else if (edge.Category == GraphEdgeCategory.Messaging || edge.Kind is RelationshipKind.PublishesTo or RelationshipKind.Publishes or RelationshipKind.Triggers || tgtNode?.Kind == NodeKind.Topic)
            {
                if (edge.Kind is not (RelationshipKind.SubscribesTo or RelationshipKind.SubscribedBy or RelationshipKind.Subscribes or RelationshipKind.Consumes))
                {
                    if (!IsInternalTopic(edge, tgtNode, edge.Target) && !contract.PublishedTopics.Contains(tgtName))
                    {
                        contract.PublishedTopics.Add(tgtName);
                    }
                }
            }
            else if (tgtNode?.Kind == NodeKind.ExternalService)
            {
                if (!contract.ExternalServices.Contains(tgtName)) contract.ExternalServices.Add(tgtName);
            }
            else if (edge.Category == GraphEdgeCategory.ServiceCall || edge.Kind is RelationshipKind.ServiceCall or RelationshipKind.CallsEndpoint)
            {
                if (!contract.OutboundServiceCalls.Contains(tgtName)) contract.OutboundServiceCalls.Add(tgtName);
            }
        }
    }

    private static bool IsInternalTopic(GraphEdgeDto edge, GraphNodeDto? topicNode, string endpointId) =>
        edge.Properties?.GetValueOrDefault(PropIsInternal) == TrueLiteral ||
        topicNode?.Properties?.GetValueOrDefault(PropIsInternal) == TrueLiteral ||
        string.Equals(topicNode?.Properties?.GetValueOrDefault(PropBrokerType), MediatrBroker, StringComparison.OrdinalIgnoreCase) ||
        endpointId.Contains(MediatrMarker, StringComparison.Ordinal);

    internal static CrossServiceFlowDto BuildCrossServiceFlow(
        GraphDataDto archGraph,
        string startService,
        string? entryPoint,
        int maxDepth)
    {
        var startNode = archGraph.Nodes.FirstOrDefault(n =>
            n.Name.Equals(startService, StringComparison.OrdinalIgnoreCase) ||
            n.Id.Equals(startService, StringComparison.OrdinalIgnoreCase));

        var flow = new CrossServiceFlowDto
        {
            StartService = startNode?.Name ?? startService,
            EntryPoint = entryPoint,
            MaxDepth = Math.Clamp(maxDepth, 1, 5)
        };

        if (startNode == null)
        {
            return flow;
        }

        var nodeLookup = archGraph.Nodes
            .GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startNode.Id };
        var queue = new Queue<(string CurrentId, int Depth)>();
        queue.Enqueue((startNode.Id, 0));

        var step = 1;
        while (queue.Count > 0)
        {
            var (currId, depth) = queue.Dequeue();
            var currNode = nodeLookup.GetValueOrDefault(currId);
            if (currNode != null && !flow.VisitedServices.Contains(currNode.Name))
            {
                flow.VisitedServices.Add(currNode.Name);
            }

            if (depth >= flow.MaxDepth) continue;

            foreach (var edge in archGraph.Edges)
            {
                if (!edge.Source.Equals(currId, StringComparison.OrdinalIgnoreCase)) continue;

                var tgtNode = nodeLookup.GetValueOrDefault(edge.Target);
                var tgtName = tgtNode?.Name ?? edge.Target;

                flow.Hops.Add(new CrossServiceHopDto
                {
                    Step = step++,
                    Source = currNode?.Name ?? currId,
                    Target = tgtName,
                    Kind = edge.Kind,
                    Protocol = edge.Properties?.GetValueOrDefault(PropProtocol) ?? edge.Category?.ToCanonicalString(),
                    Details = edge.Properties?.GetValueOrDefault(PropViaLibrary) != null ? $"via {edge.Properties[PropViaLibrary]}" : null
                });

                if (visited.Add(edge.Target))
                {
                    queue.Enqueue((edge.Target, depth + 1));
                }
            }
        }

        return flow;
    }
}
