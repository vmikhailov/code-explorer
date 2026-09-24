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
    Component,     // C3: Component Drill-down for a selected container/project
    DomainMap,     // Bounded Contexts / Domain Microservices Map
    Tiers          // Layered Architecture Tiers (Ingress, Domain, Infra, Foundation, Tests)
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
            ArchitectureViewType.DomainMap => await GetDomainArchitectureGraphAsync(request.IncludeLibraries, ct),
            ArchitectureViewType.Tiers => await GetTieredArchitectureGraphAsync(request.IncludeLibraries, ct),
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

        // Query macro nodes: Services, Apps, Libraries, Workers, CliTools, Databases, Topics, ExternalServices, Packages
        var nodesQuery = includeLibraries
            ? "MATCH (n) WHERE labels(n)[0] IN ['Project', 'Service', 'App', 'Library', 'Worker', 'CliTool', 'Database', 'Topic', 'ExternalService'] OR (labels(n)[0] = 'Package' AND (n.is_external = 'true' OR n.is_external = true OR (n.is_external IS NULL AND NOT (n)-[:IMPLEMENTED_BY]->(:Project)))) RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.display_name AS display_name, n.path AS path, n.framework AS framework, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type, n.package_type AS package_type, n.type AS pkg_type, n.version AS version, n.properties AS properties, n.layer AS layer, n.layerId AS layerId, n.layerName AS layerName, n.layerOrder AS layerOrder, n.layerColor AS layerColor, n.layerIcon AS layerIcon, n.package_count AS package_count"
            : "MATCH (n) WHERE labels(n)[0] IN ['Service', 'App', 'Worker', 'CliTool', 'Database', 'Topic', 'ExternalService'] OR (labels(n)[0] = 'Project' AND (n.is_library <> 'true' OR n.is_library IS NULL) AND (n.role <> 'SharedLibrary' AND n.role <> 'Test' OR n.role IS NULL)) RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.display_name AS display_name, n.path AS path, n.framework AS framework, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type, n.package_type AS package_type, n.type AS pkg_type, n.version AS version, n.properties AS properties, n.layer AS layer, n.layerId AS layerId, n.layerName AS layerName, n.layerOrder AS layerOrder, n.layerColor AS layerColor, n.layerIcon AS layerIcon, n.package_count AS package_count";

        var nodesJson = await db.ExecuteQueryAsync(nodesQuery, null, ct);
        using var nodesDoc = JsonDocument.Parse(nodesJson);

        foreach (var elem in nodesDoc.RootElement.EnumerateArray())
        {
            var id = elem.GetStringProp("id");
            var kind = elem.GetStringProp("kind");
            var name = elem.GetStringProp("name", id);

            if (string.IsNullOrEmpty(id)) continue;

            if (!string.IsNullOrWhiteSpace(projectFilter) && IsProjectNodeKind(kind) && !name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
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
                else if (isStandaloneDb)
                {
                    canonicalId = id;
                }
                else
                {
                    canonicalId = $"workspace:database:{cType.ToLowerInvariant()}:{cKey.ToLowerInvariant()}";
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
                if (!props.ContainsKey("layer"))
                {
                    props["layer"] = StandardLayers.Foundation.LayerId;
                    props["layerId"] = StandardLayers.Foundation.LayerId;
                    props["layerName"] = StandardLayers.Foundation.LayerName;
                    props["layerOrder"] = StandardLayers.Foundation.Order.ToString();
                    props["layerColor"] = StandardLayers.Foundation.Color;
                    props["layerIcon"] = StandardLayers.Foundation.Icon;
                }
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
                if (!props.ContainsKey("layer"))
                {
                    var targetLayer = isMsg ? StandardLayers.Foundation : StandardLayers.Egress;
                    props["layer"] = targetLayer.LayerId;
                    props["layerId"] = targetLayer.LayerId;
                    props["layerName"] = targetLayer.LayerName;
                    props["layerOrder"] = targetLayer.Order.ToString();
                    props["layerColor"] = targetLayer.Color;
                    props["layerIcon"] = targetLayer.Icon;
                }
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
                    Kind = !string.IsNullOrEmpty(kind) ? kind : "Project",
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
                if (!props.ContainsKey("layer"))
                {
                    var defaultLayer = isLibProject ? StandardLayers.Foundation : StandardLayers.Components;
                    props["layer"] = defaultLayer.LayerId;
                    props["layerId"] = defaultLayer.LayerId;
                    props["layerName"] = defaultLayer.LayerName;
                    props["layerOrder"] = defaultLayer.Order.ToString();
                    props["layerColor"] = defaultLayer.Color;
                    props["layerIcon"] = defaultLayer.Icon;
                }

                nodeMap[id] = projNode;
                graph.Nodes.Add(projNode);
            }
        }

        // Fallback package count query if needed
        var needsPkgCount = graph.Nodes.Any(n => IsProjectNodeKind(n.Kind) && (n.Properties == null || !n.Properties.ContainsKey("package_count")));
        if (needsPkgCount)
        {
            try
            {
                var pkgCountQuery = "MATCH (p)-[:DEPENDS_ON]->(pkg:Package) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) RETURN p.id AS projId, count(pkg) AS pkgCount";
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

        var projectNodes = graph.Nodes.Where(n => IsProjectNodeKind(n.Kind)).ToList();

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
            IsProjectNodeKind(n.Kind) &&
            (n.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
             (n.DisplayName != null && n.DisplayName.Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             n.Id.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
             (n.FilePath != null && n.FilePath.Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             n.Id.Equals($"workspace:project:{targetName}:", StringComparison.OrdinalIgnoreCase) ||
             (n.Id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase) &&
              n.Id["workspace:project:".Length..].TrimEnd(':').Equals(targetName, StringComparison.OrdinalIgnoreCase))));

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
        var query = "MATCH (p)-[:CONTAINS*1..3]->(c) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) AND (p.id = $scope OR p.name = $scope) AND labels(c)[0] IN ['Endpoint', 'EntryPoint', 'Type', 'Table', 'Query'] RETURN c.id AS id, labels(c)[0] AS kind, c.name AS name, c.path AS path";
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

                var (category, depType, normalizedKind) = PostIndexAnalyzer.NormalizeEdgeCategory(kind, null, null);

                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{fromId}->{toId}:{normalizedKind}",
                    Source = fromId,
                    Target = toId,
                    Kind = normalizedKind,
                    Category = category,
                    Properties = new Dictionary<string, string>
                    {
                        ["category"] = category,
                        ["dependency_type"] = depType
                    }
                });
            }
        }

        return graph;
    }

    public async Task<List<string>> GetAllProjectsAsync(CancellationToken ct = default)
    {
        var query = "MATCH (p) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) RETURN DISTINCT p.name AS name ORDER BY p.name";
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
            var query = "MATCH (p) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) RETURN p.name AS name, p.path AS path, p.id AS id";
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
        string? service = null,
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

        if (string.Equals(safeKind, "all", StringComparison.OrdinalIgnoreCase))
        {
            safeKind = null;
        }

        string? serviceId = null;
        string? serviceNameResolved = null;
        if (!string.IsNullOrWhiteSpace(service))
        {
            try
            {
                var sQuery = "MATCH (s) WHERE (s.name = $srv OR s.id = $srv OR s.id = 'workspace:project:' + $srv) AND (s:Project OR s:Service OR s:App OR s:Worker OR s:CliTool) RETURN s.id AS id, coalesce(s.name, s.id) AS name LIMIT 1";
                var sJson = await db.ExecuteQueryAsync(sQuery, new Dictionary<string, object> { ["srv"] = service }, ct);
                using var sDoc = JsonDocument.Parse(sJson);
                var first = sDoc.RootElement.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    serviceId = first.GetStringProp("id");
                    serviceNameResolved = first.GetStringProp("name");
                }
            }
            catch { }
            serviceId ??= service;
            serviceNameResolved ??= service;
        }

        var isRel = safeKind != null && (
            safeKind.StartsWith("Layer5", StringComparison.OrdinalIgnoreCase) ||
            (safeKind.Length > 1 && safeKind.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c)))
        );

        if (isRel)
        {
            var relType = safeKind != null && !safeKind.StartsWith("Layer5", StringComparison.OrdinalIgnoreCase)
                ? safeKind
                : null;

            var matchRel = relType != null
                ? $"MATCH (src)-[r:{relType}]->(tgt)"
                : "MATCH (src)-[r]->(tgt)";

            var sFilter = serviceId != null
                ? $"((src.name = '{serviceNameResolved!.Replace("'", "''")}' OR src.id = '{serviceId.Replace("'", "''")}') OR (tgt.name = '{serviceNameResolved!.Replace("'", "''")}' OR tgt.id = '{serviceId.Replace("'", "''")}'))"
                : null;

            var searchRel = string.IsNullOrWhiteSpace(search)
                ? (sFilter != null ? $"WHERE {sFilter} " : "")
                : (sFilter != null
                    ? $"WHERE {sFilter} AND (toLower(src.name) CONTAINS toLower('{search.Replace("'", "''")}') OR toLower(tgt.name) CONTAINS toLower('{search.Replace("'", "''")}')) "
                    : $"WHERE toLower(src.name) CONTAINS toLower('{search.Replace("'", "''")}') OR toLower(tgt.name) CONTAINS toLower('{search.Replace("'", "''")}') ");

            var countQuery = $"{matchRel} {searchRel}RETURN count(r) AS total";
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

            var relDataQuery = $"{matchRel} {searchRel}RETURN coalesce(r.id, src.name + '->' + tgt.name) AS id, coalesce(r.name, src.name + ' ➔ ' + tgt.name) AS name, type(r) AS relType, src.name AS srcName, tgt.name AS tgtName, coalesce(r.file_path, src.file_path) AS file_path, coalesce(r.line, src.line) AS line ORDER BY src.name ASC, tgt.name ASC SKIP {offset} LIMIT {limit}";

            try
            {
                var dataJson = await db.ExecuteQueryAsync(relDataQuery, null, ct);
                using var dataDoc = JsonDocument.Parse(dataJson);
                foreach (var row in dataDoc.RootElement.EnumerateArray())
                {
                    var id = row.GetStringProp("id");
                    var name = row.GetStringProp("name");
                    var relTypeVal = row.GetStringProp("relType");
                    var srcName = row.GetStringProp("srcName");
                    var tgtName = row.GetStringProp("tgtName");
                    var filePath = row.GetStringProp("file_path");
                    int? line = null;
                    if (row.TryGetProperty("line", out var lp) && lp.ValueKind == JsonValueKind.Number)
                    {
                        line = lp.GetInt32();
                    }

                    var dto = new GraphNodeDto
                    {
                        Id = id,
                        Name = string.IsNullOrEmpty(name) ? $"{srcName} ➔ {tgtName}" : name,
                        DisplayName = $"{srcName} ➔ {tgtName}",
                        Kind = string.IsNullOrEmpty(relTypeVal) ? (relType ?? "RELATIONSHIP") : relTypeVal,
                        FilePath = filePath,
                        LineStart = line,
                        Properties = new Dictionary<string, string>
                        {
                            ["source"] = srcName,
                            ["target"] = tgtName,
                            ["type"] = string.IsNullOrEmpty(relTypeVal) ? (relType ?? "RELATIONSHIP") : relTypeVal
                        }
                    };
                    result.Nodes.Add(dto);
                }
            }
            catch { }

            return result;
        }

        string? layerFilter = null;
        if (string.Equals(safeKind, "Layer1", StringComparison.OrdinalIgnoreCase))
        {
            layerFilter = "WHERE (n:File OR n:Folder OR n:GitSettings)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Layer2", StringComparison.OrdinalIgnoreCase))
        {
            layerFilter = "WHERE (n:Project OR n:Library OR n:SharedLibrary OR n:Package OR n:Service OR n:App OR n:Worker)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Layer3", StringComparison.OrdinalIgnoreCase))
        {
            layerFilter = "WHERE (n:Type OR n:Function OR n:Member)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Layer4", StringComparison.OrdinalIgnoreCase))
        {
            layerFilter = "WHERE (n:Service OR n:App OR n:Worker OR n:CliTool OR n:EntryPoint OR n:Endpoint OR n:Procedure OR n:Database OR n:Table OR n:DataSet OR n:Topic OR n:ExternalService OR n:CloudService OR n:ApiInUse OR n:Query)";
            safeKind = null;
        }

        var matchClause = safeKind != null ? $"MATCH (n:{safeKind})" : "MATCH (n)";

        string countQueryFinal;
        string whereClause;

        if (serviceId != null)
        {
            var sIdEsc = serviceId.Replace("'", "''");
            var sNameEsc = (serviceNameResolved ?? serviceId).Replace("'", "''");
            if (string.Equals(safeKind, "Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (s)-[:CONTAINS]->(n:Endpoint) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}')";
            }
            else if (string.Equals(safeKind, "Database", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (s)-[:USES_DB|CONTAINS]->(n:Database) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}')";
            }
            else if (string.Equals(safeKind, "Topic", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (s)-[:PUBLISHES_TO|SUBSCRIBED_BY|SUBSCRIBES_TO|TRIGGERS]-(n:Topic) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}')";
            }
            else if (string.Equals(safeKind, "ExternalService", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (s)-[:CALLS_ENDPOINT|SERVICE_CALL|INTEGRATES_WITH]->(n) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}') AND (n:ExternalService OR n:CloudService)";
            }
            else if (string.Equals(safeKind, "Service", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (n) WHERE (n.id = '{sIdEsc}' OR n.name = '{sNameEsc}')";
            }
            else
            {
                matchClause = $"MATCH (s)-[r]->(n) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}') AND (n:Endpoint OR n:Database OR n:Topic OR n:ExternalService OR n:CloudService OR n:File OR n:Type)";
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                whereClause = $"AND toLower(n.name) CONTAINS toLower('{search.Replace("'", "''")}') ";
            }
            else
            {
                whereClause = "";
            }
            countQueryFinal = $"{matchClause} {whereClause}RETURN count(DISTINCT n) AS total";
        }
        else if (layerFilter != null)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                whereClause = $"{layerFilter} ";
                countQueryFinal = $"{matchClause} {whereClause}RETURN count(n) AS total";
            }
            else
            {
                whereClause = $"{layerFilter} AND toLower(n.name) CONTAINS toLower('{search.Replace("'", "''")}') ";
                countQueryFinal = $"{matchClause} {whereClause}RETURN count(n) AS total";
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                whereClause = "";
                countQueryFinal = $"{matchClause} RETURN count(n) AS total";
            }
            else
            {
                whereClause = $"WHERE toLower(n.name) CONTAINS toLower('{search.Replace("'", "''")}') ";
                countQueryFinal = $"{matchClause} {whereClause}RETURN count(n) AS total";
            }
        }

        try
        {
            var countJson = await db.ExecuteQueryAsync(countQueryFinal, null, ct);
            using var countDoc = JsonDocument.Parse(countJson);
            var firstRow = countDoc.RootElement.EnumerateArray().FirstOrDefault();
            if (firstRow.ValueKind == JsonValueKind.Object && firstRow.TryGetProperty("total", out var totProp) && totProp.ValueKind == JsonValueKind.Number)
            {
                result.Total = totProp.GetInt64();
            }
        }
        catch { }

        var dataQuery = $"{matchClause} {whereClause}RETURN DISTINCT n.id AS id, n.name AS name, n.file_path AS file_path, n.path AS path, n.line AS line, labels(n) AS lbl, n.framework AS framework, n.method AS method, n.route AS route, n.protocol AS protocol ORDER BY n.name ASC SKIP {offset} LIMIT {limit}";


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

                var protocol = row.GetStringProp("protocol");
                if (!string.IsNullOrEmpty(protocol)) nodeDto.Properties["protocol"] = protocol;

                result.Nodes.Add(nodeDto);
            }
        }
        catch { }

        return result;
    }

    public static bool IsProjectNodeKind(string? kind) =>
        kind != null && (
            kind.Equals("Project", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("Service", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("App", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("FrontendApp", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("Worker", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("CliTool", StringComparison.OrdinalIgnoreCase));

    public static bool IsLibraryProject(GraphNodeDto? node)
    {
        if (node == null) return false;
        if (node.Kind.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
            node.Kind.Equals("SharedLibrary", StringComparison.OrdinalIgnoreCase))
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
        var services = graph.Nodes.Where(n => IsProjectNodeKind(n.Kind) && !IsLibraryProject(n)).ToList();
        var libraries = graph.Nodes.Where(n => IsProjectNodeKind(n.Kind) && IsLibraryProject(n)).ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

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
                        else if (IsProjectNodeKind(targetNode.Kind) && !IsLibraryProject(targetNode) && targetNode.Id != service.Id)
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

    // =========================================================================
    // 5. Ontology Layers & Taxonomy
    // =========================================================================

    public async Task<OntologyLayersResponseDto> GetOntologyLayersAsync(CancellationToken ct = default)
    {
        var meta = await GetMetadataAsync(ct);
        var counts = meta.NodeCounts;
        var relCounts = meta.RelationshipCounts;

        var l1Categories = new List<OntologyCategoryDto>
        {
            new() { Kind = "File", Label = "Files", Icon = "file-code", Count = counts.GetValueOrDefault("File", 0), LayerId = 1 },
            new() { Kind = "Folder", Label = "Folders", Icon = "folder", Count = counts.GetValueOrDefault("Folder", 0), LayerId = 1 },
            new() { Kind = "GitSettings", Label = "Git Settings", Icon = "git-commit", Count = counts.GetValueOrDefault("GitSettings", 0), LayerId = 1 }
        };

        var l2Categories = new List<OntologyCategoryDto>();
        if (counts.GetValueOrDefault("Project", 0) > 0 || counts.GetValueOrDefault("Library", 0) == 0)
        {
            l2Categories.Add(new() { Kind = "Project", Label = "Projects", Icon = "project", Count = counts.GetValueOrDefault("Project", 0), LayerId = 2 });
        }
        if (counts.GetValueOrDefault("Service", 0) > 0)
        {
            l2Categories.Add(new() { Kind = "Service", Label = "Services", Icon = "server-process", Count = counts.GetValueOrDefault("Service", 0), LayerId = 2 });
        }
        if (counts.GetValueOrDefault("App", 0) > 0)
        {
            l2Categories.Add(new() { Kind = "App", Label = "Applications", Icon = "browser", Count = counts.GetValueOrDefault("App", 0), LayerId = 2 });
        }
        if (counts.GetValueOrDefault("Worker", 0) > 0)
        {
            l2Categories.Add(new() { Kind = "Worker", Label = "Workers", Icon = "gear", Count = counts.GetValueOrDefault("Worker", 0), LayerId = 2 });
        }
        if (counts.GetValueOrDefault("Library", 0) > 0 || counts.GetValueOrDefault("SharedLibrary", 0) > 0)
        {
            l2Categories.Add(new() { Kind = "Library", Label = "Libraries & SDKs", Icon = "library", Count = counts.GetValueOrDefault("Library", 0) + counts.GetValueOrDefault("SharedLibrary", 0), LayerId = 2 });
        }
        l2Categories.Add(new() { Kind = "Package", Label = "Packages & Dependencies", Icon = "package", Count = counts.GetValueOrDefault("Package", 0), LayerId = 2 });

        var l3Categories = new List<OntologyCategoryDto>
        {
            new() { Kind = "Type", Label = "Types (Classes, Interfaces)", Icon = "symbol-class", Count = counts.GetValueOrDefault("Type", 0), LayerId = 3 },
            new() { Kind = "Function", Label = "Functions & Methods", Icon = "symbol-method", Count = counts.GetValueOrDefault("Function", 0), LayerId = 3 },
            new() { Kind = "Member", Label = "Members & Fields", Icon = "symbol-field", Count = counts.GetValueOrDefault("Member", 0), LayerId = 3 }
        };

        var totalServiceWorkloads = counts.GetValueOrDefault("Service", 0) +
                                    counts.GetValueOrDefault("App", 0) +
                                    counts.GetValueOrDefault("FrontendApp", 0) +
                                    counts.GetValueOrDefault("Worker", 0) +
                                    counts.GetValueOrDefault("CliTool", 0);

        var l4Categories = new List<OntologyCategoryDto>
        {
            new() { Kind = "Service", Label = "Services & Workloads", Icon = "server-process", Count = totalServiceWorkloads, LayerId = 4 },
            new() { Kind = "Endpoint", Label = "API Endpoints (REST, gRPC, WS)", Icon = "radio-tower", Count = counts.GetValueOrDefault("Endpoint", 0), LayerId = 4 },
            new() { Kind = "Database", Label = "Databases & Storage", Icon = "database", Count = counts.GetValueOrDefault("Database", 0) + counts.GetValueOrDefault("Table", 0), LayerId = 4 },
            new() { Kind = "Topic", Label = "Message Topics & Queues", Icon = "mail", Count = counts.GetValueOrDefault("Topic", 0), LayerId = 4 },
            new() { Kind = "ExternalService", Label = "External & Cloud APIs", Icon = "cloud", Count = counts.GetValueOrDefault("ExternalService", 0) + counts.GetValueOrDefault("CloudService", 0), LayerId = 4 }
        };
        if (counts.GetValueOrDefault("EntryPoint", 0) > 0)
        {
            l4Categories.Add(new() { Kind = "EntryPoint", Label = "Execution EntryPoints", Icon = "sign-in", Count = counts.GetValueOrDefault("EntryPoint", 0), LayerId = 4 });
        }
        if (counts.GetValueOrDefault("Procedure", 0) > 0)
        {
            l4Categories.Add(new() { Kind = "Procedure", Label = "Stored Procedures", Icon = "database", Count = counts.GetValueOrDefault("Procedure", 0), LayerId = 4 });
        }
        if (counts.GetValueOrDefault("Query", 0) > 0)
        {
            l4Categories.Add(new() { Kind = "Query", Label = "SQL Queries", Icon = "search", Count = counts.GetValueOrDefault("Query", 0), LayerId = 4 });
        }

        var topRels = new[] { "CALLS", "DEPENDS_ON", "EXPOSED_BY", "TRIGGERS", "QUERIED_BY", "PUBLISHED_BY", "SUBSCRIBED_BY", "INTEGRATES_WITH", "USES_DB", "IMPLEMENTS", "INHERITS_FROM", "USES_TYPE" };
        var l5Categories = new List<OntologyCategoryDto>();
        foreach (var rel in topRels)
        {
            var cnt = relCounts.GetValueOrDefault(rel, 0);
            if (cnt > 0 || rel is "CALLS" or "DEPENDS_ON" or "INTEGRATES_WITH" or "USES_DB")
            {
                l5Categories.Add(new() { Kind = rel, Label = rel, Icon = "arrow-right", Count = cnt, LayerId = 5 });
            }
        }

        var l1 = new OntologyLayerDto
        {
            LayerId = 1,
            Name = "Physical Topology",
            Title = "Layer 1: Physical Topology",
            Description = "Files, Folders, and Git configuration",
            Icon = "folder-library",
            TotalCount = l1Categories.Sum(c => c.Count),
            Categories = l1Categories
        };

        var l2 = new OntologyLayerDto
        {
            LayerId = 2,
            Name = "Project Boundary",
            Title = "Layer 2: Project Boundary",
            Description = "Logical compilation scopes, projects, and external packages",
            Icon = "project",
            TotalCount = l2Categories.Sum(c => c.Count),
            Categories = l2Categories
        };

        var l3 = new OntologyLayerDto
        {
            LayerId = 3,
            Name = "Syntactic AST",
            Title = "Layer 3: Syntactic AST",
            Description = "Abstract Syntax Tree declarations (Types, Methods, Fields)",
            Icon = "symbol-structure",
            TotalCount = l3Categories.Sum(c => c.Count),
            Categories = l3Categories
        };

        var l4 = new OntologyLayerDto
        {
            LayerId = 4,
            Name = "Semantic Runtime",
            Title = "Layer 4: Semantic Runtime",
            Description = "Runtime architecture (Services, Endpoints, Databases, Topics, External APIs)",
            Icon = "radio-tower",
            TotalCount = l4Categories.Sum(c => c.Count),
            Categories = l4Categories
        };

        var l5 = new OntologyLayerDto
        {
            LayerId = 5,
            Name = "System Bindings",
            Title = "Layer 5: System Bindings",
            Description = "Cross-project late-bound relationships (CALLS, IMPLEMENTS, USES_DB, INTEGRATES_WITH)",
            Icon = "references",
            TotalCount = meta.TotalEdges > 0 ? meta.TotalEdges : relCounts.Values.Sum(),
            Categories = l5Categories
        };

        return new OntologyLayersResponseDto
        {
            Layers = [l1, l2, l3, l4, l5],
            TotalNodes = meta.TotalNodes,
            TotalEdges = meta.TotalEdges
        };
    }

    public async Task<List<ServiceSummaryDto>> GetServicesOntologySummaryAsync(CancellationToken ct = default)
    {
        var list = new List<ServiceSummaryDto>();
        try
        {
            var query = "MATCH (s) WHERE (s:Project OR s:Service OR s:App OR s:Worker OR s:CliTool OR s:FrontendApp) RETURN DISTINCT s.id AS id, coalesce(s.name, s.id) AS name, labels(s) AS lbl, s.framework AS framework, s.language AS language ORDER BY s.name ASC";
            var json = await db.ExecuteQueryAsync(query, null, ct);
            using var doc = JsonDocument.Parse(json);
            var serviceMap = new Dictionary<string, ServiceSummaryDto>(StringComparer.OrdinalIgnoreCase);
            var idToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var kind = "Service";
                if (row.TryGetProperty("lbl", out var lblProp) && lblProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var l in lblProp.EnumerateArray())
                    {
                        var ls = l.GetString();
                        if (ls is "Service" or "Worker" or "App" or "CliTool" or "FrontendApp")
                        {
                            kind = ls;
                            break;
                        }
                    }
                }

                if (!serviceMap.ContainsKey(name))
                {
                    var dto = new ServiceSummaryDto
                    {
                        ServiceId = id,
                        ServiceName = name,
                        Kind = kind,
                        Framework = row.GetStringProp("framework"),
                        Language = row.GetStringProp("language")
                    };
                    serviceMap[name] = dto;
                }
                idToName[id] = name;
            }

            // Tally endpoints directly from graph
            try
            {
                var epCountQuery = "MATCH (s)-[:CONTAINS]->(ep:Endpoint) WHERE (s:Project OR s:Service OR s:App OR s:Worker OR s:CliTool) RETURN coalesce(s.name, s.id) AS sName, s.id AS sId, count(ep) AS epCount";
                var epJson = await db.ExecuteQueryAsync(epCountQuery, null, ct);
                using var epDoc = JsonDocument.Parse(epJson);
                foreach (var row in epDoc.RootElement.EnumerateArray())
                {
                    var sName = row.GetStringProp("sName");
                    var sId = row.GetStringProp("sId");
                    ServiceSummaryDto? summary = null;
                    if (!string.IsNullOrEmpty(sName) && serviceMap.TryGetValue(sName, out summary)) { }
                    else if (!string.IsNullOrEmpty(sId) && idToName.TryGetValue(sId, out var mappedName) && serviceMap.TryGetValue(mappedName, out summary)) { }

                    if (summary != null && row.TryGetProperty("epCount", out var cp) && cp.ValueKind == JsonValueKind.Number)
                    {
                        summary.EndpointCount = cp.GetInt32();
                    }
                }
            }
            catch { }

            // Tally databases, topics, and external APIs from system context view
            var archGraph = await GetSystemContextViewAsync(includeLibraries: true, projectFilter: null, ct: ct);
            var nodeMap = archGraph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var edge in archGraph.Edges)
            {
                var srcNode = nodeMap.GetValueOrDefault(edge.Source);
                var tgtNode = nodeMap.GetValueOrDefault(edge.Target);

                if (srcNode != null && serviceMap.TryGetValue(srcNode.Name, out var srcSummary))
                {
                    if (edge.Category == "database" || edge.Kind == "USES_DB" || tgtNode?.Kind == "Database")
                    {
                        srcSummary.DatabaseCount++;
                    }
                    else if (edge.Category == "messaging" || edge.Kind is "PUBLISHES_TO" or "TRIGGERS" || tgtNode?.Kind == "Topic")
                    {
                        srcSummary.TopicCount++;
                    }
                    else if (tgtNode?.Kind is "ExternalService" or "CloudService" || edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT")
                    {
                        srcSummary.ExternalCount++;
                    }
                }

                if (tgtNode != null && serviceMap.TryGetValue(tgtNode.Name, out var tgtSummary))
                {
                    if (edge.Category == "messaging" || edge.Kind is "SUBSCRIBED_BY" or "SUBSCRIBES_TO" || srcNode?.Kind == "Topic")
                    {
                        tgtSummary.TopicCount++;
                    }
                }
            }

            list.AddRange(serviceMap.Values.OrderBy(s => s.ServiceName));
        }
        catch
        {
        }

        return list;
    }

    public async Task<ServiceOntologyDetailsDto> GetServiceCapabilitiesAsync(string serviceName, CancellationToken ct = default)
    {
        var archGraph = await GetSystemContextViewAsync(includeLibraries: true, projectFilter: null, ct: ct);
        var targetNode = archGraph.Nodes.FirstOrDefault(n => n.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase) || n.Id.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        var details = new ServiceOntologyDetailsDto
        {
            ServiceName = targetNode?.Name ?? serviceName,
            ServiceId = targetNode?.Id ?? serviceName,
            Kind = targetNode?.Kind ?? "Service",
            Framework = targetNode?.Properties?.GetValueOrDefault("framework"),
            Language = targetNode?.Properties?.GetValueOrDefault("language") ?? targetNode?.Properties?.GetValueOrDefault("project_type")
        };

        var targetId = targetNode?.Id ?? serviceName;

        var epGroup = new ServiceOntologyGroupDto { CategoryKey = "endpoints", Label = "API Endpoints", Icon = "radio-tower" };
        var dbGroup = new ServiceOntologyGroupDto { CategoryKey = "databases", Label = "Databases & Storage", Icon = "database" };
        var topicGroup = new ServiceOntologyGroupDto { CategoryKey = "topics", Label = "Message Topics & Queues", Icon = "mail" };
        var extGroup = new ServiceOntologyGroupDto { CategoryKey = "external", Label = "External & Cloud APIs", Icon = "cloud" };

        // 1. Endpoints
        try
        {
            var epQuery = "MATCH (s)-[:CONTAINS]->(ep:Endpoint) WHERE s.id = $id OR s.name = $name RETURN ep.id AS id, ep.name AS name, ep.http_method AS method, ep.route_template AS route, ep.protocol AS protocol, coalesce(ep.file_path, ep.path) AS file_path, ep.line AS line ORDER BY ep.name ASC";
            var epJson = await db.ExecuteQueryAsync(epQuery, new Dictionary<string, object> { ["id"] = targetId, ["name"] = serviceName }, ct);
            using var epDoc = JsonDocument.Parse(epJson);
            foreach (var row in epDoc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var method = row.GetStringProp("method");
                var route = row.GetStringProp("route");
                var protocol = row.GetStringProp("protocol") ?? "REST";
                var filePath = row.GetStringProp("file_path");
                int? line = null;
                if (row.TryGetProperty("line", out var lp) && lp.ValueKind == JsonValueKind.Number) line = lp.GetInt32();

                epGroup.Items.Add(new ServiceCapabilityItemDto
                {
                    Id = id,
                    Name = name,
                    Kind = "Endpoint",
                    Method = method,
                    Route = route,
                    Protocol = protocol,
                    FilePath = filePath,
                    Line = line,
                    Details = $"[{protocol}] {method} {route}"
                });
            }
        }
        catch { }
        epGroup.Count = epGroup.Items.Count;

        // 2. Databases, Topics, and External Services from archGraph
        var seenDbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in archGraph.Edges)
        {
            if (edge.Source.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            {
                var tgtNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Target, StringComparison.OrdinalIgnoreCase));
                var tgtName = tgtNode?.Name ?? edge.Target;

                if (edge.Category == "database" || edge.Kind == "USES_DB" || tgtNode?.Kind == "Database")
                {
                    if (seenDbs.Add(tgtName))
                    {
                        dbGroup.Items.Add(new ServiceCapabilityItemDto
                        {
                            Id = tgtNode?.Id ?? edge.Target,
                            Name = tgtName,
                            Kind = "Database",
                            Details = tgtNode?.Properties?.GetValueOrDefault("engine") ?? tgtNode?.Properties?.GetValueOrDefault("db_type") ?? "database",
                            FilePath = tgtNode?.FilePath,
                            Line = tgtNode?.LineStart
                        });
                    }
                }
                else if (edge.Category == "messaging" || edge.Kind is "PUBLISHES_TO" or "TRIGGERS" || tgtNode?.Kind == "Topic")
                {
                    if (seenTopics.Add(tgtName))
                    {
                        topicGroup.Items.Add(new ServiceCapabilityItemDto
                        {
                            Id = tgtNode?.Id ?? edge.Target,
                            Name = tgtName,
                            Kind = "Topic",
                            Details = "Published topic",
                            FilePath = tgtNode?.FilePath,
                            Line = tgtNode?.LineStart
                        });
                    }
                }
                else if (tgtNode?.Kind is "ExternalService" or "CloudService" || edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT")
                {
                    if (seenExt.Add(tgtName))
                    {
                        extGroup.Items.Add(new ServiceCapabilityItemDto
                        {
                            Id = tgtNode?.Id ?? edge.Target,
                            Name = tgtName,
                            Kind = tgtNode?.Kind ?? "ExternalService",
                            Details = tgtNode?.Properties?.GetValueOrDefault("service_type") ?? "external API",
                            FilePath = tgtNode?.FilePath,
                            Line = tgtNode?.LineStart
                        });
                    }
                }
            }
            else if (edge.Target.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            {
                if (edge.Category == "messaging" || edge.Kind is "SUBSCRIBED_BY" or "SUBSCRIBES_TO" or "TRIGGERS")
                {
                    var srcNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Source, StringComparison.OrdinalIgnoreCase));
                    var topicName = srcNode?.Name ?? edge.Source;
                    if (seenTopics.Add(topicName))
                    {
                        topicGroup.Items.Add(new ServiceCapabilityItemDto
                        {
                            Id = srcNode?.Id ?? edge.Source,
                            Name = topicName,
                            Kind = "Topic",
                            Details = "Subscribed topic",
                            FilePath = srcNode?.FilePath,
                            Line = srcNode?.LineStart
                        });
                    }
                }
            }
        }

        dbGroup.Count = dbGroup.Items.Count;
        topicGroup.Count = topicGroup.Items.Count;
        extGroup.Count = extGroup.Items.Count;

        details.Groups = [epGroup, dbGroup, topicGroup, extGroup];
        return details;
    }


    // =========================================================================
    // 6. Domain Architecture (Bounded Contexts & Service Map)
    // =========================================================================

    private static readonly System.Text.RegularExpressions.Regex SubProjectSuffixRegex =
        new(@"\.(Logic|Client|Contracts|Data|Core|Domain|Infrastructure|Api|Service|Services|Web|Worker|Test|Tests|Shared|Models|Dto|SDK|UnitTests|IntegrationTests)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly string[] IngressKeywords = ["admin", "app", "ui", "fe", "gateway", "bff", "portal", "web", "client-app", "landing"];
    private static readonly string[] IngressFrameworks = ["angular", "react", "vue", "svelte", "next", "vite", "blazor", "express", "fastify"];

    public static (string DomainKey, string DomainDisplayName, bool IsIngressHint) ExtractDomainKey(GraphNodeDto node)
    {
        var name = node.Name ?? "";
        var path = (node.FilePath ?? "").Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        // 1. Suffix match
        var suffixMatch = SubProjectSuffixRegex.Match(name);
        if (suffixMatch.Success)
        {
            var parentName = name[..suffixMatch.Index];
            var dotParts = parentName.Split('.');
            var shortName = dotParts[^1];
            return ($"domain:{parentName.ToLowerInvariant()}", $"{shortName} Service", false);
        }

        // 2. Directory structure
        if (!string.IsNullOrEmpty(path))
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var servicesIdx = Array.FindIndex(parts, p => p.Equals("services", StringComparison.OrdinalIgnoreCase) || p.Equals("microservices", StringComparison.OrdinalIgnoreCase));
            if (servicesIdx != -1 && servicesIdx + 1 < parts.Length)
            {
                var folder = parts[servicesIdx + 1];
                var cleanName = char.ToUpperInvariant(folder[0]) + folder[1..];
                return ($"domain:{folder.ToLowerInvariant()}", $"{cleanName} Service", false);
            }

            if (parts.Length >= 2 && !parts[0].Equals("src", StringComparison.OrdinalIgnoreCase) && !parts[0].Equals("packages", StringComparison.OrdinalIgnoreCase) && !parts[0].Equals("libs", StringComparison.OrdinalIgnoreCase))
            {
                var folder = parts[0];
                var cleanName = char.ToUpperInvariant(folder[0]) + folder[1..];
                var isIngressHint = IngressKeywords.Any(kw => folder.Contains(kw, StringComparison.OrdinalIgnoreCase));
                return ($"domain:{folder.ToLowerInvariant()}", cleanName, isIngressHint);
            }
        }

        // 3. Standalone
        var framework = node.Properties?.GetValueOrDefault("framework", "") ?? "";
        var isIngress = node.Kind.Equals("App", StringComparison.OrdinalIgnoreCase) ||
                        node.Kind.Equals("FrontendApp", StringComparison.OrdinalIgnoreCase) ||
                        IngressKeywords.Any(kw => lowerName.Contains(kw)) ||
                        IngressFrameworks.Any(fw => framework.Contains(fw, StringComparison.OrdinalIgnoreCase));

        return ($"domain:{lowerName}", name, isIngress);
    }

    public async Task<DomainArchitectureDto> GetDomainArchitectureAsync(bool includeLibraries = true, CancellationToken ct = default)
    {
        var archGraph = await GetSystemContextViewAsync(includeLibraries: true, projectFilter: null, ct: ct);
        var result = new DomainArchitectureDto();

        var projToDomainMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainProjectsMap = new Dictionary<string, List<DomainProjectInfoDto>>(StringComparer.OrdinalIgnoreCase);
        var domainPrimaryMap = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var domainZoneMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainNameMap = new Dictionary<string, (string Name, string DisplayName, string? Framework, string? Language)>(StringComparer.OrdinalIgnoreCase);

        // 1. Categorize Project-like nodes into domains
        foreach (var node in archGraph.Nodes)
        {
            if (!IsProjectNodeKind(node.Kind)) continue;

            var (domainKey, domainDisplayName, isIngressHint) = ExtractDomainKey(node);
            projToDomainMap[node.Id] = domainKey;
            projToDomainMap[node.Name] = domainKey;

            if (!domainProjectsMap.TryGetValue(domainKey, out var pList))
            {
                pList = [];
                domainProjectsMap[domainKey] = pList;

                var isIngress = (node.Kind.Equals("App", StringComparison.OrdinalIgnoreCase) || node.Kind.Equals("FrontendApp", StringComparison.OrdinalIgnoreCase) || isIngressHint) &&
                                !node.Kind.Equals("Service", StringComparison.OrdinalIgnoreCase) &&
                                !node.Kind.Equals("Worker", StringComparison.OrdinalIgnoreCase);
                domainZoneMap[domainKey] = isIngress ? "ingress" : "service";
                domainNameMap[domainKey] = (node.Name, domainDisplayName, node.Properties?.GetValueOrDefault("framework"), node.Properties?.GetValueOrDefault("language") ?? node.Properties?.GetValueOrDefault("project_type"));
            }

            var isLib = node.Kind.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
                        node.Kind.Equals("SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
                        node.Properties?.GetValueOrDefault("is_library") == "true";

            pList.Add(new DomainProjectInfoDto
            {
                Id = node.Id,
                Name = node.Name,
                Kind = node.Kind,
                FilePath = node.FilePath,
                IsLibrary = isLib
            });

            if (!domainPrimaryMap.TryGetValue(domainKey, out var currPrimary) || (IsLibraryProject(currPrimary) && !isLib))
            {
                domainPrimaryMap[domainKey] = node;
            }
        }

        // 2. Identify Infrastructure Nodes (Databases, Topics, ExternalServices)
        var dbNodes = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var topicNodes = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var extNodes = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in archGraph.Nodes)
        {
            if (node.Kind.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                dbNodes[node.Id] = node;
                projToDomainMap[node.Id] = node.Id;
            }
            else if (node.Kind.Equals("Topic", StringComparison.OrdinalIgnoreCase))
            {
                topicNodes[node.Id] = node;
                projToDomainMap[node.Id] = node.Id;
            }
            else if (node.Kind.Equals("ExternalService", StringComparison.OrdinalIgnoreCase))
            {
                extNodes[node.Id] = node;
                projToDomainMap[node.Id] = node.Id;
            }
        }

        // 3. Aggregate Macro Edges between Domains & Infrastructure
        var macroEdges = new Dictionary<string, DomainMacroEdgeDto>(StringComparer.OrdinalIgnoreCase);
        var inCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dbUsage = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var msgUsage = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in archGraph.Edges)
        {
            var srcDomain = projToDomainMap.GetValueOrDefault(edge.Source) ?? (dbNodes.ContainsKey(edge.Source) ? edge.Source : null) ?? (topicNodes.ContainsKey(edge.Source) ? edge.Source : null);
            var tgtDomain = projToDomainMap.GetValueOrDefault(edge.Target) ?? (dbNodes.ContainsKey(edge.Target) ? edge.Target : null) ?? (topicNodes.ContainsKey(edge.Target) ? edge.Target : null) ?? (extNodes.ContainsKey(edge.Target) ? edge.Target : null);

            if (srcDomain == null || tgtDomain == null || srcDomain.Equals(tgtDomain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string cat;
            string label;

            if (dbNodes.ContainsKey(tgtDomain) || edge.Category == "database" || edge.Kind == "USES_DB")
            {
                cat = "database";
                label = "USES_DB";
                if (!dbUsage.ContainsKey(srcDomain)) dbUsage[srcDomain] = new(StringComparer.OrdinalIgnoreCase);
                dbUsage[srcDomain].Add(tgtDomain);
            }
            else if (topicNodes.ContainsKey(tgtDomain) || topicNodes.ContainsKey(srcDomain) || edge.Category == "messaging" || edge.Kind == "TRIGGERS" || edge.Kind == "PUBLISHES_TO")
            {
                cat = "messaging";
                label = topicNodes.ContainsKey(srcDomain) ? "SUBSCRIBES" : "PUBLISHES";
                if (topicNodes.ContainsKey(tgtDomain))
                {
                    if (!msgUsage.ContainsKey(srcDomain)) msgUsage[srcDomain] = new(StringComparer.OrdinalIgnoreCase);
                    msgUsage[srcDomain].Add(tgtDomain);
                }
                else if (topicNodes.ContainsKey(srcDomain))
                {
                    if (!msgUsage.ContainsKey(tgtDomain)) msgUsage[tgtDomain] = new(StringComparer.OrdinalIgnoreCase);
                    msgUsage[tgtDomain].Add(srcDomain);
                }
            }
            else if (extNodes.ContainsKey(tgtDomain))
            {
                cat = "external";
                label = "CALLS";
            }
            else
            {
                cat = "service_call";
                label = "CALLS";
            }

            if (cat == "service_call")
            {
                outCalls[srcDomain] = outCalls.GetValueOrDefault(srcDomain, 0) + 1;
                inCalls[tgtDomain] = inCalls.GetValueOrDefault(tgtDomain, 0) + 1;
            }

            var edgeKey = $"{srcDomain}->{tgtDomain}:{cat}";
            if (macroEdges.TryGetValue(edgeKey, out var existing))
            {
                existing.Count++;
            }
            else
            {
                macroEdges[edgeKey] = new DomainMacroEdgeDto
                {
                    Id = edgeKey,
                    Source = srcDomain,
                    Target = tgtDomain,
                    Category = cat,
                    Label = label,
                    Count = 1
                };
            }
        }

        // 4. Build Domain Entities
        var stats = new DomainStatsDto();
        var nodes = new List<DomainEntityDto>();

        foreach (var (domainId, meta) in domainNameMap)
        {
            var zone = domainZoneMap.GetValueOrDefault(domainId, "service");
            var isIngress = zone == "ingress";
            var projects = domainProjectsMap.GetValueOrDefault(domainId, []);
            var primary = domainPrimaryMap.GetValueOrDefault(domainId);
            var isWorker = primary?.Kind?.Equals("Worker", StringComparison.OrdinalIgnoreCase) == true ||
                           primary?.Properties?.GetValueOrDefault("role") == "Worker" ||
                           projects.Any(p => p.Kind?.Equals("Worker", StringComparison.OrdinalIgnoreCase) == true);
            var isLibDomain = (primary != null && IsLibraryProject(primary)) &&
                              !projects.Any(p => p.Kind?.Equals("Service", StringComparison.OrdinalIgnoreCase) == true || p.Kind?.Equals("App", StringComparison.OrdinalIgnoreCase) == true);

            string tag;
            string nodeKind;
            string bgColor;
            string borderColor;
            int size;

            if (isIngress)
            {
                stats.Ingress++;
                tag = ":Ingress";
                nodeKind = "Ingress";
                bgColor = "#0288d1";
                borderColor = "#01579b";
                size = 54;
            }
            else if (isWorker)
            {
                stats.Workers++;
                tag = ":Worker";
                nodeKind = "Worker";
                bgColor = "#d97706";
                borderColor = "#92400e";
                size = 48;
            }
            else if (isLibDomain)
            {
                stats.Libraries++;
                tag = ":Library";
                nodeKind = "Library";
                bgColor = "#475569";
                borderColor = "#1e293b";
                size = 44;
            }
            else
            {
                stats.Services++;
                tag = ":Service";
                nodeKind = "Service";
                bgColor = "#e53935";
                borderColor = "#7f1d1d";
                size = 50;
            }

            nodes.Add(new DomainEntityDto
            {
                Id = domainId,
                Name = meta.Name,
                DisplayName = meta.DisplayName,
                Kind = nodeKind,
                DisplayTag = tag,
                Zone = zone,
                BgColor = bgColor,
                BorderColor = borderColor,
                Size = size,
                Framework = meta.Framework,
                Language = meta.Language,
                PrimaryFilePath = primary?.FilePath ?? projects.FirstOrDefault()?.FilePath,
                Projects = projects,
                InboundCallsCount = inCalls.GetValueOrDefault(domainId, 0),
                OutboundCallsCount = outCalls.GetValueOrDefault(domainId, 0),
                DbCount = dbUsage.GetValueOrDefault(domainId)?.Count ?? 0,
                MessagingCount = msgUsage.GetValueOrDefault(domainId)?.Count ?? 0
            });
        }

        // Databases
        foreach (var (dbId, dbNode) in dbNodes)
        {
            var isUsed = macroEdges.Values.Any(e => e.Target.Equals(dbId, StringComparison.OrdinalIgnoreCase) || e.Source.Equals(dbId, StringComparison.OrdinalIgnoreCase));
            if (!isUsed && dbNodes.Count > 20) continue;

            stats.Databases++;
            nodes.Add(new DomainEntityDto
            {
                Id = dbId,
                Name = dbNode.Name,
                DisplayName = dbNode.DisplayName ?? dbNode.Name,
                Kind = "Database",
                DisplayTag = ":DB",
                Zone = "database",
                BgColor = "#7b1fa2",
                BorderColor = "#4a148c",
                Size = 46,
                Framework = dbNode.Properties?.GetValueOrDefault("db_type", "relational")
            });
        }

        // Topics
        foreach (var (tId, tNode) in topicNodes)
        {
            var isUsed = macroEdges.Values.Any(e => e.Target.Equals(tId, StringComparison.OrdinalIgnoreCase) || e.Source.Equals(tId, StringComparison.OrdinalIgnoreCase));
            if (!isUsed && topicNodes.Count > 25) continue;

            stats.Topics++;
            nodes.Add(new DomainEntityDto
            {
                Id = tId,
                Name = tNode.Name,
                DisplayName = tNode.DisplayName ?? tNode.Name,
                Kind = "Topic",
                DisplayTag = ":Topic",
                Zone = "topic",
                BgColor = "#f59e0b",
                BorderColor = "#b45309",
                Size = 44,
                Framework = tNode.Properties?.GetValueOrDefault("broker_type", "Topic")
            });
        }

        // External Services
        foreach (var (extId, extNode) in extNodes)
        {
            var isUsed = macroEdges.Values.Any(e => e.Target.Equals(extId, StringComparison.OrdinalIgnoreCase) || e.Source.Equals(extId, StringComparison.OrdinalIgnoreCase));
            if (!isUsed && extNodes.Count > 25) continue;

            stats.External++;
            nodes.Add(new DomainEntityDto
            {
                Id = extId,
                Name = extNode.Name,
                DisplayName = extNode.DisplayName ?? extNode.Name,
                Kind = "ExternalService",
                DisplayTag = ":External",
                Zone = "external",
                BgColor = "#26a69a",
                BorderColor = "#004d40",
                Size = 44,
                Framework = extNode.Properties?.GetValueOrDefault("service_type", "External")
            });
        }

        var validNodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredEdges = macroEdges.Values.Where(e => validNodeIds.Contains(e.Source) && validNodeIds.Contains(e.Target)).ToList();

        stats.TotalDomains = nodes.Count(n => n.Kind is "Service" or "Ingress" or "Worker" or "Library");
        stats.ServiceCalls = filteredEdges.Where(e => e.Category == "service_call").Sum(e => e.Count);
        stats.Messages = filteredEdges.Where(e => e.Category == "messaging").Sum(e => e.Count);

        result.Nodes = nodes;
        result.Edges = filteredEdges;
        result.Stats = stats;
        return result;
    }

    public async Task<GraphDataDto> GetDomainArchitectureGraphAsync(bool includeLibraries = true, CancellationToken ct = default)
    {
        var domainDto = await GetDomainArchitectureAsync(includeLibraries, ct);
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "DomainMap",
                ["level"] = "DomainMap",
                ["graphType"] = "architecture"
            }
        };

        foreach (var d in domainDto.Nodes)
        {
            graph.Nodes.Add(new GraphNodeDto
            {
                Id = d.Id,
                Kind = d.Kind,
                Name = d.Name,
                DisplayName = d.DisplayName,
                FilePath = d.PrimaryFilePath,
                Properties = new Dictionary<string, string>
                {
                    ["displayTag"] = d.DisplayTag,
                    ["zone"] = d.Zone,
                    ["bgColor"] = d.BgColor,
                    ["borderColor"] = d.BorderColor,
                    ["size"] = d.Size.ToString(),
                    ["inboundCalls"] = d.InboundCallsCount.ToString(),
                    ["outboundCalls"] = d.OutboundCallsCount.ToString(),
                    ["dbCount"] = d.DbCount.ToString(),
                    ["messagingCount"] = d.MessagingCount.ToString(),
                    ["framework"] = d.Framework ?? "",
                    ["language"] = d.Language ?? ""
                }
            });
        }

        foreach (var e in domainDto.Edges)
        {
            graph.Edges.Add(new GraphEdgeDto
            {
                Id = e.Id,
                Source = e.Source,
                Target = e.Target,
                Kind = e.Label,
                Category = e.Category,
                Properties = new Dictionary<string, string>
                {
                    ["count"] = e.Count.ToString(),
                    ["label"] = e.Label,
                    ["category"] = e.Category
                }
            });
        }

        return graph;
    }

    public async Task<GraphDataDto> GetTieredArchitectureGraphAsync(bool includeLibraries = true, CancellationToken ct = default)
    {
        var arch = await GetSystemContextViewAsync(includeLibraries, null, ct);
        arch.Metadata ??= new Dictionary<string, string>();
        arch.Metadata["view"] = "Tiers";
        arch.Metadata["level"] = "Tiers";
        return arch;
    }

    // =========================================================================
    // 7. Service Contracts & Cross-Service Flow Tracing
    // =========================================================================

    public async Task<ServiceContractDto> GetServiceContractsAsync(string serviceName, string direction = "all", CancellationToken ct = default)
    {
        var archGraph = await GetSystemContextViewAsync(includeLibraries: true, projectFilter: null, ct: ct);
        var targetNode = archGraph.Nodes.FirstOrDefault(n => n.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase) || n.Id.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        var contract = new ServiceContractDto
        {
            ServiceName = targetNode?.Name ?? serviceName,
            Kind = targetNode?.Kind ?? "Service",
            Framework = targetNode?.Properties?.GetValueOrDefault("framework"),
            Language = targetNode?.Properties?.GetValueOrDefault("language") ?? targetNode?.Properties?.GetValueOrDefault("project_type")
        };

        if (targetNode == null)
        {
            return contract;
        }

        var targetId = targetNode.Id;

        // Ingress
        if (direction is "all" or "ingress")
        {
            try
            {
                var epQuery = "MATCH (s)-[:CONTAINS]->(ep:Endpoint) WHERE s.id = $id OR s.name = $name RETURN ep.name AS epName";
                var epJson = await db.ExecuteQueryAsync(epQuery, new Dictionary<string, object> { ["id"] = targetId, ["name"] = serviceName }, ct);
                using var epDoc = JsonDocument.Parse(epJson);
                foreach (var row in epDoc.RootElement.EnumerateArray())
                {
                    var epName = row.GetStringProp("epName");
                    if (!string.IsNullOrEmpty(epName) && !contract.IngressEndpoints.Contains(epName))
                    {
                        contract.IngressEndpoints.Add(epName);
                    }
                }
            }
            catch { }

            foreach (var edge in archGraph.Edges)
            {
                if (edge.Target.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                {
                    if (edge.Category == "messaging" || edge.Kind is "SUBSCRIBES_TO" or "TRIGGERS")
                    {
                        var srcNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Source, StringComparison.OrdinalIgnoreCase));
                        var topicName = srcNode?.Name ?? edge.Source;
                        if (!contract.SubscribedTopics.Contains(topicName))
                        {
                            contract.SubscribedTopics.Add(topicName);
                        }
                    }
                }
            }
        }

        // Egress
        if (direction is "all" or "egress")
        {
            foreach (var edge in archGraph.Edges)
            {
                if (edge.Source.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                {
                    var tgtNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Target, StringComparison.OrdinalIgnoreCase));
                    var tgtName = tgtNode?.Name ?? edge.Target;

                    if (edge.Category == "database" || edge.Kind == "USES_DB" || tgtNode?.Kind == "Database")
                    {
                        if (!contract.Databases.Contains(tgtName)) contract.Databases.Add(tgtName);
                    }
                    else if (edge.Category == "messaging" || edge.Kind is "PUBLISHES_TO" or "TRIGGERS" || tgtNode?.Kind == "Topic")
                    {
                        if (!contract.PublishedTopics.Contains(tgtName)) contract.PublishedTopics.Add(tgtName);
                    }
                    else if (tgtNode?.Kind == "ExternalService")
                    {
                        if (!contract.ExternalServices.Contains(tgtName)) contract.ExternalServices.Add(tgtName);
                    }
                    else if (edge.Category == "service_call" || edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT")
                    {
                        if (!contract.OutboundServiceCalls.Contains(tgtName)) contract.OutboundServiceCalls.Add(tgtName);
                    }
                }
            }
        }

        return contract;
    }

    public async Task<CrossServiceFlowDto> TraceCrossServiceFlowAsync(string startService, string? entryPoint = null, int maxDepth = 3, CancellationToken ct = default)
    {
        var archGraph = await GetSystemContextViewAsync(includeLibraries: true, projectFilter: null, ct: ct);
        var startNode = archGraph.Nodes.FirstOrDefault(n => n.Name.Equals(startService, StringComparison.OrdinalIgnoreCase) || n.Id.Equals(startService, StringComparison.OrdinalIgnoreCase));

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

        var nodeLookup = archGraph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startNode.Id };
        var queue = new Queue<(string CurrentId, int Depth)>();
        queue.Enqueue((startNode.Id, 0));

        int step = 1;
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
                if (edge.Source.Equals(currId, StringComparison.OrdinalIgnoreCase))
                {
                    var tgtNode = nodeLookup.GetValueOrDefault(edge.Target);
                    var tgtName = tgtNode?.Name ?? edge.Target;

                    flow.Hops.Add(new CrossServiceHopDto
                    {
                        Step = step++,
                        Source = currNode?.Name ?? currId,
                        Target = tgtName,
                        Kind = edge.Kind,
                        Protocol = edge.Properties?.GetValueOrDefault("protocol") ?? edge.Category,
                        Details = edge.Properties?.GetValueOrDefault("via_library") != null ? $"via {edge.Properties["via_library"]}" : null
                    });

                    if (visited.Add(edge.Target))
                    {
                        queue.Enqueue((edge.Target, depth + 1));
                    }
                }
            }
        }

        return flow;
    }

    // =========================================================================
    // 8. Multi-Format LLM Serializers (Markdown, TOON, Mermaid, JSON)
    // =========================================================================

    public static string SerializeDomainArchitecture(DomainArchitectureDto dto, string format)
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }),
            "mermaid" => ToMermaid(dto),
            "toon" => ToToon(dto),
            _ => ToMarkdown(dto)
        };
    }

    public static string ToMarkdown(DomainArchitectureDto dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Domain Microservices Architecture");
        sb.AppendLine();
        sb.AppendLine($"**Domains:** {dto.Stats.TotalDomains} | **Services:** {dto.Stats.Services} | **Apps:** {dto.Stats.Ingress} | **Workers:** {dto.Stats.Workers} | **Databases:** {dto.Stats.Databases} | **Topics:** {dto.Stats.Topics}");
        sb.AppendLine($"**Inter-Domain Calls:** {dto.Stats.ServiceCalls} | **Async Messages:** {dto.Stats.Messages}");
        sb.AppendLine();
        sb.AppendLine("## Bounded Contexts & Services");
        sb.AppendLine("| Domain | Kind | Projects | Inbound Calls | Outbound Calls | Databases | Topics |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var d in dto.Nodes.Where(n => n.Kind is "Service" or "Ingress" or "Worker" or "Library"))
        {
            var pCount = d.Projects.Count > 0 ? string.Join(", ", d.Projects.Select(p => p.Name)) : d.Name;
            sb.AppendLine($"| {d.DisplayName} | `{d.Kind}` | {pCount} | {d.InboundCallsCount} | {d.OutboundCallsCount} | {d.DbCount} | {d.MessagingCount} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Inter-Domain Relationships");
        sb.AppendLine("| Source | Target | Category | Calls / Flow |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var e in dto.Edges)
        {
            sb.AppendLine($"| {e.Source} | {e.Target} | `{e.Category}` | {e.Count} |");
        }
        return sb.ToString();
    }

    public static string ToToon(DomainArchitectureDto dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("domain_architecture:");
        sb.AppendLine("  stats:");
        sb.AppendLine($"    domains: {dto.Stats.TotalDomains}");
        sb.AppendLine($"    services: {dto.Stats.Services}");
        sb.AppendLine($"    apps: {dto.Stats.Ingress}");
        sb.AppendLine($"    workers: {dto.Stats.Workers}");
        sb.AppendLine($"    databases: {dto.Stats.Databases}");
        sb.AppendLine($"    topics: {dto.Stats.Topics}");
        sb.AppendLine($"    calls: {dto.Stats.ServiceCalls}");
        sb.AppendLine($"    messages: {dto.Stats.Messages}");
        sb.AppendLine("  domains:");
        foreach (var d in dto.Nodes.Where(n => n.Kind is "Service" or "Ingress" or "Worker" or "Library"))
        {
            sb.AppendLine($"    - id: {d.Id}, name: {d.DisplayName}, kind: {d.Kind}, calls_in: {d.InboundCallsCount}, calls_out: {d.OutboundCallsCount}, dbs: {d.DbCount}, topics: {d.MessagingCount}");
        }
        sb.AppendLine("  macro_edges:");
        foreach (var e in dto.Edges)
        {
            sb.AppendLine($"    - from: {e.Source}, to: {e.Target}, category: {e.Category}, count: {e.Count}");
        }
        return sb.ToString();
    }

    public static string ToMermaid(DomainArchitectureDto dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("flowchart TD");
        var sanitize = (string s) => s.Replace(":", "_").Replace("-", "_").Replace(".", "_").Replace("/", "_");

        var ingress = dto.Nodes.Where(n => n.Kind == "Ingress").ToList();
        if (ingress.Count > 0)
        {
            sb.AppendLine("  subgraph Ingress [\"Apps & Ingress\"]");
            foreach (var node in ingress) sb.AppendLine($"    {sanitize(node.Id)}[\"{node.DisplayName}\"]");
            sb.AppendLine("  end");
        }

        var services = dto.Nodes.Where(n => n.Kind == "Service" || n.Kind == "Worker").ToList();
        if (services.Count > 0)
        {
            sb.AppendLine("  subgraph Services [\"Core Services\"]");
            foreach (var node in services) sb.AppendLine($"    {sanitize(node.Id)}[\"{node.DisplayName}\"]");
            sb.AppendLine("  end");
        }

        var dbs = dto.Nodes.Where(n => n.Kind == "Database").ToList();
        if (dbs.Count > 0)
        {
            sb.AppendLine("  subgraph Databases [\"Databases\"]");
            foreach (var node in dbs) sb.AppendLine($"    {sanitize(node.Id)}[(\"🗄️ {node.DisplayName}\")]");
            sb.AppendLine("  end");
        }

        var topics = dto.Nodes.Where(n => n.Kind == "Topic").ToList();
        if (topics.Count > 0)
        {
            sb.AppendLine("  subgraph Topics [\"Message Queues\"]");
            foreach (var node in topics) sb.AppendLine($"    {sanitize(node.Id)}>\"📬 {node.DisplayName}\"]");
            sb.AppendLine("  end");
        }

        foreach (var e in dto.Edges)
        {
            sb.AppendLine($"  {sanitize(e.Source)} -->|{e.Label} ({e.Count})| {sanitize(e.Target)}");
        }

        return sb.ToString();
    }

    public static string SerializeServiceContract(ServiceContractDto dto, string format)
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }),
            "toon" => ToToon(dto),
            _ => ToMarkdown(dto)
        };
    }

    public static string ToMarkdown(ServiceContractDto dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Service Contract: {dto.ServiceName}");
        sb.AppendLine();
        sb.AppendLine($"**Kind:** `{dto.Kind}` | **Framework:** {dto.Framework ?? "N/A"} | **Language:** {dto.Language ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("## Ingress (Inbound API & Messaging)");
        if (dto.IngressEndpoints.Count > 0)
        {
            sb.AppendLine("### HTTP Endpoints");
            foreach (var ep in dto.IngressEndpoints) sb.AppendLine($"- `{ep}`");
        }
        if (dto.SubscribedTopics.Count > 0)
        {
            sb.AppendLine("### Subscribed Message Topics");
            foreach (var t in dto.SubscribedTopics) sb.AppendLine($"- 📬 `{t}`");
        }
        sb.AppendLine();
        sb.AppendLine("## Egress (Outbound Dependencies)");
        if (dto.OutboundServiceCalls.Count > 0)
        {
            sb.AppendLine("### Service-to-Service Calls");
            foreach (var c in dto.OutboundServiceCalls) sb.AppendLine($"- ⚡ `{c}`");
        }
        if (dto.PublishedTopics.Count > 0)
        {
            sb.AppendLine("### Published Message Topics");
            foreach (var p in dto.PublishedTopics) sb.AppendLine($"- ✉️ `{p}`");
        }
        if (dto.Databases.Count > 0)
        {
            sb.AppendLine("### Databases & Storage");
            foreach (var db in dto.Databases) sb.AppendLine($"- 🗄️ `{db}`");
        }
        if (dto.ExternalServices.Count > 0)
        {
            sb.AppendLine("### External APIs");
            foreach (var ext in dto.ExternalServices) sb.AppendLine($"- ☁️ `{ext}`");
        }
        return sb.ToString();
    }

    public static string ToToon(ServiceContractDto dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("service_contract:");
        sb.AppendLine($"  service: {dto.ServiceName}");
        sb.AppendLine($"  kind: {dto.Kind}");
        sb.AppendLine($"  framework: {dto.Framework ?? "N/A"}");
        sb.AppendLine($"  language: {dto.Language ?? "N/A"}");
        sb.AppendLine($"  ingress_endpoints: [{string.Join(", ", dto.IngressEndpoints)}]");
        sb.AppendLine($"  subscribed_topics: [{string.Join(", ", dto.SubscribedTopics)}]");
        sb.AppendLine($"  outbound_service_calls: [{string.Join(", ", dto.OutboundServiceCalls)}]");
        sb.AppendLine($"  published_topics: [{string.Join(", ", dto.PublishedTopics)}]");
        sb.AppendLine($"  databases: [{string.Join(", ", dto.Databases)}]");
        sb.AppendLine($"  external_services: [{string.Join(", ", dto.ExternalServices)}]");
        return sb.ToString();
    }

    public static string SerializeCrossServiceFlow(CrossServiceFlowDto dto, string format)
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }),
            "mermaid" => ToMermaid(dto),
            _ => ToMarkdown(dto)
        };
    }

    public static string ToMarkdown(CrossServiceFlowDto dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Cross-Service Execution Flow: {dto.StartService}");
        if (!string.IsNullOrEmpty(dto.EntryPoint)) sb.AppendLine($"**Entry Point:** `{dto.EntryPoint}`");
        sb.AppendLine($"**Max Depth:** {dto.MaxDepth} | **Visited Services:** {string.Join(" -> ", dto.VisitedServices)}");
        sb.AppendLine();
        sb.AppendLine("## Execution Trace Hops");
        sb.AppendLine("| Step | Source | Target | Relationship | Protocol / Details |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var hop in dto.Hops)
        {
            sb.AppendLine($"| {hop.Step} | **{hop.Source}** | **{hop.Target}** | `{hop.Kind}` | {hop.Protocol ?? "N/A"} {hop.Details} |");
        }
        return sb.ToString();
    }

    public static string ToMermaid(CrossServiceFlowDto dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("flowchart LR");
        var sanitize = (string s) => s.Replace(":", "_").Replace("-", "_").Replace(".", "_").Replace("/", "_");
        foreach (var hop in dto.Hops)
        {
            sb.AppendLine($"  {sanitize(hop.Source)} -->|{hop.Kind}| {sanitize(hop.Target)}");
        }
        return sb.ToString();
    }

    public static string SerializeGraph(GraphDataDto graph, string format, string title = "Architecture View")
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(new { results = graph }, new JsonSerializerOptions { WriteIndented = false }),
            "mermaid" => ToMermaid(graph),
            "toon" or "yaml" => ToToon(graph),
            _ => ToMarkdown(graph, title)
        };
    }

    public static string ToMermaid(GraphDataDto graph)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("flowchart TD");
        var sanitize = (string s) => s.Replace(":", "_").Replace("-", "_").Replace(".", "_").Replace("/", "_");

        foreach (var node in graph.Nodes)
        {
            var label = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Id;
            sb.AppendLine($"  {sanitize(node.Id)}[\"{label}\"]");
        }

        foreach (var edge in graph.Edges)
        {
            var kind = !string.IsNullOrWhiteSpace(edge.Kind) ? edge.Kind : "DEPENDS_ON";
            sb.AppendLine($"  {sanitize(edge.Source)} -->|{kind}| {sanitize(edge.Target)}");
        }

        return sb.ToString();
    }

    public static string ToToon(GraphDataDto graph)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("graph_view:");
        sb.AppendLine($"  nodes_count: {graph.Nodes.Count}");
        sb.AppendLine($"  edges_count: {graph.Edges.Count}");
        sb.AppendLine("  nodes:");
        foreach (var node in graph.Nodes)
        {
            var label = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Id;
            sb.AppendLine($"    - id: {node.Id}");
            sb.AppendLine($"      name: {label}");
            sb.AppendLine($"      kind: {node.Kind}");
        }
        sb.AppendLine("  edges:");
        foreach (var edge in graph.Edges)
        {
            sb.AppendLine($"    - {edge.Source} -> {edge.Target} [{edge.Kind}]");
        }
        return sb.ToString();
    }

    public static string ToMarkdown(GraphDataDto graph, string title)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"**Total Nodes:** {graph.Nodes.Count} | **Total Edges:** {graph.Edges.Count}");
        sb.AppendLine();
        sb.AppendLine("## Nodes");
        sb.AppendLine("| Node | Kind | Subkind |");
        sb.AppendLine("|---|---|---|");
        foreach (var node in graph.Nodes.OrderBy(n => n.Kind).ThenBy(n => n.Name))
        {
            var label = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Id;
            var subkind = node.Properties != null && node.Properties.TryGetValue("subkind", out var sk) ? sk : "N/A";
            sb.AppendLine($"| **{label}** | `{node.Kind}` | {subkind} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Relationships");
        sb.AppendLine("| Source | Target | Relationship | Category |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var edge in graph.Edges.OrderBy(e => e.Kind).ThenBy(e => e.Source))
        {
            sb.AppendLine($"| **{edge.Source}** | **{edge.Target}** | `{edge.Kind}` | {edge.Category ?? "N/A"} |");
        }
        return sb.ToString();
    }
}
