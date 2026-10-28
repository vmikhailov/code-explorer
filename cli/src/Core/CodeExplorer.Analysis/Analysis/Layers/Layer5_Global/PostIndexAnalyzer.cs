using System.Text.Json;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;



public record CanonicalizeDatabasesResult(int CanonicalNodesCount, int MaterializedRelationshipsCount);

public partial class PostIndexAnalyzer(IGraphClient db)
{
    public async Task RunAsync(string workspaceId, ParsingContext? ctx = null)
    {
        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");

        ctx?.Log("[PostIndex  ] Canonicalizing databases across workspace...");
        await CanonicalizeDatabasesAsync(db, widPrefix);
        ctx?.Log("[PostIndex  ] Normalizing architectural edges...");
        await NormalizeEdgesAsync(db, widPrefix);
        ctx?.LogDebug("[PostIndex  ] Purging phantom external services...");
        await PurgePhantomExternalServicesAsync(db, widPrefix);
        ctx?.LogDebug("[PostIndex  ] Purging placeholder topics...");
        await PurgePlaceholderTopicsAsync(db, widPrefix);
        ctx?.LogDebug("[PostIndex  ] Deduplicating project nodes...");
        await DeduplicateProjectNodesAsync(db, widPrefix);

        ctx?.LogDebug("[PostIndex  ] Loading post-index graph reachability data...");
        var graphData = await db.LoadPostIndexGraphDataAsync(widPrefix);
        if (graphData.CallsAdjacency.Count > 0 || graphData.Sinks.Count > 0 || graphData.Callers.Count > 0)
        {
            ctx?.LogDebug("[PostIndex  ] Analyzing call paths and external API dependencies...");
            var result = Analyze(graphData, widPrefix);
            ctx?.LogDebug("[PostIndex  ] Saving reachability analysis results to database...");
            await db.SavePostIndexResultsAsync(widPrefix, result);
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
                list = [];
                callsAdjacency[from] = list;
            }
            list.Add(to);
        }

        void AddRel(Relationship rel)
        {
            if (rel.Kind == OntologyConstants.Relationships.Calls ||
                rel.Kind == OntologyConstants.Relationships.CallsEndpoint ||
                rel.Kind == OntologyConstants.Relationships.UsesDb)
            {
                AddCall(rel.From, rel.To);
            }
            else if (rel.Kind == OntologyConstants.Relationships.CalledBy ||
                     rel.Kind == OntologyConstants.Relationships.QueriedBy ||
                     rel.Kind == OntologyConstants.Relationships.AccessedBy)
            {
                AddCall(rel.To, rel.From);
            }
        }

        foreach (var rel in referenceRelationships) AddRel(rel);
        foreach (var rel in lateBoundRels) AddRel(rel);
        foreach (var rel in ctx.TreeRelationships) AddRel(rel);
        foreach (var rel in ctx.GlobalProjectDependencies) AddRel(rel);

        var sinks = new Dictionary<string, string>();
        var sinkDomains = new Dictionary<string, string?>();
        var entryPointIds = new HashSet<string>();
        var functionIds = new HashSet<string>();

