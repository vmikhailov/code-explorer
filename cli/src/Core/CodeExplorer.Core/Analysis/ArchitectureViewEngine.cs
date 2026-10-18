using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
using ArchitectureViewType = CodeExplorer.Core.Analysis.ArchitectureViewType;

namespace CodeExplorer.Core.Analysis;

public enum ArchitectureViewType
{
    SystemContext,  // C1: Macro System Context view (Services, Databases, Topics, ExternalServices)
    ServiceFlow,    // C2: Container & Service Flow view (Interactions, Messaging, Shared Libraries)
    Component,      // C3: Component Drill-down for a selected container/project
    DomainMap,      // Bounded Contexts / Domain Microservices Map
    Tiers,          // Layered Architecture Tiers (Ingress, Domain, Infra, Foundation, Tests)
    BoundedContexts // AI-Distilled Bounded Contexts Map (Logical DDD domains, entities, capabilities, CQRS, and cross-context links)
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
public partial class ArchitectureViewEngine(IGraphClient db)
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GraphDataDto> SystemContextCache = new();

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
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
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
            return sqliteClient.DbPath;
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
            ArchitectureViewType.DomainMap => await GetDomainArchitectureGraphAsync(false, request.SharedDatabasesOnly, ct),
            ArchitectureViewType.Tiers => await GetTieredArchitectureGraphAsync(request.IncludeLibraries, ct),
            ArchitectureViewType.BoundedContexts => await GetBoundedContextGraphAsync(request.Scope, ct),
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

                var domain = SyntaxEnricher.CleanProjectNameToDomain(nname);
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
                        ? projectCandidates.FirstOrDefault(p => !string.IsNullOrEmpty(p.Path) && epPath.StartsWith(p.Path, StringComparison.OrdinalIgnoreCase))
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
                                 id.StartsWith("urn:", StringComparison.OrdinalIgnoreCase);
        var isStandaloneDb = id.StartsWith("db:", StringComparison.OrdinalIgnoreCase);

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

