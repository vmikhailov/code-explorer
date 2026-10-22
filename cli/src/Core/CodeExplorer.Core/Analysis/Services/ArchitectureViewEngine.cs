using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Analysis;

public enum ArchitectureViewType
{
    SystemContext,  // C1: Macro System Context view (Services, Databases, Topics, ExternalServices)
    ServiceFlow,    // C2: Container & Service Flow view (Interactions, Messaging, Shared Libraries)
    Component,      // C3: Component Drill-down for a selected container/project
    DomainMap,      // Bounded Contexts / Domain Microservices Map
    Tiers,          // Layered Architecture Tiers (Ingress, Domain, Infra, Foundation, Tests)
    BoundedContexts, // AI-Distilled Bounded Contexts Map (Logical DDD domains, entities, capabilities, CQRS, and cross-context links)
    ProjectMap      // C3: Internal module map of a single project (view-only projection: folder modules, lifted calls, cycles)
}

public class ArchitectureViewRequest
{
    public ArchitectureViewType ViewType { get; set; } = ArchitectureViewType.SystemContext;
    public string? Scope { get; set; }
    public bool IncludeLibraries { get; set; } = false;
    public bool SharedDatabasesOnly { get; set; } = false;
}

/// <summary>
/// Executes single-query architectural projections directly against the graph database,
/// bypassing runtime BFS lifting and heuristics.
/// </summary>
public partial class ArchitectureViewEngine(IGraphClient db, Microsoft.Extensions.Logging.ILogger? logger = null)
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GraphDataDto> SystemContextCache = new();

    private void RecordEvidenceWarning(List<string>? warnings, string context, Exception ex)
    {
        logger?.LogWarning(ex, "[Evidence] {Context}: {Message}", context, ex.Message);
        var msg = $"{context}: {ex.Message}";
        if (warnings != null && !warnings.Contains(msg, StringComparer.Ordinal))
        {
            warnings.Add(msg);
        }
    }

    public static void InvalidateCache(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            SystemContextCache.Clear();
            return;
        }

        var prefix = dbPath + ":";
        foreach (var key in SystemContextCache.Keys)
        {
            if (key.StartWithIC(prefix))
            {
                SystemContextCache.TryRemove(key, out _);
            }
        }
    }

    private string ResolveDbCachePrefix()
    {
        if (db is SqliteGraphClient sqliteClient &&
            !string.IsNullOrWhiteSpace(sqliteClient.DbPath) &&
            !string.Equals(sqliteClient.DbPath, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            var dbPath = sqliteClient.DbPath;
            long dbLastWriteTicks = File.Exists(dbPath) ? File.GetLastWriteTimeUtc(dbPath).Ticks : 0;
            long walLastWriteTicks = File.Exists(dbPath + "-wal") ? File.GetLastWriteTimeUtc(dbPath + "-wal").Ticks : 0;
            return $"{dbPath}:{dbLastWriteTicks}:{walLastWriteTicks}";
        }

        return $"mem:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(db)}";
    }

    public async Task<GraphDataDto> GetViewAsync(ArchitectureViewRequest request, CancellationToken ct = default)
    {
        return request.ViewType switch
        {
            ArchitectureViewType.SystemContext => await GetSystemContextViewAsync(request.IncludeLibraries, request.Scope, request.SharedDatabasesOnly, ct),
            ArchitectureViewType.ServiceFlow => await GetServiceFlowViewAsync(request.Scope, request.IncludeLibraries, ct),
            ArchitectureViewType.Component => await GetComponentViewAsync(request.Scope, ct),
            ArchitectureViewType.DomainMap => await GetDomainArchitectureGraphAsync(request.IncludeLibraries, request.SharedDatabasesOnly, ct),
            ArchitectureViewType.Tiers => await GetTieredArchitectureGraphAsync(request.IncludeLibraries, ct),
            ArchitectureViewType.BoundedContexts => await GetBoundedContextGraphAsync(request.Scope, ct),
            ArchitectureViewType.ProjectMap => await GetProjectMapViewAsync(request.Scope, ct),
            _ => await GetSystemContextViewAsync(request.IncludeLibraries, request.Scope, request.SharedDatabasesOnly, ct)
        };
    }

    private sealed record SystemContextDiscoveryState(
        Dictionary<string, string> InternalProjectsLookup,
        List<ProjectMatchCandidate> ProjectCandidates,
        List<EndpointMatchCandidate> EndpointCandidates,
        string? PrimaryRelationalEngine);

    private async Task<SystemContextDiscoveryState> DiscoverSystemContextCandidatesAsync(
        JsonDocument nodesDoc,
        CancellationToken ct)
    {
        var internalProjectsLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projectCandidates = new List<ProjectMatchCandidate>();

        foreach (var elem in nodesDoc.RootElement.EnumerateArray())
        {
            var k = elem.GetStringProp("kind");
            var nid = elem.GetStringProp("id");
            var nname = elem.GetStringProp("name");
            var npath = elem.GetStringProp("path");
            if (string.IsNullOrEmpty(nid) || string.IsNullOrEmpty(nname)) continue;

            if (k is "Project" or "Service" or "App" or "Worker" or "Library" or "CliTool")
            {
                var isLib = elem.GetStringProp("is_library") == "true" || elem.GetStringProp("role") is "SharedLibrary" or "Test";
                projectCandidates.Add(new ProjectMatchCandidate(nid, nname, npath, null, isLib));

                internalProjectsLookup.TryAdd(nname, nid);
                var normName = EndpointScoringEngine.NormalizeAlphanumeric(nname);
                if (!string.IsNullOrEmpty(normName)) internalProjectsLookup.TryAdd(normName, nid);

                if (!string.IsNullOrEmpty(npath))
                {
                    var folder = Path.GetFileName(npath.TrimEnd('/', '\\'));
                    if (!string.IsNullOrEmpty(folder))
                    {
                        internalProjectsLookup.TryAdd(folder, nid);
                        var normFolder = EndpointScoringEngine.NormalizeAlphanumeric(folder);
                        if (!string.IsNullOrEmpty(normFolder)) internalProjectsLookup.TryAdd(normFolder, nid);
                    }
                }

                var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(nname);
                if (!string.IsNullOrEmpty(domain))
                {
                    internalProjectsLookup.TryAdd(domain, nid);
                    internalProjectsLookup.TryAdd(domain.ToLowerInvariant(), nid);
                    var normDomain = EndpointScoringEngine.NormalizeAlphanumeric(domain);
                    if (!string.IsNullOrEmpty(normDomain)) internalProjectsLookup.TryAdd(normDomain, nid);
                }
            }
        }

        var endpointCandidates = new List<EndpointMatchCandidate>();
        try
        {
            var epQuery =
                "MATCH (ep:Endpoint) RETURN ep.id AS id, ep.name AS name, json_extract(ep.properties, '$.route_template') AS route, json_extract(ep.properties, '$.http_method') AS method, json_extract(ep.properties, '$.path') AS path";
            var epJson = await db.ExecuteQueryAsync(epQuery, null, ct);
            using var epDoc = JsonDocument.Parse(epJson);
            foreach (var epRow in epDoc.RootElement.EnumerateArray())
            {
                var epId = epRow.GetStringProp("id");
                var epName = epRow.GetStringProp("name");
                var epRoute = epRow.GetStringProp("route");
                var epMethod = epRow.GetStringProp("method");
                var epPath = epRow.GetStringProp("path");

                if (string.IsNullOrEmpty(epRoute) && !string.IsNullOrEmpty(epName) && epName.Contains(':'))
                {
                    var parts = epName.Split(':', 2);
                    epMethod ??= parts[0];
                    epRoute = parts[1];
                }

                if (!string.IsNullOrEmpty(epId) && !string.IsNullOrEmpty(epRoute))
                {
                    var ownerProj = !string.IsNullOrEmpty(epPath)
                        ? projectCandidates.FirstOrDefault(p => !string.IsNullOrEmpty(p.Path) && epPath.StartWithIC(p.Path))
                        : null;
                    endpointCandidates.Add(new EndpointMatchCandidate(epId, epRoute, epMethod, ownerProj?.ProjectId));
                }
            }
        }
        catch
        {
        }

        string? primaryRelationalEngine = null;

        foreach (var elem in nodesDoc.RootElement.EnumerateArray())
        {
            if (elem.GetStringProp("kind") == "Database")
            {
                var dt = elem.GetStringProp("db_type", "relational");
                if (!dt.Equals("relational", StringComparison.OrdinalIgnoreCase)) continue;

                var eng = elem.GetStringProp("engine");
                var nm = elem.GetStringProp("name");

                var candidate =
                    !string.IsNullOrWhiteSpace(eng) &&
                    !eng.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
                    !eng.Equals("default", StringComparison.OrdinalIgnoreCase)
                        ? eng
                        : (nm.Contains('.') ? nm.Split('.', 2)[0] : nm);

                if (!string.IsNullOrWhiteSpace(candidate) &&
                    !candidate.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
                    !candidate.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                    !ResourceReconciliationService.IsGenericConfigKey(candidate) &&
                    PostIndexAnalyzer.IsRelationalEngine(candidate))
                {
                    primaryRelationalEngine = candidate;
                    break;
                }
            }
        }

        return new SystemContextDiscoveryState(internalProjectsLookup, projectCandidates, endpointCandidates, primaryRelationalEngine);
    }

    private static Dictionary<string, string> ExtractBaseNodeProperties(JsonElement elem)
    {
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
        var path = elem.GetStringProp("path", props.GetValueOrDefault("path", props.GetValueOrDefault("file_path", "")));
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

        var primaryRole = elem.GetStringProp("primary_role", props.GetValueOrDefault("primary_role", ""));
        if (!string.IsNullOrEmpty(primaryRole)) props["primary_role"] = primaryRole;
        var secondaryRoles = elem.GetStringProp("secondary_roles", props.GetValueOrDefault("secondary_roles", ""));
        if (!string.IsNullOrEmpty(secondaryRoles)) props["secondary_roles"] = secondaryRoles;
        var allRoles = elem.GetStringProp("all_roles", props.GetValueOrDefault("all_roles", ""));
        if (!string.IsNullOrEmpty(allRoles)) props["all_roles"] = allRoles;

        var memberCount = elem.GetStringProp("member_count", props.GetValueOrDefault("member_count", ""));
        if (!string.IsNullOrEmpty(memberCount)) props["member_count"] = memberCount;
        var protocols = elem.GetStringProp("protocols", props.GetValueOrDefault("protocols", ""));
        if (!string.IsNullOrEmpty(protocols)) props["protocols"] = protocols;

        return props;
    }

    private static void ProcessDatabaseNode(
        JsonElement elem,
        string id,
        string name,
        Dictionary<string, string> props,
        string? primaryRelationalEngine,
        Dictionary<string, string> dbIdToCanonicalId,
        Dictionary<string, GraphNodeDto> nodeMap,
        GraphDataDto graph)
    {
        var dbType = props.GetValueOrDefault("db_type", "");
        var engineProp = elem.GetStringProp("engine", props.GetValueOrDefault("engine", ""));
        var schemaProp = elem.GetStringProp("schema", props.GetValueOrDefault("schema", ""));

        var (cName, cType, cKey) = PostIndexAnalyzer.CanonicalizeDatabase(name, dbType, engineProp,
            schemaProp, primaryRelationalEngine);

        var isExplicitResource = id.Contains(":res:db:", StringComparison.OrdinalIgnoreCase) ||
                                 id.StartWithIC("urn:");
        var isStandaloneDb = id.StartWithIC("db:");

        string canonicalId = isExplicitResource || isStandaloneDb
            ? id
            : $"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Database}:{cType.ToLowerInvariant()}:{cKey.ToLowerInvariant()}";

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

            var engine = !string.IsNullOrEmpty(engineProp)
                ? engineProp
                : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);
            props["engine"] = engine;
            props["technology"] = engine;
            props["schema"] = cName.Contains('.') ? cName.Split('.', 2)[1] : schemaProp;

            var dispName = elem.GetStringProp("display_name");

            if (string.IsNullOrEmpty(dispName))
            {
                dispName = cName.Contains('.')
                    ? cName
                    : (!string.IsNullOrEmpty(engine) &&
                       !engine.Equals(cName, StringComparison.OrdinalIgnoreCase)
                        ? $"{cName} ({engine}) [{cType}]"
                        : $"{cName} [{cType}]");
            }

            var path = props.GetValueOrDefault("path");
            var lineStart = elem.GetNullableIntProp("start_line")
                ?? (props.TryGetValue("start_line", out var slStr) && int.TryParse(slStr, out var sl) ? sl : null);
            var lineEnd = elem.GetNullableIntProp("end_line")
                ?? (props.TryGetValue("end_line", out var elStr) && int.TryParse(elStr, out var el) ? el : null);

            var node = new GraphNodeDto
            {
                Id = canonicalId,
                Kind = NodeKind.Database,
                Name = cName,
                DisplayName = dispName,
                FilePath = string.IsNullOrEmpty(path) ? null : path,
                LineStart = lineStart,
                LineEnd = lineEnd,
                Properties = props
            };
            nodeMap[canonicalId] = node;
            graph.Nodes.Add(node);
        }
        else
        {
            var engine = elem.GetStringProp("engine", props.GetValueOrDefault("engine", ""));

            if (!string.IsNullOrEmpty(engine) && existingDbNode.Properties != null &&
                !existingDbNode.Properties.ContainsKey("engine"))
            {
                existingDbNode.Properties["engine"] = engine;

                if (existingDbNode.DisplayName != null &&
                    !existingDbNode.DisplayName.Contains(engine, StringComparison.OrdinalIgnoreCase))
                {
                    existingDbNode.DisplayName =
                        $"{existingDbNode.Name} ({engine}) [{existingDbNode.Properties.GetValueOrDefault("db_type")}]";
                }
            }
        }
    }

    private static void ProcessPackageNode(
        JsonElement elem,
        string id,
        string name,
        Dictionary<string, string> props,
        Dictionary<string, GraphNodeDto> nodeMap,
        GraphDataDto graph)
    {
        if (nodeMap.ContainsKey(id)) return;

        var ver = elem.GetStringProp("version", props.GetValueOrDefault("version", ""));
        var pType = elem.GetStringProp("package_type",
            elem.GetStringProp("pkg_type", props.GetValueOrDefault("package_type", "package")));
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
            Kind = NodeKind.Package,
            Name = name,
            DisplayName = dispName,
            Properties = props
        };
        nodeMap[id] = pkgNode;
        graph.Nodes.Add(pkgNode);
    }

    private static void ProcessTopicNode(
        JsonElement elem,
        string id,
        string name,
        Dictionary<string, string> props,
        Dictionary<string, GraphNodeDto> nodeMap,
        GraphDataDto graph)
    {
        if (nodeMap.ContainsKey(id)) return;

        var broker = elem.GetStringProp("broker_type", props.GetValueOrDefault("broker_type", "Topic"));

        var isInternal = string.Equals(broker, "mediatr", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(broker, "in-memory", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(props.GetValueOrDefault("is_internal"), "true",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(props.GetValueOrDefault("scope"), "internal",
                             StringComparison.OrdinalIgnoreCase) ||
                         id.Contains(":mediatr:", StringComparison.OrdinalIgnoreCase) ||
                         id.Contains(":in-memory:", StringComparison.OrdinalIgnoreCase);
        if (isInternal) return;

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

        var path = props.GetValueOrDefault("path");
        var lineStart = elem.GetNullableIntProp("start_line")
            ?? (props.TryGetValue("start_line", out var slStr) && int.TryParse(slStr, out var sl) ? sl : null);
        var lineEnd = elem.GetNullableIntProp("end_line")
            ?? (props.TryGetValue("end_line", out var elStr) && int.TryParse(elStr, out var el) ? el : null);

        var topicNode = new GraphNodeDto
        {
            Id = id,
            Kind = NodeKind.Topic,
            Name = name,
            DisplayName = dispName,
            FilePath = string.IsNullOrEmpty(path) ? null : path,
            LineStart = lineStart,
            LineEnd = lineEnd,
            Properties = props
        };
        nodeMap[id] = topicNode;
        graph.Nodes.Add(topicNode);
    }

    private static void ProcessCloudServiceNode(
        JsonElement elem,
        string id,
        string name,
        Dictionary<string, string> props,
        Dictionary<string, GraphNodeDto> nodeMap,
        GraphDataDto graph)
    {
        if (nodeMap.ContainsKey(id)) return;

        props["is_semantic_entity"] = "true";
        props["is_library"] = "false";
        props["entity_type"] = "resource";
        props["service_type"] = "CloudService";

        if (!props.ContainsKey("layer"))
        {
            props["layer"] = StandardLayers.Egress.LayerId;
            props["layerId"] = StandardLayers.Egress.LayerId;
            props["layerName"] = StandardLayers.Egress.LayerName;
            props["layerOrder"] = StandardLayers.Egress.Order.ToString();
            props["layerColor"] = StandardLayers.Egress.Color;
            props["layerIcon"] = StandardLayers.Egress.Icon;
        }

        var path = props.GetValueOrDefault("path");
        var cloudNode = new GraphNodeDto
        {
            Id = id,
            Kind = NodeKind.CloudService,
            Name = name,
            DisplayName = elem.GetStringProp("display_name", name),
            FilePath = string.IsNullOrEmpty(path) ? null : path,
            Properties = props
        };
        nodeMap[id] = cloudNode;
        graph.Nodes.Add(cloudNode);
    }

    private static void ProcessPeerMeshNode(
        string id,
        string name,
        Dictionary<string, string> props,
        Dictionary<string, GraphNodeDto> nodeMap,
        GraphDataDto graph)
    {
        if (nodeMap.ContainsKey(id)) return;

        props["is_semantic_entity"] = "true";
        props["is_library"] = "false";
        props["entity_type"] = "peer_mesh";
        props["topology"] = props.GetValueOrDefault("topology", "symmetric");

        if (!props.ContainsKey("layer"))
        {
            props["layer"] = StandardLayers.Egress.LayerId;
            props["layerId"] = StandardLayers.Egress.LayerId;
            props["layerName"] = StandardLayers.Egress.LayerName;
            props["layerOrder"] = StandardLayers.Egress.Order.ToString();
            props["layerColor"] = StandardLayers.Egress.Color;
            props["layerIcon"] = StandardLayers.Egress.Icon;
        }

        var meshNode = new GraphNodeDto
        {
            Id = id,
            Kind = NodeKind.PeerMesh,
            Name = name,
            DisplayName = name,
            FilePath = props.GetValueOrDefault("path"),
            Properties = props
        };
        nodeMap[id] = meshNode;
        graph.Nodes.Add(meshNode);
    }

    private static void ProcessExternalServiceNode(
        JsonElement elem,
        string id,
        string name,
        Dictionary<string, string> props,
        SystemContextDiscoveryState discovery,
        Dictionary<string, string> projectToWorkloadMap,
        Dictionary<string, GraphNodeDto> nodeMap,
        GraphDataDto graph)
    {
        if (nodeMap.ContainsKey(id)) return;

        if (name is "*" or "unknown-service" or "httprequest" ||
            PostIndexAnalyzer.IsGarbageExternalService(name)) return;

        var isExplicitExternal = props.GetValueOrDefault("is_external") == "true";

        if (!isExplicitExternal || (!name.Contains('.') && discovery.InternalProjectsLookup.Count > 0))
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(name);
            var extIdDomain = PostIndexAnalyzer.ExtractDomainFromExternalServiceId(id);
            var normName = EndpointScoringEngine.NormalizeAlphanumeric(name);
            var normDomain = EndpointScoringEngine.NormalizeAlphanumeric(domain);
            var normExtIdDomain = EndpointScoringEngine.NormalizeAlphanumeric(extIdDomain);

            string? matchedProjId = null;

            if (discovery.InternalProjectsLookup.TryGetValue(name, out var mpId) ||
                discovery.InternalProjectsLookup.TryGetValue(domain, out mpId) ||
                discovery.InternalProjectsLookup.TryGetValue(name.ToLowerInvariant(), out mpId) ||
                discovery.InternalProjectsLookup.TryGetValue(extIdDomain, out mpId) ||
                (!string.IsNullOrEmpty(normName) && discovery.InternalProjectsLookup.TryGetValue(normName, out mpId)) ||
                (!string.IsNullOrEmpty(normDomain) && discovery.InternalProjectsLookup.TryGetValue(normDomain, out mpId)) ||
                (!string.IsNullOrEmpty(normExtIdDomain) && discovery.InternalProjectsLookup.TryGetValue(normExtIdDomain, out mpId)))
            {
                matchedProjId = mpId;
            }
            else if (name.Contains('.'))
            {
                var sub = name.Split('.', 2)[0];
                var normSub = EndpointScoringEngine.NormalizeAlphanumeric(sub);
                if (discovery.InternalProjectsLookup.TryGetValue(sub, out mpId) ||
                    (!string.IsNullOrEmpty(normSub) && discovery.InternalProjectsLookup.TryGetValue(normSub, out mpId)))
                {
                    matchedProjId = mpId;
                }
            }

            if (matchedProjId == null && !isExplicitExternal && (discovery.ProjectCandidates.Count > 0 || discovery.EndpointCandidates.Count > 0))
            {
                var svcPath = props.GetValueOrDefault("path");
                var effectiveCall = !string.IsNullOrEmpty(svcPath) && svcPath != "/" ? svcPath : name;
                var match = EndpointScoringEngine.MatchCall(effectiveCall, null, null, discovery.ProjectCandidates, discovery.EndpointCandidates);
                if (match.IsInternal && match.Project != null)
                {
                    matchedProjId = match.Project.ProjectId;
                }
            }

            if (matchedProjId != null)
            {
                projectToWorkloadMap[id] = matchedProjId;
                return;
            }
        }

        if (props.GetValueOrDefault("inferred_from_identifier") == "true")
        {
            return;
        }

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
            dispName = st == "Service" ? name : $"{name} [{st}]";
        }

        var path = props.GetValueOrDefault("path");
        var lineStart = elem.GetNullableIntProp("start_line")
            ?? (props.TryGetValue("start_line", out var slStr) && int.TryParse(slStr, out var sl) ? sl : null);
        var lineEnd = elem.GetNullableIntProp("end_line")
            ?? (props.TryGetValue("end_line", out var elStr) && int.TryParse(elStr, out var el) ? el : null);

        var esNode = new GraphNodeDto
        {
            Id = id,
            Kind = NodeKind.ExternalService,
            Name = name,
            DisplayName = dispName,
            FilePath = string.IsNullOrEmpty(path) ? null : path,
            LineStart = lineStart,
            LineEnd = lineEnd,
            Properties = props
        };
        nodeMap[id] = esNode;
        graph.Nodes.Add(esNode);
    }

    private void ProcessProjectOrWorkloadNode(
        JsonElement elem,
        string id,
        string kind,
        string name,
        Dictionary<string, string> props,
        bool includeLibraries,
        Dictionary<string, string> projectToWorkloadMap,
        Dictionary<string, GraphNodeDto> nodeMap,
        GraphDataDto graph)
    {
        var isWorkload = kind is "Service" or "App" or "Worker" or "Library" or "CliTool" or "FrontendApp"
            or "SharedLibrary" or "TestSuite" or "Test";
        var associatedProjectId = props.GetValueOrDefault("project_id") ?? props.GetValueOrDefault("associated_project_id");

        if (string.IsNullOrEmpty(associatedProjectId))
        {
            associatedProjectId =
                (id.Contains($":{OntologyConstants.IdPrefixes.Project}:",
                     StringComparison.OrdinalIgnoreCase) ||
                 id.Contains(":project:", StringComparison.OrdinalIgnoreCase))
                    ? id
                    : $"{id.Split(':')[0]}:{OntologyConstants.IdPrefixes.Project}:{name}";
        }

        if (isWorkload)
        {
            projectToWorkloadMap[associatedProjectId] = id;
            var trimmedAssoc = associatedProjectId.TrimEnd(':');
            projectToWorkloadMap[trimmedAssoc] = id;
            projectToWorkloadMap[trimmedAssoc + ":"] = id;

            GraphNodeDto? rawProj = null;
            if (!nodeMap.TryGetValue(associatedProjectId, out rawProj) &&
                !nodeMap.TryGetValue(trimmedAssoc, out rawProj) &&
                !nodeMap.TryGetValue(trimmedAssoc + ":", out rawProj))
            {
                var nPath = props.GetValueOrDefault("path");
                rawProj = nodeMap.Values.FirstOrDefault(p =>
                    p.Kind == NodeKind.Project && (
                        (!string.IsNullOrEmpty(nPath) && string.Equals(p.FilePath, nPath, StringComparison.OrdinalIgnoreCase)) ||
                        string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));
            }

            if (rawProj != null)
            {
                projectToWorkloadMap[rawProj.Id] = id;
                projectToWorkloadMap[rawProj.Id.TrimEnd(':')] = id;
                projectToWorkloadMap[rawProj.Id.TrimEnd(':') + ":"] = id;

                if (rawProj.Properties != null)
                {
                    foreach (var key in new[]
                             {
                                 "path", "FilePath", "layer", "layerId", "layerName", "layerOrder", "layerColor",
                                 "layerIcon", "primary_role", "secondary_roles", "all_roles"
                             })
                    {
                        if (!props.ContainsKey(key) && rawProj.Properties.TryGetValue(key, out var val))
                        {
                            props[key] = val;
                        }
                    }
                }
                if (!props.ContainsKey("path") && !string.IsNullOrEmpty(rawProj.FilePath))
                {
                    props["path"] = rawProj.FilePath;
                }

                graph.Nodes.Remove(rawProj);
                nodeMap.Remove(rawProj.Id);
            }
        }
        else if (kind == "Project")
        {
            string? workloadId = null;
            if (projectToWorkloadMap.TryGetValue(id, out var wId) ||
                projectToWorkloadMap.TryGetValue(id.TrimEnd(':'), out wId) ||
                projectToWorkloadMap.TryGetValue(id.TrimEnd(':') + ":", out wId))
            {
                workloadId = wId;
            }
            else
            {
                var pPath = props.GetValueOrDefault("path");
                var matchingWorkload = nodeMap.Values.FirstOrDefault(w =>
                    (w.Kind is NodeKind.App or NodeKind.Service or NodeKind.Worker or NodeKind.CliTool or NodeKind.Library or NodeKind.TestSuite) &&
                    ((!string.IsNullOrEmpty(pPath) && string.Equals(w.FilePath, pPath, StringComparison.OrdinalIgnoreCase)) ||
                     string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)));
                if (matchingWorkload != null)
                {
                    workloadId = matchingWorkload.Id;
                    projectToWorkloadMap[id] = workloadId;
                    projectToWorkloadMap[id.TrimEnd(':')] = workloadId;
                    projectToWorkloadMap[id.TrimEnd(':') + ":"] = workloadId;
                }
            }

            if (workloadId != null && nodeMap.TryGetValue(workloadId, out var existingWorkload))
            {
                if (existingWorkload.Properties != null && props != null)
                {
                    foreach (var key in new[]
                             {
                                 "path", "FilePath", "layer", "layerId", "layerName", "layerOrder", "layerColor",
                                 "layerIcon", "primary_role", "secondary_roles", "all_roles"
                             })
                    {
                        if (!existingWorkload.Properties.ContainsKey(key) &&
                            props.TryGetValue(key, out var val))
                        {
                            existingWorkload.Properties[key] = val;
                        }
                    }
                }
                if (string.IsNullOrEmpty(existingWorkload.FilePath) && props != null && props.TryGetValue("path", out var pVal))
                {
                    existingWorkload.FilePath = pVal;
                }

                return;
            }

            if (workloadId != null)
            {
                return;
            }
        }

        if (nodeMap.ContainsKey(id)) return;

        var framework = props.GetValueOrDefault("framework", "");
        var dispName = elem.GetStringProp("display_name");

        if (string.IsNullOrEmpty(dispName))
        {
            dispName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})";
        }

        var path = props.GetValueOrDefault("path");
        var lineStart = elem.GetNullableIntProp("start_line")
            ?? (props.TryGetValue("start_line", out var slStr) && int.TryParse(slStr, out var sl) ? sl : null);
        var lineEnd = elem.GetNullableIntProp("end_line")
            ?? (props.TryGetValue("end_line", out var elStr) && int.TryParse(elStr, out var el) ? el : null);

        var projNode = new GraphNodeDto
        {
            Id = id,
            Kind = !string.IsNullOrEmpty(kind) ? EnumExtensions.ParseNodeKind(kind) : NodeKind.Project,
            Name = name,
            DisplayName = dispName,
            FilePath = string.IsNullOrEmpty(path) ? null : path,
            LineStart = lineStart,
            LineEnd = lineEnd,
            Properties = props
        };
        var isLibProject = IsLibraryProject(projNode);

        if (!includeLibraries && isLibProject)
        {
            return;
        }

        var role = props.GetValueOrDefault("role", "");
        var isCliTool = projNode.Kind == NodeKind.CliTool ||
                        string.Equals(role, "CliTool", StringComparison.OrdinalIgnoreCase);
        var isApp = projNode.Kind == NodeKind.App ||
                    kind == "App" || kind == "FrontendApp" ||
                    string.Equals(role, "FrontendApp", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, "App", StringComparison.OrdinalIgnoreCase);
        var isWorker = projNode.Kind == NodeKind.Worker ||
                       kind == "Worker" ||
                       string.Equals(role, "Worker", StringComparison.OrdinalIgnoreCase);
        var isTestSuite = projNode.Kind == NodeKind.TestSuite ||
                          kind == "TestSuite" || kind == "Test" ||
                          string.Equals(role, "Test", StringComparison.OrdinalIgnoreCase);

        if (isApp)
        {
            projNode.Kind = NodeKind.App;
        }
        else if (isCliTool)
        {
            projNode.Kind = NodeKind.CliTool;
        }
        else if (isWorker)
        {
            projNode.Kind = NodeKind.Worker;
        }
        else if (isTestSuite)
        {
            projNode.Kind = NodeKind.TestSuite;
        }

        var isArchitecturalSubsystem = IsArchitecturalSubsystem(projNode);
        props["is_library"] = isLibProject ? "true" : "false";
        props["entity_type"] = isLibProject && !isArchitecturalSubsystem ? "library"
            : (isApp ? "app"
            : (isCliTool ? "clitool"
            : (isWorker ? "worker"
            : (isTestSuite ? "testsuite" : "service"))));
        props["is_semantic_entity"] = isLibProject && !isArchitecturalSubsystem ? "false" : "true";

        if (!props.ContainsKey("layer"))
        {
            var isAppOrFrontend = kind is "App" or "FrontendApp" ||
                                  isCliTool ||
                                  string.Equals(props.GetValueOrDefault("role"), "FrontendApp",
                                      StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(props.GetValueOrDefault("role"), "App",
                                      StringComparison.OrdinalIgnoreCase) ||
                                  (name?.ToLowerInvariant().Contains("gateway") == true);

            var defaultLayer = isLibProject && !isArchitecturalSubsystem ? StandardLayers.Foundation :
                (isAppOrFrontend || isCliTool) ? StandardLayers.Ingress : StandardLayers.Components;
            props["layer"] = defaultLayer.LayerId;
            props["layerId"] = defaultLayer.LayerId;
            props["layerName"] = defaultLayer.LayerName;
            props["layerOrder"] = defaultLayer.Order.ToString();
            props["layerColor"] = defaultLayer.Color;
            props["layerIcon"] = defaultLayer.Icon;
        }

        var (domKey, domDisp, _) = ExtractDomainKey(projNode);
        if (!props.ContainsKey("domainId"))
        {
            props["domainId"] = domKey;
        }
        if (!props.ContainsKey("domain"))
        {
            var cleanDomName = domKey.StartWithIC("domain:")
                ? domKey["domain:".Length..]
                : domKey;
            props["domain"] = ToPascalCase(cleanDomName);
        }
        if (!props.ContainsKey("domainDisplayName"))
        {
            props["domainDisplayName"] = domDisp;
        }

        nodeMap[id] = projNode;
        graph.Nodes.Add(projNode);
    }
}
