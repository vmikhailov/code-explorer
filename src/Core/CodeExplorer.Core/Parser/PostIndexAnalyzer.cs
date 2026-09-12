using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public record PostIndexGraphData(
    Dictionary<string, List<string>> CallsAdjacency,
    Dictionary<string, string> Sinks,
    Dictionary<string, string?> SinkDomains,
    List<string> Callers,
    Dictionary<string, List<string>> Implements,
    List<string> EntryPointIds,
    Dictionary<string, List<string>> ProjectToEntryPoints
);

public record PostIndexAnalysisResult(
    List<(string From, string To, int Hops)> TransitivelyCalls,
    List<(string EpId, string SinkId, int Hops, string SinkKind)> AttributedTo,
    Dictionary<string, List<string>> ProjectExternalApis
);

public class PostIndexAnalyzer(IGraphClient db)
{
    public async Task RunAsync(string workspaceId)
    {
        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");

        if (db is SqliteGraphClient sqliteClient)
        {
            var graphData = await sqliteClient.LoadPostIndexGraphDataAsync(widPrefix);
            var result = Analyze(graphData, widPrefix);
            await sqliteClient.SavePostIndexResultsAsync(widPrefix, result);
        }
        else
        {
            await WriteTransitivelyCallsAsync(widPrefix);
            await WriteAttributedToAsync(widPrefix);
            await WriteProjectApiAnnotationsAsync(widPrefix);
        }
    }

    public async Task RunInMemoryAsync(
        ParsingContext ctx,
        Layer4Result l4Result,
        List<Relationship> referenceRelationships,
        List<Relationship> lateBoundRels)
    {
        var callsAdjacency = new Dictionary<string, List<string>>();
        void AddCall(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            if (!callsAdjacency.TryGetValue(from, out var list))
            {
                list = new List<string>();
                callsAdjacency[from] = list;
            }
            list.Add(to);
        }

        foreach (var rel in referenceRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Calls)
            {
                AddCall(rel.From, rel.To);
            }
        }

        foreach (var rel in lateBoundRels)
        {
            if (rel.Kind == OntologyConstants.Relationships.Calls)
            {
                AddCall(rel.From, rel.To);
            }
        }

        var sinks = new Dictionary<string, string>();
        var sinkDomains = new Dictionary<string, string?>();
        var entryPointIds = new List<string>();

        void CollectNodes(IOntologyNode node)
        {
            if (node is ExternalServiceNode es)
            {
                sinks[es.Id] = "ExternalService";
                sinkDomains[es.Id] = es.DomainOrService;
            }
            else if (node is DatabaseNode db)
            {
                sinks[db.Id] = "DB";
            }
            else if (node is QueryNode q)
            {
                sinks[q.Id] = "Query";
            }
            else if (node is CloudServiceNode cs)
            {
                sinks[cs.Id] = "ExternalService";
                sinkDomains[cs.Id] = cs.Name;
            }
            else if (node is EntryPointNode ep)
            {
                entryPointIds.Add(ep.Id);
            }
            else if (node is EndpointNode endp)
            {
                entryPointIds.Add(endp.Id);
            }

            foreach (var child in node.Children)
            {
                CollectNodes(child);
            }
        }

        var root = l4Result.Prev.Prev.Prev.Workspace;
        CollectNodes(root);

        foreach (var (key, id) in ctx.GlobalSymbols)
        {
            if (key.Kind == OntologyConstants.NodeLabels.EntryPoint || key.Kind == OntologyConstants.NodeLabels.Endpoint)
            {
                if (!entryPointIds.Contains(id))
                {
                    entryPointIds.Add(id);
                }
            }
        }

        var implements = new Dictionary<string, List<string>>();
        void AddImplements(string epId, string fnId)
        {
            if (string.IsNullOrEmpty(epId) || string.IsNullOrEmpty(fnId)) return;
            if (!implements.TryGetValue(epId, out var list))
            {
                list = new List<string>();
                implements[epId] = list;
            }
            if (!list.Contains(fnId))
            {
                list.Add(fnId);
            }
        }

        foreach (var rel in ctx.GlobalProjectDependencies)
        {
            if (rel.Kind == OntologyConstants.Relationships.ImplementedBy)
            {
                AddImplements(rel.From, rel.To);
            }
            else if (rel.Kind == OntologyConstants.Relationships.Implements)
            {
                AddImplements(rel.To, rel.From);
            }
        }