        void CollectNodes(IOntologyNode node)
        {
            if (node is ExternalServiceNode es)
            {
                sinks[es.Id] = "ExternalService";
                sinkDomains[es.Id] = es.DomainOrService;
            }
            else if (node is DatabaseNode db)
            {
                sinks[db.Id] = "Database";
                sinkDomains[db.Id] = db.Name;
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
            else if (node is FunctionNode fn)
            {
                functionIds.Add(fn.Id);
            }

            foreach (var child in node.Children)
            {
                CollectNodes(child);
            }
        }

        var root = l4Result.Prev.Prev.Prev.Workspace;
        CollectNodes(root);
        if (l4Result.SemanticStructure != null)
        {
            CollectNodes(l4Result.SemanticStructure);
        }

        foreach (var (key, id) in ctx.GlobalSymbols)
        {
            if (key.Kind == OntologyConstants.NodeLabels.EntryPoint || key.Kind == OntologyConstants.NodeLabels.Endpoint)
            {
                entryPointIds.Add(id);
            }
        }

        var implements = new Dictionary<string, List<string>>();
        void AddImplements(string epId, string fnId)
        {
            if (string.IsNullOrEmpty(epId) || string.IsNullOrEmpty(fnId)) return;
            if (!implements.TryGetValue(epId, out var list))
            {
                list = [];
                implements[epId] = list;
            }
            if (!list.Contains(fnId))
            {
                list.Add(fnId);
            }
        }

        void ProcessImplementsRel(Relationship rel)
        {
            if (rel.Kind == OntologyConstants.Relationships.ImplementedBy ||
                rel.Kind == OntologyConstants.Relationships.ExposedBy)
            {
                if (entryPointIds.Contains(rel.From))
                {
                    AddImplements(rel.From, rel.To);
                }
            }
            else if (rel.Kind == OntologyConstants.Relationships.Implements)
            {
                if (entryPointIds.Contains(rel.To))
                {
                    AddImplements(rel.To, rel.From);
                }
            }
        }

        foreach (var rel in ctx.GlobalProjectDependencies) ProcessImplementsRel(rel);
        foreach (var rel in referenceRelationships) ProcessImplementsRel(rel);
        foreach (var rel in ctx.TreeRelationships) ProcessImplementsRel(rel);

        var projectToEntryPoints = new Dictionary<string, List<string>>();
        foreach (var project in l4Result.Prev.Prev.Projects)
        {
            var epList = new List<string>();
            foreach (var child in project.Children)
            {
                if (child is EntryPointNode ep && !epList.Contains(ep.Id)) epList.Add(ep.Id);
                else if (child is EndpointNode endp && !epList.Contains(endp.Id)) epList.Add(endp.Id);
            }
            projectToEntryPoints[project.Id] = epList;
        }
        if (l4Result.SemanticStructure != null)
        {
            foreach (var workloadNode in l4Result.SemanticStructure.Children)
            {
                var projId = workloadNode.Extensions?.GetValueOrDefault("project_id");
                if (!string.IsNullOrEmpty(projId) && projectToEntryPoints.TryGetValue(projId, out var epList))
                {
                    foreach (var child in workloadNode.Children)
                    {
                        if (child is EntryPointNode ep && !epList.Contains(ep.Id)) epList.Add(ep.Id);
                        else if (child is EndpointNode endp && !epList.Contains(endp.Id)) epList.Add(endp.Id);
                    }
                }
            }
        }

        var callersSet = new HashSet<string>(callsAdjacency.Keys.Where(functionIds.Contains));
        foreach (var fnList in implements.Values)
        {
            foreach (var fnId in fnList.Where(functionIds.Contains))
            {
                callersSet.Add(fnId);
            }
        }

        var widPrefix = string.IsNullOrEmpty(ctx.WorkspaceId) ? "" : (ctx.WorkspaceId.EndsWith(':') ? ctx.WorkspaceId : ctx.WorkspaceId + ":");
        var graphData = new PostIndexGraphData(callsAdjacency, sinks, sinkDomains, [.. callersSet], implements, [.. entryPointIds], projectToEntryPoints);
        var rawResult = Analyze(graphData, widPrefix);
        var result = rawResult with
        {
            TransitivelyCalls = [.. rawResult.TransitivelyCalls.Select(tc => (From: tc.To, To: tc.From, Hops: tc.Hops))]
        };

        // Update project nodes in memory
        foreach (var (projId, domains) in result.ProjectExternalApis)
        {
            var proj = l4Result.Prev.Prev.Projects.FirstOrDefault(p => p.Id == projId);
            if (proj != null && proj.Extensions != null)
            {
                proj.Extensions["external_apis"] = JsonSerializer.Serialize(domains);
            }
        }

        await db.SavePostIndexResultsAsync(widPrefix, result);

        // Materialize direct architectural relationships (C4 Macro-Edges)
        var allCombinedRels = new List<Relationship>();
        allCombinedRels.AddRange(ctx.GlobalProjectDependencies);
        allCombinedRels.AddRange(referenceRelationships);
        allCombinedRels.AddRange(lateBoundRels);
        allCombinedRels.AddRange(ctx.TreeRelationships);

        ctx.LogDebug("[PostIndex  ] Building file-to-project map...");
        var fileToProject = Layer2ProjectParser.BuildFileToProjectMap(
            l4Result.Prev.Prev.Prev.Files,
            l4Result.Prev.Prev.Projects
        );

        // Canonicalize databases and link projects in graph
        ctx.Log("[PostIndex  ] Canonicalizing databases and linking projects...");
        var (canonicalDbRels, rawToCanonicalDbMap) = await CanonicalizeDatabasesInMemoryAsync(
            ctx,
            l4Result,
            allCombinedRels,
            fileToProject
        );
        allCombinedRels.AddRange(canonicalDbRels);

        var projectToWorkloadMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var workloadToProjectMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (l4Result.SemanticRelationships != null)
        {
            foreach (var rel in l4Result.SemanticRelationships)
            {
                if (rel.Kind == OntologyConstants.Relationships.Deploys)
                {
                    projectToWorkloadMap[rel.From] = rel.To;
                    projectToWorkloadMap[rel.From.TrimEnd(':')] = rel.To;
                    projectToWorkloadMap[rel.From.TrimEnd(':') + ":"] = rel.To;
                    workloadToProjectMap[rel.To] = rel.From.TrimEnd(':');
                }
                else if (rel.Kind == OntologyConstants.Relationships.ImplementedBy &&
                         (rel.To.Contains(":p:") || rel.To.Contains(":project:")))
                {
                    projectToWorkloadMap[rel.To] = rel.From;
                    projectToWorkloadMap[rel.To.TrimEnd(':')] = rel.From;
                    projectToWorkloadMap[rel.To.TrimEnd(':') + ":"] = rel.From;
                    workloadToProjectMap[rel.From] = rel.To.TrimEnd(':');
                }
            }
        }

        ctx.LogDebug("[PostIndex  ] Materializing direct project relationships...");
        var directProjectRels = MaterializeDirectProjectRelationships(
            l4Result.Prev.Prev.Projects,
            l4Result.Prev.Prev.Prev.Files,
            allCombinedRels,
            dbCanonicalMap: rawToCanonicalDbMap,
            prebuiltFileToProject: fileToProject,
            projectToWorkloadMap: projectToWorkloadMap
        );

        if (directProjectRels.Count > 0)
        {
            ctx.LogDebug($"[PostIndex  ] Uploading {directProjectRels.Count} direct project relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(directProjectRels);
            ctx.AddRelsCount(directProjectRels.Count);
            allCombinedRels.AddRange(directProjectRels);
        }

        ctx.LogDebug("[PostIndex  ] Materializing transitive semantic relationships...");
        var liftedSemanticRels = LiftTransitiveSemanticRelations(
            l4Result.Prev.Prev.Projects,
            allCombinedRels,
            dbCanonicalMap: rawToCanonicalDbMap,
            projectToWorkloadMap: projectToWorkloadMap
        );
        if (liftedSemanticRels.Count > 0)
        {
            ctx.LogDebug($"[PostIndex  ] Uploading {liftedSemanticRels.Count} transitive semantic relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(liftedSemanticRels);
            ctx.AddRelsCount(liftedSemanticRels.Count);
        }

        // Classify projects and persist layer metadata into SQLite nodes
        ctx.Log($"[PostIndex  ] Classifying architectural layers for {l4Result.Prev.Prev.Projects.Count} projects...");
        var classifierItems = l4Result.Prev.Prev.Projects.Select(p =>
        {
            var allChildren = new List<IOntologyNode>();
            void Collect(IOntologyNode parent)
            {
                foreach (var c in parent.Children)
                {
                    allChildren.Add(c);
                    Collect(c);
                }
            }
            Collect(p);
            var wlId = projectToWorkloadMap.GetValueOrDefault(p.Id);
            if (l4Result.SemanticStructure != null)
            {
                foreach (var wl in l4Result.SemanticStructure.Children)
                {
                    if ((wlId != null && wl.Id == wlId) || wl.Extensions?.GetValueOrDefault("project_id") == p.Id)
                    {
                        Collect(wl);
                    }
                }
            }

            var endpointsCount = allChildren.OfType<EndpointNode>().Count();
            var entryPoints = allChildren.OfType<EntryPointNode>().ToList();
            var externalServicesCount = allChildren.OfType<ExternalServiceNode>().Count();
            var usesDbCount = allCombinedRels.Count(r => (r.From == p.Id || (wlId != null && r.From == wlId)) && r.Kind == OntologyConstants.Relationships.UsesDb);

            return new ProjectClassifierItem
            {
                Id = p.Id,
                Name = p.Name,
                FilePath = p.Path,
                Framework = p.Extensions?.GetValueOrDefault("framework") ?? p.ProjectType,
                Role = p.Role,
                IsLibrary = p.IsLibrary,
                EndpointsCount = endpointsCount,
                EntryPoints = entryPoints,
                ExternalServicesCount = externalServicesCount,
                UsesDbCount = usesDbCount,
                Extensions = p.Extensions
            };
        });

        var dependencyItems = allCombinedRels
            .Where(r => r.Kind == OntologyConstants.Relationships.DependsOn ||
                        r.Kind == OntologyConstants.Relationships.ServiceCall ||
                        r.Kind == OntologyConstants.Relationships.UsesDb)
            .Select(r => new DependencyItem
            {
                SourceId = workloadToProjectMap.GetValueOrDefault(r.From, r.From),
                TargetId = workloadToProjectMap.GetValueOrDefault(r.To, r.To)
            });

        var layerMap = ProjectLayerClassifier.Classify(classifierItems, dependencyItems);

        var updatedProjectNodes = new List<Node>();
        foreach (var p in l4Result.Prev.Prev.Projects)
        {
            p.Extensions ??= [];
            if (layerMap.TryGetValue(p.Id, out var layerInfo))
            {
                p.Extensions["layer"] = layerInfo.LayerId;
                p.Extensions["layerId"] = layerInfo.LayerId;
                p.Extensions["layerName"] = layerInfo.LayerName;
                p.Extensions["layerOrder"] = layerInfo.Order.ToString();
                p.Extensions["layerColor"] = layerInfo.Color;
                p.Extensions["layerIcon"] = layerInfo.Icon;
            }
            p.Extensions["role"] = p.Role;
            p.Extensions["is_library"] = p.IsLibrary ? "true" : "false";
            p.Extensions["entity_type"] = p.IsLibrary ? "library" : "service";
            p.Extensions["is_semantic_entity"] = p.IsLibrary ? "false" : "true";
            p.Extensions["entity_kind"] = p.EntityKind.ToString();
            p.Extensions["sub_kind"] = p.SubKind.ToString();
            p.Extensions["kind"] = p.EntityKind != ProjectEntityKind.Unknown ? p.EntityKind.ToString() : p.Role;
            p.Extensions["language"] = p.ProjectType;
            p.Extensions["project_type"] = p.ProjectType;

            if (!p.Extensions.ContainsKey("primary_role") || p.Extensions["primary_role"] == "General")
            {
                p.Extensions["primary_role"] = p.Role;
            }
            if (!p.Extensions.ContainsKey("secondary_roles"))
            {
                p.Extensions["secondary_roles"] = "[]";
            }
            if (!p.Extensions.ContainsKey("all_roles") || p.Extensions["all_roles"] == "[]")
            {
                p.Extensions["all_roles"] = JsonSerializer.Serialize(new[] { p.Extensions["primary_role"] });
            }

            updatedProjectNodes.Add(Node.FromNode(p));
        }

        if (l4Result.SemanticStructure != null)
        {
            foreach (var workloadNode in l4Result.SemanticStructure.Children)
            {
                if (workloadNode is CompositeNode compNode)
                {
                    compNode.Extensions ??= [];
                    var projId = compNode.Extensions.GetValueOrDefault("project_id");
                    if (projId != null && layerMap.TryGetValue(projId, out var layerInfo))
                    {
                        compNode.Extensions["layer"] = layerInfo.LayerId;
                        compNode.Extensions["layerId"] = layerInfo.LayerId;
                        compNode.Extensions["layerName"] = layerInfo.LayerName;
                        compNode.Extensions["layerOrder"] = layerInfo.Order.ToString();
                        compNode.Extensions["layerColor"] = layerInfo.Color;
                        compNode.Extensions["layerIcon"] = layerInfo.Icon;
                        updatedProjectNodes.Add(Node.FromNode(compNode));
                    }
                }
            }
        }

        if (updatedProjectNodes.Count > 0)
        {
            ctx.LogDebug($"[PostIndex  ] Uploading {updatedProjectNodes.Count} classified project nodes...");
            await ctx.DbClient.UploadNodesAsync(updatedProjectNodes);
        }

        // Normalize edges in SQLite graph database
        ctx.Log("[PostIndex  ] Normalizing architectural edges and cleaning database...");
        await NormalizeEdgesAsync(ctx.DbClient, widPrefix, ctx.CancellationToken);
        await PurgePhantomExternalServicesAsync(ctx.DbClient, widPrefix, ctx.CancellationToken);
        await PurgePlaceholderTopicsAsync(ctx.DbClient, widPrefix, ctx.CancellationToken);
        await DeduplicateProjectNodesAsync(ctx.DbClient, widPrefix, ctx.CancellationToken);

    }

    public static List<Relationship> LiftTransitiveSemanticRelations(
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<Relationship> allRelationships,
        IReadOnlyDictionary<string, string>? nodeKindsById = null,
        IReadOnlyDictionary<string, string>? dbCanonicalMap = null,
        IReadOnlyDictionary<string, string>? projectToWorkloadMap = null)
    {
        var liftedRels = new List<Relationship>();
        var services = projects.Where(p => !p.IsLibrary).ToList();
        var libraries = projects.Where(p => p.IsLibrary).ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

        // Build adjacency
        var outEdges = new Dictionary<string, List<Relationship>>(StringComparer.OrdinalIgnoreCase);
        var inEdges = new Dictionary<string, List<Relationship>>(StringComparer.OrdinalIgnoreCase);

        foreach (var rel in allRelationships)
        {
            if (!outEdges.TryGetValue(rel.From, out var outList))
            {
                outList = [];
                outEdges[rel.From] = outList;
            }
            outList.Add(rel);

            if (!inEdges.TryGetValue(rel.To, out var inList))
            {
                inList = [];
                inEdges[rel.To] = inList;
            }
            inList.Add(rel);
        }

        var existingEdges = new HashSet<(string From, string To, string Kind)>(
            allRelationships.Select(r => (r.From, r.To, r.Kind))
        );

        var topicEvidenceFiles = BuildTopicEvidenceFileIndex(allRelationships);

        var workloadToProject = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (projectToWorkloadMap != null)
        {
            foreach (var (k, v) in projectToWorkloadMap)
            {
                if (!k.EndsWith(':')) workloadToProject[v] = k;
            }
        }

        void AddLifted(string fromId, string toId, string relKind, Dictionary<string, object> properties)
        {
            if (projectToWorkloadMap != null && projectToWorkloadMap.TryGetValue(fromId, out var semFrom) && semFrom != null)
            {
                var semTo = projectToWorkloadMap.GetValueOrDefault(toId, toId);
                if (semFrom != semTo && existingEdges.Add((semFrom, semTo, relKind)))
                {
                    var semProps = new Dictionary<string, object>(properties)
                    {
                        ["is_semantic"] = "true"
                    };
                    liftedRels.Add(new Relationship(semFrom, semTo, relKind, semProps));
                }
                return;
            }

            if (existingEdges.Add((fromId, toId, relKind)))
            {
                liftedRels.Add(new Relationship(fromId, toId, relKind, properties));
            }
        }

        foreach (var service in services)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { service.Id };
            var queue = new Queue<(string CurrentId, int Depth, List<string> Chain)>();
            var sourceReachability = ParseSourceReachability(service);

            bool IsTopicReachable(string libraryId, string topicId, bool isSubscription) =>
                IsTopicEvidenceReachable(sourceReachability, libraries.GetValueOrDefault(libraryId), topicEvidenceFiles, topicId, isSubscription);

            var initialEdgeList = new List<Relationship>();
            if (outEdges.TryGetValue(service.Id, out var initialProjEdges)) initialEdgeList.AddRange(initialProjEdges);
            var serviceWlId = projectToWorkloadMap?.GetValueOrDefault(service.Id);
            if (serviceWlId != null && outEdges.TryGetValue(serviceWlId, out var initialWlEdges)) initialEdgeList.AddRange(initialWlEdges);

            foreach (var edge in initialEdgeList)
            {
                var targetProjId = workloadToProject.GetValueOrDefault(edge.To, edge.To);
                if (libraries.TryGetValue(targetProjId, out var libNode))
                {
                    if (visited.Add(libNode.Id))
                    {
                        queue.Enqueue((libNode.Id, 1, [libNode.Name]));
                    }
                }
            }

            while (queue.Count > 0)
            {
                var (currId, depth, chain) = queue.Dequeue();
                var viaLib = chain[0];
                var chainStr = string.Join(" -> ", chain);
                var currWlId = projectToWorkloadMap?.GetValueOrDefault(currId);

                var incomingToLib = new List<Relationship>();
                if (inEdges.TryGetValue(currId, out var inProj)) incomingToLib.AddRange(inProj);
                if (currWlId != null && inEdges.TryGetValue(currWlId, out var inWl)) incomingToLib.AddRange(inWl);

                // Inbound Case: Message Broker / Topic subscribes into Library -> Lift to Service
                if (incomingToLib.Count > 0)
                {
                    foreach (var inEdge in incomingToLib)
                    {
                        var srcKind = nodeKindsById?.GetValueOrDefault(inEdge.From);
                        var isTopic = (srcKind != null && srcKind.Equals(OntologyConstants.NodeLabels.Topic, StringComparison.OrdinalIgnoreCase))
                                      || inEdge.From.Contains($":{OntologyConstants.IdPrefixes.Topic}:")
                                      || inEdge.From.Contains(":topic:")
                                      || inEdge.Kind == OntologyConstants.Relationships.Triggers
                                      || inEdge.Kind == OntologyConstants.Relationships.SubscribesTo;

                        if (isTopic && IsTopicReachable(currId, inEdge.From, isSubscription: true))
                        {
                            AddLifted(
                                service.Id,
                                inEdge.From,
                                OntologyConstants.Relationships.SubscribesTo,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "messaging",
                                    ["is_semantic"] = "true",
                                    ["semantic_lifted"] = "true",
                                    ["via_library"] = viaLib,
                                    ["call_chain"] = chainStr
                                }
                            );
                        }
                    }
                }

                var edges = new List<Relationship>();
                if (outEdges.TryGetValue(currId, out var outProj)) edges.AddRange(outProj);
                if (currWlId != null && outEdges.TryGetValue(currWlId, out var outWl)) edges.AddRange(outWl);

                if (edges.Count > 0)
                {
                    foreach (var edge in edges)
                    {
                        var tgtKind = nodeKindsById?.GetValueOrDefault(edge.To);
                        var targetProjId = workloadToProject.GetValueOrDefault(edge.To, edge.To);

                        // Case 1: Library connects to Database -> Lift direct USES_DB to Service
                        var isDb = (tgtKind != null && tgtKind.Equals(OntologyConstants.NodeLabels.Database, StringComparison.OrdinalIgnoreCase))
                                   || edge.To.Contains(":db:")
                                   || edge.To.Contains(":res:db:")
                                   || edge.To.Contains(":database:")
                                   || edge.Kind == OntologyConstants.Relationships.UsesDb;

                        if (isDb)
                        {
                            if (dbCanonicalMap != null)
                            {
                                continue;
                            }
                            var targetDbId = dbCanonicalMap?.GetValueOrDefault(edge.To, edge.To) ?? edge.To;
                            AddLifted(
                                service.Id,
                                targetDbId,
                                OntologyConstants.Relationships.UsesDb,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "database",
                                    ["is_semantic"] = "true",
                                    ["semantic_lifted"] = "true",
                                    ["is_canonical"] = "true",
                                    ["via_library"] = viaLib,
                                    ["call_chain"] = chainStr
                                }
                            );
                        }
                        // Case 2: Library calls another Service -> Lift direct SERVICE_CALL to Service
                        // Only lift if the library's edge to the target service is an actual service call (not a generic compile-time project dependency)
                        else if (projects.Any(p => p.Id == targetProjId && !p.IsLibrary && p.Id != service.Id) &&
                                 (edge.Kind is OntologyConstants.Relationships.ServiceCall or "CALLS_ENDPOINT" or "CALLS" ||
                                  (edge.Properties?.TryGetValue("dependency_type", out var dt) == true && dt?.ToString() == "service_call")))
                        {
                            AddLifted(
                                service.Id,
                                targetProjId,
                                OntologyConstants.Relationships.ServiceCall,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "service_call",
                                    ["is_semantic"] = "true",
                                    ["semantic_lifted"] = "true",
                                    ["via_library"] = viaLib,
                                    ["call_chain"] = chainStr
                                }
                            );
                        }
                        // Case 3: Library connects to External Service
                        else if ((tgtKind != null && tgtKind.Equals(OntologyConstants.NodeLabels.ExternalService, StringComparison.OrdinalIgnoreCase))
                                 || edge.To.Contains($":{OntologyConstants.IdPrefixes.ExternalService}:")
                                 || edge.To.Contains(":externalservice:")
                                 || edge.To.Contains(":res:service:external:"))
                        {
                            AddLifted(
                                service.Id,
                                edge.To,
                                OntologyConstants.Relationships.ServiceCall,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "service_call",
                                    ["is_semantic"] = "true",
                                    ["semantic_lifted"] = "true",
                                    ["via_library"] = viaLib,
                                    ["call_chain"] = chainStr
                                }
                            );
                        }
                        // Case 4: Library publishes or subscribes to Topic
                        else if ((tgtKind != null && tgtKind.Equals(OntologyConstants.NodeLabels.Topic, StringComparison.OrdinalIgnoreCase))
                                 || edge.To.Contains($":{OntologyConstants.IdPrefixes.Topic}:")
                                 || edge.To.Contains(":topic:")
                                 || edge.To.Contains(":res:topic:")
                                 || edge.Kind == OntologyConstants.Relationships.PublishesTo
                                 || edge.Kind == OntologyConstants.Relationships.SubscribesTo
                                 || edge.Kind == OntologyConstants.Relationships.Triggers)
                        {
                            var outKind = (edge.Kind == OntologyConstants.Relationships.SubscribesTo || edge.Kind == OntologyConstants.Relationships.Triggers)
                                ? OntologyConstants.Relationships.SubscribesTo
                                : OntologyConstants.Relationships.PublishesTo;
                            if (!IsTopicReachable(currId, edge.To, outKind == OntologyConstants.Relationships.SubscribesTo))
                            {
                                continue;
                            }
                            AddLifted(
                                service.Id,
                                edge.To,
                                outKind,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "messaging",
                                    ["is_semantic"] = "true",
                                    ["semantic_lifted"] = "true",
                                    ["via_library"] = viaLib,
                                    ["call_chain"] = chainStr
                                }
                            );
                        }
                        // Case 5: Library depends on another Library -> continue BFS traversal up to depth 8
                        else if (libraries.TryGetValue(targetProjId, out var nextLib) && depth < 8)
                        {
                            if (visited.Add(nextLib.Id))
                            {
                                var nextChain = new List<string>(chain) { nextLib.Name };
                                queue.Enqueue((nextLib.Id, depth + 1, nextChain));
                            }
                        }
                    }
                }
            }
        }

        return liftedRels;
    }

    /// <summary>
    /// Indexes, per (topic, direction), the workspace-relative source files whose symbols publish to or subscribe to it.
    /// </summary>
    private static Dictionary<(string TopicId, bool IsSubscription), HashSet<string>> BuildTopicEvidenceFileIndex(
        IReadOnlyList<Relationship> relationships)
    {
        var index = new Dictionary<(string, bool), HashSet<string>>();
        var topicMarker = $":{OntologyConstants.IdPrefixes.Topic}:";

        foreach (var rel in relationships)
        {
            bool isSubscription;
            switch (rel.Kind)
            {
                case OntologyConstants.Relationships.PublishedBy or OntologyConstants.Relationships.PublishesTo:
                    isSubscription = false;
                    break;
                case OntologyConstants.Relationships.SubscribedBy or OntologyConstants.Relationships.SubscribesTo or OntologyConstants.Relationships.Triggers:
                    isSubscription = true;
                    break;
                default:
                    continue;
            }

            var (topicId, otherId) = rel.From.Contains(topicMarker, StringComparison.Ordinal) ? (rel.From, rel.To)
                : rel.To.Contains(topicMarker, StringComparison.Ordinal) ? (rel.To, rel.From)
                : (null, null);
            if (topicId is null || otherId is null || !TryGetSourceFilePath(otherId, out var filePath))
            {
                continue;
            }

            if (!index.TryGetValue((topicId, isSubscription), out var files))
            {
                files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                index[(topicId, isSubscription)] = files;
            }
            files.Add(filePath);
        }

        return index;
    }

    private static bool TryGetSourceFilePath(string nodeId, out string filePath)
    {
        filePath = string.Empty;
        var symbolMarker = $":{OntologyConstants.IdPrefixes.Symbol}:";
        var fileMarker = $":{OntologyConstants.IdPrefixes.File}:";

        var symbolIndex = nodeId.IndexOf(symbolMarker, StringComparison.Ordinal);
        if (symbolIndex >= 0)
        {
            var rest = nodeId[(symbolIndex + symbolMarker.Length)..];
            var end = rest.IndexOf(':');
            filePath = end >= 0 ? rest[..end] : rest;
            return filePath.Length > 0;
        }

        var fileIndex = nodeId.IndexOf(fileMarker, StringComparison.Ordinal);
        if (fileIndex >= 0)
        {
            filePath = nodeId[(fileIndex + fileMarker.Length)..];
            return filePath.Length > 0;
        }

        return false;
    }

    private static Dictionary<string, HashSet<string>>? ParseSourceReachability(ProjectNode project)
    {
        var json = project.Extensions?.GetValueOrDefault(OntologyConstants.PropertyNames.SourceReachability);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
            return parsed?.ToDictionary(
                entry => entry.Key.TrimEnd('/'),
                entry => entry.Value.ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A library topic edge is lifted to a workload only if at least one of the library's source files that
    /// produce/consume the topic is reachable from the workload. Unknown reachability or evidence keeps the edge.
    /// </summary>
    private static bool IsTopicEvidenceReachable(
        Dictionary<string, HashSet<string>>? sourceReachability,
        ProjectNode? library,
        Dictionary<(string TopicId, bool IsSubscription), HashSet<string>> topicEvidenceFiles,
        string topicId,
        bool isSubscription)
    {
        if (sourceReachability is null || library is null ||
            !sourceReachability.TryGetValue(library.Path.TrimEnd('/'), out var reachableFiles) ||
            !topicEvidenceFiles.TryGetValue((topicId, isSubscription), out var evidenceFiles))
        {
            return true;
        }

        var libraryPrefix = library.Path.TrimEnd('/') + "/";
        var libraryEvidence = evidenceFiles
            .Where(file => file.StartsWith(libraryPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return libraryEvidence.Count == 0 || libraryEvidence.Any(reachableFiles.Contains);
    }
}
