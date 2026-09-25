using System.Text.Json;
using System.Text.RegularExpressions;
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

public record CanonicalizeDatabasesResult(int CanonicalNodesCount, int MaterializedRelationshipsCount);

public class PostIndexAnalyzer(IGraphClient db)
{
    public async Task RunAsync(string workspaceId)
    {
        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");

        await CanonicalizeDatabasesAsync(db, widPrefix);
        await NormalizeEdgesAsync(db, widPrefix);

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

        // Canonicalize databases and link projects in graph
        var (canonicalDbRels, rawToCanonicalDbMap) = await CanonicalizeDatabasesInMemoryAsync(
            ctx,
            l4Result,
            allCombinedRels
        );
        allCombinedRels.AddRange(canonicalDbRels);

        var directProjectRels = MaterializeDirectProjectRelationships(
            l4Result.Prev.Prev.Projects,
            l4Result.Prev.Prev.Prev.Files,
            allCombinedRels,
            dbCanonicalMap: rawToCanonicalDbMap
        );

        if (directProjectRels.Count > 0)
        {
            ctx.Log($"[PostIndexAnalyzer] Materializing {directProjectRels.Count} direct project relationships into SQLite graph...");
            await ctx.DbClient.UploadRelationshipsAsync(directProjectRels);
            ctx.AddRelsCount(directProjectRels.Count);
            allCombinedRels.AddRange(directProjectRels);
        }

        var liftedSemanticRels = LiftTransitiveSemanticRelations(
            l4Result.Prev.Prev.Projects,
            allCombinedRels,
            dbCanonicalMap: rawToCanonicalDbMap
        );
        if (liftedSemanticRels.Count > 0)
        {
            ctx.Log($"[PostIndexAnalyzer] Materializing {liftedSemanticRels.Count} transitive semantic relationships into SQLite graph...");
            await ctx.DbClient.UploadRelationshipsAsync(liftedSemanticRels);
            ctx.AddRelsCount(liftedSemanticRels.Count);
        }

        // Classify projects and persist layer metadata into SQLite nodes
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

            var endpointsCount = allChildren.OfType<EndpointNode>().Count();
            var entryPoints = allChildren.OfType<EntryPointNode>().ToList();
            var externalServicesCount = allChildren.OfType<ExternalServiceNode>().Count();
            var usesDbCount = allCombinedRels.Count(r => r.From == p.Id && r.Kind == OntologyConstants.Relationships.UsesDb);

            return new CodeExplorer.Core.Analysis.ProjectClassifierItem
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
            await ctx.DbClient.UploadNodesAsync(updatedProjectNodes);
        }

        // Normalize edges in SQLite graph database
        await NormalizeEdgesAsync(ctx.DbClient, widPrefix, ctx.CancellationToken);