        foreach (var rel in referenceRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.ImplementedBy)
            {
                AddImplements(rel.From, rel.To);
            }
            else if (rel.Kind == OntologyConstants.Relationships.Implements)
            {
                AddImplements(rel.To, rel.From);
            }
        }

        var projectToEntryPoints = new Dictionary<string, List<string>>();
        foreach (var project in l4Result.Prev.Prev.Projects)
        {
            var epList = new List<string>();
            var projectSemanticId = $"{ctx.WorkspaceId}:project:{project.Path}:project_semantic";
            var semNode = ctx.SemanticStructure?.Children.FirstOrDefault(c => c.Id == projectSemanticId);
            if (semNode != null)
            {
                foreach (var child in semNode.Children)
                {
                    if (child is EntryPointNode ep && !epList.Contains(ep.Id)) epList.Add(ep.Id);
                    else if (child is EndpointNode endp && !epList.Contains(endp.Id)) epList.Add(endp.Id);
                }
            }
            foreach (var child in project.Children)
            {
                if (child is EntryPointNode ep && !epList.Contains(ep.Id)) epList.Add(ep.Id);
                else if (child is EndpointNode endp && !epList.Contains(endp.Id)) epList.Add(endp.Id);
            }
            projectToEntryPoints[project.Id] = epList;
        }

        var callersSet = new HashSet<string>(callsAdjacency.Keys);
        foreach (var fnList in implements.Values)
        {
            foreach (var fnId in fnList)
            {
                callersSet.Add(fnId);
            }
        }

        var widPrefix = string.IsNullOrEmpty(ctx.WorkspaceId) ? "" : (ctx.WorkspaceId.EndsWith(':') ? ctx.WorkspaceId : ctx.WorkspaceId + ":");
        var graphData = new PostIndexGraphData(callsAdjacency, sinks, sinkDomains, callersSet.ToList(), implements, entryPointIds, projectToEntryPoints);
        var result = Analyze(graphData, widPrefix);

        // Update project nodes in memory
        foreach (var (projId, domains) in result.ProjectExternalApis)
        {
            var proj = l4Result.Prev.Prev.Projects.FirstOrDefault(p => p.Id == projId);
            if (proj != null && proj.Extensions != null)
            {
                proj.Extensions["external_apis"] = JsonSerializer.Serialize(domains);
            }
        }

        if (db is SqliteGraphClient sqliteClient)
        {
            await sqliteClient.SavePostIndexResultsAsync(widPrefix, result);
        }
        else
        {
            var tcRels = result.TransitivelyCalls.Select(tc => new Relationship(
                tc.From,
                tc.To,
                OntologyConstants.Relationships.TransitivelyCalls,
                new Dictionary<string, object> { ["hops"] = tc.Hops }
            )).ToList();

            var attrRels = result.AttributedTo.Select(attr => new Relationship(
                attr.EpId,
                attr.SinkId,
                OntologyConstants.Relationships.AttributedTo,
                new Dictionary<string, object> { ["hops"] = attr.Hops, ["sink_kind"] = attr.SinkKind }
            )).ToList();

            if (tcRels.Count > 0) await db.UploadRelationshipsAsync(tcRels);
            if (attrRels.Count > 0) await db.UploadRelationshipsAsync(attrRels);
        }

        ctx.Log($"[PostIndexAnalyzer] In-memory analysis complete: {result.TransitivelyCalls.Count} TRANSITIVELY_CALLS, {result.AttributedTo.Count} ATTRIBUTED_TO, {result.ProjectExternalApis.Count} project external_apis.");
    }

    public static PostIndexAnalysisResult Analyze(PostIndexGraphData data, string widPrefix = "")
    {
        var transitivelyCalls = new List<(string From, string To, int Hops)>();
        var attributedTo = new List<(string EpId, string SinkId, int Hops, string SinkKind)>();
        var projectExternalApis = new Dictionary<string, List<string>>();

        // 1. BFS for TransitivelyCalls and caller-to-sink paths
        var callerToSinks = new Dictionary<string, Dictionary<string, int>>();
        var queue = new Queue<(string NodeId, int Depth)>();
        var visited = new HashSet<string>();

        // Collect all potential callers: callers list + any implementing functions
        var allCallers = new HashSet<string>(data.Callers);
        foreach (var fnList in data.Implements.Values)
        {
            foreach (var fnId in fnList)
            {
                allCallers.Add(fnId);
            }
        }

        foreach (var callerId in allCallers)
        {
            if (!data.CallsAdjacency.ContainsKey(callerId)) continue;

            queue.Clear();
            visited.Clear();
            var cSinks = new Dictionary<string, int>();

            visited.Add(callerId);
            queue.Enqueue((callerId, 0));

            while (queue.Count > 0)
            {
                var (curr, depth) = queue.Dequeue();
                if (depth > 0 && data.Sinks.ContainsKey(curr))
                {
                    cSinks.TryAdd(curr, depth);
                }

                if (depth >= 15) continue;

                if (data.CallsAdjacency.TryGetValue(curr, out var nextList))
                {
                    foreach (var next in nextList)
                    {
                        if (visited.Add(next))
                        {
                            queue.Enqueue((next, depth + 1));
                        }
                    }
                }
            }

            if (cSinks.Count > 0)
            {
                callerToSinks[callerId] = cSinks;

                if (string.IsNullOrEmpty(widPrefix) || callerId.StartsWith(widPrefix, StringComparison.Ordinal))
                {
                    foreach (var (sinkId, hops) in cSinks)
                    {
                        transitivelyCalls.Add((callerId, sinkId, hops));
                    }
                }
            }
        }

        // 2. AttributedTo: ep <-[:IMPLEMENTS]- fn -[:CALLS*0..15]-> sink
        // Path length = 1 (implements) + callHops
        var epToDomains = new Dictionary<string, HashSet<string>>();

        foreach (var epId in data.EntryPointIds)
        {
            if (!string.IsNullOrEmpty(widPrefix) && !epId.StartsWith(widPrefix, StringComparison.Ordinal)) continue;
            if (!data.Implements.TryGetValue(epId, out var fnIds)) continue;

            var epSinks = new Dictionary<string, int>();
            foreach (var fnId in fnIds)
            {
                // 0 call hops: fn is itself a sink (length of path = 1)
                if (data.Sinks.ContainsKey(fnId))
                {
                    if (!epSinks.TryGetValue(fnId, out var h) || 1 < h)
                    {
                        epSinks[fnId] = 1;
                    }
                }

                // 1..15 call hops: fn calls sink (length of path = 1 + hops)
                if (callerToSinks.TryGetValue(fnId, out var sMap))
                {
                    foreach (var (sinkId, hops) in sMap)
                    {
                        var totalHops = 1 + hops;
                        if (!epSinks.TryGetValue(sinkId, out var h) || totalHops < h)
                        {
                            epSinks[sinkId] = totalHops;
                        }
                    }
                }
            }

            foreach (var (sinkId, hops) in epSinks)
            {
                var sinkKind = data.Sinks.TryGetValue(sinkId, out var sk) ? sk : "";
                attributedTo.Add((epId, sinkId, hops, sinkKind));

                // Track external service domains for project annotations
                if (sinkKind.Equals("ExternalService", StringComparison.OrdinalIgnoreCase))
                {
                    if (data.SinkDomains.TryGetValue(sinkId, out var domain) && !string.IsNullOrEmpty(domain))
                    {
                        if (!epToDomains.TryGetValue(epId, out var dSet))
                        {
                            dSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            epToDomains[epId] = dSet;
                        }
                        dSet.Add(domain);
                    }
                }
            }
        }

        // 3. Project external_apis
        foreach (var (projId, epList) in data.ProjectToEntryPoints)
        {
            if (!string.IsNullOrEmpty(widPrefix) && !projId.StartsWith(widPrefix, StringComparison.Ordinal)) continue;

            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var epId in epList)
            {
                if (epToDomains.TryGetValue(epId, out var dSet))
                {
                    foreach (var d in dSet)
                    {
                        domains.Add(d);
                    }
                }
            }

            projectExternalApis[projId] = domains.OrderBy(x => x).ToList();
        }

        return new PostIndexAnalysisResult(transitivelyCalls, attributedTo, projectExternalApis);
    }

    private Task WriteTransitivelyCallsAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH path = (caller:Function)-[:CALLS*1..15]->(sink)
        WHERE caller.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Query)
        WITH caller, sink, min(length(path)) AS hops
        MERGE (caller)-[r:TRANSITIVELY_CALLS]->(sink)
        SET r.hops = hops
        """, new { widPrefix });

    private Task WriteAttributedToAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH path = (ep:EntryPoint)<-[:IMPLEMENTS]-(fn:Function)-[:CALLS*0..15]->(sink)
        WHERE ep.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Query)
        WITH ep, sink, min(length(path)) AS hops, labels(sink)[0] AS sinkKind
        MERGE (ep)-[r:ATTRIBUTED_TO]->(sink)
        SET r.hops = hops, r.sink_kind = sinkKind
        """, new { widPrefix });

    private Task WriteProjectApiAnnotationsAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH (p:Project)-[:CONTAINS|EXPOSES*1..2]->(ep:EntryPoint)-[:ATTRIBUTED_TO]->(es:ExternalService)
        WHERE p.id STARTS WITH $widPrefix
        WITH p, collect(DISTINCT es.domain_or_service) AS domains
        SET p.external_apis = domains
        """, new { widPrefix });
}
