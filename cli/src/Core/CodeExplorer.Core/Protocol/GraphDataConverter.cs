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
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.framework AS framework, p.path AS path";
        var projJson = await client.ExecuteQueryAsync(projQuery, null, cancellationToken);
        using var projDoc = JsonDocument.Parse(projJson);

        foreach (var row in projDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

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

            nodeMap[id] = node;
            graph.Nodes.Add(node);
        }

        // 2. Databases
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type";
        var dbJson = await client.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        using var dbDoc = JsonDocument.Parse(dbJson);

        foreach (var row in dbDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
            var dbType = row.TryGetProperty("db_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Database") : "Database";

            var node = new GraphNodeDto
            {
                Id = id,
                Kind = "Database",
                Name = name,
                DisplayName = $"{name} [{dbType}]",
                Properties = new Dictionary<string, string> { ["db_type"] = dbType }
            };

            nodeMap[id] = node;
            graph.Nodes.Add(node);
        }

        // 3. External Services / Message Brokers
        var svcQuery = "MATCH (s:ExternalService) RETURN s.id AS id, s.name AS name, s.service_type AS service_type";
        var svcJson = await client.ExecuteQueryAsync(svcQuery, null, cancellationToken);
        using var svcDoc = JsonDocument.Parse(svcJson);

        foreach (var row in svcDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
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

        // 4. Project -> Project Dependencies
        var depQuery = "MATCH (p1:Project)-[r:DEPENDS_ON]->(p2:Project) RETURN p1.id AS source, p2.id AS target, r.kind AS kind";
        var depJson = await client.ExecuteQueryAsync(depQuery, null, cancellationToken);
        using var depDoc = JsonDocument.Parse(depJson);

        foreach (var row in depDoc.RootElement.EnumerateArray())
        {
            var src = row.GetProperty("source").GetString() ?? "";
            var tgt = row.GetProperty("target").GetString() ?? "";
            var kind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "DEPENDS_ON") : "DEPENDS_ON";

            if (nodeMap.ContainsKey(src) && nodeMap.ContainsKey(tgt))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{src}->{tgt}:{kind}",
                    Source = src,
                    Target = tgt,
                    Kind = kind
                });
            }
        }

        // 5. Project -> Database
        var usesDbQuery = "MATCH (p:Project)-[r:USES_DB]->(d:Database) RETURN p.id AS source, d.id AS target, r.kind AS kind";
        var usesDbJson = await client.ExecuteQueryAsync(usesDbQuery, null, cancellationToken);
        using var usesDbDoc = JsonDocument.Parse(usesDbJson);

        foreach (var row in usesDbDoc.RootElement.EnumerateArray())
        {
            var src = row.GetProperty("source").GetString() ?? "";
            var tgt = row.GetProperty("target").GetString() ?? "";
            var kind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "USES_DB") : "USES_DB";

            if (nodeMap.ContainsKey(src) && nodeMap.ContainsKey(tgt))
            {
                graph.Edges.Add(new GraphEdgeDto
                {
                    Id = $"{src}->{tgt}:{kind}",
                    Source = src,
                    Target = tgt,
                    Kind = kind
                });
            }
        }

        // 6. Layer Classification
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
            .Where(e => e.Kind.Equals("DEPENDS_ON", StringComparison.OrdinalIgnoreCase))
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
                    node.Properties["layerId"] = layer.LayerId;
                    node.Properties["layerName"] = layer.LayerName;
                    node.Properties["layerOrder"] = layer.Order.ToString();
                    node.Properties["layerColor"] = layer.Color;
                    node.Properties["layerIcon"] = layer.Icon;
                }
            }
            else if (node.Kind.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                node.Properties["layerId"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerId;
                node.Properties["layerName"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.LayerName;
                node.Properties["layerOrder"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Order.ToString();
                node.Properties["layerColor"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Color;
                node.Properties["layerIcon"] = CodeExplorer.Core.Analysis.StandardLayers.Foundation.Icon;
            }
        }

        graph.Metadata ??= new Dictionary<string, string>();
        graph.Metadata["layers"] = JsonSerializer.Serialize(CodeExplorer.Core.Analysis.StandardLayers.All);

        return graph;
    }

    public static async Task<List<string>> GetAllProjectsAsync(
        IGraphClient client,
        CancellationToken cancellationToken = default)
    {
        var query = "MATCH (p:Project) RETURN p.name AS name ORDER BY p.name";
        var json = await client.ExecuteQueryAsync(query, null, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var result = new List<string>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var name = row.GetProperty("name").GetString();
            if (!string.IsNullOrEmpty(name))
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

        if (allProjects.Count == 0)
        {
            return graph;
        }

        var targetName = string.IsNullOrWhiteSpace(projectName) ? allProjects[0] : projectName;

        // 1. Target Center Project
        var centerQuery = "MATCH (p:Project) WHERE p.name = $name OR p.id = $name RETURN p.id AS id, p.name AS name, p.framework AS framework, p.path AS path LIMIT 1";
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
        var centerId = centerRow.GetProperty("id").GetString() ?? "";
        var centerProjName = centerRow.GetProperty("name").GetString() ?? centerId;
        var centerFramework = centerRow.TryGetProperty("framework", out var cf) && cf.ValueKind == JsonValueKind.String ? cf.GetString() : null;
        var centerPath = centerRow.TryGetProperty("path", out var cp) && cp.ValueKind == JsonValueKind.String ? cp.GetString() : null;

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

        graph.Nodes.Add(centerNode);
        graph.Metadata["selectedProject"] = centerProjName;

        // 2. Inbound Project Dependencies (Left Column)
        var inQuery = "MATCH (in:Project)-[r:DEPENDS_ON]->(p:Project {id: $centerId}) RETURN in.id AS id, in.name AS name, in.framework AS framework, in.path AS path, r.kind AS kind";
        var inJson = await client.ExecuteQueryAsync(inQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
        using var inDoc = JsonDocument.Parse(inJson);

        foreach (var row in inDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            var kind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "DEPENDS_ON") : "DEPENDS_ON";

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
                graph.Nodes.Add(inNode);
            }

            graph.Edges.Add(new GraphEdgeDto
            {
                Id = $"{id}->{centerId}:{kind}",
                Source = id,
                Target = centerId,
                Kind = kind
            });
        }

        // 3. Outbound Project Dependencies (Right Column)
        var outQuery = "MATCH (p:Project {id: $centerId})-[r:DEPENDS_ON]->(out:Project) RETURN out.id AS id, out.name AS name, out.framework AS framework, out.path AS path, r.kind AS kind";
        var outJson = await client.ExecuteQueryAsync(outQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
        using var outDoc = JsonDocument.Parse(outJson);

        foreach (var row in outDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var path = row.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            var kind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "DEPENDS_ON") : "DEPENDS_ON";

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
                graph.Nodes.Add(outNode);
            }

            graph.Edges.Add(new GraphEdgeDto
            {
                Id = $"{centerId}->{id}:{kind}",
                Source = centerId,
                Target = id,
                Kind = kind
            });
        }

        // 4. Outbound Databases / Services (Right Column)
        var dbQuery = "MATCH (p:Project {id: $centerId})-[r:USES_DB]->(d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, r.kind AS kind";
        var dbJson = await client.ExecuteQueryAsync(dbQuery, new Dictionary<string, object> { ["centerId"] = centerId }, cancellationToken);
        using var dbDoc = JsonDocument.Parse(dbJson);

        foreach (var row in dbDoc.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString() ?? "";
            var name = row.GetProperty("name").GetString() ?? id;
            var dbType = row.TryGetProperty("db_type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "Database") : "Database";
            var kind = row.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "USES_DB") : "USES_DB";

            if (graph.Nodes.All(n => n.Id != id))
            {
                var dbNode = new GraphNodeDto
                {
                    Id = id,
                    Kind = "Database",
                    Name = name,
                    DisplayName = $"{name} [{dbType}]",
                    Properties = new Dictionary<string, string>
                    {
                        ["column"] = "right",
                        ["role"] = "database",
                        ["db_type"] = dbType
                    }
                };
                graph.Nodes.Add(dbNode);
            }

            graph.Edges.Add(new GraphEdgeDto
            {
                Id = $"{centerId}->{id}:{kind}",
                Source = centerId,
                Target = id,
                Kind = kind
            });
        }

        return graph;
    }
}