        if (!isExplicitExternal)
        {
            var domain = SyntaxEnricher.CleanProjectNameToDomain(name);
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

            if (matchedProjId == null && (discovery.ProjectCandidates.Count > 0 || discovery.EndpointCandidates.Count > 0))
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

            if (nodeMap.TryGetValue(associatedProjectId, out var rawProj) ||
                nodeMap.TryGetValue(trimmedAssoc, out rawProj) ||
                nodeMap.TryGetValue(trimmedAssoc + ":", out rawProj))
            {
                if (rawProj.Properties != null)
                {
                    foreach (var key in new[]
                             {
                                 "path", "FilePath", "layer", "layerId", "layerName", "layerOrder", "layerColor",
                                 "layerIcon"
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
            if ((projectToWorkloadMap.TryGetValue(id, out var workloadId) ||
                 projectToWorkloadMap.TryGetValue(id.TrimEnd(':'), out workloadId) ||
                 projectToWorkloadMap.TryGetValue(id.TrimEnd(':') + ":", out workloadId)) &&
                nodeMap.TryGetValue(workloadId, out var existingWorkload))
            {
                if (existingWorkload.Properties != null && props != null)
                {
                    foreach (var key in new[]
                             {
                                 "path", "FilePath", "layer", "layerId", "layerName", "layerOrder", "layerColor",
                                 "layerIcon"
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

            if (projectToWorkloadMap.ContainsKey(id) ||
                projectToWorkloadMap.ContainsKey(id.TrimEnd(':')) ||
                projectToWorkloadMap.ContainsKey(id.TrimEnd(':') + ":"))
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

        var role = props.GetValueOrDefault("role");
        var isCliTool = projNode.Kind == NodeKind.CliTool ||
                        string.Equals(role, "CliTool", StringComparison.OrdinalIgnoreCase);

        if (isCliTool)
        {
            projNode.Kind = NodeKind.CliTool;
        }

        props["is_library"] = isLibProject ? "true" : "false";
        props["entity_type"] = isLibProject ? "library" : (isCliTool ? "clitool" : "service");
        props["is_semantic_entity"] = isLibProject ? "false" : "true";

        if (!props.ContainsKey("layer"))
        {
            var isAppOrFrontend = kind is "App" or "FrontendApp" ||
                                  isCliTool ||
                                  string.Equals(props.GetValueOrDefault("role"), "FrontendApp",
                                      StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(props.GetValueOrDefault("role"), "App",
                                      StringComparison.OrdinalIgnoreCase) ||
                                  (name?.ToLowerInvariant().Contains("gateway") == true);

            var defaultLayer = isLibProject ? StandardLayers.Foundation :
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
            var cleanDomName = domKey.StartsWith("domain:", StringComparison.OrdinalIgnoreCase)
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
        foreach (var elem in nodesDoc.RootElement.EnumerateArray())
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
            "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['SERVICE_CALL', 'DEPENDS_ON', 'USES_DB', 'PUBLISHES_TO', 'TRIGGERS', 'SUBSCRIBES_TO', 'INTEGRATES_WITH', 'CALLS_ENDPOINT', 'LIBRARY', 'SUBSCRIBED_BY', 'PUBLISHED_BY'] RETURN src.id AS from_id, tgt.id AS to_id, r.kind AS kind, r.properties AS properties";
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

            if (isTargetLib)
            {
                category = DependencyCategory.Library;
                depType = DependencyCategoryExtensions.Library;
                normalizedKind = (rKind is "DEPENDS_ON" or "DependsOn") ? "DEPENDS_ON" : "LIBRARY";
            }

            var categoryStr = category.ToCategoryString();
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

            var outKind = category switch
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

        var nodesQuery = includeLibraries
            ? $"MATCH (n) WHERE labels(n)[0] IN ['Project', 'Service', 'App', 'Library', 'Worker', 'CliTool', 'TestSuite', 'Database', 'ExternalService']{topicClause} OR (labels(n)[0] = 'Package' AND (n.is_external = 'true' OR n.is_external = true OR (n.is_external IS NULL AND NOT (n)-[:IMPLEMENTED_BY]->(:Project)))) RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.display_name AS display_name, n.path AS path, n.framework AS framework, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type, n.package_type AS package_type, n.type AS pkg_type, n.version AS version, n.properties AS properties, n.layer AS layer, n.layerId AS layerId, n.layerName AS layerName, n.layerOrder AS layerOrder, n.layerColor AS layerColor, n.layerIcon AS layerIcon, n.package_count AS package_count"
            : $"MATCH (n) WHERE labels(n)[0] IN ['Service', 'App', 'Worker', 'CliTool', 'TestSuite', 'Database', 'ExternalService']{topicClause} OR (labels(n)[0] = 'Project' AND (n.is_library <> true OR n.is_library IS NULL) AND (n.is_library <> 'true' OR n.is_library IS NULL) AND (n.role <> 'SharedLibrary' OR n.role IS NULL) AND (n.role <> 'Test' OR n.role IS NULL) AND (n.entity_kind <> 'Test' OR n.entity_kind IS NULL)) RETURN n.id AS id, labels(n)[0] AS kind, n.name AS name, n.display_name AS display_name, n.path AS path, n.framework AS framework, n.role AS role, n.is_library AS is_library, n.db_type AS db_type, n.project_type AS project_type, n.package_type AS package_type, n.type AS pkg_type, n.version AS version, n.properties AS properties, n.layer AS layer, n.layerId AS layerId, n.layerName AS layerName, n.layerOrder AS layerOrder, n.layerColor AS layerColor, n.layerIcon AS layerIcon, n.package_count AS package_count";

        var nodesJson = await db.ExecuteQueryAsync(nodesQuery, null, ct);
        using var nodesDoc = JsonDocument.Parse(nodesJson);

        var discovery = await DiscoverSystemContextCandidatesAsync(nodesDoc, ct);

        PopulateSystemContextNodes(nodesDoc, graph, nodeMap, dbIdToCanonicalId, projectToWorkloadMap, discovery, includeLibraries, projectFilter);

        await PopulatePackageCountsAsync(graph, nodeMap, ct);

        var projectNodes = graph.Nodes.Where(n => IsProjectNodeKind(n.Kind)).ToList();

        await PopulateSystemContextEdgesAsync(graph, nodeMap, dbIdToCanonicalId, projectToWorkloadMap, projectNodes, ct);

        LiftTransitiveSemanticRelations(graph);

        if (sharedDatabasesOnly)
        {
            FilterSharedDatabases(graph);
        }

        var allProjects = await GetAllProjectsAsync(ct);
        graph.Metadata["allProjects"] = JsonSerializer.Serialize(allProjects);
        var projectPaths = await GetProjectPathsMapAsync(ct);
        graph.Metadata["projectPaths"] = JsonSerializer.Serialize(projectPaths);

        return graph;
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
             (n.Id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase) &&
              n.Id["workspace:project:".Length..].TrimEnd(':').Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             (n.Id.StartsWith($"workspace:{OntologyConstants.IdPrefixes.Project}:", StringComparison.OrdinalIgnoreCase) &&
              n.Id[$"workspace:{OntologyConstants.IdPrefixes.Project}:".Length..].TrimEnd(':').Equals(targetName, StringComparison.OrdinalIgnoreCase)) ||
             (n.Id.StartsWith($"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Project}:", StringComparison.OrdinalIgnoreCase) &&
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
            var id = elem.GetProperty("id").GetString()!;
            var kind = elem.GetProperty("kind").GetString()!;
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
                    var fromId = elem.GetProperty("from_id").GetString()!;
                    var toId = elem.GetProperty("to_id").GetString()!;
                    var kind = elem.GetProperty("kind").GetString()!;

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
            if (sourceId.StartsWith(trimId + ":", StringComparison.OrdinalIgnoreCase) ||
                sourceId.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase))
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
        if (bestMatch != null) return bestMatch;

        // Match by Project Name segment in path (only as a fallback when no project matched by path)
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
