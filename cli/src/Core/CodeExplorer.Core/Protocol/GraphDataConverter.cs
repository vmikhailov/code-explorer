using System.Text.Json;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Protocol;

public static class GraphDataConverter
{
    public static async Task<GraphDataDto> GetArchitectureGraphAsync(
        IGraphClient client,
        string? projectFilter = null,
        CancellationToken cancellationToken = default)
    {
        var graph = new GraphDataDto();
        var nodeMap = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);

        // 1. Projects
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.framework AS framework, p.path AS path, p.project_type AS project_type, p.role AS role, p.is_library AS is_library, p.layer AS layer, p.layerId AS layerId, p.layerName AS layerName, p.layerOrder AS layerOrder, p.layerColor AS layerColor, p.layerIcon AS layerIcon, p.package_count AS package_count, p.properties AS properties";
        var projJson = await client.ExecuteQueryAsync(projQuery, null, cancellationToken);
        using var projDoc = JsonDocument.Parse(projJson);

        foreach (var row in projDoc.RootElement.EnumerateArray())
        {
            var id = row.GetStringProp("id");
            var name = row.GetStringProp("name", id);
            var framework = row.GetStringProp("framework");
            var path = row.GetStringProp("path");
            var projectType = row.GetStringProp("project_type");

            if (!string.IsNullOrWhiteSpace(projectFilter) && !name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var props = row.ExtractProperties();
            if (!string.IsNullOrEmpty(framework)) props["framework"] = framework;
            if (!string.IsNullOrEmpty(path)) props["path"] = path;
            if (!string.IsNullOrEmpty(projectType)) props["project_type"] = projectType;

            var role = row.GetStringProp("role", props.GetValueOrDefault("role", ""));
            if (!string.IsNullOrEmpty(role)) props["role"] = role;

            var isLibStr = row.GetStringProp("is_library", props.GetValueOrDefault("is_library", ""));
            if (!string.IsNullOrEmpty(isLibStr)) props["is_library"] = isLibStr;

            var layer = row.GetStringProp("layer", props.GetValueOrDefault("layer", ""));
            if (!string.IsNullOrEmpty(layer))
            {
                props["layer"] = layer;
                props["layerId"] = row.GetStringProp("layerId", props.GetValueOrDefault("layerId", layer));
                props["layerName"] = row.GetStringProp("layerName", props.GetValueOrDefault("layerName", ""));
                props["layerOrder"] = row.GetStringProp("layerOrder", props.GetValueOrDefault("layerOrder", ""));
                props["layerColor"] = row.GetStringProp("layerColor", props.GetValueOrDefault("layerColor", ""));
                props["layerIcon"] = row.GetStringProp("layerIcon", props.GetValueOrDefault("layerIcon", ""));
            }

            var pkgCount = row.GetStringProp("package_count", props.GetValueOrDefault("package_count", ""));
            if (!string.IsNullOrEmpty(pkgCount)) props["package_count"] = pkgCount;

            var node = new GraphNodeDto
            {
                Id = id,
                Kind = "Project",
                Name = name,
                DisplayName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})",
                FilePath = path,
                Properties = props
            };

