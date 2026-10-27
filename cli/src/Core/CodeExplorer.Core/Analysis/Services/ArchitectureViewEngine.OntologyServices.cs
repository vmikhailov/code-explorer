using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    // =========================================================================
    // 5. Ontology Layers & Taxonomy
    // =========================================================================

    public async Task<OntologyLayersResponseDto> GetOntologyLayersAsync(CancellationToken ct = default)
    {
        var meta = await GetMetadataAsync(ct);
        var counts = meta.NodeCounts;
        var relCounts = meta.RelationshipCounts;

        var l1Categories = new List<OntologyCategoryDto>();
        var l2Categories = new List<OntologyCategoryDto>();
        var l3Categories = new List<OntologyCategoryDto>();
        var l4Categories = new List<OntologyCategoryDto>();
        var l5Categories = new List<OntologyCategoryDto>();

        foreach (var node in Mcp.OntologyRegistry.AllNodes.OrderBy(n => n.Attribute.Order))
        {
            var attr = node.Attribute;
            var kind = node.Kind;
            if (attr.IsSystemNode || Mcp.OntologyRegistry.IsSystemNode(kind))
            {
                continue;
            }
            var count = counts.GetValueOrDefault(kind, 0);

            if (attr.Layer == OntologyConstants.Layers.Physical)
            {
                l1Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 1 });
            }
            else if (attr.Layer == OntologyConstants.Layers.ProjectBoundary)
            {
                if (kind is "EntryPoint" && count == 0)
                {
                    continue;
                }
                l2Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 2 });
            }
            else if (attr.Layer == OntologyConstants.Layers.Syntactic)
            {
                l3Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 3 });
            }
            else if (attr.Layer == OntologyConstants.Layers.Semantic)
            {
                if (kind is "DataSet" || (kind is "Procedure" or "Query" or "ApiInUse" && count == 0))
                {
                    continue;
                }
                l4Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 4 });
            }
            else if (attr.Layer == OntologyConstants.Layers.Logical)
            {
                l5Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 5 });
            }
        }

        var l1 = new OntologyLayerDto
        {
            LayerId = 1,
            Name = "File Structure",
            Title = "Layer 1: File Structure",
            Description = "Files, Folders, and Git configuration",
            Icon = "folder-library",
            TotalCount = l1Categories.Sum(c => c.Count),
            Categories = l1Categories
        };

        var l2 = new OntologyLayerDto
        {
            LayerId = 2,
            Name = "Project Structure",
            Title = "Layer 2: Project Structure",
            Description = "Logical compilation scopes, projects, and external packages",
            Icon = "project",
            TotalCount = l2Categories.Sum(c => c.Count),
            Categories = l2Categories
        };

        var l3 = new OntologyLayerDto
        {
            LayerId = 3,
            Name = "Syntax Structure",
            Title = "Layer 3: Syntax Structure",
            Description = "Abstract Syntax Tree declarations (Types, Methods, Fields, Variables)",
            Icon = "symbol-structure",
            TotalCount = l3Categories.Sum(c => c.Count),
            Categories = l3Categories
        };

        var l4 = new OntologyLayerDto
        {
            LayerId = 4,
            Name = "Semantic Structure",
            Title = "Layer 4: Semantic Structure",
            Description = "Runtime architecture (Services, Endpoints, Databases, Topics, External APIs)",
            Icon = "radio-tower",
            TotalCount = l4Categories.Sum(c => c.Count),
            Categories = l4Categories
        };

        var l5 = new OntologyLayerDto
        {
            LayerId = 5,
            Name = "Logical Structure",
            Title = "Layer 5: Logical Structure",
            Description = "Domain-Driven Design architecture (Domains, Bounded Contexts)",
            Icon = "globe",
            TotalCount = l5Categories.Sum(c => c.Count),
            Categories = l5Categories
        };

        return new OntologyLayersResponseDto
        {
            Layers = [l1, l2, l3, l4, l5],
            TotalNodes = l1Categories.Sum(c => c.Count) + l2Categories.Sum(c => c.Count) + l3Categories.Sum(c => c.Count) + l4Categories.Sum(c => c.Count) + l5Categories.Sum(c => c.Count),
            TotalEdges = meta.TotalEdges > 0 ? meta.TotalEdges : relCounts.Values.Sum()
        };
    }

    public async Task<List<ServiceSummaryDto>> GetServicesOntologySummaryAsync(CancellationToken ct = default)
    {
        var list = new List<ServiceSummaryDto>();
        try
        {
            var query = "MATCH (s) WHERE (s:Service OR s:App OR s:Worker OR s:CliTool OR s:FrontendApp OR s:TestSuite) RETURN DISTINCT s.id AS id, coalesce(s.name, s.id) AS name, labels(s) AS lbl, s.role AS role, s.primary_role AS primary_role, s.secondary_roles AS secondary_roles, s.framework AS framework, s.language AS language ORDER BY s.name ASC";
            var json = await db.ExecuteQueryAsync(query, null, ct);

            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            JsonDocument? fallbackDoc = null;

            if (root.GetArrayLength() == 0)
            {
                var fallbackQuery =
                    "MATCH (s:Project) WHERE json_extract(s.properties, '$.role') IN ['Service', 'App', 'FrontendApp', 'Worker', 'CliTool', 'Test', 'TestSuite'] RETURN DISTINCT s.id AS id, coalesce(s.name, s.id) AS name, labels(s) AS lbl, s.role AS role, s.primary_role AS primary_role, s.secondary_roles AS secondary_roles, s.framework AS framework, s.language AS language ORDER BY s.name ASC";
                var fbJson = await db.ExecuteQueryAsync(fallbackQuery, null, ct);
                fallbackDoc = JsonDocument.Parse(fbJson);
                root = fallbackDoc.RootElement;
            }

            var serviceMap = new Dictionary<string, ServiceSummaryDto>(StringComparer.OrdinalIgnoreCase);
            var idToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in root.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var nodeKind = NodeKind.Service;

                if (row.TryGetProperty("role", out var roleProp) && roleProp.ValueKind == JsonValueKind.String)
                {
                    var r = roleProp.GetString();

                    if (r is "FrontendApp" or "App") nodeKind = NodeKind.App;
                    else if (r is "Worker") nodeKind = NodeKind.Worker;
                    else if (r is "SharedLibrary" or "Library") nodeKind = NodeKind.Library;
                    else if (r is "CliTool" or "ScriptProject") nodeKind = NodeKind.CliTool;
                    else if (r is "Test" or "TestSuite") nodeKind = NodeKind.TestSuite;
                }
                else if (row.TryGetProperty("lbl", out var lblProp) && lblProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var l in lblProp.EnumerateArray())
                    {
                        var ls = l.GetString();

                        if (ls is "Service" or "Worker" or "App" or "CliTool" or "FrontendApp" or "TestSuite" or "Test" or "ScriptProject")
                        {
                            nodeKind = ls is "ScriptProject" ? NodeKind.CliTool : EnumExtensions.ParseNodeKind(ls);
                            break;
                        }
                    }
                }

                var primaryRole = row.GetStringProp("primary_role");
                if (string.IsNullOrEmpty(primaryRole) && row.TryGetProperty("role", out var rProp))
                {
                    primaryRole = rProp.GetString();
                }

                var secondaryRolesList = new List<string>();
                if (row.TryGetProperty("secondary_roles", out var secProp))
                {
                    if (secProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in secProp.EnumerateArray())
                        {
                            var val = el.GetString();
                            if (!string.IsNullOrWhiteSpace(val) && !secondaryRolesList.Contains(val, StringComparer.OrdinalIgnoreCase))
                                secondaryRolesList.Add(val);
                        }
                    }
                    else if (secProp.ValueKind == JsonValueKind.String)
                    {
                        var rawSec = secProp.GetString();
                        if (!string.IsNullOrWhiteSpace(rawSec))
                        {
                            if (rawSec.TrimStart().StartsWith("["))
                            {
                                try
                                {
                                    using var secDoc = JsonDocument.Parse(rawSec);
                                    if (secDoc.RootElement.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var el in secDoc.RootElement.EnumerateArray())
                                        {
                                            var val = el.GetString();
                                            if (!string.IsNullOrWhiteSpace(val) && !secondaryRolesList.Contains(val, StringComparer.OrdinalIgnoreCase))
                                                secondaryRolesList.Add(val);
                                        }
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                foreach (var part in rawSec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                {
                                    if (!string.IsNullOrWhiteSpace(part) && !secondaryRolesList.Contains(part, StringComparer.OrdinalIgnoreCase))
                                        secondaryRolesList.Add(part);
                                }
                            }
                        }
                    }
                }

                if (!serviceMap.TryGetValue(name, out var existing))
                {
                    var dto = new ServiceSummaryDto
                    {
                        ServiceId = id,
                        ServiceName = name,
                        Kind = nodeKind,
                        PrimaryRole = primaryRole,
                        SecondaryRoles = secondaryRolesList,
                        Framework = row.GetStringProp("framework"),
                        Language = row.GetStringProp("language")
                    };
                    serviceMap[name] = dto;
                }
                else
                {
                    if (existing.Kind == NodeKind.Project && nodeKind != NodeKind.Project)
                    {
                        existing.ServiceId = id;
                        existing.Kind = nodeKind;
                    }
                    if (!string.IsNullOrEmpty(primaryRole) && string.IsNullOrEmpty(existing.PrimaryRole))
                        existing.PrimaryRole = primaryRole;
                    if (secondaryRolesList.Count > 0 && existing.SecondaryRoles.Count == 0)
                        existing.SecondaryRoles = secondaryRolesList;

                    if (!string.IsNullOrEmpty(row.GetStringProp("framework")))
                        existing.Framework = row.GetStringProp("framework");

                    if (!string.IsNullOrEmpty(row.GetStringProp("language")))
                        existing.Language = row.GetStringProp("language");
                }

                idToName[id] = name;
            }

            fallbackDoc?.Dispose();

            // Tally endpoints directly from graph
            try
            {
                string epJson;
                if (db is SqliteGraphClient sqliteClient)
                {
                    const string fastEpQuery = """
                        SELECT coalesce(json_extract(s.properties, '$.name'), s.id) AS sName, s.id AS sId, COUNT(ep.id) AS epCount
                        FROM nodes ep
                        JOIN edges _r0 ON _r0.to_id = ep.id AND _r0.kind = 'CONTAINS'
                        JOIN nodes s ON s.id = _r0.from_id
                        WHERE ep.kind = 'Endpoint'
                          AND (s.kind IN ('Project', 'Service', 'App', 'Worker', 'CliTool', 'TestSuite'))
                        GROUP BY sName, sId;
                        """;
                    epJson = await sqliteClient.ExecuteRawAsync(fastEpQuery, null, ct);
                }
                else
                {
                    var epCountQuery =
                        "MATCH (s)-[:CONTAINS]->(ep:Endpoint) WHERE (s:Project OR s:Service OR s:App OR s:Worker OR s:CliTool OR s:TestSuite) RETURN coalesce(s.name, s.id) AS sName, s.id AS sId, count(ep) AS epCount";
                    epJson = await db.ExecuteQueryAsync(epCountQuery, null, ct);
                }

                using var epDoc = JsonDocument.Parse(epJson);

                foreach (var row in epDoc.RootElement.EnumerateArray())
                {
                    var sName = row.GetStringProp("sName");
                    var sId = row.GetStringProp("sId");
                    ServiceSummaryDto? summary = null;

                    if (!string.IsNullOrEmpty(sName) && serviceMap.TryGetValue(sName, out summary))
                    {
                    }
                    else if (!string.IsNullOrEmpty(sId) && idToName.TryGetValue(sId, out var mappedName) &&
                             serviceMap.TryGetValue(mappedName, out summary))
                    {
                    }

                    if (summary != null && row.TryGetProperty("epCount", out var cp) &&
                        cp.ValueKind == JsonValueKind.Number)
                    {
                        summary.EndpointCount = cp.GetInt32();
                    }
                }
            }
            catch
            {
            }

            // Tally databases, topics, downstream services, and external APIs from system context view
            var archGraph = await GetSystemContextViewAsync(includeLibraries: true, projectFilter: null, ct: ct);
            var nodeMap = archGraph.Nodes.GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var projectNodes = archGraph.Nodes.Where(n => IsProjectNodeKind(n.Kind)).ToList();
            var internalProjectsByName = BuildInternalProjectsLookup(projectNodes);

            var seenServiceDbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenServiceTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenServiceSvcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenServiceExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var edge in archGraph.Edges)
            {
                var srcNode = nodeMap.GetValueOrDefault(edge.Source);
                var tgtNode = nodeMap.GetValueOrDefault(edge.Target);

                if (srcNode != null && serviceMap.TryGetValue(srcNode.Name, out var srcSummary))
                {
                    var tgtName = tgtNode?.Name ?? edge.Target;

                    if (edge.Category == "database" || edge.Kind == "USES_DB" || tgtNode?.Kind == NodeKind.Database)
                    {
                        if (seenServiceDbs.Add($"{srcSummary.ServiceName}->{tgtName}"))
                        {
                            srcSummary.DatabaseCount++;
                        }
                    }
                    else if (edge.Category == "messaging" || edge.Kind is "PUBLISHES_TO" or "TRIGGERS" ||
                             tgtNode?.Kind == NodeKind.Topic)
                    {
                        var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                         tgtNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                         string.Equals(tgtNode?.Properties?.GetValueOrDefault("broker_type"),
                                             "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                         edge.Target.Contains(":mediatr:");

                        if (!isInternal && seenServiceTopics.Add($"{srcSummary.ServiceName}->{tgtName}"))
                        {
                            srcSummary.TopicCount++;
                        }
                    }
                    else
                    {
                        var isInternalDirect = tgtNode != null && IsProjectNodeKind(tgtNode.Kind) &&
                                               tgtNode.Kind is not (NodeKind.ExternalService or NodeKind.CloudService);

                        var isServiceCall = edge.Category == "service_call" ||
                                            edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT" ||
                                            (edge.Kind == "DEPENDS_ON" &&
                                             edge.Properties?.GetValueOrDefault("dependency_type") ==
                                             "service_call");

                        if (isInternalDirect && isServiceCall)
                        {
                            if (seenServiceSvcs.Add($"{srcSummary.ServiceName}->{tgtName}"))
                            {
                                srcSummary.ServiceCount++;
                            }
                        }
                        else if (tgtNode?.Kind is NodeKind.ExternalService or NodeKind.CloudService ||
                                 (!isInternalDirect && (edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT")))
                        {
                            var resolved = ResolveInternalServiceNode(tgtName, internalProjectsByName);

                            if (resolved != null)
                            {
                                if (seenServiceSvcs.Add($"{srcSummary.ServiceName}->{resolved.Name}"))
                                {
                                    srcSummary.ServiceCount++;
                                }
                            }
                            else if (IsGenericPlaceholder(tgtName))
                            {
                                // Skip generic/wildcard tokens
                            }
                            else if (tgtName.StartWithIC("environment.") ||
                                     tgtName.StartWithIC("env."))
                            {
                                var cleanSvc = Regex.Replace(tgtName, @"^(environment\.|env\.)", "",
                                    RegexOptions.IgnoreCase);

                                if (seenServiceSvcs.Add($"{srcSummary.ServiceName}->{cleanSvc}"))
                                {
                                    srcSummary.ServiceCount++;
                                }
                            }
                            else
                            {
                                if (seenServiceExt.Add($"{srcSummary.ServiceName}->{tgtName}"))
                                {
                                    srcSummary.ExternalCount++;
                                }
                            }
                        }
                    }
                }

                if (tgtNode != null && serviceMap.TryGetValue(tgtNode.Name, out var tgtSummary))
                {
                    if (edge.Category == "messaging" || edge.Kind is "SUBSCRIBED_BY" or "SUBSCRIBES_TO" ||
                        srcNode?.Kind == NodeKind.Topic)
                    {
                        var topicName = srcNode?.Name ?? edge.Source;

                        var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                         srcNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                         string.Equals(srcNode?.Properties?.GetValueOrDefault("broker_type"),
                                             "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                         edge.Source.Contains(":mediatr:");

                        if (!isInternal && seenServiceTopics.Add($"{tgtSummary.ServiceName}<-{topicName}"))
                        {
                            tgtSummary.TopicCount++;
                        }
                    }
                }
            }

            list.AddRange(serviceMap.Values.OrderBy(s => s.ServiceName, StringComparer.OrdinalIgnoreCase));
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
            Kind = targetNode?.Kind ?? NodeKind.Service,
            Framework = targetNode?.Properties?.GetValueOrDefault("framework"),
            Language = targetNode?.Properties?.GetValueOrDefault("language") ?? targetNode?.Properties?.GetValueOrDefault("project_type")
        };

        var targetId = targetNode?.Id ?? serviceName;

        var epGroup = new ServiceOntologyGroupDto { CategoryKey = "endpoints", Label = "API Endpoints", Icon = "radio-tower" };
        var svcGroup = new ServiceOntologyGroupDto { CategoryKey = "services", Label = "Downstream Services", Icon = "zap" };
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

                if (row.TryGetProperty("line", out var lp) && lp.ValueKind == JsonValueKind.Number)
                    line = lp.GetInt32();

                epGroup.Items.Add(new ServiceCapabilityItemDto
                {
                    Id = id,
                    Name = name,
                    Kind = NodeKind.Endpoint,
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

        // 2. Downstream Services, Databases, Topics, and External Services from archGraph
        var projectNodes = archGraph.Nodes.Where(n => IsProjectNodeKind(n.Kind)).ToList();
        var internalProjectsByName = BuildInternalProjectsLookup(projectNodes);

        var seenDbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in archGraph.Edges)
        {
            if (edge.Source.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            {
                var tgtNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Target, StringComparison.OrdinalIgnoreCase));
                var tgtName = tgtNode?.Name ?? edge.Target;

                if (edge.Category == "database" || edge.Kind == "USES_DB" || tgtNode?.Kind == NodeKind.Database)
                {
                    if (seenDbs.Add(tgtName))
                    {
                        dbGroup.Items.Add(new ServiceCapabilityItemDto
                        {
                            Id = tgtNode?.Id ?? edge.Target,
                            Name = tgtName,
                            Kind = NodeKind.Database,
                            Details = tgtNode?.Properties?.GetValueOrDefault("engine") ?? tgtNode?.Properties?.GetValueOrDefault("db_type") ?? "database",
                            FilePath = tgtNode?.FilePath,
                            Line = tgtNode?.LineStart
                        });
                    }
                }
                else if (edge.Category == "messaging" || edge.Kind is "PUBLISHES_TO" or "TRIGGERS" or "SUBSCRIBES_TO" || tgtNode?.Kind == NodeKind.Topic)
                {
                    var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                     tgtNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                     string.Equals(tgtNode?.Properties?.GetValueOrDefault("broker_type"), "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                     edge.Target.Contains(":mediatr:");

                    if (!isInternal && seenTopics.Add(tgtName))
                    {
                        var isSubscriber = edge.Kind is "SUBSCRIBES_TO" or "SUBSCRIBES" or "SUBSCRIBED_BY" or "CONSUMES";
                        topicGroup.Items.Add(new ServiceCapabilityItemDto
                        {
                            Id = tgtNode?.Id ?? edge.Target,
                            Name = tgtName,
                            Kind = NodeKind.Topic,
                            Details = isSubscriber ? "Subscribed topic" : "Published topic",
                            FilePath = tgtNode?.FilePath,
                            Line = tgtNode?.LineStart
                        });
                    }
                }
                else
                {
                    var isInternalDirect = tgtNode != null && IsProjectNodeKind(tgtNode.Kind) && tgtNode.Kind is not (NodeKind.ExternalService or NodeKind.CloudService);
                    var isServiceCall = edge.Category == "service_call" || edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT" || (edge.Kind == "DEPENDS_ON" && edge.Properties?.GetValueOrDefault("dependency_type") == "service_call");

                    if (isInternalDirect && isServiceCall)
                    {
                        if (seenServices.Add(tgtName))
                        {
                            var role = tgtNode?.Properties?.GetValueOrDefault("role");
                            var fw = tgtNode?.Properties?.GetValueOrDefault("framework");
                            var lang = tgtNode?.Properties?.GetValueOrDefault("language") ?? tgtNode?.Properties?.GetValueOrDefault("project_type");
                            var detailsParts = new List<string>();
                            if (!string.IsNullOrWhiteSpace(role)) detailsParts.Add(role);
                            if (!string.IsNullOrWhiteSpace(fw)) detailsParts.Add(fw);
                            else if (!string.IsNullOrWhiteSpace(lang)) detailsParts.Add(lang);
                            var detailsStr = detailsParts.Count > 0 ? string.Join(" • ", detailsParts) : "Internal Service";

                            svcGroup.Items.Add(new ServiceCapabilityItemDto
                            {
                                Id = tgtNode?.Id ?? edge.Target,
                                Name = tgtName,
                                Kind = tgtNode?.Kind ?? NodeKind.Service,
                                Details = detailsStr,
                                FilePath = tgtNode?.FilePath,
                                Line = tgtNode?.LineStart
                            });
                        }
                    }
                    else if (tgtNode?.Kind is NodeKind.ExternalService or NodeKind.CloudService || (!isInternalDirect && (edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT")))
                    {
                        var resolved = ResolveInternalServiceNode(tgtName, internalProjectsByName);
                        if (resolved != null)
                        {
                            var resolvedName = resolved.Name;
                            if (seenServices.Add(resolvedName))
                            {
                                var role = resolved.Properties?.GetValueOrDefault("role");
                                var fw = resolved.Properties?.GetValueOrDefault("framework");
                                var lang = resolved.Properties?.GetValueOrDefault("language") ?? resolved.Properties?.GetValueOrDefault("project_type");
                                var detailsParts = new List<string>();
                                if (!string.IsNullOrWhiteSpace(role)) detailsParts.Add(role);
                                if (!string.IsNullOrWhiteSpace(fw)) detailsParts.Add(fw);
                                else if (!string.IsNullOrWhiteSpace(lang)) detailsParts.Add(lang);
                                var detailsStr = detailsParts.Count > 0 ? string.Join(" • ", detailsParts) : "Internal Service";

                                svcGroup.Items.Add(new ServiceCapabilityItemDto
                                {
                                    Id = resolved.Id,
                                    Name = resolvedName,
                                    Kind = resolved.Kind,
                                    Details = detailsStr,
                                    FilePath = resolved.FilePath,
                                    Line = resolved.LineStart
                                });
                            }
                        }
                        else if (IsGenericPlaceholder(tgtName))
                        {
                            // Skip generic/wildcard tokens
                        }
                        else if (tgtName.StartWithIC("environment.") ||
                                 tgtName.StartWithIC("env."))
                        {
                            var cleanSvc = Regex.Replace(tgtName, @"^(environment\.|env\.)", "", RegexOptions.IgnoreCase);
                            if (seenServices.Add(cleanSvc))
                            {
                                svcGroup.Items.Add(new ServiceCapabilityItemDto
                                {
                                    Id = tgtNode?.Id ?? edge.Target,
                                    Name = cleanSvc,
                                    Kind = NodeKind.Service,
                                    Details = "Internal Service (via environment)",
                                    FilePath = tgtNode?.FilePath,
                                    Line = tgtNode?.LineStart
                                });
                            }
                        }
                        else
                        {
                            if (seenExt.Add(tgtName))
                            {
                                extGroup.Items.Add(new ServiceCapabilityItemDto
                                {
                                    Id = tgtNode?.Id ?? edge.Target,
                                    Name = tgtName,
                                    Kind = tgtNode?.Kind ?? NodeKind.ExternalService,
                                    Details = tgtNode?.Properties?.GetValueOrDefault("service_type") ?? "external API",
                                    FilePath = tgtNode?.FilePath,
                                    Line = tgtNode?.LineStart
                                });
                            }
                        }
                    }
                }
            }
            else if (edge.Target.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            {
                if (edge.Category == "messaging" || edge.Kind is "SUBSCRIBED_BY" or "SUBSCRIBES_TO" or "TRIGGERS")
                {
                    var srcNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Source, StringComparison.OrdinalIgnoreCase));
                    var topicName = srcNode?.Name ?? edge.Source;
                    var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                     srcNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                     string.Equals(srcNode?.Properties?.GetValueOrDefault("broker_type"), "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                     edge.Source.Contains(":mediatr:");

                    if (!isInternal && seenTopics.Add(topicName))
                    {
                        var isPub = edge.Kind is "PUBLISHES" or "PUBLISHES_TO" or "PUBLISHED_BY";
                        topicGroup.Items.Add(new ServiceCapabilityItemDto
                        {
                            Id = srcNode?.Id ?? edge.Source,
                            Name = topicName,
                            Kind = NodeKind.Topic,
                            Details = isPub ? "Published topic" : "Subscribed topic",
                            FilePath = srcNode?.FilePath,
                            Line = srcNode?.LineStart
                        });
                    }
                }
            }
        }

        // Sort items alphabetically by Name within all capability groups
        epGroup.Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        svcGroup.Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        dbGroup.Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        topicGroup.Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        extGroup.Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        epGroup.Count = epGroup.Items.Count;
        svcGroup.Count = svcGroup.Items.Count;
        dbGroup.Count = dbGroup.Items.Count;
        topicGroup.Count = topicGroup.Items.Count;
        extGroup.Count = extGroup.Items.Count;

        details.Groups = [epGroup, svcGroup, dbGroup, topicGroup, extGroup];
        return details;
    }

    private static bool IsGenericPlaceholder(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var n = name.Trim().ToLowerInvariant();
        return n is "*" or "unknown-service" or "http-call" or "service" or "base";
    }

    private static Dictionary<string, GraphNodeDto> BuildInternalProjectsLookup(List<GraphNodeDto> projectNodes)
    {
        var lookup = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in projectNodes)
        {
            lookup.TryAdd(p.Name, p);
            var normP = WorkspaceConventions.NormalizeServiceName(p.Name);
            var clean = normP.Replace("-", "").Replace("_", "");
            if (clean.Length > 0) lookup.TryAdd(clean, p);
            if (normP.Length > 0) lookup.TryAdd(normP, p);
            var norm = p.Name.Replace("-", "").Replace("_", "");
            if (norm.Length > 0) lookup.TryAdd(norm, p);

            if (!string.IsNullOrEmpty(p.FilePath))
            {
                var folder = Path.GetFileName(p.FilePath.TrimEnd('/', '\\'));
                if (!string.IsNullOrEmpty(folder))
                {
                    lookup.TryAdd(folder, p);
                    var cleanFolder = folder.Replace("-", "").Replace("_", "");
                    if (cleanFolder.Length > 0) lookup.TryAdd(cleanFolder, p);
                }
            }
        }
        return lookup;
    }

    private static GraphNodeDto? ResolveInternalServiceNode(
        string? nameOrDomain,
        Dictionary<string, GraphNodeDto> internalProjectsByName)
    {
        if (string.IsNullOrWhiteSpace(nameOrDomain)) return null;
        var n = nameOrDomain.Trim();
        var clean = Regex.Replace(n, @"^(environment\.|env\.|config\.|base_url_)", "", RegexOptions.IgnoreCase)
                         .Replace("-", "").Replace("_", "");
        if (clean.Length > 0 && internalProjectsByName.TryGetValue(clean, out var matched))
        {
            return matched;
        }

        // Subdomain check (e.g. rule-configurator.at-systems.biz -> ruleconfigurator)
        if (n.Contains('.'))
        {
            var sub = n.Split('.', 2)[0].Replace("-", "").Replace("_", "");
            if (sub.Length > 0 && internalProjectsByName.TryGetValue(sub, out var subMatched))
            {
                return subMatched;
            }
        }

        foreach (var (k, v) in internalProjectsByName)
        {
            if (k.Length >= 3 && (k.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                                  clean.StartWithIC(k) ||
                                  k.StartWithIC(clean)))
            {
                return v;
            }
        }
        return null;
    }
}
