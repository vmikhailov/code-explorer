using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    internal async Task<GraphDataDto> BuildBaseArchitectureTopologyAsync(
        bool includeLibraries = false,
        bool includeTopics = true,
        string? projectFilter = null,
        bool sharedDatabasesOnly = false,
        CancellationToken ct = default)
    {
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>()
        };

        var nodeMap = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var dbIdToCanonicalId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projectToWorkloadMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var topicClause = includeTopics
            ? " OR (labels(n)[0] = 'Topic' AND (n.broker_type IS NULL OR (n.broker_type <> 'mediatr' AND n.broker_type <> 'in-memory')) AND (n.is_internal IS NULL OR n.is_internal <> 'true'))"
            : "";

        var nodesQuery =
            $"MATCH (n) WHERE labels(n)[0] IN ['Project', 'Service', 'App', 'Library', 'Worker', 'CliTool', 'TestSuite', 'Database', 'ExternalService']{topicClause} OR (labels(n)[0] = 'Package' AND (n.is_external = 'true' OR n.is_external = true OR (n.is_external IS NULL AND NOT (n)-[:IMPLEMENTED_BY]->(:Project)))) RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.display_name AS display_name, n.path AS path, n.framework AS framework, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type, n.package_type AS package_type, n.type AS pkg_type, n.version AS version, n.properties AS properties, n.layer AS layer, n.layerId AS layerId, n.layerName AS layerName, n.layerOrder AS layerOrder, n.layerColor AS layerColor, n.layerIcon AS layerIcon, n.package_count AS package_count";

        var nodesJson = await db.ExecuteQueryAsync(nodesQuery, null, ct);
        using var nodesDoc = JsonDocument.Parse(nodesJson);

        var discovery = await DiscoverSystemContextCandidatesAsync(nodesDoc, ct);

        PopulateSystemContextNodes(nodesDoc, graph, nodeMap, dbIdToCanonicalId, projectToWorkloadMap, discovery, includeLibraries: true, projectFilter);

        await PopulatePackageCountsAsync(graph, nodeMap, ct);

        var projectNodes = graph.Nodes.Where(n => IsProjectNodeKind(n.Kind)).ToList();

        await PopulateSystemContextEdgesAsync(graph, nodeMap, dbIdToCanonicalId, projectToWorkloadMap, projectNodes, ct);

        LiftTransitiveSemanticRelations(graph);

        if (!includeLibraries)
        {
            var libOrPkgIds = graph.Nodes
                .Where(n => IsLibraryProject(n) || n.Kind is NodeKind.Package or NodeKind.Library or NodeKind.SharedLibrary)
                .Select(n => n.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (libOrPkgIds.Count > 0)
            {
                graph.Nodes.RemoveAll(n => libOrPkgIds.Contains(n.Id));
                graph.Edges.RemoveAll(e => libOrPkgIds.Contains(e.Source) || libOrPkgIds.Contains(e.Target));
            }
        }

        if (sharedDatabasesOnly)
        {
            FilterSharedDatabases(graph);
        }

        // Exclude Database nodes that have NO USES_DB / READS_FROM / WRITES_TO edges from non-test workloads
        var nonTestNodeIds = graph.Nodes
            .Where(n => n.Kind != NodeKind.TestSuite && 
                        (n.Properties == null || (!string.Equals(n.Properties.GetValueOrDefault("role"), "Test", StringComparison.OrdinalIgnoreCase) &&
                                                  !string.Equals(n.Properties.GetValueOrDefault("role"), "TestSuite", StringComparison.OrdinalIgnoreCase) &&
                                                  !string.Equals(n.Properties.GetValueOrDefault("is_test"), "true", StringComparison.OrdinalIgnoreCase) &&
                                                  !string.Equals(n.Properties.GetValueOrDefault("entity_kind"), "Test", StringComparison.OrdinalIgnoreCase))) &&
                        !WorkspaceConventions.IsTestFilePath(n.FilePath))
            .Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usedDbIds = graph.Edges
            .Where(e => (e.Kind is "USES_DB" or "READS_FROM" or "WRITES_TO" or "ACCESSES_TABLE" || string.Equals(e.Category, "database", StringComparison.OrdinalIgnoreCase)) &&
                        (nonTestNodeIds.Contains(e.Source) || nonTestNodeIds.Contains(e.Target)))
            .SelectMany(e => new[] { e.Source, e.Target })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphanOrTestOnlyDbs = graph.Nodes
            .Where(n => n.Kind == NodeKind.Database && !usedDbIds.Contains(n.Id))
            .Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (orphanOrTestOnlyDbs.Count > 0)
        {
            graph.Nodes.RemoveAll(n => orphanOrTestOnlyDbs.Contains(n.Id));
            graph.Edges.RemoveAll(e => orphanOrTestOnlyDbs.Contains(e.Source) || orphanOrTestOnlyDbs.Contains(e.Target));
        }

        var allProjects = await GetAllProjectsAsync(ct);
        graph.Metadata["allProjects"] = JsonSerializer.Serialize(allProjects);
        var projectPaths = await GetProjectPathsMapAsync(ct);
        graph.Metadata["projectPaths"] = JsonSerializer.Serialize(projectPaths);

        return graph;
    }


    private static string EscapeCypherStringLiteral(string s) =>
        $"'{s.Replace(@"\", @"\\").Replace("'", @"\'")}'";


    public static bool IsProjectNodeKind(NodeKind kind) =>
        kind is NodeKind.Project or NodeKind.Service or NodeKind.App or NodeKind.Ingress or NodeKind.Library or NodeKind.SharedLibrary or NodeKind.Worker or NodeKind.CliTool or NodeKind.TestSuite;

    public static bool IsProjectNodeKind(string? kind) =>
        IsProjectNodeKind(EnumExtensions.ParseNodeKind(kind));

    public static bool IsLibraryProject(GraphNodeDto? node)
    {
        if (node == null) return false;
        if (node.Kind is NodeKind.TestSuite) return false;
        if (node.Kind is NodeKind.Library or NodeKind.SharedLibrary)
        {
            return true;
        }
        if (!IsProjectNodeKind(node.Kind)) return false;

        var isLibProp = node.Properties?.GetValueOrDefault("is_library");
        if (isLibProp == "true") return true;
        if (isLibProp == "false") return false;

        var projectType = node.Properties?.GetValueOrDefault("project_type")?.ToLowerInvariant();
        if (projectType == "library") return true;

        var role = node.Properties?.GetValueOrDefault("role");
        if (role is "Test" or "TestSuite") return false;
        if (role is "SharedLibrary") return true;
        if (role is "Service" or "FrontendApp" or "Worker" or "CliTool") return false;

        var (_, isLib) = ProjectRoleDetector.DetectRole(
            "",
            [],
            node.FilePath ?? "",
            node.Name,
            projectType ?? ""
        );
        return isLib;
    }

    public static GraphNodeDto? FindOwningProject(string sourceId, IEnumerable<GraphNodeDto> projects)
    {
        if (string.IsNullOrEmpty(sourceId)) return null;

        if (Urn.IsExternalOrPackageId(sourceId)) return null;

        var projList = projects as IList<GraphNodeDto> ?? [.. projects];
        if (projList.Count == 0) return null;

        // 1. Direct project ID match
        foreach (var p in projList)
        {
            if (p.Id.Equals(sourceId, StringComparison.OrdinalIgnoreCase)) return p;
        }

        // 2. Project ID prefix match
        foreach (var p in projList)
        {
            var trimId = p.Id.TrimEnd(':');
            if (sourceId.StartWithIC(trimId + ":") ||
                sourceId.StartWithIC(p.Id))
            {
                return p;
            }
        }

        // 3. File path resolution (e.g. ws:f:action-scheduler/src/... or ws:sym:action-scheduler/...)
        var filePath = sourceId;
        bool isKnownFileOrSymbolUrn = false;
        if (Urn.TryParse(sourceId, out var urn) && !string.IsNullOrEmpty(urn.Path))
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
            filePath = urn.Path;
        }
        else
        {
            var fileIdx = filePath.IndexOf($":{OntologyConstants.IdPrefixes.File}:", StringComparison.OrdinalIgnoreCase);
            if (fileIdx < 0) fileIdx = filePath.IndexOf(":file:", StringComparison.OrdinalIgnoreCase);
            if (fileIdx >= 0)
            {
                isKnownFileOrSymbolUrn = true;
                var markerLen = filePath.IndexOf($":{OntologyConstants.IdPrefixes.File}:", StringComparison.OrdinalIgnoreCase) >= 0
                    ? $":{OntologyConstants.IdPrefixes.File}:".Length
                    : ":file:".Length;
                filePath = filePath[(fileIdx + markerLen)..];
            }
            else
            {
                var symIdx = filePath.IndexOf($":{OntologyConstants.IdPrefixes.Symbol}:", StringComparison.OrdinalIgnoreCase);
                if (symIdx < 0) symIdx = filePath.IndexOf(":symbol:", StringComparison.OrdinalIgnoreCase);
                if (symIdx >= 0)
                {
                    isKnownFileOrSymbolUrn = true;
                    var markerLen = filePath.IndexOf($":{OntologyConstants.IdPrefixes.Symbol}:", StringComparison.OrdinalIgnoreCase) >= 0
                        ? $":{OntologyConstants.IdPrefixes.Symbol}:".Length
                        : ":symbol:".Length;
                    filePath = filePath[(symIdx + markerLen)..];
                }
                else
                {
                    var projIdx = filePath.IndexOf($":{OntologyConstants.IdPrefixes.Project}:", StringComparison.OrdinalIgnoreCase);
                    if (projIdx < 0) projIdx = filePath.IndexOf(":project:", StringComparison.OrdinalIgnoreCase);
                    if (projIdx >= 0)
                    {
                        isKnownFileOrSymbolUrn = true;
                        var markerLen = filePath.IndexOf($":{OntologyConstants.IdPrefixes.Project}:", StringComparison.OrdinalIgnoreCase) >= 0
                            ? $":{OntologyConstants.IdPrefixes.Project}:".Length
                            : ":project:".Length;
                        filePath = filePath[(projIdx + markerLen)..];
                    }
                }
            }
        }

        var normFilePath = filePath.Replace('\\', '/').TrimStart('/');

        // Match against project FilePath (longest match first)
        GraphNodeDto? bestMatch = null;
        int bestLen = -1;
        GraphNodeDto? rootProject = null;

        foreach (var p in projList)
        {
            var pPath = (p.FilePath ?? p.Properties?.GetValueOrDefault("path") ?? "").Replace('\\', '/').TrimStart('/').TrimEnd('/');
            if (string.IsNullOrEmpty(pPath) || pPath == ".")
            {
                rootProject ??= p;
                continue;
            }

            if (normFilePath.StartWithIC(pPath + "/") ||
                normFilePath.Equals(pPath, StringComparison.OrdinalIgnoreCase))
            {
                if (pPath.Length > bestLen)
                {
                    bestLen = pPath.Length;
                    bestMatch = p;
                }
            }
        }
        if (bestMatch != null) return bestMatch;

        // Match by Project Name segment in path (only as a fallback when no project matched by path)
        foreach (var p in projList)
        {
            if (!string.IsNullOrEmpty(p.Name) && p.Name.Length > 2)
            {
                if (normFilePath.StartWithIC(p.Name + "/") ||
                    normFilePath.Contains("/" + p.Name + "/"))
                {
                    return p;
                }
            }
        }

        // Root project matches as fallback only if source was confirmed to be a file/symbol within workspace
        if (rootProject != null && isKnownFileOrSymbolUrn && !normFilePath.Contains(':'))
        {
            return rootProject;
        }

        return null;
    }

    public static void LiftTransitiveSemanticRelations(GraphDataDto graph)
    {
        var nodesById = graph.Nodes.GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var services = graph.Nodes.Where(n => IsProjectNodeKind(n.Kind) && !IsLibraryProject(n)).ToList();
        var libraries = graph.Nodes.Where(n => IsProjectNodeKind(n.Kind) && IsLibraryProject(n)).GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var outEdges = new Dictionary<string, List<GraphEdgeDto>>(StringComparer.OrdinalIgnoreCase);
        var inEdges = new Dictionary<string, List<GraphEdgeDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            if (!outEdges.TryGetValue(edge.Source, out var outList))
            {
                outList = [];
                outEdges[edge.Source] = outList;
            }
            outList.Add(edge);

            if (!inEdges.TryGetValue(edge.Target, out var inList))
            {
                inList = [];
                inEdges[edge.Target] = inList;
            }
            inEdges[edge.Target].Add(edge);
        }

        foreach (var service in services)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { service.Id };
            var queue = new Queue<(string CurrentId, int Depth, string ViaLib)>();

            if (outEdges.TryGetValue(service.Id, out var initialEdges))
            {
                foreach (var edge in initialEdges)
                {
                    if (libraries.TryGetValue(edge.Target, out var libNode))
                    {
                        if (visited.Add(libNode.Id))
                        {
                            queue.Enqueue((libNode.Id, 1, libNode.Name));
                        }
                    }
                }
            }

            while (queue.Count > 0)
            {
                var (currId, depth, viaLib) = queue.Dequeue();

                // Inbound Case: Message Broker / Topic subscribes into Library -> Lift to Service
                if (inEdges.TryGetValue(currId, out var incomingToLib))
                {
                    foreach (var inEdge in incomingToLib)
                    {
                        if (!nodesById.TryGetValue(inEdge.Source, out var srcNode)) continue;
                        if (srcNode.Kind == NodeKind.Topic || inEdge.Kind == "TRIGGERS" || inEdge.Kind == "SUBSCRIBES_TO")
                        {
                            if (IsInternalTopic(srcNode) || inEdge.Properties?.GetValueOrDefault("is_internal") == "true") continue;
                            if (graph.Edges.All(e => !(e.Source == srcNode.Id && e.Target == service.Id && (e.Kind == "SUBSCRIBES_TO" || e.Kind == "SUBSCRIBED_BY"))))
                            {
                                graph.Edges.Add(new GraphEdgeDto
                                {
                                    Id = $"{srcNode.Id}->{service.Id}:SUBSCRIBES_TO",
                                    Source = srcNode.Id,
                                    Target = service.Id,
                                    Kind = "SUBSCRIBES_TO",
                                    Category = "messaging",
                                    IsTransitive = true,
                                    IsInferred = true,
                                    Properties = new Dictionary<string, string>
                                    {
                                        ["dependency_type"] = "messaging",
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib
                                    }
                                });
                            }
                        }
                    }
                }

                if (outEdges.TryGetValue(currId, out var edges))
                {
                    foreach (var edge in edges)
                    {
                        if (!nodesById.TryGetValue(edge.Target, out var targetNode)) continue;

                        // Case 1: Library connects to Database -> Lift direct USES_DB to Service
                        if (targetNode.Kind == NodeKind.Database || edge.Kind == "USES_DB")
                        {
                            if (graph.Edges.All(e => !(e.Source == service.Id && e.Target == targetNode.Id)))
                            {
                                graph.Edges.Add(new GraphEdgeDto
                                {
                                    Id = $"{service.Id}->{targetNode.Id}:USES_DB",
                                    Source = service.Id,
                                    Target = targetNode.Id,
                                    Kind = "USES_DB",
                                    Category = "database",
                                    IsTransitive = false,
                                    IsInferred = true,
                                    Properties = new Dictionary<string, string>
                                    {
                                        ["dependency_type"] = "database",
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib
                                    }
                                });
                            }
                        }
                        // Case 2: Library calls another Service -> Lift direct SERVICE_CALL to Service
                        // Only lift if the library's edge to the target service is an actual service call (not a generic compile-time project dependency)
                        else if (IsProjectNodeKind(targetNode.Kind) && !IsLibraryProject(targetNode) && targetNode.Id != service.Id &&
                                 (edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT" || edge.Category == "service_call" ||
                                  edge.Properties?.GetValueOrDefault("dependency_type") == "service_call"))
                        {
                            if (graph.Edges.All(e => !(e.Source == service.Id && e.Target == targetNode.Id && (e.Kind == "SERVICE_CALL" || e.Kind == "CALLS_ENDPOINT"))))
                            {
                                graph.Edges.Add(new GraphEdgeDto
                                {
                                    Id = $"{service.Id}->{targetNode.Id}:SERVICE_CALL",
                                    Source = service.Id,
                                    Target = targetNode.Id,
                                    Kind = "SERVICE_CALL",
                                    Category = "service_call",
                                    IsTransitive = false,
                                    IsInferred = true,
                                    Properties = new Dictionary<string, string>
                                    {
                                        ["dependency_type"] = "service_call",
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib
                                    }
                                });
                            }
                        }
                        // Case 3: Library connects to External Service -> Lift direct SERVICE_CALL
                        else if (targetNode.Kind == NodeKind.ExternalService)
                        {
                            if (graph.Edges.All(e => !(e.Source == service.Id && e.Target == targetNode.Id && e.Kind == "SERVICE_CALL")))
                            {
                                graph.Edges.Add(new GraphEdgeDto
                                {
                                    Id = $"{service.Id}->{targetNode.Id}:SERVICE_CALL",
                                    Source = service.Id,
                                    Target = targetNode.Id,
                                    Kind = "SERVICE_CALL",
                                    Category = "service_call",
                                    IsTransitive = false,
                                    IsInferred = true,
                                    Properties = new Dictionary<string, string>
                                    {
                                        ["dependency_type"] = "service_call",
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib
                                    }
                                });
                            }
                        }
                        // Case 4: Library publishes to Topic -> Lift direct PUBLISHES_TO
                        else if (targetNode.Kind == NodeKind.Topic)
                        {
                            if (IsInternalTopic(targetNode) || edge.Properties?.GetValueOrDefault("is_internal") == "true") continue;
                            if (graph.Edges.All(e => !(e.Source == service.Id && e.Target == targetNode.Id && (e.Kind == "TRIGGERS" || e.Kind == "PUBLISHES_TO"))))
                            {
                                graph.Edges.Add(new GraphEdgeDto
                                {
                                    Id = $"{service.Id}->{targetNode.Id}:PUBLISHES_TO",
                                    Source = service.Id,
                                    Target = targetNode.Id,
                                    Kind = "PUBLISHES_TO",
                                    Category = "messaging",
                                    IsTransitive = true,
                                    IsInferred = true,
                                    Properties = new Dictionary<string, string>
                                    {
                                        ["dependency_type"] = "messaging",
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib
                                    }
                                });
                            }
                        }
                        // Case 5: Library depends on another Library -> continue BFS traversal up to depth 8
                        else if (libraries.TryGetValue(edge.Target, out var nextLib) && depth < 8)
                        {
                            if (visited.Add(nextLib.Id))
                            {
                                queue.Enqueue((nextLib.Id, depth + 1, $"{viaLib} -> {nextLib.Name}"));
                            }
                        }
                    }
                }
            }
        }
    }
}