            nodeMap[id] = node;
            graph.Nodes.Add(node);
        }

        // 1b. Query Package Dependency Counts per Project (fallback if not already in properties)
        try
        {
            var needsPkgCount = graph.Nodes.Any(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase) && n.Properties?.ContainsKey("package_count") != true);
            if (needsPkgCount)
            {
                var pkgCountQuery = "MATCH (p:Project)-[:DEPENDS_ON]->(pkg:Package) RETURN p.id AS projId, count(pkg) AS pkgCount";
                var pkgCountJson = await client.ExecuteQueryAsync(pkgCountQuery, null, cancellationToken);
                using var pkgCountDoc = JsonDocument.Parse(pkgCountJson);
                foreach (var row in pkgCountDoc.RootElement.EnumerateArray())
                {
                    var projId = row.GetStringProp("projId");
                    var count = row.TryGetProperty("pkgCount", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt64() : 0;
                    if (!string.IsNullOrEmpty(projId) && nodeMap.TryGetValue(projId, out var pNode) && pNode != null)
                    {
                        pNode.Properties ??= new Dictionary<string, string>();
                        pNode.Properties["package_count"] = count.ToString();
                    }
                }
            }
        }
        catch { }

        // 2. Databases (Consolidate project-scoped DB nodes into canonical data stores to prevent duplication)
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.display_name AS display_name, d.db_type AS db_type, d.engine AS engine, d.is_canonical AS is_canonical, d.role AS role, d.layer AS layer, d.properties AS properties";
        var dbJson = await client.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        using var dbDoc = JsonDocument.Parse(dbJson);
        var dbIdToCanonicalId = new Dictionary<string, string>();

        var rawDbList = new List<(string Id, string Name, string DisplayName, string DbType, string Engine, Dictionary<string, string> Props)>();
        foreach (var row in dbDoc.RootElement.EnumerateArray())
        {
            var id = row.GetStringProp("id");
            var name = row.GetStringProp("name", id);
            var props = row.ExtractProperties();
            var dbType = row.GetStringProp("db_type", props.GetValueOrDefault("db_type", "Database"));
            var engine = row.GetStringProp("engine", props.GetValueOrDefault("engine", ""));
            var dispName = row.GetStringProp("display_name", props.GetValueOrDefault("display_name", ""));
            rawDbList.Add((id, name, dispName, dbType, engine, props));
        }

        foreach (var (id, name, dispName, dbType, engine, props) in rawDbList)
        {
            var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType);
            var isProjectScoped = id.IndexOf("db:", StringComparison.OrdinalIgnoreCase) > 0 ||
                                  id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase);
            var isWorkspaceDatabase = id.StartsWith("workspace:database:", StringComparison.OrdinalIgnoreCase);
            var canonicalId = (isProjectScoped || isWorkspaceDatabase || cKey == "typeorm")
                ? $"workspace:database:{cType}:{cKey}"
                : id;

            dbIdToCanonicalId[id] = canonicalId;

            var engineToUse = !string.IsNullOrEmpty(engine) ? engine : props.GetValueOrDefault("engine", "");
            var formattedDispName = !string.IsNullOrEmpty(dispName)
                ? dispName
                : (!string.IsNullOrEmpty(engineToUse) ? $"{cName} ({engineToUse}) [{cType}]" : $"{cName} [{cType}]");

            if (!nodeMap.TryGetValue(canonicalId, out var existingDbNode))
            {
                var nodeProps = new Dictionary<string, string>(props)
                {
                    ["db_type"] = cType,
                    ["role"] = "database",
                    ["is_canonical"] = "true",
                    ["is_semantic_entity"] = "true",
                    ["layer"] = props.GetValueOrDefault("layer", CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId),
                    ["layerId"] = props.GetValueOrDefault("layerId", CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId),
                    ["layerName"] = props.GetValueOrDefault("layerName", CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName),
                    ["layerOrder"] = props.GetValueOrDefault("layerOrder", CodeExplorer.Core.Analysis.StandardLayers.Foundation.Order.ToString()),
                    ["layerColor"] = props.GetValueOrDefault("layerColor", CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color),
                    ["layerIcon"] = props.GetValueOrDefault("layerIcon", CodeExplorer.Core.Analysis.StandardLayers.Foundation.Icon)
                };
                if (!string.IsNullOrEmpty(engineToUse)) nodeProps["engine"] = engineToUse;

                var node = new GraphNodeDto
                {
                    Id = canonicalId,
                    Kind = "Database",
                    Name = cName,
                    DisplayName = formattedDispName,
                    Properties = nodeProps
                };

                nodeMap[canonicalId] = node;
                graph.Nodes.Add(node);
            }
            else
            {
                existingDbNode.Properties ??= new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(engineToUse) && !existingDbNode.Properties.ContainsKey("engine"))
                {
                    existingDbNode.Properties["engine"] = engineToUse;
                    if (existingDbNode.DisplayName != null && !existingDbNode.DisplayName.Contains(engineToUse, StringComparison.OrdinalIgnoreCase))
                    {
                        existingDbNode.DisplayName = $"{existingDbNode.Name} ({engineToUse}) [{existingDbNode.Properties.GetValueOrDefault("db_type")}]";
                    }
                }
            }
        }

        // 3. External Services / Message Brokers
        var svcQuery = "MATCH (s:ExternalService) RETURN s.id AS id, s.name AS name, s.service_type AS service_type";
        var svcJson = await client.ExecuteQueryAsync(svcQuery, null, cancellationToken);
        using var svcDoc = JsonDocument.Parse(svcJson);

        foreach (var row in svcDoc.RootElement.EnumerateArray())
        {
            var id = row.GetStringProp("id");
            var name = row.GetStringProp("name", id);
            var st = row.TryGetProperty("service_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Service") : "Service";

            if (!nodeMap.ContainsKey(id))
            {
                var node = new GraphNodeDto
                {
                    Id = id,
                    Kind = "ExternalService",
                    Name = name,
                    DisplayName = $"{name} [{st}]",
                    Properties = new Dictionary<string, string> { ["service_type"] = st }
                };
                nodeMap[id] = node;
                graph.Nodes.Add(node);
            }
        }

        // 3b. Topics (PubSub, Message Queues, Event Streams)
        try
        {
            var topicQuery = "MATCH (t:Topic) RETURN t.id AS id, t.name AS name, t.broker_type AS broker_type";
            var topicJson = await client.ExecuteQueryAsync(topicQuery, null, cancellationToken);
            using var topicDoc = JsonDocument.Parse(topicJson);

            foreach (var row in topicDoc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var broker = row.TryGetProperty("broker_type", out var b) && b.ValueKind == JsonValueKind.String ? (b.GetString() ?? "Topic") : "Topic";

                if (!nodeMap.ContainsKey(id))
                {
                    var node = new GraphNodeDto
                    {
                        Id = id,
                        Kind = "Topic",
                        Name = name,
                        DisplayName = $"{name} [{broker}]",
                        Properties = new Dictionary<string, string>
                        {
                            ["broker_type"] = broker,
                            ["role"] = "topic",
                            ["entity_type"] = "topic",
                            ["is_semantic_entity"] = "true",
                            ["is_library"] = "false"
                        }
                    };
                    nodeMap[id] = node;
                    graph.Nodes.Add(node);
                }
            }
        }
        catch { }

        // 4. Query Raw Project Dependencies
        var depQuery = "MATCH (p1:Project)-[r:DEPENDS_ON]->(p2:Project) RETURN p1.id AS source, p2.id AS target, r.kind AS kind, r.dependency_type AS dep_type";
        var depJson = await client.ExecuteQueryAsync(depQuery, null, cancellationToken);
        using var depDoc = JsonDocument.Parse(depJson);
        var rawDeps = new List<(string Source, string Target, string RawKind, string? DepType)>();

        foreach (var row in depDoc.RootElement.EnumerateArray())
        {
            var src = row.GetStringProp("source");
            var tgt = row.GetStringProp("target");
            var dt = row.TryGetProperty("dep_type", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            var rk = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "DEPENDS_ON") : "DEPENDS_ON";
            if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt))
            {
                rawDeps.Add((src, tgt, rk, dt));
            }
        }

        // 4b. Project -> Package -> Project Dependencies (Workspace Packages/Modules fallback)
        var rawPkgDeps = new List<(string Source, string Target)>();
        try
        {
            var pkgDepQuery = "MATCH (p1:Project)-[:DEPENDS_ON]->(pkg:Package)-[:IMPLEMENTED_BY]->(p2:Project) WHERE p1.id <> p2.id RETURN DISTINCT p1.id AS source, p2.id AS target";
            var pkgDepJson = await client.ExecuteQueryAsync(pkgDepQuery, null, cancellationToken);
            using var pkgDepDoc = JsonDocument.Parse(pkgDepJson);
            foreach (var row in pkgDepDoc.RootElement.EnumerateArray())
            {
                var src = row.GetStringProp("source");
                var tgt = row.GetStringProp("target");
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt) && src != tgt)
                {
                    rawPkgDeps.Add((src, tgt));
                }
            }
        }
        catch { }

        // 5. Layer Classification (Computed BEFORE edge assignment so target layers are known!)
        var classifierItems = graph.Nodes
            .Where(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase))
            .Select(n => new CodeExplorer.Core.Analysis.ProjectClassifierItem
            {
                Id = n.Id,
                Name = n.Name,
                FilePath = n.FilePath,
                Framework = n.Properties?.GetValueOrDefault("framework")
            });

        var dependencyItems = rawDeps
            .Select(d => new CodeExplorer.Core.Analysis.DependencyItem
            {
                SourceId = d.Source,
                TargetId = d.Target
            })
            .Concat(rawPkgDeps.Select(d => new CodeExplorer.Core.Analysis.DependencyItem
            {
                SourceId = d.Source,
                TargetId = d.Target
            }));

        var needsClassification = graph.Nodes.Any(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase) && n.Properties?.ContainsKey("layer") != true);
        var layerMap = needsClassification
            ? CodeExplorer.Core.Analysis.ProjectLayerClassifier.Classify(classifierItems, dependencyItems)
            : new Dictionary<string, CodeExplorer.Core.Analysis.ProjectLayerInfo>();

        foreach (var node in graph.Nodes)
        {
            node.Properties ??= new Dictionary<string, string>();

            if (node.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase))
            {
                if (!node.Properties.ContainsKey("layer") && layerMap.TryGetValue(node.Id, out var layer))
                {
                    node.Properties["layer"] = layer.LayerId;
                    node.Properties["layerId"] = layer.LayerId;
                    node.Properties["layerName"] = layer.LayerName;
                    node.Properties["layerOrder"] = layer.Order.ToString();
                    node.Properties["layerColor"] = layer.Color;
                    node.Properties["layerIcon"] = layer.Icon;
                }

                var isLib = IsLibraryProject(node);
                node.Properties["is_library"] = isLib ? "true" : "false";
                node.Properties["entity_type"] = isLib ? "library" : "service";
                node.Properties["is_semantic_entity"] = isLib ? "false" : "true";
            }
            else if (node.Kind.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                node.Properties["layer"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId;
                node.Properties["layerId"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId;
                node.Properties["layerName"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName;
                node.Properties["layerOrder"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Order.ToString();
                node.Properties["layerColor"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color;
                node.Properties["layerIcon"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Icon;
                node.Properties["is_library"] = "false";
                node.Properties["entity_type"] = "database";
                node.Properties["is_semantic_entity"] = "true";
            }
            else if (node.Kind.Equals("ExternalService", StringComparison.OrdinalIgnoreCase))
            {
                var st = node.Properties.GetValueOrDefault("service_type") ?? "";
                var isMsg = st.Equals("MessageBroker", StringComparison.OrdinalIgnoreCase);
                node.Properties["is_library"] = "false";
                node.Properties["entity_type"] = isMsg ? "topic" : "external";
                node.Properties["is_semantic_entity"] = "true";
            }
            else if (node.Kind.Equals("Topic", StringComparison.OrdinalIgnoreCase))
            {
                node.Properties["layer"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId;
                node.Properties["layerId"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId;
                node.Properties["layerName"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName;
                node.Properties["layerOrder"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Order.ToString();
                node.Properties["layerColor"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color;
                node.Properties["layerIcon"] = "symbol-event";
                node.Properties["is_library"] = "false";
                node.Properties["entity_type"] = "topic";
                node.Properties["is_semantic_entity"] = "true";
            }
        }

        graph.Metadata ??= new Dictionary<string, string>();
        graph.Metadata["layers"] = JsonSerializer.Serialize(CodeExplorer.Core.Analysis.StandardLayers.All);

        // 6. Generate Project -> Project Dependency Edges with Accurate Types
        foreach (var (src, tgt, rk, rawDepType) in rawDeps)
        {
            if (!nodeMap.ContainsKey(src) || !nodeMap.ContainsKey(tgt)) continue;

            nodeMap.TryGetValue(tgt, out var targetNode);
            var isTargetLib = IsLibraryProject(targetNode);

            var depType = rawDepType;
            if (string.IsNullOrEmpty(depType))
            {
                depType = isTargetLib ? "library" : "service_call";
            }
            else if (isTargetLib && depType != "service_call")
            {
                depType = "library";
            }

            var kind = depType == "library" ? "LIBRARY" : (rk == "TRIGGERS" ? "TRIGGERS" : "SERVICE_CALL");

            graph.Edges.Add(new GraphEdgeDto
            {
                Id = $"{src}->{tgt}:{kind}",
                Source = src,
                Target = tgt,
                Kind = kind,
                Properties = new Dictionary<string, string>
                {
                    ["dependency_type"] = depType
                }
            });
        }

        foreach (var (src, tgt) in rawPkgDeps)
        {
            if (nodeMap.ContainsKey(src) && nodeMap.ContainsKey(tgt) && src != tgt)
            {
                if (graph.Edges.All(e => !(e.Source == src && e.Target == tgt)))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{src}->{tgt}:LIBRARY",
                        Source = src,
                        Target = tgt,
                        Kind = "LIBRARY",
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = "library"
                        }
                    });
                }
            }
        }

        // 6c. Project -> External Package Dependencies (npm, NuGet, etc. not implemented by workspace projects)
        try
        {
            var extPkgQuery = "MATCH (p:Project)-[r:DEPENDS_ON]->(pkg:Package) WHERE (pkg.is_external = true OR (pkg.is_external IS NULL AND NOT (pkg)-[:IMPLEMENTED_BY]->(:Project))) AND NOT (pkg)-[:IMPLEMENTED_BY]->(p) AND toLower(pkg.name) <> toLower(p.name) RETURN p.id AS source, pkg.id AS id, pkg.name AS name, pkg.version AS version, pkg.type AS pkg_type, coalesce(pkg.is_external, true) AS is_external";
            var extPkgJson = await client.ExecuteQueryAsync(extPkgQuery, null, cancellationToken);
            using var extPkgDoc = JsonDocument.Parse(extPkgJson);
            foreach (var row in extPkgDoc.RootElement.EnumerateArray())
            {
                var src = row.GetStringProp("source");
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var version = row.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var pkgType = row.TryGetProperty("pkg_type", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : "package";

                var displayName = string.IsNullOrEmpty(version) ? name : $"{name}@{version}";

                if (!nodeMap.ContainsKey(id))
                {
                    var pkgNode = new GraphNodeDto
                    {
                        Id = id,
                        Kind = "Package",
                        Name = name,
                        DisplayName = displayName,
                        Properties = new Dictionary<string, string>
                        {
                            ["is_library"] = "true",
                            ["is_external"] = "true",
                            ["entity_type"] = "library",
                            ["package_type"] = pkgType ?? "package",
                            ["version"] = version ?? "",
                            ["layer"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId,
                            ["layerId"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId,
                            ["layerName"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName,
                            ["layerColor"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color
                        }
                    };
                    nodeMap[id] = pkgNode;
                    graph.Nodes.Add(pkgNode);
                }

                if (nodeMap.ContainsKey(src) && !graph.Edges.Any(e => e.Source == src && e.Target == id && e.Kind == "LIBRARY"))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{src}->{id}:LIBRARY",
                        Source = src,
                        Target = id,
                        Kind = "LIBRARY",
                        Category = "library",
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = "library"
                        }
                    });
                }
            }
        }
        catch { }

        // 7. Macro Edges (Materialized in Graph: USES_DB, SERVICE_CALL, PUBLISHES_TO, TRIGGERS, etc.)
        var projectNodes = graph.Nodes.Where(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase)).ToList();

        try
        {
            var edgesQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['SERVICE_CALL', 'DEPENDS_ON', 'USES_DB', 'PUBLISHES_TO', 'TRIGGERS', 'SUBSCRIBES_TO', 'INTEGRATES_WITH', 'CALLS_ENDPOINT'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties";
            var edgesJson = await client.ExecuteQueryAsync(edgesQuery, null, cancellationToken);
            using var edgesDoc = JsonDocument.Parse(edgesJson);

            foreach (var row in edgesDoc.RootElement.EnumerateArray())
            {
                var rawSrc = row.GetStringProp("source");
                var rawTgt = row.GetStringProp("target");
                var rKind = row.GetStringProp("kind");

                var src = dbIdToCanonicalId.GetValueOrDefault(rawSrc, rawSrc);
                var tgt = dbIdToCanonicalId.GetValueOrDefault(rawTgt, rawTgt);

                if (!nodeMap.ContainsKey(src))
                {
                    var owner = FindOwningProject(src, projectNodes);
                    if (owner != null) src = owner.Id;
                }
                if (!nodeMap.ContainsKey(tgt))
                {
                    var owner = FindOwningProject(tgt, projectNodes);
                    if (owner != null) tgt = owner.Id;
                }

                if (nodeMap.ContainsKey(src) && nodeMap.ContainsKey(tgt) && src != tgt)
                {
                    var edgeProps = row.ExtractProperties();
                    nodeMap.TryGetValue(tgt, out var targetNode);
                    var isTargetLib = IsLibraryProject(targetNode);

                    var (category, dType, normalizedKind) = CodeExplorer.Core.Parser.PostIndexAnalyzer.NormalizeEdgeCategory(
                        rKind,
                        nodeMap.GetValueOrDefault(src)?.Kind,
                        targetNode?.Kind,
                        edgeProps.GetValueOrDefault("category"),
                        edgeProps.GetValueOrDefault("dependency_type"),
                        isTargetLib
                    );

                    edgeProps["dependency_type"] = dType;
                    edgeProps["category"] = category;

                    var outKind = category == "library" ? "LIBRARY" : normalizedKind;

                    if (graph.Edges.All(e => !(e.Source == src && e.Target == tgt && e.Kind == outKind)))
                    {
                        graph.Edges.Add(new GraphEdgeDto
                        {
                            Id = $"{src}->{tgt}:{outKind}",
                            Source = src,
                            Target = tgt,
                            Kind = outKind,
                            Category = category,
                            Properties = edgeProps
                        });
                    }
                }
            }
        }
        catch { }

        // 8. Synthesize project -> database edges from project prefix (for unmaterialized project-scoped databases)
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
                    Properties = new Dictionary<string, string>
                    {
                        ["dependency_type"] = "database",
                        ["is_semantic"] = "true"
                    }
                });
            }
        }

        // 9. Lift Transitive Semantic Relations through Internal Libraries
        LiftTransitiveSemanticRelations(graph);

        // 10. Tag Edge Semantic Properties
        foreach (var edge in graph.Edges)
        {
            edge.Properties ??= new Dictionary<string, string>();
            var srcNode = nodeMap.GetValueOrDefault(edge.Source);
            var tgtNode = nodeMap.GetValueOrDefault(edge.Target);

            var srcIsSemantic = srcNode?.Properties?.GetValueOrDefault("is_semantic_entity") == "true";
            var tgtIsSemantic = tgtNode?.Properties?.GetValueOrDefault("is_semantic_entity") == "true";

            if (srcIsSemantic && tgtIsSemantic && edge.Kind != "LIBRARY")
            {
                edge.Properties["is_semantic"] = "true";
            }
            else if (!edge.Properties.ContainsKey("is_semantic"))
            {
                edge.Properties["is_semantic"] = "false";
            }
        }

        NormalizeEdges(graph);

        graph.Metadata ??= new Dictionary<string, string>();
        graph.Metadata["graphType"] = "architecture";
        var projectPaths = await GetProjectPathsMapAsync(client, cancellationToken);
        graph.Metadata["projectPaths"] = JsonSerializer.Serialize(projectPaths);

        return graph;
    }

    public static async Task<Dictionary<string, string>> GetProjectPathsMapAsync(
        IGraphClient client,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var query = "MATCH (p:Project) RETURN p.name AS name, p.path AS path, p.id AS id";
            var json = await client.ExecuteQueryAsync(query, null, cancellationToken);
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

    public static async Task<List<string>> GetAllProjectsAsync(
        IGraphClient client,
        CancellationToken cancellationToken = default)
    {
        var query = "MATCH (p:Project) RETURN DISTINCT p.name AS name ORDER BY p.name";
        var json = await client.ExecuteQueryAsync(query, null, cancellationToken);
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

    public static async Task<GraphDataDto> GetProjectNeighborhoodAsync(
        IGraphClient client,
        string? projectName,
        CancellationToken cancellationToken = default)
    {
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>()
        };
        var allProjects = await GetAllProjectsAsync(client, cancellationToken);
        graph.Metadata["allProjects"] = JsonSerializer.Serialize(allProjects);
        var neighborhoodProjectPaths = await GetProjectPathsMapAsync(client, cancellationToken);
        graph.Metadata["projectPaths"] = JsonSerializer.Serialize(neighborhoodProjectPaths);

        if (allProjects.Count == 0)
        {
            return graph;
        }

        var targetName = string.IsNullOrWhiteSpace(projectName) ? allProjects[0] : projectName;

        // 1. Target Center Project
        var centerQuery = "MATCH (p:Project) WHERE p.name = $name OR p.id = $name OR p.path = $name OR p.id = ('workspace:project:' + $name + ':') RETURN p.id AS id, p.name AS name, p.framework AS framework, p.path AS path, p.project_type AS project_type LIMIT 1";
        var centerJson = await client.ExecuteQueryAsync(centerQuery, new Dictionary<string, object> { ["name"] = targetName }, cancellationToken);
        using var centerDoc = JsonDocument.Parse(centerJson);

        if (centerDoc.RootElement.GetArrayLength() == 0)
        {
            // Fallback to first project if name not found
            targetName = allProjects[0];
            centerJson = await client.ExecuteQueryAsync(centerQuery, new Dictionary<string, object> { ["name"] = targetName }, cancellationToken);
        }

        using var actualCenterDoc = JsonDocument.Parse(centerJson);
        if (actualCenterDoc.RootElement.GetArrayLength() == 0)
        {
            return graph;
        }

        var centerRow = actualCenterDoc.RootElement[0];
        var centerId = centerRow.GetStringProp("id");
        var centerProjName = centerRow.GetStringProp("name", centerId);
        var centerFramework = centerRow.TryGetProperty("framework", out var cf) && cf.ValueKind == JsonValueKind.String ? cf.GetString() : null;
        var centerPath = centerRow.TryGetProperty("path", out var cp) && cp.ValueKind == JsonValueKind.String ? cp.GetString() : null;
        var centerProjectType = centerRow.TryGetProperty("project_type", out var cpt) && cpt.ValueKind == JsonValueKind.String ? cpt.GetString() : null;

        var centerNode = new GraphNodeDto
        {
            Id = centerId,
            Kind = "Project",
            Name = centerProjName,
            DisplayName = string.IsNullOrEmpty(centerFramework) ? centerProjName : $"{centerProjName} ({centerFramework})",
            FilePath = centerPath,
            Properties = new Dictionary<string, string>
            {
                ["column"] = "center",
                ["role"] = "target"
            }
        };
        if (!string.IsNullOrEmpty(centerFramework)) centerNode.Properties["framework"] = centerFramework;
        if (!string.IsNullOrEmpty(centerPath)) centerNode.Properties["path"] = centerPath;
        if (!string.IsNullOrEmpty(centerProjectType)) centerNode.Properties["project_type"] = centerProjectType;

        try
        {
            var centerPkgCountQuery = "MATCH (p:Project {id: $centerId})-[:DEPENDS_ON]->(pkg:Package) RETURN count(pkg) AS pkgCount";
            var centerPkgCountJson = await client.ExecuteQueryAsync(centerPkgCountQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
            using var centerPkgCountDoc = JsonDocument.Parse(centerPkgCountJson);
            if (centerPkgCountDoc.RootElement.GetArrayLength() > 0)
            {
                var row = centerPkgCountDoc.RootElement[0];
                var count = row.TryGetProperty("pkgCount", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt64() : 0;
                centerNode.Properties["package_count"] = count.ToString();
            }
        }
        catch { }

        graph.Nodes.Add(centerNode);
        graph.Metadata["selectedProject"] = centerProjName;

        // 2. Fetch the architecture graph which has already materialized and lifted all macro relationships
        var archGraph = await GetArchitectureGraphAsync(client, null, cancellationToken);
        var archCenter = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(centerId, StringComparison.OrdinalIgnoreCase));
        if (archCenter?.Properties != null)
        {
            foreach (var (k, v) in archCenter.Properties)
            {
                if (!centerNode.Properties.ContainsKey(k))
                {
                    centerNode.Properties[k] = v;
                }
            }
        }

        var nodeLookup = archGraph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        // 3. Project 1-hop inbound and outbound neighborhood from archGraph
        foreach (var edge in archGraph.Edges)
        {
            if (edge.Source.Equals(centerId, StringComparison.OrdinalIgnoreCase) && !edge.Target.Equals(centerId, StringComparison.OrdinalIgnoreCase))
            {
                // Outbound from center
                if (nodeLookup.TryGetValue(edge.Target, out var tgtNode))
                {
                    if (tgtNode.Kind.Equals("Topic", StringComparison.OrdinalIgnoreCase) && edge.Kind.Equals("SUBSCRIBES_TO", StringComparison.OrdinalIgnoreCase))
                    {
                        // Subscribed topic acts as an inbound trigger into center
                        AddNeighborNode(graph, tgtNode, column: "left", role: "topic");
                        AddNeighborEdge(graph, tgtNode.Id, centerId, "TRIGGERS", "messaging", edge.Properties);
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

                        AddNeighborNode(graph, tgtNode, column: "right", role: role);
                        AddNeighborEdge(graph, centerId, tgtNode.Id, edge.Kind, cat, edge.Properties);
                    }
                }
            }
            else if (edge.Target.Equals(centerId, StringComparison.OrdinalIgnoreCase) && !edge.Source.Equals(centerId, StringComparison.OrdinalIgnoreCase))
            {
                // Inbound to center
                if (nodeLookup.TryGetValue(edge.Source, out var srcNode))
                {
                    var role = srcNode.Kind.Equals("Topic", StringComparison.OrdinalIgnoreCase) ? "topic" : "inbound";
                    var cat = edge.Category ?? (srcNode.Kind switch
                    {
                        "Topic" => "messaging",
                        _ => (edge.Kind == "LIBRARY" ? "library" : "service_call")
                    });

                    AddNeighborNode(graph, srcNode, column: "left", role: role);
                    AddNeighborEdge(graph, srcNode.Id, centerId, edge.Kind, cat, edge.Properties);
                }
            }
        }

        static void AddNeighborNode(GraphDataDto g, GraphNodeDto sourceNode, string column, string role)
        {
            if (g.Nodes.All(n => !n.Id.Equals(sourceNode.Id, StringComparison.OrdinalIgnoreCase)))
            {
                var clone = new GraphNodeDto
                {
                    Id = sourceNode.Id,
                    Kind = sourceNode.Kind,
                    Name = sourceNode.Name,
                    DisplayName = sourceNode.DisplayName,
                    FilePath = sourceNode.FilePath,
                    Properties = sourceNode.Properties != null
                        ? new Dictionary<string, string>(sourceNode.Properties)
                        : new Dictionary<string, string>()
                };
                clone.Properties["column"] = column;
                clone.Properties["role"] = role;
                g.Nodes.Add(clone);
            }
        }

        static void AddNeighborEdge(GraphDataDto g, string source, string target, string kind, string category, Dictionary<string, string>? properties)
        {
            if (g.Edges.All(e => !(e.Source.Equals(source, StringComparison.OrdinalIgnoreCase) &&
                                  e.Target.Equals(target, StringComparison.OrdinalIgnoreCase) &&
                                  e.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))))
            {
                var props = properties != null ? new Dictionary<string, string>(properties) : new Dictionary<string, string>();
                if (!props.ContainsKey("dependency_type"))
                {
                    props["dependency_type"] = category;
                }
                g.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{source}->{target}:{kind}",
                    Source = source,
                    Target = target,
                    Kind = kind,
                    Category = category,
                    Properties = props
                });
            }
        }

        ApplyLayerClassification(graph);

        NormalizeEdges(graph);

        graph.Metadata ??= new Dictionary<string, string>();
        graph.Metadata["graphType"] = "flow";

        return graph;
    }

    public static bool IsLibraryProject(GraphNodeDto? node)
    {
        if (node == null || !node.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase)) return false;

        var isLibProp = node.Properties?.GetValueOrDefault("is_library");
        if (isLibProp == "true") return true;
        if (isLibProp == "false") return false;

        var projectType = node.Properties?.GetValueOrDefault("project_type")?.ToLowerInvariant();
        if (projectType == "library") return true;

        var role = node.Properties?.GetValueOrDefault("role");
        if (role is "SharedLibrary" or "Test") return true;
        if (role is "Service" or "FrontendApp" or "Worker" or "CliTool") return false;

        var (_, isLib) = CodeExplorer.Core.Analysis.ProjectRoleDetector.DetectRole(
            "",
            [],
            node.FilePath ?? "",
            node.Name ?? "",
            node.Properties?.GetValueOrDefault("project_type") ?? ""
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
            filePath = filePath.Substring(fileIdx + ":file:".Length);
        }
        else
        {
            var symIdx = filePath.IndexOf(":symbol:", StringComparison.OrdinalIgnoreCase);
            if (symIdx >= 0)
            {
                filePath = filePath.Substring(symIdx + ":symbol:".Length);
            }
        }

        var normFilePath = filePath.Replace('\\', '/').TrimStart('/');

        // Match against project FilePath (longest match first)
        GraphNodeDto? bestMatch = null;
        int bestLen = -1;
        foreach (var p in projList)
        {
            var normProj = (p.FilePath ?? "").Replace('\\', '/').TrimStart('/').TrimEnd('/');
            if (normProj == "" || normProj == ".")
            {
                // Root project matches everything with length 0 as fallback
                if (bestLen < 0)
                {
                    bestMatch = p;
                    bestLen = 0;
                }
                continue;
            }

            if (normFilePath.StartsWith(normProj + "/", StringComparison.OrdinalIgnoreCase) ||
                normFilePath.Equals(normProj, StringComparison.OrdinalIgnoreCase))
            {
                if (normProj.Length > bestLen)
                {
                    bestLen = normProj.Length;
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
        var services = graph.Nodes.Where(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase) && !IsLibraryProject(n)).ToList();
        var libraries = graph.Nodes.Where(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase) && IsLibraryProject(n)).ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        // Build adjacency list for all outgoing and incoming edges
        var outEdges = new Dictionary<string, List<GraphEdgeDto>>(StringComparer.OrdinalIgnoreCase);
        var inEdges = new Dictionary<string, List<GraphEdgeDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            if (!outEdges.TryGetValue(edge.Source, out var outList))
            {
                outList = new List<GraphEdgeDto>();
                outEdges[edge.Source] = outList;
            }
            outList.Add(edge);

            if (!inEdges.TryGetValue(edge.Target, out var inList))
            {
                inList = new List<GraphEdgeDto>();
                inEdges[edge.Target] = inList;
            }
            inList.Add(edge);
        }

        // For each Service, traverse through Libraries up to depth 3
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
                        if (srcNode.Kind.Equals("Topic", StringComparison.OrdinalIgnoreCase) || inEdge.Kind == "TRIGGERS")
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
                        if (targetNode.Kind.Equals("Database", StringComparison.OrdinalIgnoreCase) || edge.Kind == "USES_DB")
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
                        else if (targetNode.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase) && !IsLibraryProject(targetNode) && targetNode.Id != service.Id)
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
                        // Case 3: Library connects to Message Broker / External Service -> Lift to Service
                        else if (targetNode.Kind.Equals("ExternalService", StringComparison.OrdinalIgnoreCase) || targetNode.Kind.Equals("Topic", StringComparison.OrdinalIgnoreCase))
                        {
                            var kind = edge.Kind == "TRIGGERS" ? "TRIGGERS" : "SERVICE_CALL";
                            var depType = edge.Kind == "TRIGGERS" ? "messaging" : "service_call";
                            if (graph.Edges.All(e => !(e.Source == service.Id && e.Target == targetNode.Id && e.Kind == kind)))
                            {
                                graph.Edges.Add(new GraphEdgeDto
                                {
                                    Id = $"{service.Id}->{targetNode.Id}:{kind}",
                                    Source = service.Id,
                                    Target = targetNode.Id,
                                    Kind = kind,
                                    Category = depType,
                                    Properties = new Dictionary<string, string>
                                    {
                                        ["dependency_type"] = depType,
                                        ["is_semantic"] = "true",
                                        ["semantic_lifted"] = "true",
                                        ["via_library"] = viaLib
                                    }
                                });
                            }
                        }
                        // Case 4: Continues transitively through another Library
                        else if (libraries.TryGetValue(targetNode.Id, out var nextLib) && depth < 3)
                        {
                            if (visited.Add(nextLib.Id))
                            {
                                queue.Enqueue((nextLib.Id, depth + 1, viaLib));
                            }
                        }
                    }
                }
            }
        }
    }

    public static void ApplyLayerClassification(GraphDataDto graph)
    {
        var classifierItems = graph.Nodes
            .Where(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase))
            .Select(n => new CodeExplorer.Core.Analysis.ProjectClassifierItem
            {
                Id = n.Id,
                Name = n.Name,
                FilePath = n.FilePath,
                Framework = n.Properties?.GetValueOrDefault("framework")
            });

        var dependencyItems = graph.Edges
            .Select(e => new CodeExplorer.Core.Analysis.DependencyItem
            {
                SourceId = e.Source,
                TargetId = e.Target
            });

        var layerMap = CodeExplorer.Core.Analysis.ProjectLayerClassifier.Classify(classifierItems, dependencyItems);

        foreach (var node in graph.Nodes)
        {
            node.Properties ??= new Dictionary<string, string>();

            if (node.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase))
            {
                if (!node.Properties.ContainsKey("layer") && layerMap.TryGetValue(node.Id, out var layer))
                {
                    node.Properties["layer"] = layer.LayerId;
                    node.Properties["layerId"] = layer.LayerId;
                    node.Properties["layerName"] = layer.LayerName;
                    node.Properties["layerOrder"] = layer.Order.ToString();
                    node.Properties["layerColor"] = layer.Color;
                    node.Properties["layerIcon"] = layer.Icon;
                }

                if (IsLibraryProject(node))
                {
                    node.Properties["is_library"] = "true";
                }
            }
            else if (node.Kind.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                node.Properties["layer"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId;
                node.Properties["layerId"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId;
                node.Properties["layerName"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName;
                node.Properties["layerOrder"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Order.ToString();
                node.Properties["layerColor"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color;
                node.Properties["layerIcon"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Icon;
            }
        }

        graph.Metadata ??= new Dictionary<string, string>();
        graph.Metadata["layers"] = JsonSerializer.Serialize(CodeExplorer.Core.Analysis.StandardLayers.All);
    }

    private static string GetStringProp(this JsonElement elem, string prop, string fallback = "")
    {
        return elem.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? (v.GetString() ?? fallback)
            : fallback;
    }

    private static Dictionary<string, string> ExtractProperties(this JsonElement elem, string propName = "properties")
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!elem.TryGetProperty(propName, out var pElem)) return result;

        if (pElem.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in pElem.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ToString();
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
                        result[prop.Name] = prop.Value.ToString();
                    }
                }
            }
            catch { }
        }

        return result;
    }

    public static (string CanonicalName, string CanonicalType, string CanonicalKey) CanonicalizeDatabase(string rawName, string? rawDbType)
    {
        return CodeExplorer.Core.Parser.PostIndexAnalyzer.CanonicalizeDatabase(rawName, rawDbType);
    }

    public static bool IsGenericOrOrmDatabase(string rawName)
    {
        return CodeExplorer.Core.Analysis.ResourceReconciliationService.IsGenericConfigKey(rawName);
    }

    public static void NormalizeEdges(GraphDataDto graph)
    {
        var nodesById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var edge in graph.Edges)
        {
            edge.Properties ??= new Dictionary<string, string>();
            var depType = edge.Properties.GetValueOrDefault("dependency_type");

            nodesById.TryGetValue(edge.Source, out var sourceNode);
            nodesById.TryGetValue(edge.Target, out var targetNode);

            var (category, dType, normalizedKind) = CodeExplorer.Core.Parser.PostIndexAnalyzer.NormalizeEdgeCategory(
                edge.Kind,
                sourceNode?.Kind,
                targetNode?.Kind,
                edge.Category,
                depType,
                targetNode != null && IsLibraryProject(targetNode)
            );

            edge.Category = category;
            edge.Properties["dependency_type"] = dType;
            edge.Kind = category == "library" ? "LIBRARY" : normalizedKind;
        }
    }

    public static async Task<MetadataResponseDto> GetMetadataAsync(
        IGraphClient client,
        CancellationToken cancellationToken = default)
    {
        var result = new MetadataResponseDto();

        // 1. Node counts by label
        try
        {
            var nodeQuery = "MATCH (n) RETURN labels(n) AS lbl, count(n) AS cnt";
            var nodeJson = await client.ExecuteQueryAsync(nodeQuery, null, cancellationToken);
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
            var relJson = await client.ExecuteQueryAsync(relQuery, null, cancellationToken);
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

    public static async Task<NodesResponseDto> GetNodesAsync(
        IGraphClient client,
        string? kind = null,
        int offset = 0,
        int limit = 50,
        string? search = null,
        CancellationToken cancellationToken = default)
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

        // Validate kind (must be alphanumeric/underscore only to prevent injection)
        var safeKind = !string.IsNullOrWhiteSpace(kind) && System.Text.RegularExpressions.Regex.IsMatch(kind, "^[A-Za-z0-9_]+$")
            ? kind
            : null;

        var matchClause = safeKind != null ? $"MATCH (n:{safeKind})" : "MATCH (n)";

        // Total count query
        var countQuery = string.IsNullOrWhiteSpace(search)
            ? $"{matchClause} RETURN count(n) AS total"
            : $"{matchClause} WHERE toLower(n.name) CONTAINS toLower('{search.Replace("'", "''")}') RETURN count(n) AS total";

        try
        {
            var countJson = await client.ExecuteQueryAsync(countQuery, null, cancellationToken);
            using var countDoc = JsonDocument.Parse(countJson);
            var firstRow = countDoc.RootElement.EnumerateArray().FirstOrDefault();
            if (firstRow.ValueKind == JsonValueKind.Object && firstRow.TryGetProperty("total", out var totProp) && totProp.ValueKind == JsonValueKind.Number)
            {
                result.Total = totProp.GetInt64();
            }
        }
        catch { }

        // Paged items query
        var whereClause = string.IsNullOrWhiteSpace(search)
            ? ""
            : $"WHERE toLower(n.name) CONTAINS toLower('{search.Replace("'", "''")}') ";

        var dataQuery = $"{matchClause} {whereClause}RETURN n.id AS id, n.name AS name, n.file_path AS file_path, n.path AS path, n.line AS line, labels(n) AS lbl, n.framework AS framework, n.method AS method, n.route AS route ORDER BY n.name ASC SKIP {offset} LIMIT {limit}";

        try
        {
            var dataJson = await client.ExecuteQueryAsync(dataQuery, null, cancellationToken);
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
}

