using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private void PopulateSystemContextNodes(
        JsonDocument nodesDoc,
        GraphDataDto graph,
        Dictionary<string, GraphNodeDto> nodeMap,
        Dictionary<string, string> dbIdToCanonicalId,
        Dictionary<string, string> projectToWorkloadMap,
        SystemContextDiscoveryState discovery,
        bool includeLibraries,
        string? projectFilter)
    {
        var sortedElems = nodesDoc.RootElement.EnumerateArray()
            .OrderBy(e => e.GetStringProp("kind") == "Project" ? 1 : 0);

        foreach (var elem in sortedElems)
        {
            var id = elem.GetStringProp("id");
            var kind = elem.GetStringProp("kind");
            var name = elem.GetStringProp("name", id);

            if (string.IsNullOrEmpty(id)) continue;

            if (!string.IsNullOrWhiteSpace(projectFilter) && IsProjectNodeKind(kind) &&
                !name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var props = ExtractBaseNodeProperties(elem);

            switch (kind)
            {
                case "Database":
                    ProcessDatabaseNode(elem, id, name, props, discovery.PrimaryRelationalEngine, dbIdToCanonicalId, nodeMap, graph);
                    break;
                case "Package":
                    ProcessPackageNode(elem, id, name, props, nodeMap, graph);
                    break;
                case "Topic":
                    ProcessTopicNode(elem, id, name, props, nodeMap, graph);
                    break;
                case "CloudService":
                    ProcessCloudServiceNode(elem, id, name, props, nodeMap, graph);
                    break;
                case "PeerMesh":
                    ProcessPeerMeshNode(id, name, props, nodeMap, graph);
                    break;
                case "ExternalService":
                    ProcessExternalServiceNode(elem, id, name, props, discovery, projectToWorkloadMap, nodeMap, graph);
                    break;
                default:
                    ProcessProjectOrWorkloadNode(elem, id, kind, name, props, includeLibraries, projectToWorkloadMap, nodeMap, graph);
                    break;
            }
        }
    }

    private async Task PopulatePackageCountsAsync(
        GraphDataDto graph,
        Dictionary<string, GraphNodeDto> nodeMap,
        CancellationToken ct)
    {
        var needsPkgCount = graph.Nodes.Any(n =>
            IsProjectNodeKind(n.Kind) && (n.Properties == null || !n.Properties.ContainsKey("package_count")));

        if (needsPkgCount)
        {
            try
            {
                var pkgCountQuery =
                    "MATCH (p)-[:DEPENDS_ON]->(pkg:Package) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) RETURN p.id AS projId, count(pkg) AS pkgCount";
                var pkgCountJson = await db.ExecuteQueryAsync(pkgCountQuery, null, ct);

                using var pkgCountDoc = JsonDocument.Parse(pkgCountJson);

                foreach (var row in pkgCountDoc.RootElement.EnumerateArray())
                {
                    var projId = row.GetStringProp("projId");

                    var count = row.TryGetProperty("pkgCount", out var pc) &&
                                pc.ValueKind == JsonValueKind.Number
                        ? pc.GetInt64()
                        : 0;

                    if (!string.IsNullOrEmpty(projId) && nodeMap.TryGetValue(projId, out var pNode) &&
                        pNode.Properties != null)
                    {
                        pNode.Properties["package_count"] = count.ToString();
                    }
                }
            }
            catch
            {
            }
        }
    }

    private async Task PopulateSystemContextEdgesAsync(
        GraphDataDto graph,
        Dictionary<string, GraphNodeDto> nodeMap,
        Dictionary<string, string> dbIdToCanonicalId,
        Dictionary<string, string> projectToWorkloadMap,
        List<GraphNodeDto> projectNodes,
        CancellationToken ct)
    {
        var edgesQuery =
            "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['SERVICE_CALL', 'DEPENDS_ON', 'USES_DB', 'USES_CLOUD', 'READS_FROM', 'WRITES_TO', 'COORDINATES_WITH', 'PUBLISHES_TO', 'TRIGGERS', 'SUBSCRIBES_TO', 'INTEGRATES_WITH', 'CALLS_ENDPOINT', 'LIBRARY', 'SUBSCRIBED_BY', 'PUBLISHED_BY', 'TESTS'] RETURN src.id AS from_id, tgt.id AS to_id, r.kind AS kind, r.properties AS properties";
        var edgesJson = await db.ExecuteQueryAsync(edgesQuery, null, ct);

        using var edgesDoc = JsonDocument.Parse(edgesJson);

        var edgeSet = new HashSet<(string From, string To, string Kind)>();

        foreach (var elem in edgesDoc.RootElement.EnumerateArray())
        {
            var rawFrom = elem.GetStringProp("from_id");
            var rawTo = elem.GetStringProp("to_id");
            var rKind = elem.GetStringProp("kind");

            var fromId = dbIdToCanonicalId.GetValueOrDefault(rawFrom, rawFrom);
            var toId = dbIdToCanonicalId.GetValueOrDefault(rawTo, rawTo);

            fromId = projectToWorkloadMap.GetValueOrDefault(fromId, fromId);
            toId = projectToWorkloadMap.GetValueOrDefault(toId, toId);

            if (!nodeMap.ContainsKey(fromId))
            {
                var owner = FindOwningProject(fromId, projectNodes);
                if (owner != null) fromId = projectToWorkloadMap.GetValueOrDefault(owner.Id, owner.Id);
            }

            if (!nodeMap.ContainsKey(toId))
            {
                var owner = FindOwningProject(toId, projectNodes);
                if (owner != null) toId = projectToWorkloadMap.GetValueOrDefault(owner.Id, owner.Id);
            }

            if (!nodeMap.TryGetValue(fromId, out var srcNode) || !nodeMap.TryGetValue(toId, out var tgtNode) ||
                fromId.Equals(toId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var edgeProps = elem.ExtractProperties();
            var isTargetLib = tgtNode.Kind == NodeKind.Package || IsLibraryProject(tgtNode);

            var (category, depType, normalizedKind) = PostIndexAnalyzer.NormalizeEdgeCategory(rKind,
                srcNode.Kind, tgtNode.Kind, edgeProps.GetValueOrDefault("category"),
                edgeProps.GetValueOrDefault("dependency_type"), isTargetLib);

            if (rKind is "TESTS" or "tests")
            {
                category = DependencyCategory.UnidentifiedDependency;
                depType = "tests";
                normalizedKind = OntologyConstants.Relationships.Tests;
            }
            else if (isTargetLib)
            {
                category = DependencyCategory.Library;
                depType = DependencyCategoryExtensions.Library;
                normalizedKind = (rKind is "DEPENDS_ON" or "DependsOn") ? "DEPENDS_ON" : "LIBRARY";
            }
            else if (rKind == "USES_CLOUD" && tgtNode.Kind == NodeKind.CloudService)
            {
                category = DependencyCategory.ServiceCall;
                depType = "external_resource";
                normalizedKind = "USES_CLOUD";
            }
            else if (tgtNode.Kind == NodeKind.CloudService && rKind is "READS_FROM" or "WRITES_TO")
            {
                category = DependencyCategory.ServiceCall;
                depType = "external_resource";
                normalizedKind = rKind;
            }
            else if (tgtNode.Kind == NodeKind.PeerMesh && rKind == "COORDINATES_WITH")
            {
                category = DependencyCategory.ServiceCall;
                depType = "peer_mesh";
                normalizedKind = rKind;
            }

            var categoryStr = (rKind is "TESTS" or "tests") ? "test" : category.ToCategoryString();
            edgeProps["category"] = categoryStr;
            edgeProps["dependency_type"] = depType;

            var isBothProjects = (srcNode.Kind is NodeKind.Project or NodeKind.Service or NodeKind.Worker or NodeKind.App) &&
                                 (tgtNode.Kind is NodeKind.Project or NodeKind.Service or NodeKind.Worker or NodeKind.App);

            if (isBothProjects)
            {
                if (rKind is "SUBSCRIBES_TO" or "SUBSCRIBED_BY" or "PUBLISHES_TO" or "PUBLISHED_BY")
                {
                    continue;
                }

                if (category == DependencyCategory.Messaging)
                {
                    category = DependencyCategory.ServiceCall;
                    depType = DependencyCategoryExtensions.ServiceCall;
                    categoryStr = DependencyCategoryExtensions.ServiceCall;
                    edgeProps["category"] = categoryStr;
                    edgeProps["dependency_type"] = depType;
                }
            }

            var outKind = (rKind is "TESTS" or "tests")
                ? OntologyConstants.Relationships.Tests
                : rKind == "USES_CLOUD" && tgtNode.Kind == NodeKind.CloudService
                    ? "USES_CLOUD"
                : tgtNode.Kind == NodeKind.CloudService && rKind is "READS_FROM" or "WRITES_TO"
                    ? rKind
                : tgtNode.Kind == NodeKind.PeerMesh && rKind == "COORDINATES_WITH"
                    ? rKind
                : category switch
            {
                DependencyCategory.Library => "LIBRARY",
                DependencyCategory.ServiceCall => (rKind == "CALLS_ENDPOINT" ? "CALLS_ENDPOINT" : "SERVICE_CALL"),
                DependencyCategory.Database => "USES_DB",
                DependencyCategory.Messaging => (rKind is "PUBLISHES" or "PUBLISHES_TO" or "PUBLISHED_BY"
                    ? "PUBLISHES_TO"
                    : "SUBSCRIBES_TO"),
                _ => normalizedKind
            };

            if (category == DependencyCategory.Messaging)
            {
                // In data-flow architecture:
                // - PUBLISH: Service -> Topic (Service sends message to topic)
                // - SUBSCRIBE: Topic -> Service (Service receives message from topic)
                var isTopicSrc = srcNode.Kind == NodeKind.Topic || fromId.Contains($":{OntologyConstants.IdPrefixes.Topic}:") || fromId.Contains(":topic:") || fromId.Contains(":res:topic:");
                var isTopicTgt = tgtNode.Kind == NodeKind.Topic || toId.Contains($":{OntologyConstants.IdPrefixes.Topic}:") || toId.Contains(":topic:") || toId.Contains(":res:topic:");

                if (outKind == "PUBLISHES_TO")
                {
                    if (isTopicSrc && !isTopicTgt)
                    {
                        (fromId, toId) = (toId, fromId);
                        (srcNode, tgtNode) = (tgtNode, srcNode);
                    }
                }
                else if (outKind == "SUBSCRIBES_TO")
                {
                    if (isTopicTgt && !isTopicSrc)
                    {
                        (fromId, toId) = (toId, fromId);
                        (srcNode, tgtNode) = (tgtNode, srcNode);
                    }
                }
            }

            var srcIsSemantic = srcNode.Properties?.GetValueOrDefault("is_semantic_entity") == "true";
            var tgtIsSemantic = tgtNode.Properties?.GetValueOrDefault("is_semantic_entity") == "true";

            if (srcIsSemantic && tgtIsSemantic && outKind != "LIBRARY")
            {
                edgeProps["is_semantic"] = "true";
            }
            if (!edgeProps.ContainsKey("is_semantic"))
            {
                edgeProps["is_semantic"] = "false";
            }

            var isTrans = edgeProps.GetValueOrDefault("is_transitive") == "true" ||
                          edgeProps.GetValueOrDefault("semantic_lifted") == "true";
            var isInferred = isTrans ||
                             edgeProps.GetValueOrDefault("is_late_bound") == "true" ||
                             edgeProps.GetValueOrDefault("inferred") == "true" ||
                             edgeProps.GetValueOrDefault("is_semantic") == "true";

            if (edgeSet.Add((fromId, toId, outKind)))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{fromId}->{toId}:{outKind}",
                    Source = fromId,
                    Target = toId,
                    Kind = outKind,
                    Category = categoryStr,
                    IsTransitive = isTrans,
                    IsInferred = isInferred,
                    Properties = edgeProps
                });
            }
        }

        foreach (var (rawDbId, canonicalId) in dbIdToCanonicalId)
        {
            var owningProj = FindOwningProject(rawDbId, projectNodes);

            if (owningProj != null)
            {
                var srcId = projectToWorkloadMap.GetValueOrDefault(owningProj.Id, owningProj.Id);
                if (nodeMap.ContainsKey(srcId) &&
                    graph.Edges.All(e => !(e.Source == srcId && e.Target == canonicalId)))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{srcId}->{canonicalId}:USES_DB",
                        Source = srcId,
                        Target = canonicalId,
                        Kind = "USES_DB",
                        Category = "database",
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = "database", ["is_semantic"] = "true"
                        }
                    });
                }
            }
        }
    }

    private static void FilterSharedDatabases(GraphDataDto graph)
    {
        var dbInDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in graph.Edges)
        {
            if (edge.Category == "database" || edge.Kind == "USES_DB")
            {
                dbInDegree[edge.Target] = dbInDegree.GetValueOrDefault(edge.Target) + 1;
            }
        }

        var singleConnDbIds = graph.Nodes
            .Where(n => n.Kind == NodeKind.Database && dbInDegree.GetValueOrDefault(n.Id) <= 1).Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (singleConnDbIds.Count > 0)
        {
            graph.Nodes.RemoveAll(n => singleConnDbIds.Contains(n.Id));

            graph.Edges.RemoveAll(e =>
                singleConnDbIds.Contains(e.Target) || singleConnDbIds.Contains(e.Source));
        }
    }


    /// <summary>
    /// C1: System Context View.
    /// Retrieves top-level executable services, external APIs, databases, and message topics with materialized macro-edges.
    /// </summary>
    public async Task<GraphDataDto> GetSystemContextViewAsync(
        bool includeLibraries = false,
        string? projectFilter = null,
        bool sharedDatabasesOnly = false,
        CancellationToken ct = default)
    {
        var dbKey = ResolveDbCachePrefix();
        var cacheKey = $"{dbKey}:c1:libs={includeLibraries}:filter={projectFilter ?? ""}:sharedDbs={sharedDatabasesOnly}";
        if (SystemContextCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var graph = await BuildBaseArchitectureTopologyAsync(
            includeLibraries: includeLibraries,
            includeTopics: true,
            projectFilter: projectFilter,
            sharedDatabasesOnly: sharedDatabasesOnly,
            ct: ct);

        graph.Metadata ??= new Dictionary<string, string>();
        graph.Metadata["view"] = "SystemContext";
        graph.Metadata["level"] = "C1";
        graph.Metadata["graphType"] = "architecture";

        SystemContextCache[cacheKey] = graph;
        return graph;
    }

    /// <summary>
    /// C2: Service Flow View.
    /// Focuses on service-to-service interactions, data persistence, and shared library dependencies for a container or workspace.
    /// </summary>
    public async Task<GraphDataDto> GetServiceFlowViewAsync(string? scope, bool includeLibraries = true, CancellationToken ct = default)
    {
        var allProjects = await GetAllProjectsAsync(ct);
        var projectPaths = await GetProjectPathsMapAsync(ct);

        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "ServiceFlow",
                ["level"] = "C2",
                ["scope"] = scope ?? "",
                ["graphType"] = "flow",
                ["allProjects"] = JsonSerializer.Serialize(allProjects),
                ["projectPaths"] = JsonSerializer.Serialize(projectPaths)
            }
        };

        if (allProjects.Count == 0)
        {
            return graph;
        }

        var targetName = string.IsNullOrWhiteSpace(scope) ? allProjects[0] : scope;

        var archGraph = await GetSystemContextViewAsync(includeLibraries: true, projectFilter: null, ct: ct);

        var centerNode = archGraph.Nodes.FirstOrDefault(n =>
            IsProjectNodeKind(n.Kind) &&
            (n.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
             (n.DisplayName != null && n.DisplayName.Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             n.Id.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
             (n.FilePath != null && n.FilePath.Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             n.Id.Equals($"workspace:project:{targetName}:", StringComparison.OrdinalIgnoreCase) ||
             n.Id.Equals($"workspace:{OntologyConstants.IdPrefixes.Project}:{targetName}:", StringComparison.OrdinalIgnoreCase) ||
             n.Id.Equals($"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Project}:{targetName}:", StringComparison.OrdinalIgnoreCase) ||
             (n.Id.StartWithIC("workspace:project:") &&
              n.Id["workspace:project:".Length..].TrimEnd(':').Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             (n.Id.StartWithIC($"workspace:{OntologyConstants.IdPrefixes.Project}:") &&
              n.Id[$"workspace:{OntologyConstants.IdPrefixes.Project}:".Length..].TrimEnd(':').Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             (n.Id.StartWithIC($"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Project}:") &&
              n.Id[$"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Project}:".Length..].TrimEnd(':').Equals(targetName, StringComparison.OrdinalIgnoreCase))));

        if (centerNode == null)
        {
            centerNode = archGraph.Nodes.FirstOrDefault(n =>
                n.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                (n.DisplayName != null && n.DisplayName.Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
                n.Id.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        }

        if (centerNode == null && allProjects.Count > 0 && targetName != allProjects[0])
        {
            targetName = allProjects[0];
            centerNode = archGraph.Nodes.FirstOrDefault(n =>
                IsProjectNodeKind(n.Kind) &&
                (n.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                 n.Id.Equals(targetName, StringComparison.OrdinalIgnoreCase)));
        }

        if (centerNode == null)
        {
            return graph;
        }

        var centerId = centerNode.Id;
        graph.Metadata["selectedProject"] = centerNode.Name;

        var centerDto = new GraphNodeDto
        {
            Id = centerNode.Id,
            Kind = centerNode.Kind,
            Name = centerNode.Name,
            DisplayName = centerNode.DisplayName,
            FilePath = centerNode.FilePath,
            LineStart = centerNode.LineStart,
            LineEnd = centerNode.LineEnd,
            ParentId = centerNode.ParentId,
            Properties = new Dictionary<string, string>(centerNode.Properties ?? new())
            {
                ["column"] = "center"
            }
        };
        if (!centerDto.Properties.ContainsKey("role"))
        {
            centerDto.Properties["role"] = "target";
        }
        graph.Nodes.Add(centerDto);

        var nodeLookup = archGraph.Nodes.GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var addedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { centerId };

        void AddNeighborNode(GraphNodeDto src, string column, string role)
        {
            if (addedNodeIds.Add(src.Id))
            {
                var nProps = new Dictionary<string, string>(src.Properties ?? new())
                {
                    ["column"] = column
                };
                if (!nProps.ContainsKey("role") || string.Equals(nProps["role"], "inbound", StringComparison.OrdinalIgnoreCase) || string.Equals(nProps["role"], "outbound", StringComparison.OrdinalIgnoreCase) || string.Equals(nProps["role"], "target", StringComparison.OrdinalIgnoreCase))
                {
                    nProps["role"] = role;
                }
                else
                {
                    nProps["flow_direction"] = role;
                }

                var nDto = new GraphNodeDto
                {
                    Id = src.Id,
                    Kind = src.Kind,
                    Name = src.Name,
                    DisplayName = src.DisplayName,
                    FilePath = src.FilePath,
                    LineStart = src.LineStart,
                    LineEnd = src.LineEnd,
                    ParentId = src.ParentId,
                    Properties = nProps
                };
                graph.Nodes.Add(nDto);
            }
        }

        void AddNeighborEdge(string from, string to, string kind, string category, Dictionary<string, string>? props)
        {
            var eProps = props != null ? new Dictionary<string, string>(props) : new Dictionary<string, string>();
            if (!eProps.ContainsKey("dependency_type"))
            {
                eProps["dependency_type"] = category;
            }
            if (!eProps.ContainsKey("category"))
            {
                eProps["category"] = category;
            }

            if (graph.Edges.All(e => !(e.Source == from && e.Target == to && e.Kind == kind)))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{from}->{to}:{kind}",
                    Source = from,
                    Target = to,
                    Kind = kind,
                    Category = category,
                    Properties = eProps
                });
            }
        }

        foreach (var edge in archGraph.Edges)
        {
            if (edge.Source.Equals(centerId, StringComparison.OrdinalIgnoreCase) && !edge.Target.Equals(centerId, StringComparison.OrdinalIgnoreCase))
            {
                if (nodeLookup.TryGetValue(edge.Target, out var tgtNode))
                {
                    if (tgtNode.Kind == NodeKind.Topic && edge.Kind.Equals("SUBSCRIBES_TO", StringComparison.OrdinalIgnoreCase))
                    {
                        AddNeighborNode(tgtNode, column: "left", role: "topic");
                        AddNeighborEdge(tgtNode.Id, centerId, "SUBSCRIBES", "messaging", edge.Properties);
                    }
                    else
                    {
                        var role = tgtNode.Kind switch
                        {
                            NodeKind.Database => "database",
                            NodeKind.ExternalService => "service",
                            NodeKind.Topic => "topic",
                            NodeKind.Package => "outbound",
                            _ => "outbound"
                        };
                        var cat = edge.Category ?? (tgtNode.Kind switch
                        {
                            NodeKind.Database => "database",
                            NodeKind.ExternalService => "service_call",
                            NodeKind.Topic => "messaging",
                            NodeKind.Package => "library",
                            _ => (edge.Kind == "LIBRARY" ? "library" : "service_call")
                        });

                        AddNeighborNode(tgtNode, column: "right", role: role);
                        AddNeighborEdge(centerId, tgtNode.Id, edge.Kind, cat, edge.Properties);
                    }
                }
            }
            else if (edge.Target.Equals(centerId, StringComparison.OrdinalIgnoreCase) && !edge.Source.Equals(centerId, StringComparison.OrdinalIgnoreCase))
            {
                if (nodeLookup.TryGetValue(edge.Source, out var srcNode))
                {
                    var role = srcNode.Kind == NodeKind.Topic ? "topic" : "inbound";
                    var cat = edge.Category ?? (srcNode.Kind switch
                    {
                        NodeKind.Topic => "messaging",
                        _ => (edge.Kind == "LIBRARY" ? "library" : "service_call")
                    });

                    AddNeighborNode(srcNode, column: "left", role: role);
                    AddNeighborEdge(srcNode.Id, centerId, edge.Kind, cat, edge.Properties);
                }
            }
        }

        return graph;
    }

    /// <summary>
    /// C3: Component Drill-down View.
    /// Returns internal controllers, entities, queries, and tables within the specified project container.
    /// </summary>
    public async Task<GraphDataDto> GetComponentViewAsync(string? projectScope, CancellationToken ct = default)
    {
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "Component",
                ["level"] = "C3",
                ["scope"] = projectScope ?? "",
                ["graphType"] = "component"
            }
        };

        if (string.IsNullOrWhiteSpace(projectScope)) return graph;

        // Query all components declared in or persisted in this project (excluding unit tests)
        var query = "MATCH (p)-[:CONTAINS*1..3]->(c) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) AND (p.id = $scope OR p.name = $scope) AND labels(c)[0] IN ['Endpoint', 'EntryPoint', 'Type', 'Table', 'Query'] AND (c.is_test IS NULL OR c.is_test = false) RETURN c.id AS id, labels(c)[0] AS kind, c.name AS name, c.path AS path";
        var resJson = await db.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["scope"] = projectScope }, ct);

        using var doc = JsonDocument.Parse(resJson);

        var compIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var id = elem.GetProperty("id").GetString();
            var kind = elem.GetProperty("kind").GetString();
            if (id == null || kind == null) continue;
            var name = elem.GetProperty("name").GetString() ?? id;
            var path = elem.TryGetProperty("path", out var pp) ? pp.GetString() : null;

            if (compIds.Add(id))
            {
                graph.Nodes.Add(new GraphNodeDto { Id = id, Name = name, Kind = EnumExtensions.ParseNodeKind(kind), FilePath = path });
            }
        }

        // Query internal component relationships: CALLS, EXPOSES_ENDPOINT, PERSISTED_IN, QUERIES_DB
        if (compIds.Count > 0)
        {
            var seenEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var batch in compIds.Chunk(200))
            {
                var inListStr = string.Join(", ", batch.Select(EscapeCypherStringLiteral));

                var relsQuery =
                    $"MATCH (src)-[r]->(tgt) WHERE src.id IN [{inListStr}] RETURN src.id AS from_id, tgt.id AS to_id, r.kind AS kind";
                var relsJson = await db.ExecuteQueryAsync(relsQuery, null, ct);

                using var relsDoc = JsonDocument.Parse(relsJson);

                foreach (var elem in relsDoc.RootElement.EnumerateArray())
                {
                    var fromId = elem.GetProperty("from_id").GetString();
                    var toId = elem.GetProperty("to_id").GetString();
                    var kind = elem.GetProperty("kind").GetString();
                    if (fromId == null || toId == null || kind == null) continue;

                    if (!compIds.Contains(toId)) continue;

                    var (category, depType, normalizedKind) =
                        PostIndexAnalyzer.NormalizeEdgeCategory(kind, null, null);

                    var edgeId = $"{fromId}->{toId}:{normalizedKind}";
                    if (!seenEdges.Add(edgeId)) continue;

                    var categoryStr = category.ToCategoryString();
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = edgeId,
                        Source = fromId,
                        Target = toId,
                        Kind = normalizedKind,
                        Category = categoryStr,
                        Properties = new Dictionary<string, string>
                        {
                            ["category"] = categoryStr, ["dependency_type"] = depType
                        }
                    });
                }
            }
        }

        return graph;
    }
}
