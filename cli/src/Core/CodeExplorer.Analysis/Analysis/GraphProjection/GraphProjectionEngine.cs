using System.Globalization;
using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis.GraphProjection;

/// <summary>
/// Universal Graph Projection Engine that projects code property graphs into uniform Architecture Maps (C2/C3).
/// </summary>
public sealed class GraphProjectionEngine(IGraphClient db) : IGraphProjectionEngine
{
    private static readonly string[] ClassLiftedKinds =
        ["CALLS", "USES_TYPE", "IMPLEMENTS", "INHERITS_FROM", "READS_FROM", "WRITES_TO", "PUBLISHES_TO", "SUBSCRIBES_TO", "USES_DB", "CALLS_ENDPOINT"];

    public async Task<GraphDataDto> ProjectGraphAsync(GraphProjectionRequest request, CancellationToken ct = default)
    {
        return request.Level switch
        {
            ProjectionLevel.Component => await ProjectComponentMapAsync(request, ct),
            _ => throw new NotSupportedException($"Projection level {request.Level} is not implemented in ProjectGraphAsync.")
        };
    }

    private async Task<GraphDataDto> ProjectComponentMapAsync(GraphProjectionRequest request, CancellationToken ct)
    {
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "ProjectMap",
                ["level"] = "C3",
                ["graphType"] = "projectmap",
                ["scope"] = request.Scope ?? ""
            }
        };

        // 1. Stage 1: Node Extraction (Types, EntryPoints, Resources within project scope)
        var (nodes, leafToContainerMap, interfaceImplementors) = await ExtractComponentNodesAsync(request.Scope, request.ExcludeTests, ct);

        if (nodes.Count == 0)
        {
            graph.Metadata["error"] = string.IsNullOrWhiteSpace(request.Scope)
                ? "No indexed projects or types found."
                : $"Project '{request.Scope}' contains no indexed classes or components.";
            return graph;
        }

        var nodeMap = nodes.Where(n => !string.IsNullOrEmpty(n.Id)).ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        // 2. Stage 2: Edge Lifting via Cypher and In-Memory Resolution
        var rawEdges = await ExtractRawComponentEdgesAsync(request.Scope, ct);

        var liftedEdges = UniversalCypherEdgeLifter.LiftInMemory(
            rawEdges,
            leafId =>
            {
                if (leafToContainerMap.TryGetValue(leafId, out var containerId))
                {
                    // If target container is an interface and an implementor exists, redirect to implementor
                    if (interfaceImplementors.TryGetValue(containerId, out var implementorId))
                    {
                        return implementorId;
                    }
                    return containerId;
                }
                return null;
            });

        // 3. Stage 3: Topology & Cycle Analyzer (Tiers 0..6 + Tarjan SCC)
        var topology = TopologyAndCycleAnalyzer.Analyze(nodes, liftedEdges);

        // Apply analysis results to nodes and edges
        foreach (var node in nodes)
        {
            if (!string.IsNullOrEmpty(node.Id) && topology.NodeTiers.TryGetValue(node.Id, out var tier))
            {
                node.Properties ??= new Dictionary<string, string>();
                node.Properties["tier"] = ((int)tier).ToString(CultureInfo.InvariantCulture);
                node.Properties["tier_label"] = TopologyAndCycleAnalyzer.GetTierLabel(tier);
                node.Properties["tier_reason"] = topology.TierReasons.GetValueOrDefault(node.Id, "");
            }
        }

        var cycleIndex = 1;
        var nodeToCycleGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cycle in topology.Cycles)
        {
            foreach (var memberId in cycle)
            {
                nodeToCycleGroup[memberId] = cycleIndex;
                if (nodeMap.TryGetValue(memberId, out var n))
                {
                    n.Properties ??= new Dictionary<string, string>();
                    n.Properties["cycle_group"] = cycleIndex.ToString(CultureInfo.InvariantCulture);
                }
            }
            cycleIndex++;
        }

        foreach (var edge in liftedEdges)
        {
            edge.Properties ??= new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(edge.Id) && topology.CircularEdgeIds.Contains(edge.Id))
            {
                edge.Properties["is_circular"] = "true";
            }
        }

        // 4. Stage 4: GraphDataDto Mapper
        graph.Nodes.AddRange(nodes);
        graph.Edges.AddRange(liftedEdges);

        // Populate standard metadata
        graph.Metadata["module_count"] = nodes.Count(n => n.Kind is NodeKind.Type).ToString(CultureInfo.InvariantCulture);
        graph.Metadata["resource_count"] = nodes.Count(n => n.Kind is NodeKind.Table or NodeKind.Database or NodeKind.Topic or NodeKind.ExternalService).ToString(CultureInfo.InvariantCulture);
        graph.Metadata["edge_count"] = liftedEdges.Count.ToString(CultureInfo.InvariantCulture);
        graph.Metadata["cycle_count"] = topology.Cycles.Count.ToString(CultureInfo.InvariantCulture);
        graph.Metadata["cycles"] = JsonSerializer.Serialize(topology.Cycles);
        graph.Metadata["tiers"] = JsonSerializer.Serialize(
            TopologyAndCycleAnalyzer.StandardTierDefinitions.Select(t => new
            {
                tier = (int)t.Tier,
                label = t.Label,
                description = t.Description
            }));

        return graph;
    }

    private async Task<(List<GraphNodeDto> Nodes, Dictionary<string, string> LeafToContainer, Dictionary<string, string> InterfaceImplementors)>
        ExtractComponentNodesAsync(string? projectScope, bool excludeTests, CancellationToken ct)
    {
        var nodes = new List<GraphNodeDto>();
        var leafToContainer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var interfaceImplementors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Scope filter
        var scopeClause = !string.IsNullOrWhiteSpace(projectScope)
            ? "WHERE (p.id = $scope OR p.name = $scope)"
            : "";
        var paramsDict = !string.IsNullOrWhiteSpace(projectScope)
            ? new Dictionary<string, object?> { ["scope"] = projectScope }
            : null;

        // Query Types and Functions
        var typesQuery = $@"
            MATCH (p:Project)-[:CONTAINS*1..2]->(t:Type)
            {scopeClause}
            RETURN t.id AS id, t.name AS name, t.sub_kind AS sub_kind, t.path AS path, t.is_test AS is_test
        ";

        var json = await db.ExecuteQueryAsync(typesQuery, paramsDict, ct);
        using var doc = JsonDocument.Parse(json);

        var classesByName = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var id = elem.GetStringProp(OntologyConstants.PropertyNames.Id);
            var name = elem.GetStringProp(OntologyConstants.PropertyNames.Name);
            var subKind = elem.GetStringProp(OntologyConstants.PropertyNames.SubKind, OntologyConstants.PropertyNames.DefaultSubKind);
            var path = elem.GetStringProp(OntologyConstants.PropertyNames.Path);
            var isTest = elem.GetBoolProp(OntologyConstants.PropertyNames.IsTest);

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) continue;
            if (excludeTests && isTest) continue;

            // Clean generic arity: Foo`1 -> Foo
            var cleanName = name;
            var tick = cleanName.IndexOf('`');
            if (tick > 0) cleanName = cleanName[..tick];

            var containerId = $"type:{cleanName}";

            if (!classesByName.TryGetValue(cleanName, out var nodeDto))
            {
                nodeDto = new GraphNodeDto
                {
                    Id = containerId,
                    Name = cleanName,
                    DisplayName = cleanName,
                    Kind = NodeKind.Type,
                    FilePath = path,
                    Properties = new Dictionary<string, string>
                    {
                        [OntologyConstants.PropertyNames.SubKind] = subKind,
                        [OntologyConstants.PropertyNames.Path] = path,
                        [OntologyConstants.PropertyNames.IsTest] = isTest ? "true" : "false"
                    }
                };
                classesByName[cleanName] = nodeDto;
                nodes.Add(nodeDto);
            }

            // Map both raw type id and clean name to the container
            leafToContainer[id] = containerId;
            leafToContainer[cleanName] = containerId;
        }

        // Map functions to their containing types
        var funcQuery = $@"
            MATCH (p:Project)-[:CONTAINS*1..2]->(t:Type)-[:CONTAINS]->(fn:Function)
            {scopeClause}
            RETURN fn.id AS fn_id, t.name AS type_name
        ";
        try
        {
            var funcJson = await db.ExecuteQueryAsync(funcQuery, paramsDict, ct);
            using var funcDoc = JsonDocument.Parse(funcJson);
            foreach (var elem in funcDoc.RootElement.EnumerateArray())
            {
                var fnId = elem.GetStringProp(OntologyConstants.PropertyNames.FnId);
                var typeName = elem.GetStringProp(OntologyConstants.PropertyNames.TypeName);
                if (!string.IsNullOrEmpty(fnId) && !string.IsNullOrEmpty(typeName))
                {
                    var clean = typeName;
                    var tick = clean.IndexOf('`');
                    if (tick > 0) clean = clean[..tick];

                    leafToContainer[fnId] = $"type:{clean}";
                }
            }
        }
        catch
        {
            // Ignore if functions query not supported in schema
        }

        // Query Interface implementations to resolve interface calls to concrete classes
        var implQuery = $@"
            MATCH (impl:Type)-[:IMPLEMENTS]->(iface:Type)
            RETURN impl.name AS impl_name, iface.name AS iface_name
        ";
        try
        {
            var implJson = await db.ExecuteQueryAsync(implQuery, null, ct);
            using var implDoc = JsonDocument.Parse(implJson);
            foreach (var elem in implDoc.RootElement.EnumerateArray())
            {
                var implName = elem.GetStringProp(OntologyConstants.PropertyNames.ImplName);
                var ifaceName = elem.GetStringProp(OntologyConstants.PropertyNames.IfaceName);
                if (!string.IsNullOrEmpty(implName) && !string.IsNullOrEmpty(ifaceName))
                {
                    interfaceImplementors[$"type:{ifaceName}"] = $"type:{implName}";
                }
            }
        }
        catch
        {
            // Ignore if IMPLEMENTS query fails
        }

        // Query EntryPoints
        var epQuery = $@"
            MATCH (p:Project)-[:CONTAINS*1..2]->(ep:Endpoint)
            {scopeClause}
            RETURN ep.id AS id, ep.name AS name, ep.route AS route, ep.method AS method, ep.path AS path
        ";
        try
        {
            var epJson = await db.ExecuteQueryAsync(epQuery, paramsDict, ct);
            using var epDoc = JsonDocument.Parse(epJson);
            foreach (var elem in epDoc.RootElement.EnumerateArray())
            {
                var id = elem.GetStringProp(OntologyConstants.PropertyNames.Id);
                var name = elem.GetStringProp(OntologyConstants.PropertyNames.Name);
                var route = elem.GetStringProp(OntologyConstants.PropertyNames.Route);
                var method = elem.GetStringProp(OntologyConstants.PropertyNames.Method);
                var path = elem.GetStringProp(OntologyConstants.PropertyNames.Path);

                if (string.IsNullOrEmpty(id)) continue;
                var epLabel = !string.IsNullOrEmpty(route) ? $"{method} {route}".Trim() : name;

                nodes.Add(new GraphNodeDto
                {
                    Id = id,
                    Name = epLabel,
                    DisplayName = epLabel,
                    Kind = NodeKind.Endpoint,
                    FilePath = path,
                    Properties = new Dictionary<string, string>
                    {
                        [OntologyConstants.PropertyNames.Route] = route,
                        [OntologyConstants.PropertyNames.Method] = method,
                        [OntologyConstants.PropertyNames.Tier] = "0",
                        [OntologyConstants.PropertyNames.TierLabel] = TopologyAndCycleAnalyzer.GetTierLabel(ArchitecturalTier.Ingress)
                    }
                });

                leafToContainer[id] = id;
            }
        }
        catch
        {
            // Ignore if endpoint query fails
        }

        // Query Tables / Databases / Topics touched by project
        var resQuery = $@"
            MATCH (p:Project)-[:CONTAINS*1..3]->(fn)-[r:READS_FROM|WRITES_TO|PUBLISHES_TO|SUBSCRIBES_TO|USES_DB]->(res)
            {scopeClause}
            RETURN distinct res.id AS id, labels(res)[0] AS kind, res.name AS name
        ";
        try
        {
            var resJson = await db.ExecuteQueryAsync(resQuery, paramsDict, ct);
            using var resDoc = JsonDocument.Parse(resJson);
            foreach (var elem in resDoc.RootElement.EnumerateArray())
            {
                var id = elem.GetStringProp(OntologyConstants.PropertyNames.Id);
                var kindStr = elem.GetStringProp(OntologyConstants.PropertyNames.Kind);
                var name = elem.GetStringProp(OntologyConstants.PropertyNames.Name);

                if (string.IsNullOrEmpty(id)) continue;
                var kind = EnumExtensions.ParseNodeKind(kindStr);

                if (!leafToContainer.ContainsKey(id))
                {
                    nodes.Add(new GraphNodeDto
                    {
                        Id = id,
                        Name = name,
                        DisplayName = name,
                        Kind = kind,
                        Properties = new Dictionary<string, string>
                        {
                            [OntologyConstants.PropertyNames.ResourceKind] = kindStr
                        }
                    });

                    leafToContainer[id] = id;
                }
            }
        }
        catch
        {
            // Ignore if resource query fails
        }

        return (nodes, leafToContainer, interfaceImplementors);
    }

    private async Task<List<(string Source, string Target, string Kind, string? SourceFile, string? TargetFile, int? Line)>>
        ExtractRawComponentEdgesAsync(string? projectScope, CancellationToken ct)
    {
        var rawEdges = new List<(string Source, string Target, string Kind, string? SourceFile, string? TargetFile, int? Line)>();

        var scopeClause = !string.IsNullOrWhiteSpace(projectScope)
            ? "WHERE (p.id = $scope OR p.name = $scope)"
            : "";
        var paramsDict = !string.IsNullOrWhiteSpace(projectScope)
            ? new Dictionary<string, object?> { ["scope"] = projectScope }
            : null;

        var kinds = string.Join("|", ClassLiftedKinds);

        // 1. Function-level and Type-level calls inside/from the project
        var query = $@"
            MATCH (p:Project)-[:CONTAINS*1..2]->(u)-[r:{kinds}]->(v)
            {scopeClause}
            RETURN u.id AS source, v.id AS target, type(r) AS kind, u.path AS src_path, v.path AS tgt_path, r.line AS line
        ";

        try
        {
            var json = await db.ExecuteQueryAsync(query, paramsDict, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var src = elem.GetStringProp(OntologyConstants.PropertyNames.Source);
                var tgt = elem.GetStringProp(OntologyConstants.PropertyNames.Target);
                var kind = elem.GetStringProp(OntologyConstants.PropertyNames.Kind);
                var srcFile = elem.GetStringProp(OntologyConstants.PropertyNames.SrcPath);
                var tgtFile = elem.GetStringProp(OntologyConstants.PropertyNames.TgtPath);
                var line = elem.GetNullableIntProp(OntologyConstants.PropertyNames.Line);

                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt))
                {
                    rawEdges.Add((src, tgt, kind, srcFile, tgtFile, line));
                }
            }
        }
        catch
        {
            // Fallback: simpler edge matching if schema does not have line on r
            var simpleQuery = $@"
                MATCH (p:Project)-[:CONTAINS*1..2]->(u)-[r:{kinds}]->(v)
                {scopeClause}
                RETURN u.id AS source, v.id AS target, type(r) AS kind
            ";
            try
            {
                var sJson = await db.ExecuteQueryAsync(simpleQuery, paramsDict, ct);
                using var sDoc = JsonDocument.Parse(sJson);
                foreach (var elem in sDoc.RootElement.EnumerateArray())
                {
                    var src = elem.GetStringProp(OntologyConstants.PropertyNames.Source);
                    var tgt = elem.GetStringProp(OntologyConstants.PropertyNames.Target);
                    var kind = elem.GetStringProp(OntologyConstants.PropertyNames.Kind);
                    if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt))
                    {
                        rawEdges.Add((src, tgt, kind, null, null, null));
                    }
                }
            }
            catch
            {
                // Ignore if query fails
            }
        }

        return rawEdges;
    }
}
