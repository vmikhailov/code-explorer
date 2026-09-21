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
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.framework AS framework, p.path AS path, p.project_type AS project_type";
        var projJson = await client.ExecuteQueryAsync(projQuery, null, cancellationToken);
        using var projDoc = JsonDocument.Parse(projJson);

        foreach (var row in projDoc.RootElement.EnumerateArray())
        {
            var id = row.GetStringProp("id");
            var name = row.GetStringProp("name", id);
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            var projectType = row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;

            if (!string.IsNullOrWhiteSpace(projectFilter) && !name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var node = new GraphNodeDto
            {
                Id = id,
                Kind = "Project",
                Name = name,
                DisplayName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})",
                FilePath = path,
                Properties = new Dictionary<string, string>()
            };
            if (!string.IsNullOrEmpty(framework)) node.Properties["framework"] = framework;
            if (!string.IsNullOrEmpty(path)) node.Properties["path"] = path;
            if (!string.IsNullOrEmpty(projectType)) node.Properties["project_type"] = projectType;

            nodeMap[id] = node;
            graph.Nodes.Add(node);
        }

        // 2. Databases (Consolidate project-scoped DB nodes into canonical data stores to prevent duplication)
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type";
        var dbJson = await client.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        using var dbDoc = JsonDocument.Parse(dbJson);
        var dbIdToCanonicalId = new Dictionary<string, string>();

        foreach (var row in dbDoc.RootElement.EnumerateArray())
        {
            var id = row.GetStringProp("id");
            var name = row.GetStringProp("name", id);
            var dbType = row.TryGetProperty("db_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Database") : "Database";

            var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType);
            var isProjectScoped = id.IndexOf("db:", StringComparison.OrdinalIgnoreCase) > 0 ||
                                  id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase);
            var isWorkspaceDatabase = id.StartsWith("workspace:database:", StringComparison.OrdinalIgnoreCase);
            var canonicalId = (isProjectScoped || isWorkspaceDatabase || cKey == "typeorm")
                ? $"workspace:database:{cType}:{cKey}"
                : id;
            dbIdToCanonicalId[id] = canonicalId;

            if (!nodeMap.ContainsKey(canonicalId))
            {
                var node = new GraphNodeDto
                {
                    Id = canonicalId,
                    Kind = "Database",
                    Name = cName,
                    DisplayName = $"{cName} [{cType}]",
                    Properties = new Dictionary<string, string>
                    {
                        ["db_type"] = cType,
                        ["role"] = "database"
                    }
                };

                nodeMap[canonicalId] = node;
                graph.Nodes.Add(node);
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
            var pkgDepQuery = "MATCH (p1:Project)-[:DEPENDS_ON]->(pkg:Package)<-[:IMPLEMENTED_BY]-(p2:Project) WHERE p1.id <> p2.id RETURN DISTINCT p1.id AS source, p2.id AS target";
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

        var layerMap = CodeExplorer.Core.Analysis.ProjectLayerClassifier.Classify(classifierItems, dependencyItems);

        foreach (var node in graph.Nodes)
        {
            node.Properties ??= new Dictionary<string, string>();

            if (node.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase))
            {
                if (layerMap.TryGetValue(node.Id, out var layer))
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

        // 7. Project -> Database (Direct, File-Level, and Project-Scoped)
        var projectNodes = graph.Nodes.Where(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase)).ToList();

        // 7a. Direct Project->Database
        try
        {
            var usesDbQuery = "MATCH (p:Project)-[r:USES_DB]->(d:Database) RETURN p.id AS source, d.id AS target, r.kind AS kind";
            var usesDbJson = await client.ExecuteQueryAsync(usesDbQuery, null, cancellationToken);
            using var usesDbDoc = JsonDocument.Parse(usesDbJson);

            foreach (var row in usesDbDoc.RootElement.EnumerateArray())
            {
                var src = row.GetStringProp("source");
                var rawTgt = row.GetStringProp("target");
                var tgt = dbIdToCanonicalId.GetValueOrDefault(rawTgt, rawTgt);
                var kind = "USES_DB";

                if (nodeMap.ContainsKey(src) && nodeMap.ContainsKey(tgt))
                {
                    if (graph.Edges.All(e => !(e.Source == src && e.Target == tgt)))
                    {
                        graph.Edges.Add(new GraphEdgeDto
                        {
                            Id = $"{src}->{tgt}:{kind}",
                            Source = src,
                            Target = tgt,
                            Kind = kind,
                            Properties = new Dictionary<string, string>
                            {
                                ["dependency_type"] = "database",
                                ["is_semantic"] = "true"
                            }
                        });
                    }
                }
            }
        }
        catch { }

        // 7b. File-level Database usage: (File)-[:USES_DB]->(Database)
        try
        {
            var fileDbQuery = "MATCH (f:File)-[r:USES_DB]->(d:Database) RETURN f.id AS source, d.id AS target, r.kind AS kind";
            var fileDbJson = await client.ExecuteQueryAsync(fileDbQuery, null, cancellationToken);
            using var fileDbDoc = JsonDocument.Parse(fileDbJson);

            foreach (var row in fileDbDoc.RootElement.EnumerateArray())
            {
                var fileId = row.GetStringProp("source");
                var rawTgt = row.GetStringProp("target");
                var tgt = dbIdToCanonicalId.GetValueOrDefault(rawTgt, rawTgt);

                if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(tgt) && nodeMap.ContainsKey(tgt))
                {
                    var owningProj = FindOwningProject(fileId, projectNodes);
                    if (owningProj != null && graph.Edges.All(e => !(e.Source == owningProj.Id && e.Target == tgt)))
                    {
                        graph.Edges.Add(new GraphEdgeDto
                        {
                            Id = $"{owningProj.Id}->{tgt}:USES_DB",
                            Source = owningProj.Id,
                            Target = tgt,
                            Kind = "USES_DB",
                            Properties = new Dictionary<string, string>
                            {
                                ["dependency_type"] = "database",
                                ["is_semantic"] = "true"
                            }
                        });
                    }
                }
            }
        }
        catch { }

        // 7c. Synthesize project -> database edges from project prefix (for workspaces where direct Project->USES_DB wasn't written)
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

        // 8. External Service Links
        var svcLinkQuery = "MATCH (p:Project)-[r:CALLS_ENDPOINT|TRIGGERS]->(s:ExternalService) RETURN p.id AS source, s.id AS target, r.kind AS kind";
        var svcLinkJson = await client.ExecuteQueryAsync(svcLinkQuery, null, cancellationToken);
        using var svcLinkDoc = JsonDocument.Parse(svcLinkJson);

        foreach (var row in svcLinkDoc.RootElement.EnumerateArray())
        {
            var src = row.GetStringProp("source");
            var tgt = row.GetStringProp("target");
            var rKind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "CALLS_ENDPOINT") : "CALLS_ENDPOINT";
            var kind = rKind == "TRIGGERS" ? "TRIGGERS" : "SERVICE_CALL";
            var depType = rKind == "TRIGGERS" ? "messaging" : "service_call";

            if (nodeMap.ContainsKey(src) && nodeMap.ContainsKey(tgt))
            {
                if (graph.Edges.All(e => !(e.Source == src && e.Target == tgt && e.Kind == kind)))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{src}->{tgt}:{kind}",
                        Source = src,
                        Target = tgt,
                        Kind = kind,
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = depType,
                            ["is_semantic"] = "true"
                        }
                    });
                }
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
        var centerQuery = "MATCH (p:Project) WHERE p.name = $name OR p.id = $name RETURN p.id AS id, p.name AS name, p.framework AS framework, p.path AS path, p.project_type AS project_type LIMIT 1";
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

        graph.Nodes.Add(centerNode);
        graph.Metadata["selectedProject"] = centerProjName;

        // 2. Inbound Project Dependencies (Left Column)
        var inQuery = "MATCH (in:Project)-[r:DEPENDS_ON]->(p:Project {id: $centerId}) RETURN in.id AS id, in.name AS name, in.framework AS framework, in.path AS path, in.project_type AS project_type, r.kind AS kind, r.dependency_type AS dep_type";
        var inJson = await client.ExecuteQueryAsync(inQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
        using var inDoc = JsonDocument.Parse(inJson);

        foreach (var row in inDoc.RootElement.EnumerateArray())
        {
            var id = row.GetStringProp("id");
            var name = row.GetStringProp("name", id);
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            var projectType = row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;
            var rawKind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "DEPENDS_ON") : "DEPENDS_ON";
            var depType = row.TryGetProperty("dep_type", out var dt) && dt.ValueKind == JsonValueKind.String ? (dt.GetString() ?? "") : "";

            var isTargetLib = IsLibraryProject(centerNode);
            if (string.IsNullOrEmpty(depType))
            {
                depType = isTargetLib ? "library" : "service_call";
            }
            else if (isTargetLib && depType != "service_call")
            {
                depType = "library";
            }

            var edgeKind = depType == "library" ? "LIBRARY" : (rawKind == "TRIGGERS" ? "TRIGGERS" : "SERVICE_CALL");

            if (graph.Nodes.All(n => n.Id != id))
            {
                var inNode = new GraphNodeDto
                {
                    Id = id,
                    Kind = "Project",
                    Name = name,
                    DisplayName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})",
                    FilePath = path,
                    Properties = new Dictionary<string, string>
                    {
                        ["column"] = "left",
                        ["role"] = "inbound"
                    }
                };
                if (!string.IsNullOrEmpty(framework)) inNode.Properties["framework"] = framework;
                if (!string.IsNullOrEmpty(path)) inNode.Properties["path"] = path;
                if (!string.IsNullOrEmpty(projectType)) inNode.Properties["project_type"] = projectType;
                graph.Nodes.Add(inNode);
            }

            if (graph.Edges.All(e => !(e.Source == id && e.Target == centerId && e.Kind == edgeKind)))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{id}->{centerId}:{edgeKind}",
                    Source = id,
                    Target = centerId,
                    Kind = edgeKind,
                    Properties = new Dictionary<string, string>
                    {
                        ["dependency_type"] = depType
                    }
                });
            }
        }

        // 2b. Inbound package fallback
        try
        {
            var inPkgQuery = "MATCH (in:Project)-[:DEPENDS_ON]->(:Package)<-[:IMPLEMENTED_BY]-(p:Project {id: $centerId}) WHERE in.id <> p.id RETURN in.id AS id, in.name AS name, in.framework AS framework, in.path AS path, in.project_type AS project_type";
            var inPkgJson = await client.ExecuteQueryAsync(inPkgQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
            using var inPkgDoc = JsonDocument.Parse(inPkgJson);
            foreach (var row in inPkgDoc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                var projectType = row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;

                if (graph.Nodes.All(n => n.Id != id))
                {
                    var inNode = new GraphNodeDto
                    {
                        Id = id,
                        Kind = "Project",
                        Name = name,
                        DisplayName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})",
                        FilePath = path,
                        Properties = new Dictionary<string, string>
                        {
                            ["column"] = "left",
                            ["role"] = "inbound"
                        }
                    };
                    if (!string.IsNullOrEmpty(framework)) inNode.Properties["framework"] = framework;
                    if (!string.IsNullOrEmpty(path)) inNode.Properties["path"] = path;
                    if (!string.IsNullOrEmpty(projectType)) inNode.Properties["project_type"] = projectType;
                    graph.Nodes.Add(inNode);
                }

                if (graph.Edges.All(e => !(e.Source == id && e.Target == centerId && e.Kind == "LIBRARY")))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{id}->{centerId}:LIBRARY",
                        Source = id,
                        Target = centerId,
                        Kind = "LIBRARY",
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = "library"
                        }
                    });
                }
            }
        }
        catch { }

        // 3. Outbound Project Dependencies (Right Column)
        var outQuery = "MATCH (p:Project {id: $centerId})-[r:DEPENDS_ON]->(out:Project) RETURN out.id AS id, out.name AS name, out.framework AS framework, out.path AS path, out.project_type AS project_type, r.kind AS kind, r.dependency_type AS dep_type";
        var outJson = await client.ExecuteQueryAsync(outQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
        using var outDoc = JsonDocument.Parse(outJson);

        foreach (var row in outDoc.RootElement.EnumerateArray())
        {
            var id = row.GetStringProp("id");
            var name = row.GetStringProp("name", id);
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            var projectType = row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;
            var rawKind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "DEPENDS_ON") : "DEPENDS_ON";
            var depType = row.TryGetProperty("dep_type", out var dt) && dt.ValueKind == JsonValueKind.String ? (dt.GetString() ?? "") : "";

            if (graph.Nodes.All(n => n.Id != id))
            {
                var outNode = new GraphNodeDto
                {
                    Id = id,
                    Kind = "Project",
                    Name = name,
                    DisplayName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})",
                    FilePath = path,
                    Properties = new Dictionary<string, string>
                    {
                        ["column"] = "right",
                        ["role"] = "outbound"
                    }
                };
                if (!string.IsNullOrEmpty(framework)) outNode.Properties["framework"] = framework;
                if (!string.IsNullOrEmpty(path)) outNode.Properties["path"] = path;
                if (!string.IsNullOrEmpty(projectType)) outNode.Properties["project_type"] = projectType;
                graph.Nodes.Add(outNode);
            }

            var outTargetNode = graph.Nodes.FirstOrDefault(n => n.Id == id);
            var isOutLib = IsLibraryProject(outTargetNode);

            if (string.IsNullOrEmpty(depType))
            {
                depType = isOutLib ? "library" : "service_call";
            }
            else if (isOutLib && depType != "service_call")
            {
                depType = "library";
            }

            var edgeKind = depType == "library" ? "LIBRARY" : (rawKind == "TRIGGERS" ? "TRIGGERS" : "SERVICE_CALL");

            if (graph.Edges.All(e => !(e.Source == centerId && e.Target == id && e.Kind == edgeKind)))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{centerId}->{id}:{edgeKind}",
                    Source = centerId,
                    Target = id,
                    Kind = edgeKind,
                    Properties = new Dictionary<string, string>
                    {
                        ["dependency_type"] = depType
                    }
                });
            }
        }

        // 3b. Outbound package fallback
        try
        {
            var outPkgQuery = "MATCH (p:Project {id: $centerId})-[:DEPENDS_ON]->(:Package)<-[:IMPLEMENTED_BY]-(out:Project) WHERE p.id <> out.id RETURN out.id AS id, out.name AS name, out.framework AS framework, out.path AS path, out.project_type AS project_type";
            var outPkgJson = await client.ExecuteQueryAsync(outPkgQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
            using var outPkgDoc = JsonDocument.Parse(outPkgJson);
            foreach (var row in outPkgDoc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                var projectType = row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;

                if (graph.Nodes.All(n => n.Id != id))
                {
                    var outNode = new GraphNodeDto
                    {
                        Id = id,
                        Kind = "Project",
                        Name = name,
                        DisplayName = string.IsNullOrEmpty(framework) ? name : $"{name} ({framework})",
                        FilePath = path,
                        Properties = new Dictionary<string, string>
                        {
                            ["column"] = "right",
                            ["role"] = "outbound"
                        }
                    };
                    if (!string.IsNullOrEmpty(framework)) outNode.Properties["framework"] = framework;
                    if (!string.IsNullOrEmpty(path)) outNode.Properties["path"] = path;
                    if (!string.IsNullOrEmpty(projectType)) outNode.Properties["project_type"] = projectType;
                    graph.Nodes.Add(outNode);
                }

                if (graph.Edges.All(e => !(e.Source == centerId && e.Target == id && e.Kind == "LIBRARY")))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{centerId}->{id}:LIBRARY",
                        Source = centerId,
                        Target = id,
                        Kind = "LIBRARY",
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = "library"
                        }
                    });
                }
            }
        }
        catch { }

        // 4. Outbound Databases (Right Column)
        try
        {
            var dbQuery = "MATCH (p:Project {id: $centerId})-[r:USES_DB]->(d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, r.kind AS kind";
            var dbJson = await client.ExecuteQueryAsync(dbQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
            using var dbDoc = JsonDocument.Parse(dbJson);

            foreach (var row in dbDoc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var dbType = row.TryGetProperty("db_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Database") : "Database";
                var kind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "USES_DB") : "USES_DB";
                var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType);
                var isProjectScoped = id.IndexOf("db:", StringComparison.OrdinalIgnoreCase) > 0 ||
                                      id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase);
                var isWorkspaceDatabase = id.StartsWith("workspace:database:", StringComparison.OrdinalIgnoreCase);
                var canonicalId = (isProjectScoped || isWorkspaceDatabase || cKey == "typeorm")
                    ? $"workspace:database:{cType}:{cKey}"
                    : id;

                if (graph.Nodes.All(n => n.Id != canonicalId))
                {
                    var dbNode = new GraphNodeDto
                    {
                        Id = canonicalId,
                        Kind = "Database",
                        Name = cName,
                        DisplayName = $"{cName} [{cType}]",
                        Properties = new Dictionary<string, string>
                        {
                            ["column"] = "right",
                            ["role"] = "database",
                            ["db_type"] = cType
                        }
                    };
                    graph.Nodes.Add(dbNode);
                }

                if (graph.Edges.All(e => !(e.Source == centerId && e.Target == canonicalId)))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{centerId}->{canonicalId}:{kind}",
                        Source = centerId,
                        Target = canonicalId,
                        Kind = kind,
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = "database"
                        }
                    });
                }
            }
        }
        catch { }

        // Also query databases whose ID starts with centerId prefix (fallback for workspaces where direct Project->USES_DB wasn't stored)
        try
        {
            var centerPrefix = centerId.EndsWith(':') ? centerId : centerId + ":";
            var dbPrefixQuery = "MATCH (d:Database) WHERE d.id STARTS WITH $centerPrefix RETURN d.id AS id, d.name AS name, d.db_type AS db_type";
            var dbPrefixJson = await client.ExecuteQueryAsync(dbPrefixQuery, new Dictionary<string, object> { ["centerPrefix"] = centerPrefix }, cancellationToken);
            using var dbPrefixDoc = JsonDocument.Parse(dbPrefixJson);

            foreach (var row in dbPrefixDoc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var dbType = row.TryGetProperty("db_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Database") : "Database";
                var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType);
                var isProjectScoped = id.IndexOf("db:", StringComparison.OrdinalIgnoreCase) > 0 ||
                                      id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase);
                var isWorkspaceDatabase = id.StartsWith("workspace:database:", StringComparison.OrdinalIgnoreCase);
                var canonicalId = (isProjectScoped || isWorkspaceDatabase || cKey == "typeorm")
                    ? $"workspace:database:{cType}:{cKey}"
                    : id;

                if (graph.Nodes.All(n => n.Id != canonicalId))
                {
                    var dbNode = new GraphNodeDto
                    {
                        Id = canonicalId,
                        Kind = "Database",
                        Name = cName,
                        DisplayName = $"{cName} [{cType}]",
                        Properties = new Dictionary<string, string>
                        {
                            ["column"] = "right",
                            ["role"] = "database",
                            ["db_type"] = cType
                        }
                    };
                    graph.Nodes.Add(dbNode);
                }

                if (graph.Edges.All(e => !(e.Source == centerId && e.Target == canonicalId)))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{centerId}->{canonicalId}:USES_DB",
                        Source = centerId,
                        Target = canonicalId,
                        Kind = "USES_DB",
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = "database"
                        }
                    });
                }
            }
        }
        catch { }

        // Query file-level databases belonging to center project
        try
        {
            var fileDbQuery = "MATCH (f:File)-[r:USES_DB]->(d:Database) RETURN f.id AS fileId, d.id AS id, d.name AS name, d.db_type AS db_type";
            var fileDbJson = await client.ExecuteQueryAsync(fileDbQuery, null, cancellationToken);
            using var fileDbDoc = JsonDocument.Parse(fileDbJson);

            foreach (var row in fileDbDoc.RootElement.EnumerateArray())
            {
                var fileId = row.GetStringProp("fileId");
                if (string.IsNullOrEmpty(fileId)) continue;

                var owning = FindOwningProject(fileId, new[] { centerNode });
                if (owning != null)
                {
                    var id = row.GetStringProp("id");
                    var name = row.GetStringProp("name", id);
                    var dbType = row.TryGetProperty("db_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Database") : "Database";
                    var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType);
                    var isProjectScoped = id.IndexOf("db:", StringComparison.OrdinalIgnoreCase) > 0 ||
                                          id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase);
                    var isWorkspaceDatabase = id.StartsWith("workspace:database:", StringComparison.OrdinalIgnoreCase);
                    var canonicalId = (isProjectScoped || isWorkspaceDatabase || cKey == "typeorm")
                        ? $"workspace:database:{cType}:{cKey}"
                        : id;

                    if (graph.Nodes.All(n => n.Id != canonicalId))
                    {
                        graph.Nodes.Add(new GraphNodeDto
                        {
                            Id = canonicalId,
                            Kind = "Database",
                            Name = cName,
                            DisplayName = $"{cName} [{cType}]",
                            Properties = new Dictionary<string, string>
                            {
                                ["column"] = "right",
                                ["role"] = "database",
                                ["db_type"] = cType
                            }
                        });
                    }

                    if (graph.Edges.All(e => !(e.Source == centerId && e.Target == canonicalId)))
                    {
                        graph.Edges.Add(new GraphEdgeDto
                        {
                            Id = $"{centerId}->{canonicalId}:USES_DB",
                            Source = centerId,
                            Target = canonicalId,
                            Kind = "USES_DB",
                            Properties = new Dictionary<string, string>
                            {
                                ["dependency_type"] = "database"
                            }
                        });
                    }
                }
            }
        }
        catch { }

        // Query transitive databases via outbound libraries
        try
        {
            var outboundLibs = graph.Nodes
                .Where(n => n.Properties?.GetValueOrDefault("column") == "right" && IsLibraryProject(n))
                .ToList();

            foreach (var lib in outboundLibs)
            {
                var libDbQuery = "MATCH (p:Project {id: $libId})-[r:USES_DB]->(d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type";
                var libDbJson = await client.ExecuteQueryAsync(libDbQuery, new Dictionary<string, object> { ["libId"] = lib.Id }, cancellationToken);
                using var libDbDoc = JsonDocument.Parse(libDbJson);

                foreach (var row in libDbDoc.RootElement.EnumerateArray())
                {
                    var id = row.GetStringProp("id");
                    var name = row.GetStringProp("name", id);
                    var dbType = row.TryGetProperty("db_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Database") : "Database";
                    var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType);
                    var isProjectScoped = id.IndexOf("db:", StringComparison.OrdinalIgnoreCase) > 0 ||
                                          id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase);
                    var isWorkspaceDatabase = id.StartsWith("workspace:database:", StringComparison.OrdinalIgnoreCase);
                    var canonicalId = (isProjectScoped || isWorkspaceDatabase || cKey == "typeorm")
                        ? $"workspace:database:{cType}:{cKey}"
                        : id;

                    if (graph.Nodes.All(n => n.Id != canonicalId))
                    {
                        graph.Nodes.Add(new GraphNodeDto
                        {
                            Id = canonicalId,
                            Kind = "Database",
                            Name = cName,
                            DisplayName = $"{cName} [{cType}]",
                            Properties = new Dictionary<string, string>
                            {
                                ["column"] = "right",
                                ["role"] = "database",
                                ["db_type"] = cType
                            }
                        });
                    }

                    if (graph.Edges.All(e => !(e.Source == centerId && e.Target == canonicalId)))
                    {
                        graph.Edges.Add(new GraphEdgeDto
                        {
                            Id = $"{centerId}->{canonicalId}:USES_DB",
                            Source = centerId,
                            Target = canonicalId,
                            Kind = "USES_DB",
                            Properties = new Dictionary<string, string>
                            {
                                ["dependency_type"] = "database",
                                ["semantic_lifted"] = "true",
                                ["via_library"] = lib.Name
                            }
                        });
                    }
                }
            }
        }
        catch { }

        // 5. Outbound External Services / Message Brokers (Right Column)
        try
        {
            var svcQuery = "MATCH (p:Project {id: $centerId})-[r:CALLS_ENDPOINT|TRIGGERS]->(s:ExternalService) RETURN s.id AS id, s.name AS name, s.service_type AS service_type, r.kind AS kind";
            var svcJson = await client.ExecuteQueryAsync(svcQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
            using var svcDoc = JsonDocument.Parse(svcJson);

            foreach (var row in svcDoc.RootElement.EnumerateArray())
            {
                var id = row.GetStringProp("id");
                var name = row.GetStringProp("name", id);
                var st = row.TryGetProperty("service_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Service") : "Service";
                var rKind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "CALLS_ENDPOINT") : "CALLS_ENDPOINT";
                var kind = rKind == "TRIGGERS" ? "TRIGGERS" : "SERVICE_CALL";
                var depType = rKind == "TRIGGERS" ? "messaging" : "service_call";

                if (graph.Nodes.All(n => n.Id != id))
                {
                    var svcNode = new GraphNodeDto
                    {
                        Id = id,
                        Kind = "ExternalService",
                        Name = name,
                        DisplayName = $"{name} [{st}]",
                        Properties = new Dictionary<string, string>
                        {
                            ["column"] = "right",
                            ["role"] = "service",
                            ["service_type"] = st
                        }
                    };
                    graph.Nodes.Add(svcNode);
                }

                if (graph.Edges.All(e => !(e.Source == centerId && e.Target == id)))
                {
                    graph.Edges.Add(new GraphEdgeDto
                    {
                        Id = $"{centerId}->{id}:{kind}",
                        Source = centerId,
                        Target = id,
                        Kind = kind,
                        Properties = new Dictionary<string, string>
                        {
                            ["dependency_type"] = depType
                        }
                    });
                }
            }
        }
        catch { }

        ApplyLayerClassification(graph);

        // Normalize edge types after layer classification has established all project layers
        foreach (var edge in graph.Edges)
        {
            if (edge.Kind == "USES_DB" || edge.Kind == "TRIGGERS" || edge.Properties?.GetValueOrDefault("dependency_type") == "service_call" || edge.Properties?.GetValueOrDefault("dependency_type") == "database" || edge.Properties?.GetValueOrDefault("dependency_type") == "messaging") continue;

            var target = graph.Nodes.FirstOrDefault(n => n.Id == edge.Target);
            if (target != null && IsLibraryProject(target))
            {
                edge.Kind = "LIBRARY";
                edge.Properties ??= new Dictionary<string, string>();
                edge.Properties["dependency_type"] = "library";
                edge.Id = $"{edge.Source}->{edge.Target}:LIBRARY";
            }
        }

        graph.Metadata ??= new Dictionary<string, string>();
        graph.Metadata["graphType"] = "flow";

        return graph;
    }

    public static bool IsLibraryProject(GraphNodeDto? node)
    {
        if (node == null || !node.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase)) return false;
        var name = (node.Name ?? "").ToLowerInvariant();
        var path = (node.FilePath ?? "").Replace('\\', '/').ToLowerInvariant();
        var layerId = node.Properties?.GetValueOrDefault("layerId") ?? node.Properties?.GetValueOrDefault("layer");
        var projectType = node.Properties?.GetValueOrDefault("project_type")?.ToLowerInvariant();
        var framework = node.Properties?.GetValueOrDefault("framework");

        if (node.Properties?.GetValueOrDefault("is_library") == "true") return true;
        if (projectType == "library") return true;

        var normPath = "/" + path.Trim('/');

        // 1. Known explicit library/module directory paths
        if (normPath.Contains("/libs/") || normPath.Contains("/lib/") || normPath.Contains("/libraries/") ||
            normPath.Contains("/common/") || normPath.Contains("/shared/") || normPath.Contains("/contracts/") ||
            normPath.Contains("/dto/") || normPath.Contains("/dtos/") || normPath.Contains("/packages/"))
        {
            return true;
        }

        // 2. Known explicit library/module name patterns
        if (name == "library" || name.Contains("library") || name.EndsWith("-lib") || name.EndsWith(".lib") ||
            name.EndsWith(".core") || name.EndsWith("-core") ||
            name.EndsWith(".domain") || name.EndsWith("-domain") ||
            name.EndsWith(".models") || name.EndsWith("-models") ||
            name.EndsWith(".model") || name.EndsWith("-model") ||
            name.EndsWith(".entities") || name.EndsWith("-entities") ||
            name.EndsWith(".contracts") || name.EndsWith("-contracts") ||
            name.EndsWith(".dto") || name.EndsWith(".dtos") ||
            name.EndsWith(".types") || name.EndsWith("-types") ||
            name.EndsWith(".common") || name.EndsWith("-common") ||
            name.EndsWith(".shared") || name.EndsWith("-shared") ||
            name.EndsWith(".infra") || name.EndsWith(".infrastructure") ||
            name.EndsWith(".data") || name.EndsWith(".db"))
        {
            return true;
        }

        // 3. Service directories, service names, and service frameworks are DEFINITIVELY Services
        var isServicePath = normPath.Contains("/services/") ||
                            normPath.Contains("/apps/") ||
                            normPath.Contains("/microservices/") ||
                            normPath.Contains("/service/") ||
                            normPath.Contains("/app/");

        var isServiceName = name.EndsWith("-service") || name.EndsWith("_service") || name.EndsWith(".service") ||
                            name.EndsWith("-app") || name.EndsWith("_app") || name.EndsWith(".app") ||
                            name.EndsWith("-api") || name.EndsWith("_api") || name.EndsWith(".api") ||
                            name.Contains("gateway") || name.Contains("scheduler") || name.Contains("worker");

        var hasServiceFramework = !string.IsNullOrWhiteSpace(framework) &&
                                  !framework.Equals("Library", StringComparison.OrdinalIgnoreCase);

        if (isServicePath || isServiceName || hasServiceFramework || layerId == "layer_ingress")
        {
            return false;
        }

        if (layerId == "layer_foundation") return true;

        if (framework != null && framework.Equals("Library", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static GraphNodeDto? FindOwningProject(string sourceId, IEnumerable<GraphNodeDto> projects)
    {
        if (string.IsNullOrEmpty(sourceId)) return null;

        var projList = projects as IList<GraphNodeDto> ?? projects.ToList();

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

        // 3. File path resolution (e.g. workspace:file:action-scheduler/src/...)
        var filePath = sourceId;
        if (filePath.StartsWith("workspace:file:", StringComparison.OrdinalIgnoreCase))
        {
            filePath = filePath.Substring("workspace:file:".Length);
        }
        else if (filePath.StartsWith("workspace:symbol:", StringComparison.OrdinalIgnoreCase))
        {
            filePath = filePath.Substring("workspace:symbol:".Length);
        }

        var normFilePath = filePath.Replace('\\', '/').TrimStart('/');

        // Match against project FilePath (longest match first)
        GraphNodeDto? bestMatch = null;
        int bestLen = 0;
        foreach (var p in projList)
        {
            if (!string.IsNullOrEmpty(p.FilePath))
            {
                var normProj = p.FilePath.Replace('\\', '/').TrimStart('/').TrimEnd('/');
                if (!string.IsNullOrEmpty(normProj) &&
                    (normFilePath.StartsWith(normProj + "/", StringComparison.OrdinalIgnoreCase) ||
                     normFilePath.Equals(normProj, StringComparison.OrdinalIgnoreCase)))
                {
                    if (normProj.Length > bestLen)
                    {
                        bestLen = normProj.Length;
                        bestMatch = p;
                    }
                }
            }
        }
        if (bestMatch != null) return bestMatch;

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

        return null;
    }

    public static void LiftTransitiveSemanticRelations(GraphDataDto graph)
    {
        var nodesById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var services = graph.Nodes.Where(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase) && !IsLibraryProject(n)).ToList();
        var libraries = graph.Nodes.Where(n => n.Kind.Equals("Project", StringComparison.OrdinalIgnoreCase) && IsLibraryProject(n)).ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        // Build adjacency list for all outgoing edges
        var outEdges = new Dictionary<string, List<GraphEdgeDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            if (!outEdges.TryGetValue(edge.Source, out var list))
            {
                list = new List<GraphEdgeDto>();
                outEdges[edge.Source] = list;
            }
            list.Add(edge);
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
                if (layerMap.TryGetValue(node.Id, out var layer))
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

    public static (string CanonicalName, string CanonicalType, string CanonicalKey) CanonicalizeDatabase(string rawName, string? rawDbType)
    {
        var trimmed = (rawName ?? "").Trim();
        var lower = trimmed.ToLowerInvariant();
        var type = string.IsNullOrWhiteSpace(rawDbType) ? "relational" : rawDbType.Trim().ToLowerInvariant();

        switch (lower)
        {
            case "typeorm":
                return ("TypeORM", "relational", "typeorm");
            case "postgres":
            case "postgresql":
                return ("PostgreSQL", "relational", "postgresql");
            case "redis":
                return ("Redis", "cache", "redis");
            case "mysql":
                return ("MySQL", "relational", "mysql");
            case "mariadb":
                return ("MariaDB", "relational", "mariadb");
            case "sqlite":
            case "sqlite3":
                return ("SQLite", "relational", "sqlite");
            case "mongodb":
            case "mongo":
                return ("MongoDB", "document", "mongodb");
            case "clickhouse":
                return ("ClickHouse", "analytics", "clickhouse");
            case "bigquery":
                return ("BigQuery", "analytics", "bigquery");
            case "mssql":
            case "sqlserver":
            case "sql server":
                return ("SQL Server", "relational", "sqlserver");
            case "oracle":
                return ("Oracle", "relational", "oracle");
            case "cassandra":
                return ("Cassandra", "nosql", "cassandra");
            case "elasticsearch":
                return ("Elasticsearch", "search", "elasticsearch");
            case "neo4j":
                return ("Neo4j", "graph", "neo4j");
            case "sequelize":
                return ("Sequelize", "relational", "sequelize");
            case "prisma":
                return ("Prisma", "relational", "prisma");
            case "drizzle":
            case "drizzle orm":
                return ("Drizzle ORM", "relational", "drizzle");
            default:
                if (lower.StartsWith("redis_"))
                {
                    return ("Redis", "cache", "redis");
                }
                var canonicalKey = System.Text.RegularExpressions.Regex.Replace(lower, @"[^a-z0-9_-]", "_").Trim('_');
                if (string.IsNullOrEmpty(canonicalKey)) canonicalKey = "db";
                return (trimmed, type, canonicalKey);
        }
    }
}
