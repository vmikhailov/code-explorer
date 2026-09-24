using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
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
                     rel.Kind == OntologyConstants.Relationships.QueriedBy)
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
                AddImplements(rel.From, rel.To);
            }
            else if (rel.Kind == OntologyConstants.Relationships.Implements)
            {
                AddImplements(rel.To, rel.From);
            }
        }

        foreach (var rel in ctx.GlobalProjectDependencies) ProcessImplementsRel(rel);
        foreach (var rel in referenceRelationships) ProcessImplementsRel(rel);
        foreach (var rel in ctx.TreeRelationships) ProcessImplementsRel(rel);

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
        var graphData = new PostIndexGraphData(callsAdjacency, sinks, sinkDomains, [.. callersSet], implements, [.. entryPointIds], projectToEntryPoints);
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

        // Materialize direct architectural relationships (C4 Macro-Edges)
        var allCombinedRels = new List<Relationship>();
        allCombinedRels.AddRange(ctx.GlobalProjectDependencies);
        allCombinedRels.AddRange(referenceRelationships);
        allCombinedRels.AddRange(lateBoundRels);
        allCombinedRels.AddRange(ctx.TreeRelationships);

        var directProjectRels = MaterializeDirectProjectRelationships(
            l4Result.Prev.Prev.Projects,
            l4Result.Prev.Prev.Prev.Files,
            allCombinedRels
        );

        if (directProjectRels.Count > 0)
        {
            ctx.Log($"[PostIndexAnalyzer] Materializing {directProjectRels.Count} direct project relationships into SQLite graph...");
            await ctx.DbClient.UploadRelationshipsAsync(directProjectRels);
            ctx.AddRelsCount(directProjectRels.Count);
            allCombinedRels.AddRange(directProjectRels);
        }

        var liftedSemanticRels = LiftTransitiveSemanticRelations(l4Result.Prev.Prev.Projects, allCombinedRels);
        if (liftedSemanticRels.Count > 0)
        {
            ctx.Log($"[PostIndexAnalyzer] Materializing {liftedSemanticRels.Count} transitive semantic relationships into SQLite graph...");
            await ctx.DbClient.UploadRelationshipsAsync(liftedSemanticRels);
            ctx.AddRelsCount(liftedSemanticRels.Count);
        }

        // Classify projects and persist layer metadata into SQLite nodes
        var classifierItems = l4Result.Prev.Prev.Projects.Select(p => new CodeExplorer.Core.Analysis.ProjectClassifierItem
        {
            Id = p.Id,
            Name = p.Name,
            FilePath = p.Path,
            Framework = p.Extensions?.GetValueOrDefault("framework") ?? p.ProjectType
        });

        var dependencyItems = allCombinedRels
            .Where(r => r.Kind == OntologyConstants.Relationships.DependsOn ||
                        r.Kind == OntologyConstants.Relationships.ServiceCall ||
                        r.Kind == OntologyConstants.Relationships.UsesDb)
            .Select(r => new CodeExplorer.Core.Analysis.DependencyItem { SourceId = r.From, TargetId = r.To });

        var layerMap = CodeExplorer.Core.Analysis.ProjectLayerClassifier.Classify(classifierItems, dependencyItems);

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

            updatedProjectNodes.Add(Node.FromNode(p));
        }

        if (updatedProjectNodes.Count > 0)
        {
            await ctx.DbClient.UploadNodesAsync(updatedProjectNodes);
        }

        ctx.Log($"[PostIndexAnalyzer] In-memory analysis complete: {result.TransitivelyCalls.Count} TRANSITIVELY_CALLS, {result.AttributedTo.Count} ATTRIBUTED_TO, {directProjectRels.Count} direct project edges, {liftedSemanticRels.Count} lifted semantic edges, {result.ProjectExternalApis.Count} project external_apis.");
    }

    public static List<Relationship> MaterializeDirectProjectRelationships(
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<FileNode> files,
        IReadOnlyList<Relationship> allRelationships,
        IReadOnlyDictionary<string, string>? nodeKindsById = null)
    {
        var materializedRels = new List<Relationship>();
        var existingEdges = new HashSet<(string From, string To, string Kind)>(
            allRelationships.Select(r => (r.From, r.To, r.Kind))
        );

        var projList = projects.ToList();
        var fileToProject = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var p = projList.FirstOrDefault(pr => Layer2ProjectParser.IsEnclosedInProject(file, pr, projList));
            if (p != null)
            {
                fileToProject[file.Id] = p;
                if (!string.IsNullOrEmpty(file.Path))
                {
                    fileToProject[file.Path] = p;
                }
            }
        }

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
                                 to.Contains(":db:") || to.Contains(":database:") || to.Contains(":res:db:");
                if (isTargetDb)
                {
                    var owner = ResolveOwningProject(from);
                    if (owner != null && owner.Id != to)
                    {
                        if (existingEdges.Add((owner.Id, to, OntologyConstants.Relationships.UsesDb)))
                        {
                            materializedRels.Add(new Relationship(
                                owner.Id,
                                to,
                                OntologyConstants.Relationships.UsesDb,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "database",
                                    ["is_semantic"] = "true"
                                }
                            ));
                        }
                    }
                }
            }
            // 2. Direct Project -> Topic (PUBLISHES_TO)
            else if (kind == OntologyConstants.Relationships.PublishesTo)
            {
                var isTargetTopic = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Topic) ||
                                    to.Contains(":topic:") || to.Contains(":res:topic:");
                if (isTargetTopic)
                {
                    var owner = ResolveOwningProject(from);
                    if (owner != null && owner.Id != to)
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
            // 3. Topic -> Project (TRIGGERS)
            else if (kind == OntologyConstants.Relationships.Triggers)
            {
                var isSourceTopic = (nodeKindsById?.GetValueOrDefault(from) == OntologyConstants.NodeLabels.Topic) ||
                                    from.Contains(":topic:") || from.Contains(":res:topic:");
                if (isSourceTopic)
                {
                    var owner = ResolveOwningProject(to);
                    if (owner != null && owner.Id != from)
                    {
                        if (existingEdges.Add((from, owner.Id, OntologyConstants.Relationships.Triggers)))
                        {
                            materializedRels.Add(new Relationship(
                                from,
                                owner.Id,
                                OntologyConstants.Relationships.Triggers,
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
            // 4. Project -> Topic (SUBSCRIBES_TO)
            else if (kind == OntologyConstants.Relationships.SubscribesTo)
            {
                var isTargetTopic = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Topic) ||
                                    to.Contains(":topic:") || to.Contains(":res:topic:");
                if (isTargetTopic)
                {
                    var owner = ResolveOwningProject(from);
                    if (owner != null && owner.Id != to)
                    {
                        if (existingEdges.Add((owner.Id, to, OntologyConstants.Relationships.SubscribesTo)))
                        {
                            materializedRels.Add(new Relationship(
                                owner.Id,
                                to,
                                OntologyConstants.Relationships.SubscribesTo,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "messaging",
                                    ["is_semantic"] = "true"
                                }
                            ));
                        }
                        if (existingEdges.Add((to, owner.Id, OntologyConstants.Relationships.Triggers)))
                        {
                            materializedRels.Add(new Relationship(
                                to,
                                owner.Id,
                                OntologyConstants.Relationships.Triggers,
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
            // 6. External Service invocation -> Project -> ExternalService SERVICE_CALL
            else if (kind == OntologyConstants.Relationships.ServiceCall ||
                     kind == OntologyConstants.Relationships.UsesApi ||
                     kind == OntologyConstants.Relationships.UsesCloud)
            {
                var isTargetExt = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.ExternalService) ||
                                  to.Contains(":externalservice:") || to.Contains(":res:service:external:") || to.Contains(":cloud:");
                if (isTargetExt)
                {
                    var callerOwner = ResolveOwningProject(from);
                    if (callerOwner != null && callerOwner.Id != to)
                    {
                        if (existingEdges.Add((callerOwner.Id, to, OntologyConstants.Relationships.ServiceCall)))
                        {
                            materializedRels.Add(new Relationship(
                                callerOwner.Id,
                                to,
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
            // 7. Configures -> roll up DB / Topic configuration to Project
            else if (kind == OntologyConstants.Relationships.Configures)
            {
                var isTargetDb = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Database) ||
                                 to.Contains(":db:") || to.Contains(":database:");
                var isTargetTopic = (nodeKindsById?.GetValueOrDefault(to) == OntologyConstants.NodeLabels.Topic) ||
                                    to.Contains(":topic:");

                var owner = ResolveOwningProject(from);
                if (owner != null && isTargetDb && owner.Id != to)
                {
                    if (existingEdges.Add((owner.Id, to, OntologyConstants.Relationships.UsesDb)))
                    {
                        materializedRels.Add(new Relationship(
                            owner.Id,
                            to,
                            OntologyConstants.Relationships.UsesDb,
                            new Dictionary<string, object>
                            {
                                ["dependency_type"] = "database",
                                ["is_semantic"] = "true"
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

        var directProj = projects.FirstOrDefault(p => p.Id == nodeId || p.Id.TrimEnd(':') == nodeId.TrimEnd(':'));
        if (directProj != null) return directProj;

        if (fileToProjectMap != null && fileToProjectMap.TryGetValue(nodeId, out var proj))
        {
            return proj;
        }

        string pathPart = nodeId;
        var fileIdx = nodeId.IndexOf(":file:", StringComparison.OrdinalIgnoreCase);
        if (fileIdx >= 0)
        {
            pathPart = nodeId[(fileIdx + 6)..];
        }
        else
        {
            var projIdx = nodeId.IndexOf(":project:", StringComparison.OrdinalIgnoreCase);
            if (projIdx >= 0)
            {
                pathPart = nodeId[(projIdx + 9)..];
            }
        }

        pathPart = pathPart.TrimEnd(':').Replace('\\', '/');

        ProjectNode? bestMatch = null;
        int bestLen = -1;
        foreach (var p in projects)
        {
            var pPath = (p.Path ?? "").Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(pPath))
            {
                if (bestLen < 0) { bestMatch = p; bestLen = 0; }
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

        return bestMatch;
    }

    public static List<Relationship> LiftTransitiveSemanticRelations(
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<Relationship> allRelationships,
        IReadOnlyDictionary<string, string>? nodeKindsById = null)
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

        foreach (var service in services)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { service.Id };
            var queue = new Queue<(string CurrentId, int Depth, List<string> Chain)>();

            if (outEdges.TryGetValue(service.Id, out var initialEdges))
            {
                foreach (var edge in initialEdges)
                {
                    if (libraries.TryGetValue(edge.To, out var libNode))
                    {
                        if (visited.Add(libNode.Id))
                        {
                            queue.Enqueue((libNode.Id, 1, [libNode.Name]));
                        }
                    }
                }
            }

            while (queue.Count > 0)
            {
                var (currId, depth, chain) = queue.Dequeue();
                var viaLib = chain[0];
                var chainStr = string.Join(" -> ", chain);

                // Inbound Case: Message Broker / Topic subscribes into Library -> Lift to Service
                if (inEdges.TryGetValue(currId, out var incomingToLib))
                {
                    foreach (var inEdge in incomingToLib)
                    {
                        var srcKind = nodeKindsById?.GetValueOrDefault(inEdge.From);
                        var isTopic = (srcKind != null && srcKind.Equals(OntologyConstants.NodeLabels.Topic, StringComparison.OrdinalIgnoreCase))
                                      || inEdge.From.Contains(":topic:")
                                      || inEdge.Kind == OntologyConstants.Relationships.Triggers;

                        if (isTopic)
                        {
                            if (existingEdges.Add((inEdge.From, service.Id, OntologyConstants.Relationships.Triggers)))
                            {
                                liftedRels.Add(new Relationship(
                                    inEdge.From,
                                    service.Id,
                                    OntologyConstants.Relationships.Triggers,
                                    new Dictionary<string, object>
                                    {
                                        ["dependency_type"] = "messaging",
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib,
                                        ["call_chain"] = chainStr
                                    }
                                ));
                            }
                        }
                    }
                }

                if (outEdges.TryGetValue(currId, out var edges))
                {
                    foreach (var edge in edges)
                    {
                        var tgtKind = nodeKindsById?.GetValueOrDefault(edge.To);

                        // Case 1: Library connects to Database -> Lift direct USES_DB to Service
                        var isDb = (tgtKind != null && tgtKind.Equals(OntologyConstants.NodeLabels.Database, StringComparison.OrdinalIgnoreCase))
                                   || edge.To.Contains(":db:")
                                   || edge.To.Contains(":res:db:")
                                   || edge.To.Contains(":database:")
                                   || edge.Kind == OntologyConstants.Relationships.UsesDb;

                        if (isDb)
                        {
                            if (existingEdges.Add((service.Id, edge.To, OntologyConstants.Relationships.UsesDb)))
                            {
                                liftedRels.Add(new Relationship(
                                    service.Id,
                                    edge.To,
                                    OntologyConstants.Relationships.UsesDb,
                                    new Dictionary<string, object>
                                    {
                                        ["dependency_type"] = "database",
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib,
                                        ["call_chain"] = chainStr
                                    }
                                ));
                            }
                        }
                        // Case 2: Library calls another Service -> Lift direct SERVICE_CALL to Service
                        else if (projects.Any(p => p.Id == edge.To && !p.IsLibrary && p.Id != service.Id))
                        {
                            if (existingEdges.Add((service.Id, edge.To, OntologyConstants.Relationships.ServiceCall)))
                            {
                                liftedRels.Add(new Relationship(
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
                                ));
                            }
                        }
                        // Case 3: Library connects to External Service
                        else if ((tgtKind != null && tgtKind.Equals(OntologyConstants.NodeLabels.ExternalService, StringComparison.OrdinalIgnoreCase))
                                 || edge.To.Contains(":externalservice:")
                                 || edge.To.Contains(":res:service:external:"))
                        {
                            if (existingEdges.Add((service.Id, edge.To, OntologyConstants.Relationships.ServiceCall)))
                            {
                                liftedRels.Add(new Relationship(
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
                                ));
                            }
                        }
                        // Case 4: Library publishes to Topic
                        else if ((tgtKind != null && tgtKind.Equals(OntologyConstants.NodeLabels.Topic, StringComparison.OrdinalIgnoreCase))
                                 || edge.To.Contains(":topic:")
                                 || edge.To.Contains(":res:topic:"))
                        {
                            if (existingEdges.Add((service.Id, edge.To, OntologyConstants.Relationships.PublishesTo)))
                            {
                                liftedRels.Add(new Relationship(
                                    service.Id,
                                    edge.To,
                                    OntologyConstants.Relationships.PublishesTo,
                                    new Dictionary<string, object>
                                    {
                                        ["dependency_type"] = "messaging",
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib,
                                        ["call_chain"] = chainStr
                                    }
                                ));
                            }
                        }
                        // Case 5: Library depends on another Library -> continue BFS traversal up to depth 3
                        else if (libraries.TryGetValue(edge.To, out var nextLib) && depth < 3)
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

        return new PostIndexAnalysisResult(
            transitivelyCalls.DistinctBy(x => (x.From, x.To)).ToList(),
            attributedTo.DistinctBy(x => (x.EpId, x.SinkId)).ToList(),
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
