using System.Text.Json;
using System.Text.RegularExpressions;
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
            var kindLabel = kind.ToCypherLabel();
            if (attr.IsSystemNode || Mcp.OntologyRegistry.IsSystemNode(kindLabel))
            {
                continue;
            }
            var count = counts.GetValueOrDefault(kindLabel, 0);

            if (attr.Layer == OntologyConstants.Layers.Physical)
            {
                l1Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 1 });
            }
            else if (attr.Layer == OntologyConstants.Layers.ProjectBoundary)
            {
                if (kind == NodeKind.EntryPoint && count == 0)
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
                if (kind == NodeKind.DataSet || (kind is NodeKind.Procedure or NodeKind.Query or NodeKind.ApiInUse && count == 0))
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

    private const string EmbeddedOntologySchemaResource = "ontology-schema.json";
    private const string FallbackEmptySchemaJson = "{\"schemaVersion\":\"1.0\",\"layers\":[],\"edgeCategories\":[],\"commonProperties\":[],\"nodes\":[],\"relationships\":[]}";
    private const string ObservedTriplesSql =
        "SELECT ce_node_kind(n1.kind), ce_rel_kind(e.kind), ce_node_kind(n2.kind), count(*) " +
        "FROM edges e JOIN nodes n1 ON n1.rowid = e.from_id JOIN nodes n2 ON n2.rowid = e.to_id " +
        "GROUP BY n1.kind, e.kind, n2.kind";

    private static readonly HashSet<string> HiddenOntologyLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        OntologyConstants.NodeLabels.Domain,
        OntologyConstants.NodeLabels.BoundedContext,
        OntologyConstants.NodeLabels.LogicalStructure,
        OntologyConstants.Relationships.BelongsToDomain,
        OntologyConstants.Relationships.ExposesDomain,
        OntologyConstants.Relationships.SubdomainOf
    };

    private static readonly Lazy<JsonElement> EmbeddedOntologySchema = new(LoadEmbeddedOntologySchema);

    private static JsonElement LoadEmbeddedOntologySchema()
    {
        using var stream = typeof(ArchitectureViewEngine).Assembly.GetManifestResourceStream(EmbeddedOntologySchemaResource);
        if (stream is null)
        {
            using var fallbackDoc = JsonDocument.Parse(FallbackEmptySchemaJson);
            return fallbackDoc.RootElement.Clone();
        }
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }

    public async Task<OntologySchemaResponseDto> GetOntologySchemaAsync(CancellationToken ct = default)
    {
        var meta = await GetMetadataAsync(ct);
        var filteredNodeCounts = meta.NodeCounts
            .Where(kv => !Mcp.OntologyRegistry.IsSystemNode(kv.Key) && !HiddenOntologyLabels.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        var filteredRelCounts = meta.RelationshipCounts
            .Where(kv => !HiddenOntologyLabels.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        var triples = await QueryObservedOntologyTriplesAsync(ct);

        return new OntologySchemaResponseDto
        {
            Schema = EmbeddedOntologySchema.Value,
            Workspace = new OntologyWorkspaceStatsDto
            {
                TotalNodes = filteredNodeCounts.Values.Sum(),
                TotalEdges = filteredRelCounts.Values.Sum(),
                NodeCounts = filteredNodeCounts,
                RelationshipCounts = filteredRelCounts,
                ObservedTriples = triples
            }
        };
    }

    private async Task<List<OntologyObservedTripleDto>> QueryObservedOntologyTriplesAsync(CancellationToken ct)
    {
        if (db is not SqliteGraphClient sqliteClient || sqliteClient.IsSchemaOutdated)
        {
            return [];
        }

        try
        {
            return await sqliteClient.ExecuteRawAsync(async conn =>
            {
                var list = new List<OntologyObservedTripleDto>();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = ObservedTriplesSql;
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var fromKind = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    var rel = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var toKind = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    var cnt = reader.IsDBNull(3) ? 0L : reader.GetInt64(3);
                    if (string.IsNullOrEmpty(fromKind) || string.IsNullOrEmpty(rel) || string.IsNullOrEmpty(toKind))
                    {
                        continue;
                    }
                    if (Mcp.OntologyRegistry.IsSystemNode(fromKind) || Mcp.OntologyRegistry.IsSystemNode(toKind) ||
                        HiddenOntologyLabels.Contains(fromKind) || HiddenOntologyLabels.Contains(toKind) ||
                        HiddenOntologyLabels.Contains(rel))
                    {
                        continue;
                    }
                    list.Add(new OntologyObservedTripleDto
                    {
                        FromLabel = NormalizeSemanticWorkloadKind(fromKind),
                        Rel = rel,
                        ToLabel = NormalizeSemanticWorkloadKind(toKind),
                        Count = cnt
                    });
                }
                return MergeDuplicateTriples(list);
            }, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
            return [];
        }
    }

    private static string NormalizeSemanticWorkloadKind(string kind) => kind switch
    {
        "FrontendApp" => OntologyConstants.NodeLabels.App,
        "SharedLibrary" => OntologyConstants.NodeLabels.Library,
        "Test" => OntologyConstants.NodeLabels.TestSuite,
        _ => kind
    };

    private static List<OntologyObservedTripleDto> MergeDuplicateTriples(List<OntologyObservedTripleDto> raw) =>
        raw.GroupBy(t => (t.FromLabel, t.Rel, t.ToLabel))
           .Select(g => new OntologyObservedTripleDto
           {
               FromLabel = g.Key.FromLabel,
               Rel = g.Key.Rel,
               ToLabel = g.Key.ToLabel,
               Count = g.Sum(x => x.Count)
           })
           .OrderByDescending(t => t.Count)
           .ToList();

    public async Task<List<ServiceSummaryDto>> GetServicesOntologySummaryAsync(CancellationToken ct = default)
    {
        var list = new List<ServiceSummaryDto>();
        try
        {
            var query = "MATCH (s) WHERE (s:Service OR s:App OR s:Worker OR s:CliTool OR s:FrontendApp OR s:TestSuite) RETURN DISTINCT s.id AS id, coalesce(s.name, s.id) AS name, labels(s) AS lbl, s.role AS role, s.primary_role AS primary_role, s.secondary_roles AS secondary_roles, s.framework AS framework, s.language AS language ORDER BY s.name ASC";
            var res = await db.ExecuteCypherAsync(query, null, ct);
            var rows = res.Rows;

            if (rows.Count == 0)
            {
                var fallbackQuery =
                    "MATCH (s:Project) WHERE json_extract(s.properties, '$.role') IN ['Service', 'App', 'FrontendApp', 'Worker', 'CliTool', 'Test', 'TestSuite'] RETURN DISTINCT s.id AS id, coalesce(s.name, s.id) AS name, labels(s) AS lbl, s.role AS role, s.primary_role AS primary_role, s.secondary_roles AS secondary_roles, s.framework AS framework, s.language AS language ORDER BY s.name ASC";
                var fbRes = await db.ExecuteCypherAsync(fallbackQuery, null, ct);
                rows = fbRes.Rows;
            }

            var serviceMap = new Dictionary<string, ServiceSummaryDto>(StringComparer.OrdinalIgnoreCase);
            var idToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var nodeKind = NodeKind.Service;

                if (row.TryGetProperty("role", out var roleProp) && roleProp.Kind == CypherValueKind.String)
                {
                    var r = roleProp.AsString();

                    if (r is "FrontendApp" or "App") nodeKind = NodeKind.App;
                    else if (r is "Worker") nodeKind = NodeKind.Worker;
                    else if (r is "SharedLibrary" or "Library") nodeKind = NodeKind.Library;
                    else if (r is "CliTool" or "ScriptProject") nodeKind = NodeKind.CliTool;
                    else if (r is "Test" or "TestSuite") nodeKind = NodeKind.TestSuite;
                }
                else if (row.TryGetProperty("lbl", out var lblProp))
                {
                    foreach (var ls in lblProp.AsStringList())
                    {
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
                    primaryRole = rProp.AsNullableString();
                }

                var secondaryRolesList = new List<string>();
                if (row.TryGetProperty("secondary_roles", out var secProp))
                {
                    foreach (var val in secProp.AsStringList())
                    {
                        if (!string.IsNullOrWhiteSpace(val) && !secondaryRolesList.Contains(val, StringComparer.OrdinalIgnoreCase))
                        {
                            secondaryRolesList.Add(val);
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

            // Tally endpoints directly from graph
            try
            {
                IReadOnlyList<(string SName, string SId, int EpCount)> epRows;
                if (db is SqliteGraphClient)
                {
                    var fastEpQuery = $"""
                        SELECT coalesce(json_extract(s.properties, '$.name'), s.id) AS sName, s.id AS sId, COUNT(ep.rowid) AS epCount
                        FROM nodes ep
                        JOIN edges _r0 ON _r0.to_id = ep.rowid AND _r0.kind = {GraphKindCodec.EncodeRelKind(RelationshipKind.Contains)}
                        JOIN nodes s ON s.rowid = _r0.from_id
                        WHERE ep.kind = {GraphKindCodec.EncodeNodeKind(NodeKind.Endpoint)}
                          AND (s.kind IN ({GraphKindCodec.NodeKindsInSql(NodeKind.Project, NodeKind.Service, NodeKind.App, NodeKind.Worker, NodeKind.CliTool, NodeKind.TestSuite)}))
                        GROUP BY sName, sId;
                        """;
                    epRows = await db.QueryAsync<(string SName, string SId, int EpCount)>(fastEpQuery, null, ct);
                }
                else
                {
                    var epCountQuery =
                        "MATCH (s)-[:CONTAINS]->(ep:Endpoint) WHERE (s:Project OR s:Service OR s:App OR s:Worker OR s:CliTool OR s:TestSuite) RETURN coalesce(s.name, s.id) AS sName, s.id AS sId, count(ep) AS epCount";
                    epRows = await db.QueryAsync<(string SName, string SId, int EpCount)>(epCountQuery, null, ct);
                }

                foreach (var (sName, sId, epCount) in epRows)
                {
                    ServiceSummaryDto? summary = null;

                    if (!string.IsNullOrEmpty(sName) && serviceMap.TryGetValue(sName, out summary))
                    {
                    }
                    else if (!string.IsNullOrEmpty(sId) && idToName.TryGetValue(sId, out var mappedName) &&
                             serviceMap.TryGetValue(mappedName, out summary))
                    {
                    }

                    if (summary != null)
                    {
                        summary.EndpointCount = epCount;
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

                    if (edge.Category == GraphEdgeCategory.Database || edge.Kind == RelationshipKind.UsesDb || tgtNode?.Kind == NodeKind.Database)
                    {
                        if (seenServiceDbs.Add($"{srcSummary.ServiceName}->{tgtName}"))
                        {
                            srcSummary.DatabaseCount++;
                        }
                    }
                    else if (edge.Category == GraphEdgeCategory.Messaging || edge.Kind is RelationshipKind.PublishesTo or RelationshipKind.Triggers ||
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

                        var isServiceCall = edge.Category == GraphEdgeCategory.ServiceCall ||
                                            edge.Kind is RelationshipKind.ServiceCall or RelationshipKind.CallsEndpoint ||
                                            (edge.Kind == RelationshipKind.DependsOn &&
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
                                 (!isInternalDirect && (edge.Kind is RelationshipKind.ServiceCall or RelationshipKind.CallsEndpoint)))
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
                    if (edge.Category == GraphEdgeCategory.Messaging || edge.Kind is RelationshipKind.SubscribedBy or RelationshipKind.SubscribesTo ||
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
            var epRes = await db.ExecuteCypherAsync(epQuery, new { id = targetId, name = serviceName }, ct);

            foreach (var row in epRes.Rows)
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var method = row.GetStringProp("method");
                var route = row.GetStringProp("route");
                var protocol = row.GetStringProp("protocol", "REST");
                const string colFilePath = "file_path";
                const string colLine = "line";
                var filePath = row.GetStringProp(colFilePath);
                var line = row.GetNullableIntProp(colLine);

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

                if (edge.Category == GraphEdgeCategory.Database || edge.Kind == RelationshipKind.UsesDb || tgtNode?.Kind == NodeKind.Database)
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
                else if (edge.Category == GraphEdgeCategory.Messaging || edge.Kind is RelationshipKind.PublishesTo or RelationshipKind.Triggers or RelationshipKind.SubscribesTo || tgtNode?.Kind == NodeKind.Topic)
                {
                    var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                     tgtNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                     string.Equals(tgtNode?.Properties?.GetValueOrDefault("broker_type"), "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                     edge.Target.Contains(":mediatr:");

                    if (!isInternal && seenTopics.Add(tgtName))
                    {
                        var isSubscriber = edge.Kind is RelationshipKind.SubscribesTo or RelationshipKind.Subscribes or RelationshipKind.SubscribedBy or RelationshipKind.Consumes;
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
                    var isServiceCall = edge.Category == GraphEdgeCategory.ServiceCall || edge.Kind is RelationshipKind.ServiceCall or RelationshipKind.CallsEndpoint || (edge.Kind == RelationshipKind.DependsOn && edge.Properties?.GetValueOrDefault("dependency_type") == "service_call");

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
                    else if (tgtNode?.Kind is NodeKind.ExternalService or NodeKind.CloudService || (!isInternalDirect && (edge.Kind is RelationshipKind.ServiceCall or RelationshipKind.CallsEndpoint)))
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
                if (edge.Category == GraphEdgeCategory.Messaging || edge.Kind is RelationshipKind.SubscribedBy or RelationshipKind.SubscribesTo or RelationshipKind.Triggers)
                {
                    var srcNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Source, StringComparison.OrdinalIgnoreCase));
                    var topicName = srcNode?.Name ?? edge.Source;
                    var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                     srcNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                     string.Equals(srcNode?.Properties?.GetValueOrDefault("broker_type"), "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                     edge.Source.Contains(":mediatr:");

                    if (!isInternal && seenTopics.Add(topicName))
                    {
                        var isPub = edge.Kind is RelationshipKind.Publishes or RelationshipKind.PublishesTo or RelationshipKind.PublishedBy;
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
