using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public enum ArchitectureViewType
{
    SystemContext, // C1: Macro System Context view (Services, Databases, Topics, ExternalServices)
    ServiceFlow,   // C2: Container & Service Flow view (Interactions, Messaging, Shared Libraries)
    Component      // C3: Component Drill-down for a selected container/project
}

public class ArchitectureViewRequest
{
    public ArchitectureViewType ViewType { get; set; } = ArchitectureViewType.SystemContext;
    public string? Scope { get; set; }
    public bool IncludeLibraries { get; set; } = false;
}

/// <summary>
/// Executes single-query architectural projections directly against the graph database,
/// bypassing runtime BFS lifting and heuristics.
/// </summary>
public class ArchitectureViewEngine(IGraphClient db)
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GraphDataDto> SystemContextCache = new();

    public static void InvalidateCache()
    {
        SystemContextCache.Clear();
    }

    public async Task<GraphDataDto> GetViewAsync(ArchitectureViewRequest request, CancellationToken ct = default)
    {
        return request.ViewType switch
        {
            ArchitectureViewType.SystemContext => await GetSystemContextViewAsync(request.IncludeLibraries, request.Scope, ct),
            ArchitectureViewType.ServiceFlow => await GetServiceFlowViewAsync(request.Scope, request.IncludeLibraries, ct),
            ArchitectureViewType.Component => await GetComponentViewAsync(request.Scope, ct),
            _ => await GetSystemContextViewAsync(request.IncludeLibraries, request.Scope, ct)
        };
    }

    /// <summary>
    /// C1: System Context View.
    /// Retrieves top-level executable services, external APIs, databases, and message topics with materialized macro-edges.
    /// </summary>
    public async Task<GraphDataDto> GetSystemContextViewAsync(
        bool includeLibraries = false,
        string? projectFilter = null,
        CancellationToken ct = default)
    {
        var cacheKey = $"c1:libs={includeLibraries}:filter={projectFilter ?? ""}";
        if (SystemContextCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "SystemContext",
                ["level"] = "C1",
                ["graphType"] = "architecture"
            }
        };

        var nodeMap = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var dbIdToCanonicalId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Query macro nodes: Services, Databases, Topics, ExternalServices, Packages
        var nodesQuery = includeLibraries
            ? "MATCH (n) WHERE labels(n)[0] IN ['Project', 'Database', 'Topic', 'ExternalService'] OR (labels(n)[0] = 'Package' AND (n.is_external = 'true' OR n.is_external = true OR (n.is_external IS NULL AND NOT (n)-[:IMPLEMENTED_BY]->(:Project)))) RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.display_name AS display_name, n.path AS path, n.framework AS framework, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type, n.package_type AS package_type, n.type AS pkg_type, n.version AS version, n.properties AS properties, n.layer AS layer, n.layerId AS layerId, n.layerName AS layerName, n.layerOrder AS layerOrder, n.layerColor AS layerColor, n.layerIcon AS layerIcon, n.package_count AS package_count"
            : "MATCH (n) WHERE labels(n)[0] IN ['Database', 'Topic', 'ExternalService'] OR (labels(n)[0] = 'Project' AND (n.is_library <> 'true' OR n.is_library IS NULL) AND (n.role <> 'SharedLibrary' AND n.role <> 'Test' OR n.role IS NULL)) RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.display_name AS display_name, n.path AS path, n.framework AS framework, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type, n.package_type AS package_type, n.type AS pkg_type, n.version AS version, n.properties AS properties, n.layer AS layer, n.layerId AS layerId, n.layerName AS layerName, n.layerOrder AS layerOrder, n.layerColor AS layerColor, n.layerIcon AS layerIcon, n.package_count AS package_count";

        var nodesJson = await db.ExecuteQueryAsync(nodesQuery, null, ct);
        using var nodesDoc = JsonDocument.Parse(nodesJson);

        foreach (var elem in nodesDoc.RootElement.EnumerateArray())
        {
            var id = elem.GetStringProp("id");
            var kind = elem.GetStringProp("kind");
            var name = elem.GetStringProp("name", id);

            if (string.IsNullOrEmpty(id)) continue;

            if (!string.IsNullOrWhiteSpace(projectFilter) && kind == "Project" && !name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var props = elem.ExtractProperties();
            var role = elem.GetStringProp("role", props.GetValueOrDefault("role", ""));
            if (!string.IsNullOrEmpty(role)) props["role"] = role;
            var isLib = elem.GetStringProp("is_library", props.GetValueOrDefault("is_library", ""));
            if (!string.IsNullOrEmpty(isLib)) props["is_library"] = isLib;
            var dbType = elem.GetStringProp("db_type", props.GetValueOrDefault("db_type", ""));
            if (!string.IsNullOrEmpty(dbType)) props["db_type"] = dbType;
            var projType = elem.GetStringProp("project_type", props.GetValueOrDefault("project_type", ""));
            if (!string.IsNullOrEmpty(projType)) props["project_type"] = projType;
            var framework = elem.GetStringProp("framework", props.GetValueOrDefault("framework", ""));
            if (!string.IsNullOrEmpty(framework)) props["framework"] = framework;
            var path = elem.GetStringProp("path", props.GetValueOrDefault("path", ""));
            if (!string.IsNullOrEmpty(path)) props["path"] = path;
            var layer = elem.GetStringProp("layer", props.GetValueOrDefault("layer", ""));
            if (!string.IsNullOrEmpty(layer))
            {
                props["layer"] = layer;
                props["layerId"] = elem.GetStringProp("layerId", props.GetValueOrDefault("layerId", layer));
                props["layerName"] = elem.GetStringProp("layerName", props.GetValueOrDefault("layerName", ""));
                props["layerOrder"] = elem.GetStringProp("layerOrder", props.GetValueOrDefault("layerOrder", ""));
                props["layerColor"] = elem.GetStringProp("layerColor", props.GetValueOrDefault("layerColor", ""));
                props["layerIcon"] = elem.GetStringProp("layerIcon", props.GetValueOrDefault("layerIcon", ""));
            }
            var pkgCount = elem.GetStringProp("package_count", props.GetValueOrDefault("package_count", ""));
            if (!string.IsNullOrEmpty(pkgCount)) props["package_count"] = pkgCount;

            if (kind == "Database")
            {
                var (cName, cType, cKey) = PostIndexAnalyzer.CanonicalizeDatabase(name, dbType);
                var isExplicitResource = id.Contains(":res:db:", StringComparison.OrdinalIgnoreCase) ||
                                         id.StartsWith("urn:", StringComparison.OrdinalIgnoreCase);
                var isStandaloneDb = id.StartsWith("db:", StringComparison.OrdinalIgnoreCase);

                string canonicalId;
                if (isExplicitResource)
                {
                    canonicalId = id;
                }
                else if (cKey == "typeorm" || name.Equals("typeorm", StringComparison.OrdinalIgnoreCase))
                {
                    canonicalId = "workspace:database:relational:typeorm";
                }
                else if (isStandaloneDb)
                {
                    canonicalId = id;
                }
                else if (id.StartsWith("workspace:database:", StringComparison.OrdinalIgnoreCase) ||
                         id.StartsWith("ws:database:", StringComparison.OrdinalIgnoreCase) ||
                         id.Contains(":project:", StringComparison.OrdinalIgnoreCase) ||
                         id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase) ||
                         id.StartsWith("ws:project:", StringComparison.OrdinalIgnoreCase))
                {
                    canonicalId = $"workspace:database:{cType.ToLowerInvariant()}:{cKey.ToLowerInvariant()}";
                }
                else
                {
                    canonicalId = id;
                }
                dbIdToCanonicalId[id] = canonicalId;

                if (!nodeMap.TryGetValue(canonicalId, out var existingDbNode))
                {
                    props["entity_type"] = "database";
                    props["db_type"] = cType;
                    props["role"] = "database";
                    props["is_canonical"] = "true";
                    props["is_semantic_entity"] = "true";
                    props["is_library"] = "false";
                    if (!props.ContainsKey("layer"))
                    {
                        props["layer"] = StandardLayers.Foundation.LayerId;
                        props["layerId"] = StandardLayers.Foundation.LayerId;
                        props["layerName"] = StandardLayers.Foundation.LayerName;
                        props["layerOrder"] = StandardLayers.Foundation.Order.ToString();
                        props["layerColor"] = StandardLayers.Foundation.Color;
                        props["layerIcon"] = StandardLayers.Foundation.Icon;
                    }
                    var engine = elem.GetStringProp("engine", props.GetValueOrDefault("engine", ""));
                    if (!string.IsNullOrEmpty(engine)) props["engine"] = engine;

                    var dispName = elem.GetStringProp("display_name");
                    if (string.IsNullOrEmpty(dispName))
                    {
                        dispName = !string.IsNullOrEmpty(engine) && !engine.Equals(cName, StringComparison.OrdinalIgnoreCase)
                            ? $"{cName} ({engine}) [{cType}]"
                            : $"{cName} [{cType}]";
                    }

                    var node = new GraphNodeDto
                    {
                        Id = canonicalId,
                        Kind = "Database",
                        Name = cName,
                        DisplayName = dispName,
                        Properties = props
                    };
                    nodeMap[canonicalId] = node;
                    graph.Nodes.Add(node);
                }
                else
                {
                    var engine = elem.GetStringProp("engine", props.GetValueOrDefault("engine", ""));
                    if (!string.IsNullOrEmpty(engine) && existingDbNode.Properties != null && !existingDbNode.Properties.ContainsKey("engine"))
                    {
                        existingDbNode.Properties["engine"] = engine;
                        if (existingDbNode.DisplayName != null && !existingDbNode.DisplayName.Contains(engine, StringComparison.OrdinalIgnoreCase))
                        {
                            existingDbNode.DisplayName = $"{existingDbNode.Name} ({engine}) [{existingDbNode.Properties.GetValueOrDefault("db_type")}]";
                        }
                    }
                }
            }
            else if (kind == "Package")
            {
                if (nodeMap.ContainsKey(id)) continue;
                var ver = elem.GetStringProp("version", props.GetValueOrDefault("version", ""));
                var pType = elem.GetStringProp("package_type", elem.GetStringProp("pkg_type", props.GetValueOrDefault("package_type", "package")));
                props["is_library"] = "true";
                props["is_external"] = "true";
                props["entity_type"] = "library";
                props["is_semantic_entity"] = "false";
                props["package_type"] = pType;
                if (!string.IsNullOrEmpty(ver)) props["version"] = ver;
                if (!props.ContainsKey("layer"))
                {
                    props["layer"] = StandardLayers.Foundation.LayerId;
                    props["layerId"] = StandardLayers.Foundation.LayerId;
                    props["layerName"] = StandardLayers.Foundation.LayerName;
                    props["layerColor"] = StandardLayers.Foundation.Color;
                }
                var dispName = elem.GetStringProp("display_name");
                if (string.IsNullOrEmpty(dispName))
                {
                    dispName = string.IsNullOrEmpty(ver) ? name : $"{name}@{ver}";
                }
                var pkgNode = new GraphNodeDto
                {
                    Id = id,
                    Kind = "Package",
                    Name = name,
                    DisplayName = dispName,
                    Properties = props
                };
                nodeMap[id] = pkgNode;
                graph.Nodes.Add(pkgNode);
            }
            else if (kind == "Topic")
            {
                if (nodeMap.ContainsKey(id)) continue;
                var broker = elem.GetStringProp("broker_type", props.GetValueOrDefault("broker_type", "Topic"));
                props["broker_type"] = broker;
                props["role"] = "topic";
                props["entity_type"] = "topic";
                props["is_semantic_entity"] = "true";
                props["is_library"] = "false";
                var dispName = elem.GetStringProp("display_name");
                if (string.IsNullOrEmpty(dispName))
                {
                    dispName = $"{name} [{broker}]";
                }
                var topicNode = new GraphNodeDto
                {
                    Id = id,
                    Kind = "Topic",
                    Name = name,
                    DisplayName = dispName,
                    Properties = props
                };
                nodeMap[id] = topicNode;
                graph.Nodes.Add(topicNode);
            }
            else if (kind == "ExternalService")
            {
                if (nodeMap.ContainsKey(id)) continue;
                var st = elem.GetStringProp("service_type", props.GetValueOrDefault("service_type", "Service"));
                var isMsg = st.Equals("MessageBroker", StringComparison.OrdinalIgnoreCase) ||
                            st.Equals("Kafka", StringComparison.OrdinalIgnoreCase) ||
                            st.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase);
                props["service_type"] = st;
                props["entity_type"] = isMsg ? "topic" : "external";
                props["is_semantic_entity"] = "true";
                props["is_library"] = "false";
                var dispName = elem.GetStringProp("display_name");
                if (string.IsNullOrEmpty(dispName))
                {
                    dispName = $"{name} [{st}]";
                }
                var esNode = new GraphNodeDto
                {
                    Id = id,
                    Kind = "ExternalService",
                    Name = name,
                    DisplayName = dispName,
                    Properties = props
                };
                nodeMap[id] = esNode;
                graph.Nodes.Add(esNode);
            }
            else
            {
                // Project
                if (nodeMap.ContainsKey(id)) continue;
                var dispName = elem.GetStringProp("display_name");
                if (string.IsNullOrEmpty(dispName))
                {
                    dispName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})";
                }
                var projNode = new GraphNodeDto
                {
                    Id = id,
                    Kind = "Project",
                    Name = name,
                    DisplayName = dispName,
                    FilePath = string.IsNullOrEmpty(path) ? null : path,
                    Properties = props
                };
                var isLibProject = IsLibraryProject(projNode);
                if (!includeLibraries && isLibProject)
                {
                    continue;
                }
                props["is_library"] = isLibProject ? "true" : "false";
                props["entity_type"] = isLibProject ? "library" : "service";
                props["is_semantic_entity"] = isLibProject ? "false" : "true";

                nodeMap[id] = projNode;
                graph.Nodes.Add(projNode);
            }
        }

        // Fallback package count query if needed
        var needsPkgCount = graph.Nodes.Any(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase) && (n.Properties == null || !n.Properties.ContainsKey("package_count")));
        if (needsPkgCount)
        {
            try
            {
                var pkgCountQuery = "MATCH (p:Project)-[:DEPENDS_ON]->(pkg:Package) RETURN p.id AS projId, count(pkg) AS pkgCount";
                var pkgCountJson = await db.ExecuteQueryAsync(pkgCountQuery, null, ct);
                using var pkgCountDoc = JsonDocument.Parse(pkgCountJson);
                foreach (var row in pkgCountDoc.RootElement.EnumerateArray())
                {
                    var projId = row.GetStringProp("projId");
                    var count = row.TryGetProperty("pkgCount", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt64() : 0;
                    if (!string.IsNullOrEmpty(projId) && nodeMap.TryGetValue(projId, out var pNode) && pNode.Properties != null)
                    {
                        pNode.Properties["package_count"] = count.ToString();
                    }
                }
            }
            catch { }
        }

        var projectNodes = graph.Nodes.Where(n => n.Kind.Equals(OntologyConstants.NodeLabels.Project, StringComparison.OrdinalIgnoreCase)).ToList();

        // Query macro relationships: INTEGRATES_WITH, USES_DB, TRIGGERS, PUBLISHES_TO, DEPENDS_ON, SERVICE_CALL, CALLS_ENDPOINT, LIBRARY
        var edgesQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['SERVICE_CALL', 'DEPENDS_ON', 'USES_DB', 'PUBLISHES_TO', 'TRIGGERS', 'SUBSCRIBES_TO', 'INTEGRATES_WITH', 'CALLS_ENDPOINT', 'LIBRARY'] RETURN src.id AS from_id, tgt.id AS to_id, r.kind AS kind, r.properties AS properties";
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

            if (!nodeMap.ContainsKey(fromId))
            {
                var owner = FindOwningProject(fromId, projectNodes);
                if (owner != null) fromId = owner.Id;
            }
            if (!nodeMap.ContainsKey(toId))
            {
                var owner = FindOwningProject(toId, projectNodes);
                if (owner != null) toId = owner.Id;
            }

            if (!nodeMap.TryGetValue(fromId, out var srcNode) || !nodeMap.TryGetValue(toId, out var tgtNode) || fromId.Equals(toId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var edgeProps = elem.ExtractProperties();
            var isTargetLib = tgtNode.Kind == "Package" || IsLibraryProject(tgtNode);

            var (category, depType, normalizedKind) = PostIndexAnalyzer.NormalizeEdgeCategory(
                rKind,
                srcNode.Kind,
                tgtNode.Kind,
                edgeProps.GetValueOrDefault("category"),
                edgeProps.GetValueOrDefault("dependency_type"),
                isTargetLib
            );

            edgeProps["category"] = category;
            edgeProps["dependency_type"] = depType;

            var outKind = category switch
            {
                "library" => "LIBRARY",
                "service_call" => (rKind == "CALLS_ENDPOINT" ? "CALLS_ENDPOINT" : "SERVICE_CALL"),
                "database" => "USES_DB",
                "messaging" => (rKind is "PUBLISHES_TO" or "SUBSCRIBES_TO" ? rKind : "TRIGGERS"),
                _ => normalizedKind
            };

            var srcIsSemantic = srcNode.Properties?.GetValueOrDefault("is_semantic_entity") == "true";
            var tgtIsSemantic = tgtNode.Properties?.GetValueOrDefault("is_semantic_entity") == "true";
            if (srcIsSemantic && tgtIsSemantic && outKind != "LIBRARY")
            {
                edgeProps["is_semantic"] = "true";
            }
            else if (!edgeProps.ContainsKey("is_semantic"))
            {
                edgeProps["is_semantic"] = "false";
            }

            if (edgeSet.Add((fromId, toId, outKind)))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{fromId}->{toId}:{outKind}",
                    Source = fromId,
                    Target = toId,
                    Kind = outKind,
                    Category = category,
                    Properties = edgeProps
                });
            }
        }

        // Synthesize project -> database edges from project prefix (for unmaterialized project-scoped databases)
        foreach (var (rawDbId, canonicalId) in dbIdToCanonicalId)
        {
            var owningProj = FindOwningProject(rawDbId, projectNodes);
            if (owningProj != null && graph.Edges.All(e => !(e.Source == owningProj.Id && e.Target == canonicalId)))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{owningProj.Id}->{canonicalId}:USES_DB",
                    Source = owningProj.Id,
                    Target = canonicalId,
                    Kind = "USES_DB",
                    Category = "database",
                    Properties = new Dictionary<string, string>
                    {
                        ["dependency_type"] = "database",
                        ["is_semantic"] = "true"
                    }
                });
            }
        }

        LiftTransitiveSemanticRelations(graph);

        var allProjects = await GetAllProjectsAsync(ct);
        graph.Metadata["allProjects"] = JsonSerializer.Serialize(allProjects);
        var projectPaths = await GetProjectPathsMapAsync(ct);
        graph.Metadata["projectPaths"] = JsonSerializer.Serialize(projectPaths);

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

        var archGraph = await GetSystemContextViewAsync(includeLibraries: true, projectFilter: null, ct);

        var centerNode = archGraph.Nodes.FirstOrDefault(n =>
            n.Kind.Equals(OntologyConstants.NodeLabels.Project, StringComparison.OrdinalIgnoreCase) &&
            (n.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
             n.Id.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
             (n.FilePath != null && n.FilePath.Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             n.Id.Equals($"workspace:project:{targetName}:", StringComparison.OrdinalIgnoreCase)));

        if (centerNode == null && allProjects.Count > 0 && targetName != allProjects[0])
        {
            targetName = allProjects[0];
            centerNode = archGraph.Nodes.FirstOrDefault(n =>
                n.Kind.Equals(OntologyConstants.NodeLabels.Project, StringComparison.OrdinalIgnoreCase) &&
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
                ["column"] = "center",
                ["role"] = "target"
            }
        };
        graph.Nodes.Add(centerDto);

        var nodeLookup = archGraph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var addedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { centerId };

        void AddNeighborNode(GraphNodeDto src, string column, string role)
        {
            if (addedNodeIds.Add(src.Id))
            {
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
                    Properties = new Dictionary<string, string>(src.Properties ?? new())
                    {
                        ["column"] = column,
                        ["role"] = role
                    }
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
                    if (tgtNode.Kind.Equals("Topic", StringComparison.OrdinalIgnoreCase) && edge.Kind.Equals("SUBSCRIBES_TO", StringComparison.OrdinalIgnoreCase))
                    {
                        AddNeighborNode(tgtNode, column: "left", role: "topic");
                        AddNeighborEdge(tgtNode.Id, centerId, "TRIGGERS", "messaging", edge.Properties);
                    }
                    else
                    {
                        var role = tgtNode.Kind switch
                        {
                            "Database" => "database",
                            "ExternalService" => "service",
                            "Topic" => "topic",
                            "Package" => "outbound",
                            _ => "outbound"
                        };
                        var cat = edge.Category ?? (tgtNode.Kind switch
                        {
                            "Database" => "database",
                            "ExternalService" => "service_call",
                            "Topic" => "messaging",
                            "Package" => "library",
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
                    var role = srcNode.Kind.Equals("Topic", StringComparison.OrdinalIgnoreCase) ? "topic" : "inbound";
                    var cat = edge.Category ?? (srcNode.Kind switch
                    {
                        "Topic" => "messaging",
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

        // Query all components declared in or persisted in this project
        var query = "MATCH (p:Project)-[:CONTAINS*1..3]->(c) WHERE (p.id = $scope OR p.name = $scope) AND labels(c)[0] IN ['Endpoint', 'EntryPoint', 'Type', 'Table', 'Query'] RETURN c.id AS id, labels(c)[0] AS kind, c.name AS name, c.path AS path";
        var resJson = await db.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["scope"] = projectScope }, ct);
        using var doc = JsonDocument.Parse(resJson);
        var compIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var id = elem.GetProperty("id").GetString()!;
            var kind = elem.GetProperty("kind").GetString()!;
            var name = elem.GetProperty("name").GetString() ?? id;
            var path = elem.TryGetProperty("path", out var pp) ? pp.GetString() : null;

            if (compIds.Add(id))
            {
                graph.Nodes.Add(new GraphNodeDto
                {
                    Id = id,
                    Name = name,
                    Kind = kind,
                    FilePath = path
                });
            }
        }

        // Query internal component relationships: CALLS, EXPOSES_ENDPOINT, PERSISTED_IN, QUERIES_DB
        if (compIds.Count > 0)
        {
            var inListStr = string.Join(", ", compIds.Select(id => $"'{id.Replace("'", "''")}'"));
            var relsQuery = $"MATCH (src)-[r]->(tgt) WHERE src.id IN [{inListStr}] AND tgt.id IN [{inListStr}] RETURN src.id AS from_id, tgt.id AS to_id, r.kind AS kind";
            var relsJson = await db.ExecuteQueryAsync(relsQuery, null, ct);
            using var relsDoc = JsonDocument.Parse(relsJson);

            foreach (var elem in relsDoc.RootElement.EnumerateArray())
            {
                var fromId = elem.GetProperty("from_id").GetString()!;
                var toId = elem.GetProperty("to_id").GetString()!;
                var kind = elem.GetProperty("kind").GetString()!;

                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{fromId}->{toId}:{kind}",
                    Source = fromId,
                    Target = toId,
                    Kind = kind
                });
            }
        }

        return graph;
    }

    public async Task<List<string>> GetAllProjectsAsync(CancellationToken ct = default)
    {
        var query = "MATCH (p:Project) RETURN DISTINCT p.name AS name ORDER BY p.name";
        var json = await db.ExecuteQueryAsync(query, null, ct);
        using var doc = JsonDocument.Parse(json);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var name = row.GetStringProp("name");
            if (!string.IsNullOrEmpty(name) && seen.Add(name))
            {
                result.Add(name);
            }
        }
        return result;
    }

    public async Task<Dictionary<string, string>> GetProjectPathsMapAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var query = "MATCH (p:Project) RETURN p.name AS name, p.path AS path, p.id AS id";
            var json = await db.ExecuteQueryAsync(query, null, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var name = row.GetStringProp("name");
                var path = row.GetStringProp("path");
                if (string.IsNullOrEmpty(path))
                {
                    var id = row.GetStringProp("id");
                    if (!string.IsNullOrEmpty(id) && id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase))
                    {
                        path = id["workspace:project:".Length..];
                    }
                }

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path) && !result.ContainsKey(name))
                {
                    result[name] = path;
                }
            }
        }
        catch { }
        return result;
    }

    public async Task<MetadataResponseDto> GetMetadataAsync(CancellationToken ct = default)
    {
        var result = new MetadataResponseDto();

        // 1. Node counts by label
        try
        {
            var nodeQuery = "MATCH (n) RETURN labels(n) AS lbl, count(n) AS cnt";
            var nodeJson = await db.ExecuteQueryAsync(nodeQuery, null, ct);
            using var nodeDoc = JsonDocument.Parse(nodeJson);
            long totalNodes = 0;
            foreach (var row in nodeDoc.RootElement.EnumerateArray())
            {
                if (row.TryGetProperty("lbl", out var lblProp) && lblProp.ValueKind == JsonValueKind.Array)
                {
                    var firstLbl = lblProp.EnumerateArray().FirstOrDefault().GetString();
                    if (!string.IsNullOrEmpty(firstLbl))
                    {
                        var cnt = row.TryGetProperty("cnt", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt64() : 0;
                        result.NodeCounts[firstLbl] = cnt;
                        totalNodes += cnt;
                    }
                }
            }
            result.TotalNodes = totalNodes;
        }
        catch { }

        // 2. Relationship counts by type
        try
        {
            var relQuery = "MATCH ()-[r]->() RETURN type(r) AS rel, count(r) AS cnt";
            var relJson = await db.ExecuteQueryAsync(relQuery, null, ct);
            using var relDoc = JsonDocument.Parse(relJson);
            long totalEdges = 0;
            foreach (var row in relDoc.RootElement.EnumerateArray())
            {
                var rel = row.GetStringProp("rel");
                var cnt = row.TryGetProperty("cnt", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt64() : 0;
                if (!string.IsNullOrEmpty(rel))
                {
                    result.RelationshipCounts[rel] = cnt;
                    totalEdges += cnt;
                }
            }
            result.TotalEdges = totalEdges;
        }
        catch { }

        return result;
    }

    public async Task<NodesResponseDto> GetNodesAsync(
        string? kind = null,
        int offset = 0,
        int limit = 50,
        string? search = null,
        CancellationToken ct = default)
    {
        if (offset < 0) offset = 0;
        if (limit <= 0) limit = 50;
        if (limit > 500) limit = 500;

        var result = new NodesResponseDto
        {
            Kind = kind ?? "",
            Offset = offset,
            Limit = limit
        };

        var safeKind = !string.IsNullOrWhiteSpace(kind) && System.Text.RegularExpressions.Regex.IsMatch(kind, "^[A-Za-z0-9_]+$")
            ? kind
            : null;

        var matchClause = safeKind != null ? $"MATCH (n:{safeKind})" : "MATCH (n)";

        var countQuery = string.IsNullOrWhiteSpace(search)
            ? $"{matchClause} RETURN count(n) AS total"
            : $"{matchClause} WHERE toLower(n.name) CONTAINS toLower('{search.Replace("'", "''")}') RETURN count(n) AS total";

        try
        {
            var countJson = await db.ExecuteQueryAsync(countQuery, null, ct);
            using var countDoc = JsonDocument.Parse(countJson);
            var firstRow = countDoc.RootElement.EnumerateArray().FirstOrDefault();
            if (firstRow.ValueKind == JsonValueKind.Object && firstRow.TryGetProperty("total", out var totProp) && totProp.ValueKind == JsonValueKind.Number)
            {
                result.Total = totProp.GetInt64();
            }
        }
        catch { }

        var whereClause = string.IsNullOrWhiteSpace(search)
            ? ""
            : $"WHERE toLower(n.name) CONTAINS toLower('{search.Replace("'", "''")}') ";

        var dataQuery = $"{matchClause} {whereClause}RETURN n.id AS id, n.name AS name, n.file_path AS file_path, n.path AS path, n.line AS line, labels(n) AS lbl, n.framework AS framework, n.method AS method, n.route AS route ORDER BY n.name ASC SKIP {offset} LIMIT {limit}";

        try
        {
            var dataJson = await db.ExecuteQueryAsync(dataQuery, null, ct);
            using var dataDoc = JsonDocument.Parse(dataJson);
            foreach (var row in dataDoc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var filePath = row.GetStringProp("file_path");
                if (string.IsNullOrEmpty(filePath))
                {
                    filePath = row.GetStringProp("path");
                }

                int? line = null;
                if (row.TryGetProperty("line", out var lp))
                {
                    if (lp.ValueKind == JsonValueKind.Number)
                    {
                        line = lp.GetInt32();
                    }
                    else if (lp.ValueKind == JsonValueKind.String && int.TryParse(lp.GetString(), out var parsedLine))
                    {
                        line = parsedLine;
                    }
                }

                string nodeKind = safeKind ?? "";
                if (string.IsNullOrEmpty(nodeKind) && row.TryGetProperty("lbl", out var lblProp) && lblProp.ValueKind == JsonValueKind.Array)
                {
                    nodeKind = lblProp.EnumerateArray().FirstOrDefault().GetString() ?? "";
                }

                var nodeDto = new GraphNodeDto
                {
                    Id = id,
                    Name = name,
                    Kind = nodeKind,
                    FilePath = string.IsNullOrEmpty(filePath) ? null : filePath,
                    LineStart = line,
                    Properties = new Dictionary<string, string>()
                };

                var framework = row.GetStringProp("framework");
                if (!string.IsNullOrEmpty(framework)) nodeDto.Properties["framework"] = framework;

                var method = row.GetStringProp("method");
                if (!string.IsNullOrEmpty(method)) nodeDto.Properties["method"] = method;

                var route = row.GetStringProp("route");
                if (!string.IsNullOrEmpty(route)) nodeDto.Properties["route"] = route;

                result.Nodes.Add(nodeDto);
            }
        }
        catch { }

        return result;
    }

    public static bool IsLibraryProject(GraphNodeDto? node)
    {
        if (node == null || !node.Kind.Equals(OntologyConstants.NodeLabels.Project, StringComparison.OrdinalIgnoreCase)) return false;

        var isLibProp = node.Properties?.GetValueOrDefault("is_library");
        if (isLibProp == "true") return true;
        if (isLibProp == "false") return false;

        var projectType = node.Properties?.GetValueOrDefault("project_type")?.ToLowerInvariant();
        if (projectType == "library") return true;

        var role = node.Properties?.GetValueOrDefault("role");
        if (role is "SharedLibrary" or "Test") return true;
        if (role is "Service" or "FrontendApp" or "Worker" or "CliTool") return false;

        var (_, isLib) = ProjectRoleDetector.DetectRole(
            "",
            Array.Empty<string>(),
            node.FilePath ?? "",
            node.Name,
            projectType ?? ""
        );
        return isLib;
    }

    public static GraphNodeDto? FindOwningProject(string sourceId, IEnumerable<GraphNodeDto> projects)
    {
        if (string.IsNullOrEmpty(sourceId)) return null;

        var projList = projects as IList<GraphNodeDto> ?? projects.ToList();
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
            if (sourceId.StartsWith(trimId + ":", StringComparison.OrdinalIgnoreCase) ||
                sourceId.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }

        // 3. File path resolution (e.g. workspace:file:action-scheduler/src/... or ws:symbol:action-scheduler/...)
        var filePath = sourceId;
        var fileIdx = filePath.IndexOf(":file:", StringComparison.OrdinalIgnoreCase);
        if (fileIdx >= 0)
        {
            filePath = filePath[(fileIdx + ":file:".Length)..];
        }
        else
        {
            var symIdx = filePath.IndexOf(":symbol:", StringComparison.OrdinalIgnoreCase);
            if (symIdx >= 0)
            {
                filePath = filePath[(symIdx + ":symbol:".Length)..];
            }
            else
            {
                var projIdx = filePath.IndexOf(":project:", StringComparison.OrdinalIgnoreCase);
                if (projIdx >= 0)
                {
                    filePath = filePath[(projIdx + ":project:".Length)..];
                }
            }
        }

        var normFilePath = filePath.Replace('\\', '/').TrimStart('/');

        // Match against project FilePath (longest match first)
        GraphNodeDto? bestMatch = null;
        int bestLen = -1;
        foreach (var p in projList)
        {
            var pPath = (p.FilePath ?? p.Properties?.GetValueOrDefault("path") ?? "").Replace('\\', '/').TrimStart('/').TrimEnd('/');
            if (string.IsNullOrEmpty(pPath) || pPath == ".")
            {
                // Root project matches everything with length 0 as fallback
                if (bestLen < 0)
                {
                    bestMatch = p;
                    bestLen = 0;
                }
                continue;
            }

            if (normFilePath.StartsWith(pPath + "/", StringComparison.OrdinalIgnoreCase) ||
                normFilePath.Equals(pPath, StringComparison.OrdinalIgnoreCase))
            {
                if (pPath.Length > bestLen)
                {
                    bestLen = pPath.Length;
                    bestMatch = p;
                }
            }
        }
        if (bestMatch != null && bestLen > 0) return bestMatch;

        // Match by Project Name segment in path
        foreach (var p in projList)
        {
            if (!string.IsNullOrEmpty(p.Name) && p.Name.Length > 2)
            {
                if (normFilePath.StartsWith(p.Name + "/", StringComparison.OrdinalIgnoreCase) ||
                    normFilePath.Contains("/" + p.Name + "/"))
                {
                    return p;
                }
            }
        }

        if (bestMatch != null) return bestMatch;

        return null;
    }

    public static void LiftTransitiveSemanticRelations(GraphDataDto graph)
    {
        var nodesById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var services = graph.Nodes.Where(n => n.Kind.Equals(OntologyConstants.NodeLabels.Project, StringComparison.OrdinalIgnoreCase) && !IsLibraryProject(n)).ToList();
        var libraries = graph.Nodes.Where(n => n.Kind.Equals(OntologyConstants.NodeLabels.Project, StringComparison.OrdinalIgnoreCase) && IsLibraryProject(n)).ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

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
                        if (srcNode.Kind.Equals(OntologyConstants.NodeLabels.Topic, StringComparison.OrdinalIgnoreCase) || inEdge.Kind == "TRIGGERS")
                        {
                            if (graph.Edges.All(e => !(e.Source == srcNode.Id && e.Target == service.Id && e.Kind == "TRIGGERS")))
                            {
                                graph.Edges.Add(new GraphEdgeDto
                                {
                                    Id = $"{srcNode.Id}->{service.Id}:TRIGGERS",
                                    Source = srcNode.Id,
                                    Target = service.Id,
                                    Kind = "TRIGGERS",
                                    Category = "messaging",
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
                        if (targetNode.Kind.Equals(OntologyConstants.NodeLabels.Database, StringComparison.OrdinalIgnoreCase) || edge.Kind == "USES_DB")
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
                        else if (targetNode.Kind.Equals(OntologyConstants.NodeLabels.Project, StringComparison.OrdinalIgnoreCase) && !IsLibraryProject(targetNode) && targetNode.Id != service.Id)
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
                        else if (targetNode.Kind.Equals(OntologyConstants.NodeLabels.ExternalService, StringComparison.OrdinalIgnoreCase))
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
                        // Case 4: Library publishes to Topic -> Lift direct TRIGGERS
                        else if (targetNode.Kind.Equals(OntologyConstants.NodeLabels.Topic, StringComparison.OrdinalIgnoreCase))
                        {
                            if (graph.Edges.All(e => !(e.Source == service.Id && e.Target == targetNode.Id && (e.Kind == "TRIGGERS" || e.Kind == "PUBLISHES_TO"))))
                            {
                                graph.Edges.Add(new GraphEdgeDto
                                {
                                    Id = $"{service.Id}->{targetNode.Id}:TRIGGERS",
                                    Source = service.Id,
                                    Target = targetNode.Id,
                                    Kind = "TRIGGERS",
                                    Category = "messaging",
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
                        // Case 5: Library depends on another Library -> continue BFS traversal up to depth 3
                        else if (libraries.TryGetValue(edge.Target, out var nextLib) && depth < 3)
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