        ctx.Log($"[PostIndexAnalyzer] In-memory analysis complete: {result.TransitivelyCalls.Count} TRANSITIVELY_CALLS, {result.AttributedTo.Count} ATTRIBUTED_TO, {directProjectRels.Count} direct project edges, {liftedSemanticRels.Count} lifted semantic edges, {result.ProjectExternalApis.Count} project external_apis.");
    }

    public static List<Relationship> MaterializeDirectProjectRelationships(
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<FileNode> files,
        IReadOnlyList<Relationship> allRelationships,
        IReadOnlyDictionary<string, string>? nodeKindsById = null,
        IReadOnlyDictionary<string, string>? dbCanonicalMap = null)
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
            var symIdx = nodeId.IndexOf(":symbol:", StringComparison.OrdinalIgnoreCase);
            if (symIdx >= 0)
            {
                pathPart = nodeId[(symIdx + 8)..];
            }
            else
            {
                var projIdx = nodeId.IndexOf(":project:", StringComparison.OrdinalIgnoreCase);
                if (projIdx >= 0)
                {
                    pathPart = nodeId[(projIdx + 9)..];
                }
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
        IReadOnlyDictionary<string, string>? nodeKindsById = null,
        IReadOnlyDictionary<string, string>? dbCanonicalMap = null)
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
                            var targetDbId = dbCanonicalMap?.GetValueOrDefault(edge.To, edge.To) ?? edge.To;
                            if (existingEdges.Add((service.Id, targetDbId, OntologyConstants.Relationships.UsesDb)))
                            {
                                liftedRels.Add(new Relationship(
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
                                 || edge.To.Contains(":res:topic:")
                                 || edge.Kind == OntologyConstants.Relationships.PublishesTo
                                 || edge.Kind == OntologyConstants.Relationships.Triggers)
                        {
                            var outKind = edge.Kind == OntologyConstants.Relationships.Triggers
                                ? OntologyConstants.Relationships.Triggers
                                : OntologyConstants.Relationships.PublishesTo;
                            if (existingEdges.Add((service.Id, edge.To, outKind)))
                            {
                                liftedRels.Add(new Relationship(
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

    public Task<CanonicalizeDatabasesResult> CanonicalizeDatabasesAsync(string widPrefix, CancellationToken ct = default)
    {
        return CanonicalizeDatabasesAsync(db, widPrefix, ct);
    }

    private static readonly HashSet<string> KnownDbEnginesAndNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgresql", "postgres", "mysql", "mariadb", "sqlite", "sqlite3",
        "clickhouse", "bigquery", "redis", "mongodb", "mongo",
        "sqlserver", "mssql", "sql server", "oracle", "cassandra",
        "elasticsearch", "neo4j", "dynamodb", "firestore", "cosmosdb",
        "couchdb", "cockroachdb", "tidb", "influxdb", "timescaledb",
        "memcached", "kafka", "rabbitmq", "database", "db", "default",
        "typeorm", "sequelize", "prisma", "drizzle", "dapper", "ef-core"
    };

    public static bool IsLikelyDatabaseName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        var lower = trimmed.ToLowerInvariant();
        if (lower.Length <= 1) return false;
        if (char.IsDigit(lower[0])) return false;
        
        // Reject SQL keywords, stopwords, functions, and PostgreSQL system catalogs
        if (NestedSqlParser.IsSqlKeyword(lower)) return false;
        if (lower.StartsWith("pg_") || lower.StartsWith("information_schema")) return false;

        // Matches known engine names or aliases
        if (KnownDbEnginesAndNames.Contains(lower)) return true;
        if (lower.StartsWith("redis_") || lower.StartsWith("postgres_") || lower.StartsWith("db_")) return true;
        if (lower.EndsWith("_db") || lower.EndsWith("-db") || lower.EndsWith("database")) return true;

        // Reject if it contains table or function indicators
        if (lower.Contains("_tb") || lower.Contains("_table") || lower.Contains("_history") || 
            lower.Contains("_rules") || lower.Contains("jsonb_") || lower.Contains("dblink")) return false;

        return false;
    }

    public static (string CanonicalName, string CanonicalType, string CanonicalKey) CanonicalizeDatabase(string rawName, string? rawDbType)
    {
        var trimmed = (rawName ?? "").Trim();
        var lower = trimmed.ToLowerInvariant();
        var type = string.IsNullOrWhiteSpace(rawDbType) ? "relational" : rawDbType.Trim().ToLowerInvariant();

        switch (lower)
        {
            case "database":
            case "db":
            case "typeorm":
            case "microsoft.entityframeworkcore":
            case "entity framework core":
            case "ef-core":
            case "dapper":
            case "sequelize":
            case "prisma":
            case "drizzle":
            case "drizzle orm":
            case "defaultconnection":
                return ("Database", "relational", "database");
            case "postgres":
            case "postgresql":
                return ("PostgreSQL", "relational", "postgresql");
            case "redis":
                return ("Redis", "cache", "redis");
            case "mysql":
                return ("MySQL", "relational", "mysql");
            case "mariadb":
                return ("MariaDB", "relational", "mariadb");
            case "sqlite":
            case "sqlite3":
                return ("SQLite", "relational", "sqlite");
            case "mongodb":
            case "mongo":
                return ("MongoDB", "document", "mongodb");
            case "clickhouse":
                return ("ClickHouse", "analytics", "clickhouse");
            case "bigquery":
                return ("BigQuery", "analytics", "bigquery");
            case "mssql":
            case "sqlserver":
            case "sql server":
                return ("SQL Server", "relational", "sqlserver");
            case "oracle":
                return ("Oracle", "relational", "oracle");
            case "cassandra":
                return ("Cassandra", "nosql", "cassandra");
            case "elasticsearch":
                return ("Elasticsearch", "search", "elasticsearch");
            case "neo4j":
                return ("Neo4j", "graph", "neo4j");
            default:
                if (lower.StartsWith("redis_"))
                {
                    return ("Redis", "cache", "redis");
                }
                var normName = CodeExplorer.Core.Analysis.ResourceReconciliationService.NormalizeResourceName(trimmed, type);
                var canonicalKey = Regex.Replace(normName.ToLowerInvariant(), @"[^a-z0-9_-]", "_").Trim('_');
                if (string.IsNullOrEmpty(canonicalKey)) canonicalKey = "db";
                return (normName, type, canonicalKey);
        }
    }

    public static string BuildCanonicalDatabaseId(string? rawId, string cType, string cKey, string? defaultWorkspaceId = null)
    {
        var wid = "workspace";
        if (!string.IsNullOrEmpty(rawId) && rawId.Contains(':'))
        {
            var firstPart = rawId.Split(':')[0];
            if (!string.IsNullOrWhiteSpace(firstPart))
            {
                wid = firstPart;
            }
        }
        else if (!string.IsNullOrEmpty(defaultWorkspaceId))
        {
            var trimmed = defaultWorkspaceId.TrimEnd(':');
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                wid = trimmed;
            }
        }

        return $"{wid}:database:{cType.ToLowerInvariant()}:{cKey.ToLowerInvariant()}";
    }

    public static async Task<(List<Relationship> CanonicalRels, Dictionary<string, string> RawToCanonicalMap)> CanonicalizeDatabasesInMemoryAsync(
        ParsingContext ctx,
        Layer4Result l4Result,
        List<Relationship> allRelationships)
    {
        var rawToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var canonicalDbNodes = new Dictionary<string, DatabaseNode>(StringComparer.OrdinalIgnoreCase);

        // 1. Collect all DatabaseNodes from AST / Semantic tree
        var allDbNodes = new List<DatabaseNode>();
        void CollectDbNodes(IOntologyNode node)
        {
            if (node is DatabaseNode db)
            {
                if (IsLikelyDatabaseName(db.Name) || IsLikelyDatabaseName(db.Id))
                {
                    allDbNodes.Add(db);
                }
            }
            foreach (var child in node.Children)
            {
                CollectDbNodes(child);
            }
        }

        if (l4Result.SemanticStructure != null) CollectDbNodes(l4Result.SemanticStructure);
        if (l4Result.Prev.Prev.Prev.Workspace != null) CollectDbNodes(l4Result.Prev.Prev.Prev.Workspace);
        foreach (var p in l4Result.Prev.Prev.Projects) CollectDbNodes(p);
        foreach (var f in l4Result.Prev.Prev.Prev.Files) CollectDbNodes(f);

        foreach (var db in allDbNodes)
        {
            var rawEngine = (db.Extensions != null && db.Extensions.TryGetValue("engine", out var eng) && !string.IsNullOrWhiteSpace(eng))
                ? eng
                : null;
            var (cName, cType, cKey) = CanonicalizeDatabase(db.Name, db.DbType);
            var canonicalId = BuildCanonicalDatabaseId(db.Id, cType, cKey, ctx.WorkspaceId);
            rawToCanonical[db.Id] = canonicalId;

            var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                ? rawEngine
                : cName;

            if (!canonicalDbNodes.TryGetValue(canonicalId, out var existingNode) ||
                (existingNode.Extensions != null && existingNode.Extensions.TryGetValue("engine", out var exEng) && exEng.Equals(existingNode.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
            {
                var dispName = !string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                    ? $"{cName} ({engineToUse}) [{cType}]"
                    : $"{cName} [{cType}]";

                var canonicalNode = new DatabaseNode(
                    canonicalId,
                    cName,
                    db.Path ?? "",
                    cType,
                    new Dictionary<string, string>
                    {
                        ["name"] = cName,
                        ["display_name"] = dispName,
                        ["db_type"] = cType,
                        ["role"] = "database",
                        ["engine"] = engineToUse,
                        ["is_canonical"] = "true",
                        ["is_semantic_entity"] = "true",
                        ["layer"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId,
                        ["layerId"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId,
                        ["layerName"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName,
                        ["layerColor"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color,
                        ["layerIcon"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Icon
                    }
                );
                canonicalDbNodes[canonicalId] = canonicalNode;
            }
        }

        // Also inspect all relationships for DB targets not caught above
        foreach (var rel in allRelationships)
        {
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          rel.To.Contains(":db:") || rel.To.Contains(":database:") || rel.To.Contains(":res:db:");
            if (isDbRel && !rawToCanonical.ContainsKey(rel.To))
            {
                var parts = rel.To.Split(':');
                var rawName = parts.Length > 0 ? parts[^1] : "";
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                // STRICT VALIDATION: Only canonicalize if rawName is a genuine database!
                if (!IsLikelyDatabaseName(rawName))
                {
                    continue;
                }

                var rawType = "relational";
                if (rel.Properties.TryGetValue("db_type", out var dtObj) && dtObj != null)
                {
                    rawType = dtObj.ToString() ?? "relational";
                }
                var rawEngine = rel.Properties.TryGetValue("engine", out var engObj) && engObj != null
                    ? engObj.ToString()
                    : null;

                var (cName, cType, cKey) = CanonicalizeDatabase(rawName, rawType);
                var canonicalId = BuildCanonicalDatabaseId(rel.To, cType, cKey, ctx.WorkspaceId);
                rawToCanonical[rel.To] = canonicalId;

                var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    ? rawEngine
                    : cName;

                if (!canonicalDbNodes.TryGetValue(canonicalId, out var existingNode) ||
                    (existingNode.Extensions != null && existingNode.Extensions.TryGetValue("engine", out var exEng) && exEng.Equals(existingNode.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
                {
                    var dispName = !string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                        ? $"{cName} ({engineToUse}) [{cType}]"
                        : $"{cName} [{cType}]";

                    var canonicalNode = new DatabaseNode(
                        canonicalId,
                        cName,
                        "",
                        cType,
                        new Dictionary<string, string>
                        {
                            ["name"] = cName,
                            ["display_name"] = dispName,
                            ["db_type"] = cType,
                            ["role"] = "database",
                            ["engine"] = engineToUse,
                            ["is_canonical"] = "true",
                            ["is_semantic_entity"] = "true",
                            ["layer"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId,
                            ["layerId"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId,
                            ["layerName"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName,
                            ["layerColor"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color,
                            ["layerIcon"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Icon
                        }
                    );
                    canonicalDbNodes[canonicalId] = canonicalNode;
                }
            }
        }

        // 2. Add canonical nodes to SemanticStructure and upload
        if (ctx.SemanticStructure != null)
        {
            foreach (var node in canonicalDbNodes.Values)
            {
                if (!ctx.SemanticStructure.Children.Any(c => c.Id == node.Id))
                {
                    ctx.SemanticStructure.Children.Add(node);
                }
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, node.Name, node.Id);
            }
        }

        if (canonicalDbNodes.Count > 0)
        {
            ctx.Log($"[PostIndexAnalyzer] Materializing {canonicalDbNodes.Count} canonical Database nodes into graph...");
            await ctx.DbClient.UploadNodesAsync(canonicalDbNodes.Values.Select(Node.FromNode).ToList());
            ctx.AddNodesCount(canonicalDbNodes.Count);
        }

        // 3. Link projects to canonical database nodes
        var projects = l4Result.Prev.Prev.Projects;
        var files = l4Result.Prev.Prev.Prev.Files;
        var fileToProject = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var p = projects.FirstOrDefault(pr => Layer2ProjectParser.IsEnclosedInProject(file, pr, projects));
            if (p != null)
            {
                fileToProject[file.Id] = p;
                if (!string.IsNullOrEmpty(file.Path))
                {
                    fileToProject[file.Path] = p;
                }
            }
        }

        var canonicalRels = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var rel in allRelationships)
        {
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          rawToCanonical.ContainsKey(rel.To);
            if (!isDbRel) continue;

            if (rawToCanonical.TryGetValue(rel.To, out var canonicalTarget))
            {
                var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase))
                            ?? (fileToProject.TryGetValue(rel.From, out var fp) ? fp : null);

                if (owner != null && owner.Id != canonicalTarget)
                {
                    if (seen.Add((owner.Id, canonicalTarget)))
                    {
                        var props = new Dictionary<string, object>
                        {
                            ["dependency_type"] = "database",
                            ["is_semantic"] = "true",
                            ["is_canonical"] = "true"
                        };
                        if (rel.Properties != null)
                        {
                            foreach (var (k, v) in rel.Properties)
                            {
                                if (v != null && !props.ContainsKey(k))
                                {
                                    props[k] = v;
                                }
                            }
                        }
                        canonicalRels.Add(new Relationship(
                            owner.Id,
                            canonicalTarget,
                            OntologyConstants.Relationships.UsesDb,
                            props
                        ));
                    }
                    else if (rel.Properties != null)
                    {
                        var existing = canonicalRels.FirstOrDefault(r => r.From == owner.Id && r.To == canonicalTarget);
                        if (existing?.Properties != null)
                        {
                            foreach (var (k, v) in rel.Properties)
                            {
                                if (v != null && !existing.Properties.ContainsKey(k))
                                {
                                    existing.Properties[k] = v;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (canonicalRels.Count > 0)
        {
            ctx.Log($"[PostIndexAnalyzer] Materializing {canonicalRels.Count} direct project->canonical database relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(canonicalRels);
            ctx.AddRelsCount(canonicalRels.Count);
        }

        // Clean up duplicate non-canonical database nodes in SQLite
        if (ctx.DbClient is SqliteGraphClient)
        {
            foreach (var (rawId, canonicalId) in rawToCanonical)
            {
                if (!string.Equals(rawId, canonicalId, StringComparison.Ordinal))
                {
                    await ctx.DbClient.ExecuteWriteAsync("UPDATE OR IGNORE edges SET to_id = @canonicalId WHERE to_id = @rawId;", new { canonicalId, rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE to_id = @rawId;", new { rawId });
                    await ctx.DbClient.ExecuteWriteAsync("UPDATE OR IGNORE edges SET from_id = @canonicalId WHERE from_id = @rawId;", new { canonicalId, rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE from_id = @rawId;", new { rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM nodes WHERE id = @rawId;", new { rawId });
                }
            }

            // Purge any remaining phantom Database nodes that were not canonicalized
            var validIds = canonicalDbNodes.Keys.ToList();
            if (validIds.Count > 0)
            {
                var placeholders = string.Join(",", validIds.Select((_, i) => $"@p{i}"));
                var paramDict = new Dictionary<string, object>();
                for (int i = 0; i < validIds.Count; i++) paramDict[$"p{i}"] = validIds[i];

                await ctx.DbClient.ExecuteWriteAsync(
                    $"DELETE FROM edges WHERE kind = 'USES_DB' AND to_id NOT IN ({placeholders}) AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database');",
                    paramDict);
                await ctx.DbClient.ExecuteWriteAsync(
                    $"DELETE FROM nodes WHERE kind = 'Database' AND id NOT IN ({placeholders});",
                    paramDict);
            }
        }

        return (canonicalRels, rawToCanonical);
    }

    public static async Task<CanonicalizeDatabasesResult> CanonicalizeDatabasesAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        var rawToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var canonicalNodes = new Dictionary<string, (string Name, string DbType, string Key, string Engine)>(StringComparer.OrdinalIgnoreCase);

        // 1. Query databases
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine";
        var dbJson = await db.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        using (var dbDoc = JsonDocument.Parse(dbJson))
        {
            foreach (var row in dbDoc.RootElement.EnumerateArray())
            {
                var id = GetStringProp(row, "id");
                var name = GetStringProp(row, "name", id);
                if (!IsLikelyDatabaseName(name) && !IsLikelyDatabaseName(id)) continue;

                var dbType = row.TryGetProperty("db_type", out var dt) && dt.ValueKind == JsonValueKind.String ? (dt.GetString() ?? "Database") : "Database";
                var rawEngine = row.TryGetProperty("engine", out var eg) && eg.ValueKind == JsonValueKind.String ? eg.GetString() : null;

                var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType);
                var canonicalId = BuildCanonicalDatabaseId(id, cType, cKey, widPrefix);
                rawToCanonical[id] = canonicalId;

                var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    ? rawEngine
                    : cName;

                if (!canonicalNodes.TryGetValue(canonicalId, out var existing) ||
                    (existing.Engine.Equals(existing.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
                {
                    canonicalNodes[canonicalId] = (cName, cType, cKey, engineToUse);
                }
            }
        }

        if (canonicalNodes.Count == 0)
        {
            return new CanonicalizeDatabasesResult(0, 0);
        }

        // 2. Upload canonical Database nodes
        var nodesToUpload = new List<Node>();
        foreach (var (canonicalId, (cName, cType, cKey, engineToUse)) in canonicalNodes)
        {
            var dispName = !string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                ? $"{cName} ({engineToUse}) [{cType}]"
                : $"{cName} [{cType}]";
            var props = new Dictionary<string, object>
            {
                ["name"] = cName,
                ["display_name"] = dispName,
                ["db_type"] = cType,
                ["role"] = "database",
                ["engine"] = engineToUse,
                ["is_canonical"] = "true",
                ["is_semantic_entity"] = "true",
                ["layer"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId,
                ["layerId"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId,
                ["layerName"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName,
                ["layerColor"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color,
                ["layerIcon"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Icon
            };
            nodesToUpload.Add(new Node(canonicalId, "Database", props));
        }

        await db.UploadNodesAsync(nodesToUpload);

        // 3. Query projects
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.path AS path";
        var projsJson = await db.ExecuteQueryAsync(projQuery, null, cancellationToken);
        using var projsDoc = JsonDocument.Parse(projsJson);
        var projectList = new List<(string Id, string Name, string? Path)>();
        foreach (var row in projsDoc.RootElement.EnumerateArray())
        {
            var pId = GetStringProp(row, "id");
            var pName = GetStringProp(row, "name");
            var pPath = row.TryGetProperty("path", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;
            if (!string.IsNullOrEmpty(pId))
            {
                projectList.Add((pId, pName, pPath));
            }
        }

        // 4. Query relationships targeting any database
        var edgesQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties";
        var edgesJson = await db.ExecuteQueryAsync(edgesQuery, null, cancellationToken);
        using var edgesDoc = JsonDocument.Parse(edgesJson);

        var relationshipsToUpload = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var row in edgesDoc.RootElement.EnumerateArray())
        {
            var src = GetStringProp(row, "source");
            var tgt = GetStringProp(row, "target");
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

            if (rawToCanonical.TryGetValue(tgt, out var canonicalId))
            {
                // Resolve owning project
                string? projId = null;
                var exactProj = projectList.FirstOrDefault(p => p.Id == src);
                if (!string.IsNullOrEmpty(exactProj.Id))
                {
                    projId = exactProj.Id;
                }
                else
                {
                    var matched = projectList.FirstOrDefault(p => src.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase) ||
                                                                  (!string.IsNullOrEmpty(p.Path) && src.Contains(p.Path, StringComparison.OrdinalIgnoreCase)));
                    projId = matched.Id;
                }

                if (!string.IsNullOrEmpty(projId) && projId != canonicalId)
                {
                    if (seen.Add((projId, canonicalId)))
                    {
                        var props = new Dictionary<string, object>
                        {
                            ["dependency_type"] = "database",
                            ["is_semantic"] = "true",
                            ["is_canonical"] = "true"
                        };
                        if (row.TryGetProperty("properties", out var pElem) && pElem.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var jp in pElem.EnumerateObject())
                            {
                                if (!props.ContainsKey(jp.Name))
                                {
                                    props[jp.Name] = jp.Value.ToString();
                                }
                            }
                        }
                        relationshipsToUpload.Add(new Relationship(
                            projId,
                            canonicalId,
                            OntologyConstants.Relationships.UsesDb,
                            props
                        ));
                    }
                }
            }
        }

        if (relationshipsToUpload.Count > 0)
        {
            await db.UploadRelationshipsAsync(relationshipsToUpload);
        }

        // 5. Clean up duplicate / project-scoped database nodes where id != canonicalId in SQLite
        if (db is SqliteGraphClient)
        {
            foreach (var (rawId, canonicalId) in rawToCanonical)
            {
                if (!string.Equals(rawId, canonicalId, StringComparison.Ordinal))
                {
                    await db.ExecuteWriteAsync("UPDATE OR IGNORE edges SET to_id = @canonicalId WHERE to_id = @rawId;", new { canonicalId, rawId }, cancellationToken);
                    await db.ExecuteWriteAsync("DELETE FROM edges WHERE to_id = @rawId;", new { rawId }, cancellationToken);
                    await db.ExecuteWriteAsync("UPDATE OR IGNORE edges SET from_id = @canonicalId WHERE from_id = @rawId;", new { canonicalId, rawId }, cancellationToken);
                    await db.ExecuteWriteAsync("DELETE FROM edges WHERE from_id = @rawId;", new { rawId }, cancellationToken);
                    await db.ExecuteWriteAsync("DELETE FROM nodes WHERE id = @rawId;", new { rawId }, cancellationToken);
                }
            }

            // Purge any remaining phantom Database nodes that were not canonicalized
            var validIds = canonicalNodes.Keys.ToList();
            if (validIds.Count > 0)
            {
                var placeholders = string.Join(",", validIds.Select((_, i) => $"@p{i}"));
                var paramDict = new Dictionary<string, object>();
                for (int i = 0; i < validIds.Count; i++) paramDict[$"p{i}"] = validIds[i];

                await db.ExecuteWriteAsync(
                    $"DELETE FROM edges WHERE kind = 'USES_DB' AND to_id NOT IN ({placeholders}) AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database');",
                    paramDict, cancellationToken);
                await db.ExecuteWriteAsync(
                    $"DELETE FROM nodes WHERE kind = 'Database' AND id NOT IN ({placeholders});",
                    paramDict, cancellationToken);
            }
        }

        return new CanonicalizeDatabasesResult(nodesToUpload.Count, relationshipsToUpload.Count);
    }

    public static (string Category, string DependencyType, string NormalizedKind) NormalizeEdgeCategory(
        string? rawKind,
        string? sourceKind,
        string? targetKind,
        string? existingCategory = null,
        string? existingDepType = null,
        bool isTargetLibrary = false)
    {
        var kind = (rawKind ?? "").ToUpperInvariant();

        string category;
        if (!string.IsNullOrEmpty(existingCategory))
        {
            category = existingCategory.ToLowerInvariant();
        }
        else if (!string.IsNullOrEmpty(existingDepType))
        {
            category = existingDepType.ToLowerInvariant();
        }
        else if (kind == "USES_DB" ||
                 string.Equals(targetKind, "Database", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sourceKind, "Database", StringComparison.OrdinalIgnoreCase))
        {
            category = "database";
        }
        else if (kind is "TRIGGERS" or "PUBLISHES" or "PUBLISHES_TO" or "SUBSCRIBES_TO" or "SUBSCRIBED_BY" ||
                 string.Equals(targetKind, "Topic", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sourceKind, "Topic", StringComparison.OrdinalIgnoreCase))
        {
            category = "messaging";
        }
        else if (kind is "SERVICE_CALL" or "CALLS_ENDPOINT" ||
                 string.Equals(targetKind, "ExternalService", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sourceKind, "ExternalService", StringComparison.OrdinalIgnoreCase))
        {
            category = "service_call";
        }
        else if (isTargetLibrary || string.Equals(targetKind, "Package", StringComparison.OrdinalIgnoreCase))
        {
            category = "library";
        }
        else
        {
            category = "service_call";
        }

        category = category switch
        {
            "database" or "db" => "database",
            "messaging" or "queue" or "topic" or "pubsub" => "messaging",
            "service_call" or "service" or "api" or "http" or "grpc" => "service_call",
            _ => "library"
        };

        var depType = category;

        string normalizedKind;
        if (category == "database")
        {
            normalizedKind = "USES_DB";
        }
        else if (category == "messaging")
        {
            normalizedKind = (kind is "PUBLISHES_TO" or "SUBSCRIBES_TO") ? kind : "TRIGGERS";
        }
        else if (kind is "DEPENDS_ON" or "DependsOn")
        {
            normalizedKind = "DEPENDS_ON";
        }
        else if (category == "service_call")
        {
            normalizedKind = kind == "CALLS_ENDPOINT" ? "CALLS_ENDPOINT" : "SERVICE_CALL";
        }
        else
        {
            normalizedKind = kind == "LIBRARY" ? "LIBRARY" : "DEPENDS_ON";
        }

        return (category, depType, normalizedKind);
    }

    public static async Task<int> NormalizeEdgesAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        // 1. Query all nodes to determine kind and whether project is library
        var nodeMap = new Dictionary<string, (string Kind, bool IsLibrary)>(StringComparer.OrdinalIgnoreCase);
        var nodesJson = await db.ExecuteQueryAsync("MATCH (n) RETURN n.id AS id, labels(n) AS lbl, n.is_library AS is_lib, n.role AS role", null, cancellationToken);
        using (var nodesDoc = JsonDocument.Parse(nodesJson))
        {
            foreach (var row in nodesDoc.RootElement.EnumerateArray())
            {
                var id = GetStringProp(row, "id");
                if (string.IsNullOrEmpty(id)) continue;
                var kind = "Node";
                if (row.TryGetProperty("lbl", out var lblProp) && lblProp.ValueKind == JsonValueKind.Array)
                {
                    kind = lblProp.EnumerateArray().FirstOrDefault().GetString() ?? "Node";
                }
                var isLib = row.TryGetProperty("is_lib", out var il) && il.ValueKind == JsonValueKind.String && il.GetString() == "true";
                var role = GetStringProp(row, "role");
                if (role is "SharedLibrary" or "Test") isLib = true;

                nodeMap[id] = (kind, isLib);
            }
        }

        // 2. Query architectural/macro edges
        var edgeQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON', 'SERVICE_CALL', 'INTEGRATES_WITH', 'CALLS_ENDPOINT', 'PUBLISHES_TO', 'TRIGGERS', 'SUBSCRIBES_TO', 'LIBRARY'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties";
        var edgesJson = await db.ExecuteQueryAsync(edgeQuery, null, cancellationToken);
        using var edgesDoc = JsonDocument.Parse(edgesJson);

        var updatedCount = 0;
        foreach (var row in edgesDoc.RootElement.EnumerateArray())
        {
            var src = GetStringProp(row, "source");
            var tgt = GetStringProp(row, "target");
            var rawKind = GetStringProp(row, "kind");
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

            nodeMap.TryGetValue(src, out var srcInfo);
            nodeMap.TryGetValue(tgt, out var tgtInfo);

            var existingCategory = "";
            var existingDepType = "";
            var props = new Dictionary<string, object>();

            if (row.TryGetProperty("properties", out var pElem))
            {
                if (pElem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in pElem.EnumerateObject())
                    {
                        var valStr = prop.Value.ToString();
                        props[prop.Name] = valStr;
                        if (prop.NameEquals("category")) existingCategory = valStr;
                        if (prop.NameEquals("dependency_type")) existingDepType = valStr;
                    }
                }
                else if (pElem.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(pElem.GetString()))
                {
                    try
                    {
                        using var innerDoc = JsonDocument.Parse(pElem.GetString()!);
                        if (innerDoc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in innerDoc.RootElement.EnumerateObject())
                            {
                                var valStr = prop.Value.ToString();
                                props[prop.Name] = valStr;
                                if (prop.NameEquals("category")) existingCategory = valStr;
                                if (prop.NameEquals("dependency_type")) existingDepType = valStr;
                            }
                        }
                    }
                    catch { }
                }
            }

            var (category, depType, normalizedKind) = NormalizeEdgeCategory(
                rawKind,
                srcInfo.Kind,
                tgtInfo.Kind,
                existingCategory,
                existingDepType,
                tgtInfo.IsLibrary
            );

            var needsUpdate = !props.ContainsKey("dependency_type") ||
                              !string.Equals(props.GetValueOrDefault("dependency_type")?.ToString(), depType, StringComparison.Ordinal) ||
                              !string.Equals(props.GetValueOrDefault("category")?.ToString(), category, StringComparison.Ordinal) ||
                              !string.Equals(rawKind, normalizedKind, StringComparison.Ordinal);

            if (needsUpdate)
            {
                props["dependency_type"] = depType;
                props["category"] = category;

                if (db is SqliteGraphClient)
                {
                    var propsJson = JsonSerializer.Serialize(props);
                    await db.ExecuteWriteAsync(
                        "UPDATE OR IGNORE edges SET kind = @normalizedKind, properties = @propsJson WHERE from_id = @src AND to_id = @tgt AND kind = @rawKind;",
                        new { normalizedKind, propsJson, src, tgt, rawKind },
                        cancellationToken
                    );
                    updatedCount++;
                }
            }
        }

        return updatedCount;
    }

    private static string GetStringProp(JsonElement elem, string prop, string fallback = "")
    {
        return elem.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String
            ? (val.GetString() ?? fallback)
            : fallback;
    }
}
