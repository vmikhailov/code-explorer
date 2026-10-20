using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    public static List<Relationship> MaterializeDirectProjectRelationships(
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<FileNode> files,
        IReadOnlyList<Relationship> allRelationships,
        IReadOnlyDictionary<string, string>? nodeKindsById = null,
        IReadOnlyDictionary<string, string>? dbCanonicalMap = null,
        IReadOnlyDictionary<string, ProjectNode>? prebuiltFileToProject = null)
    {
        var materializedRels = new List<Relationship>();
        var existingEdges = new HashSet<(string From, string To, string Kind)>(
            allRelationships.Select(r => (r.From, r.To, r.Kind))
        );

        var projList = projects.ToList();
        var fileToProject = prebuiltFileToProject != null
            ? (prebuiltFileToProject as Dictionary<string, ProjectNode> ?? new Dictionary<string, ProjectNode>(prebuiltFileToProject, StringComparer.OrdinalIgnoreCase))
            : Layer2ProjectParser.BuildFileToProjectMap(files, projList);

        var nodeToProject = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in projList)
        {
            nodeToProject[p.Id] = p;
            if (p.Id.EndsWith(':'))
            {
                nodeToProject[p.Id.TrimEnd(':')] = p;
            }
            nodeToProject[$"{p.Id}project_semantic"] = p;
            nodeToProject[$"{p.Id}:project_semantic"] = p;
        }

        foreach (var rel in allRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Contains ||
                rel.Kind == OntologyConstants.Relationships.Declares ||
                rel.Kind == OntologyConstants.Relationships.HasMethod ||
                rel.Kind == OntologyConstants.Relationships.HasMember)
            {
                if (fileToProject.TryGetValue(rel.From, out var proj) || nodeToProject.TryGetValue(rel.From, out proj))
                {
                    nodeToProject.TryAdd(rel.To, proj);
                }
            }
        }

        ProjectNode? ResolveOwningProject(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            if (nodeToProject.TryGetValue(nodeId, out var p)) return p;
            if (fileToProject.TryGetValue(nodeId, out p)) return p;

            if (nodeId.EndsWith("project_semantic"))
            {
                var stripped = nodeId.Replace(":project_semantic", ":").Replace("project_semantic", "");
                if (nodeToProject.TryGetValue(stripped, out p)) return p;
            }

            return FindOwningProjectForId(nodeId, projList, fileToProject);
        }

        foreach (var rel in allRelationships)
        {
            var kind = rel.Kind;
            var from = rel.From;
            var to = rel.To;

            // 1. Direct Project -> Database
            if (kind == OntologyConstants.Relationships.UsesDb)
            {
                var isTargetDb = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Database) ||
                                 to.Contains($":{OntologyConstants.IdPrefixes.Database}:") || to.Contains(":db:") || to.Contains(":database:") || to.Contains(":res:db:");
                if (isTargetDb)
                {
                    var targetDbId = dbCanonicalMap?.GetValueOrDefault(to, to) ?? to;
                    var owner = ResolveOwningProject(from);
                    if (owner != null && owner.Id != targetDbId)
                    {
                        if (existingEdges.Add((owner.Id, targetDbId, OntologyConstants.Relationships.UsesDb)))
                        {
                            materializedRels.Add(new Relationship(
                                owner.Id,
                                targetDbId,
                                OntologyConstants.Relationships.UsesDb,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "database",
                                    ["is_semantic"] = "true",
                                    ["is_canonical"] = "true"
                                }
                            ));
                        }
                    }
                }
            }
            // 2. Direct Project -> Topic (PUBLISHES_TO / PUBLISHED_BY)
            else if (kind == OntologyConstants.Relationships.PublishesTo ||
                     kind == OntologyConstants.Relationships.PublishedBy)
            {
                var isTargetTopic = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Topic) ||
                                    to.Contains($":{OntologyConstants.IdPrefixes.Topic}:") || to.Contains(":topic:") || to.Contains(":res:topic:");
                var isSourceTopic = (nodeKindsById?.GetValueOrDefault(from) == OntologyConstants.NodeLabels.Topic) ||
                                    from.Contains($":{OntologyConstants.IdPrefixes.Topic}:") || from.Contains(":topic:") || from.Contains(":res:topic:");

                var topicId = isTargetTopic ? to : isSourceTopic ? from : null;
                var symbolId = isTargetTopic ? from : isSourceTopic ? to : null;

                if (topicId != null && symbolId != null)
                {
                    // Internal in-process messages (MediatR / in-memory) remain strictly within services
                    // and are NOT materialized as macro cross-service project dependencies.
                    var isInternal = topicId.Contains(":mediatr:", StringComparison.OrdinalIgnoreCase) ||
                                     topicId.Contains(":in-memory:", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(rel.Properties?.GetValueOrDefault("is_internal")?.ToString(), "true", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(rel.Properties?.GetValueOrDefault("scope")?.ToString(), "internal", StringComparison.OrdinalIgnoreCase);

                    if (!isInternal)
                    {
                        var owner = ResolveOwningProject(symbolId);
                        if (owner != null && owner.Id != topicId)
                        {
                            if (existingEdges.Add((owner.Id, topicId, OntologyConstants.Relationships.PublishesTo)))
                            {
                                materializedRels.Add(new Relationship(
                                    owner.Id,
                                    topicId,
                                    OntologyConstants.Relationships.PublishesTo,
                                    new Dictionary<string, object>
                                    {
                                        ["dependency_type"] = "messaging",
                                        ["is_semantic"] = "true",
                                        ["category"] = "messaging"
                                    }
                                ));
                            }
                        }
                    }
                }
            }
            // 3. Topic -> Project (TRIGGERS / SUBSCRIBES_TO / SUBSCRIBED_BY)
            else if (kind == OntologyConstants.Relationships.Triggers ||
                     kind == OntologyConstants.Relationships.SubscribesTo ||
                     kind == OntologyConstants.Relationships.SubscribedBy)
            {
                var isTargetTopic = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Topic) ||
                                    to.Contains($":{OntologyConstants.IdPrefixes.Topic}:") || to.Contains(":topic:") || to.Contains(":res:topic:");
                var isSourceTopic = (nodeKindsById?.GetValueOrDefault(from) == OntologyConstants.NodeLabels.Topic) ||
                                    from.Contains($":{OntologyConstants.IdPrefixes.Topic}:") || from.Contains(":topic:") || from.Contains(":res:topic:");

                var topicId = isTargetTopic ? to : isSourceTopic ? from : null;
                var symbolId = isTargetTopic ? from : isSourceTopic ? to : null;

                if (topicId != null && symbolId != null)
                {
                    // Internal in-process messages (MediatR / in-memory) remain strictly within services
                    // and are NOT materialized as macro cross-service project dependencies.
                    var isInternal = topicId.Contains(":mediatr:", StringComparison.OrdinalIgnoreCase) ||
                                     topicId.Contains(":in-memory:", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(rel.Properties?.GetValueOrDefault("is_internal")?.ToString(), "true", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(rel.Properties?.GetValueOrDefault("scope")?.ToString(), "internal", StringComparison.OrdinalIgnoreCase);

                    if (!isInternal)
                    {
                        var owner = ResolveOwningProject(symbolId);
                        if (owner != null && owner.Id != topicId)
                        {
                            if (existingEdges.Add((owner.Id, topicId, OntologyConstants.Relationships.SubscribesTo)))
                            {
                                materializedRels.Add(new Relationship(
                                    owner.Id,
                                    topicId,
                                    OntologyConstants.Relationships.SubscribesTo,
                                    new Dictionary<string, object>
                                    {
                                        ["dependency_type"] = "messaging",
                                        ["is_semantic"] = "true",
                                        ["category"] = "messaging"
                                    }
                                ));
                            }
                        }
                    }
                }
            }
            // 5. Calls Endpoint -> Project -> Project SERVICE_CALL
            else if (kind == OntologyConstants.Relationships.CallsEndpoint)
            {
                var callerOwner = ResolveOwningProject(from);
                var targetOwner = ResolveOwningProject(to);
                if (callerOwner != null && targetOwner != null && callerOwner.Id != targetOwner.Id)
                {
                    if (existingEdges.Add((callerOwner.Id, targetOwner.Id, OntologyConstants.Relationships.ServiceCall)))
                    {
                        materializedRels.Add(new Relationship(
                            callerOwner.Id,
                            targetOwner.Id,
                            OntologyConstants.Relationships.ServiceCall,
                            new Dictionary<string, object>
                            {
                                ["dependency_type"] = "service_call",
                                ["is_semantic"] = "true"
                            }
                        ));
                    }
                }
            }
            // 5b. Service Target from configuration -> Project -> Project SERVICE_CALL
            else if (to.Contains(":service_target:"))
            {
                var serviceKey = to.Split(":service_target:", 2)[1];
                var targetProj = FindMatchingServiceProject(serviceKey, projList);
                var callerOwner = ResolveOwningProject(from);
                if (callerOwner != null && targetProj != null && callerOwner.Id != targetProj.Id)
                {
                    if (existingEdges.Add((callerOwner.Id, targetProj.Id, OntologyConstants.Relationships.ServiceCall)))
                    {
                        materializedRels.Add(new Relationship(
                            callerOwner.Id,
                            targetProj.Id,
                            OntologyConstants.Relationships.ServiceCall,
                            new Dictionary<string, object>
                            {
                                ["dependency_type"] = "service_call",
                                ["is_semantic"] = "true",
                                ["service_name"] = serviceKey
                            }
                        ));
                    }
                }
            }
            // 6. External Service invocation -> Project -> ExternalService SERVICE_CALL
            else if (kind == OntologyConstants.Relationships.ServiceCall ||
                     kind == OntologyConstants.Relationships.UsesApi ||
                     kind == OntologyConstants.Relationships.UsesCloud ||
                     kind == OntologyConstants.Relationships.CalledBy ||
                     kind == "CALLED_BY" ||
                     kind == OntologyConstants.Relationships.Calls ||
                     kind == OntologyConstants.Relationships.TransitivelyCalls)
            {
                var isTargetExt = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.ExternalService) ||
                                  to.Contains($":{OntologyConstants.IdPrefixes.ExternalService}:") || to.Contains(":externalservice:") || to.Contains(":res:service:external:") || to.Contains(":cloud:") || to.Contains($":{OntologyConstants.IdPrefixes.CloudService}:");
                var isSourceExt = (nodeKindsById?.GetValueOrDefault(from) == OntologyConstants.NodeLabels.ExternalService) ||
                                  from.Contains($":{OntologyConstants.IdPrefixes.ExternalService}:") || from.Contains(":externalservice:") || from.Contains(":res:service:external:") || from.Contains(":cloud:") || from.Contains($":{OntologyConstants.IdPrefixes.CloudService}:");

                string? callerId = null;
                string? extId = null;

                if (isTargetExt)
                {
                    callerId = from;
                    extId = to;
                }
                else if (isSourceExt && (kind == OntologyConstants.Relationships.CalledBy || kind == "CALLED_BY"))
                {
                    callerId = to;
                    extId = from;
                }

                if (callerId != null && extId != null)
                {
                    var callerOwner = ResolveOwningProject(callerId);
                    if (callerOwner != null && callerOwner.Id != extId)
                    {
                        var extDomain = ExtractDomainFromExternalServiceId(extId);
                        var internalTargetProj = FindMatchingServiceProject(extDomain, projList);

                        if (internalTargetProj != null && callerOwner.Id != internalTargetProj.Id)
                        {
                            if (existingEdges.Add((callerOwner.Id, internalTargetProj.Id, OntologyConstants.Relationships.ServiceCall)))
                            {
                                materializedRels.Add(new Relationship(
                                    callerOwner.Id,
                                    internalTargetProj.Id,
                                    OntologyConstants.Relationships.ServiceCall,
                                    new Dictionary<string, object>
                                    {
                                        ["dependency_type"] = "service_call",
                                        ["is_semantic"] = "true"
                                    }
                                ));
                            }
                        }
                        else if (internalTargetProj == null && !IsGarbageExternalService(extDomain))
                        {
                            if (existingEdges.Add((callerOwner.Id, extId, OntologyConstants.Relationships.ServiceCall)))
                            {
                                materializedRels.Add(new Relationship(
                                    callerOwner.Id,
                                    extId,
                                    OntologyConstants.Relationships.ServiceCall,
                                    new Dictionary<string, object>
                                    {
                                        ["dependency_type"] = "service_call",
                                        ["is_semantic"] = "true"
                                    }
                                ));
                            }
                        }
                    }
                }
            }
            // 7. Configures -> roll up DB / Topic configuration to Project
            else if (kind == OntologyConstants.Relationships.Configures)
            {
                var isTargetDb = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Database) ||
                                 to.Contains($":{OntologyConstants.IdPrefixes.Database}:") || to.Contains(":db:") || to.Contains(":database:");
                var isTargetTopic = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Topic) ||
                                    to.Contains($":{OntologyConstants.IdPrefixes.Topic}:") || to.Contains(":topic:");

                var owner = ResolveOwningProject(from);
                if (owner != null && isTargetDb)
                {
                    var targetDbId = dbCanonicalMap?.GetValueOrDefault(to, to) ?? to;
                    if (owner.Id != targetDbId && existingEdges.Add((owner.Id, targetDbId, OntologyConstants.Relationships.UsesDb)))
                    {
                        materializedRels.Add(new Relationship(
                            owner.Id,
                            targetDbId,
                            OntologyConstants.Relationships.UsesDb,
                            new Dictionary<string, object>
                            {
                                ["dependency_type"] = "database",
                                ["is_semantic"] = "true",
                                ["is_canonical"] = "true"
                            }
                        ));
                    }
                }
                else if (owner != null && isTargetTopic && owner.Id != to)
                {
                    if (existingEdges.Add((owner.Id, to, OntologyConstants.Relationships.PublishesTo)))
                    {
                        materializedRels.Add(new Relationship(
                            owner.Id,
                            to,
                            OntologyConstants.Relationships.PublishesTo,
                            new Dictionary<string, object>
                            {
                                ["dependency_type"] = "messaging",
                                ["is_semantic"] = "true"
                            }
                        ));
                    }
                }
            }
        }

        return materializedRels;
    }

    public static ProjectNode? FindOwningProjectForId(
        string nodeId,
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyDictionary<string, ProjectNode>? fileToProjectMap = null)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;

        if (Urn.IsExternalOrPackageId(nodeId)) return null;

        var directProj = projects.FirstOrDefault(p => p.Id == nodeId || p.Id.TrimEnd(':') == nodeId.TrimEnd(':'));
        if (directProj != null) return directProj;

        if (fileToProjectMap != null && fileToProjectMap.TryGetValue(nodeId, out var proj))
        {
            return proj;
        }

        string pathPart = nodeId;
        bool isKnownFileOrSymbolUrn = false;
        if (Urn.TryParse(nodeId, out var urn) && !string.IsNullOrEmpty(urn.Path))
        {
            var domain = (urn.Domain ?? "").ToLowerInvariant();
            isKnownFileOrSymbolUrn = domain is "file" or "f" or "folder" or "dir" or
                                               "symbol" or "sym" or "type" or "function" or "fn" or "method" or "m" or
                                               "endpoint" or "ep" or "entrypoint" or "entry" or
                                               "project" or "p";
            if (!isKnownFileOrSymbolUrn)
            {
                return null;
            }
            pathPart = urn.Path;
        }
        else
        {
            var fileIdx = nodeId.IndexOf($":{OntologyConstants.IdPrefixes.File}:", StringComparison.OrdinalIgnoreCase);
            if (fileIdx < 0) fileIdx = nodeId.IndexOf(":file:", StringComparison.OrdinalIgnoreCase);
            if (fileIdx >= 0)
            {
                isKnownFileOrSymbolUrn = true;
                var markerLen = nodeId.IndexOf($":{OntologyConstants.IdPrefixes.File}:", StringComparison.OrdinalIgnoreCase) >= 0
                    ? $":{OntologyConstants.IdPrefixes.File}:".Length
                    : ":file:".Length;
                pathPart = nodeId[(fileIdx + markerLen)..];
            }
            else
            {
                var symIdx = nodeId.IndexOf($":{OntologyConstants.IdPrefixes.Symbol}:", StringComparison.OrdinalIgnoreCase);
                if (symIdx < 0) symIdx = nodeId.IndexOf(":symbol:", StringComparison.OrdinalIgnoreCase);
                if (symIdx >= 0)
                {
                    isKnownFileOrSymbolUrn = true;
                    var markerLen = nodeId.IndexOf($":{OntologyConstants.IdPrefixes.Symbol}:", StringComparison.OrdinalIgnoreCase) >= 0
                        ? $":{OntologyConstants.IdPrefixes.Symbol}:".Length
                        : ":symbol:".Length;
                    pathPart = nodeId[(symIdx + markerLen)..];
                }
                else
                {
                    var projIdx = nodeId.IndexOf($":{OntologyConstants.IdPrefixes.Project}:", StringComparison.OrdinalIgnoreCase);
                    if (projIdx < 0) projIdx = nodeId.IndexOf(":project:", StringComparison.OrdinalIgnoreCase);
                    if (projIdx >= 0)
                    {
                        isKnownFileOrSymbolUrn = true;
                        var markerLen = nodeId.IndexOf($":{OntologyConstants.IdPrefixes.Project}:", StringComparison.OrdinalIgnoreCase) >= 0
                            ? $":{OntologyConstants.IdPrefixes.Project}:".Length
                            : ":project:".Length;
                        pathPart = nodeId[(projIdx + markerLen)..];
                    }
                }
            }
        }

        pathPart = pathPart.TrimEnd(':').Replace('\\', '/');

        ProjectNode? bestMatch = null;
        int bestLen = -1;
        ProjectNode? rootProject = null;

        foreach (var p in projects)
        {
            var pPath = (p.Path ?? "").Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(pPath))
            {
                rootProject ??= p;
                continue;
            }
            if (pathPart.StartsWith(pPath, StringComparison.OrdinalIgnoreCase))
            {
                if (pathPart.Length == pPath.Length || pathPart[pPath.Length] == '/' || pathPart[pPath.Length] == ':')
                {
                    if (pPath.Length > bestLen)
                    {
                        bestMatch = p;
                        bestLen = pPath.Length;
                    }
                }
            }
        }

        if (bestMatch != null) return bestMatch;

        if (rootProject != null && isKnownFileOrSymbolUrn && !pathPart.Contains(':'))
        {
            return rootProject;
        }

        return null;
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

            projectExternalApis[projId] = [.. domains.OrderBy(x => x)];
        }

        return new PostIndexAnalysisResult([.. transitivelyCalls.DistinctBy(x => (x.From, x.To))],
            [.. attributedTo.DistinctBy(x => (x.EpId, x.SinkId))],
            projectExternalApis);
    }

    private Task WriteTransitivelyCallsAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH path = (caller:Function)-[:CALLS|CALLS_ENDPOINT|USES_DB*1..15]->(sink)
        WHERE caller.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Database OR sink:Query)
        WITH caller, sink, min(length(path)) AS hops
        MERGE (caller)-[r:TRANSITIVELY_CALLS]->(sink)
        SET r.hops = hops
        """, new { widPrefix });

    private Task WriteAttributedToAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH path = (ep)-[:IMPLEMENTS|IMPLEMENTED_BY|EXPOSED_BY]-(fn:Function)-[:CALLS|CALLS_ENDPOINT|USES_DB*0..15]->(sink)
        WHERE (ep:EntryPoint OR ep:Endpoint) AND ep.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Database OR sink:Query)
        WITH ep, sink, min(length(path)) AS hops, labels(sink)[0] AS sinkKind
        MERGE (ep)-[r:ATTRIBUTED_TO]->(sink)
        SET r.hops = hops, r.sink_kind = sinkKind
        """, new { widPrefix });

    private Task WriteProjectApiAnnotationsAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH (p:Project)-[:CONTAINS|EXPOSES*1..2]->(ep)-[:ATTRIBUTED_TO]->(es:ExternalService)
        WHERE (ep:EntryPoint OR ep:Endpoint) AND p.id STARTS WITH $widPrefix
        WITH p, collect(DISTINCT es.domain_or_service) AS domains
        SET p.external_apis = domains
        """, new { widPrefix });
}
