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

public partial class ArchitectureViewEngine
{
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

                    if (!string.IsNullOrEmpty(id))
                    {
                        if (id.StartsWith(
                                $"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Project}:",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            path = id[
                                $"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Project}:"
                                    .Length..];
                        }
                        else if (id.StartsWith($"workspace:{OntologyConstants.IdPrefixes.Project}:",
                                     StringComparison.OrdinalIgnoreCase))
                        {
                            path = id[$"workspace:{OntologyConstants.IdPrefixes.Project}:".Length..];
                        }
                        else if (id.StartsWith("workspace:project:", StringComparison.OrdinalIgnoreCase))
                        {
                            path = id["workspace:project:".Length..];
                        }
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
        var asmVer = typeof(ArchitectureViewEngine).Assembly.GetName().Version;
        var verStr = asmVer != null ? $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}" : "";
        var result = new MetadataResponseDto
        {
            Version = verStr
        };

        if (db is SqliteGraphClient sqliteClient)
        {
            try
            {
                var (nodeCounts, relCounts, totalNodes, totalEdges) = await sqliteClient.ExecuteRawAsync(async conn =>
                {
                    var nodes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    long totalN = 0;
                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT kind, count(*) FROM nodes WHERE (kind != 'Topic' OR (coalesce(json_extract(properties, '$.is_internal'), 'false') != 'true' AND coalesce(json_extract(properties, '$.broker_type'), '') NOT IN ('mediatr', 'in-memory'))) GROUP BY kind";
                        await using var reader = await cmd.ExecuteReaderAsync(ct);
                        while (await reader.ReadAsync(ct))
                        {
                            var kind = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            var cnt = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
                            if (!string.IsNullOrEmpty(kind))
                            {
                                if (!Mcp.OntologyRegistry.IsSystemNode(kind))
                                {
                                    totalN += cnt;
                                    nodes[kind] = nodes.GetValueOrDefault(kind, 0) + cnt;
                                    if (kind.Equals("FrontendApp", StringComparison.OrdinalIgnoreCase))
                                    {
                                        nodes["App"] = nodes.GetValueOrDefault("App", 0) + cnt;
                                    }
                                    else if (kind.Equals("SharedLibrary", StringComparison.OrdinalIgnoreCase))
                                    {
                                        nodes["Library"] = nodes.GetValueOrDefault("Library", 0) + cnt;
                                    }
                                    else if (kind.Equals("Test", StringComparison.OrdinalIgnoreCase))
                                    {
                                        nodes["TestSuite"] = nodes.GetValueOrDefault("TestSuite", 0) + cnt;
                                    }
                                }
                            }
                            else
                            {
                                totalN += cnt;
                            }
                        }
                    }

                    var rels = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    long totalR = 0;
                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT kind, count(*) FROM edges GROUP BY kind";
                        await using var reader = await cmd.ExecuteReaderAsync(ct);
                        while (await reader.ReadAsync(ct))
                        {
                            var kind = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            var cnt = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
                            if (!string.IsNullOrEmpty(kind))
                            {
                                rels[kind] = cnt;
                                totalR += cnt;
                            }
                        }
                    }

                    return (nodes, rels, totalN, totalR);
                }, ct);

                result.NodeCounts = nodeCounts;
                result.RelationshipCounts = relCounts;
                result.TotalNodes = totalNodes;
                result.TotalEdges = totalEdges;

                if (result.NodeCounts.GetValueOrDefault("Service", 0) == 0 &&
                    result.NodeCounts.GetValueOrDefault("App", 0) == 0 &&
                    result.NodeCounts.GetValueOrDefault("Project", 0) > 0)
                {
                    try
                    {
                        var roleQuery =
                            "MATCH (p:Project) RETURN json_extract(p.properties, '$.role') AS role, json_extract(p.properties, '$.is_library') AS is_lib, count(p) AS cnt";
                        var roleJson = await db.ExecuteQueryAsync(roleQuery, null, ct);

                        using var roleDoc = JsonDocument.Parse(roleJson);

                        foreach (var row in roleDoc.RootElement.EnumerateArray())
                        {
                            var role = row.GetStringProp("role");
                            var isLib = row.GetStringProp("is_lib");

                            var cnt = row.TryGetProperty("cnt", out var c) && c.ValueKind == JsonValueKind.Number
                                ? c.GetInt64()
                                : 0;

                            if (role is "FrontendApp" or "App")
                            {
                                result.NodeCounts["App"] = result.NodeCounts.GetValueOrDefault("App", 0) + cnt;
                            }
                            else if (role is "Worker")
                            {
                                result.NodeCounts["Worker"] =
                                    result.NodeCounts.GetValueOrDefault("Worker", 0) + cnt;
                            }
                            else if (role is "SharedLibrary" or "Library" || isLib == "true")
                            {
                                result.NodeCounts["Library"] =
                                    result.NodeCounts.GetValueOrDefault("Library", 0) + cnt;
                            }
                            else if (role is "CliTool")
                            {
                                result.NodeCounts["CliTool"] =
                                    result.NodeCounts.GetValueOrDefault("CliTool", 0) + cnt;
                            }
                            else
                            {
                                result.NodeCounts["Service"] =
                                    result.NodeCounts.GetValueOrDefault("Service", 0) + cnt;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                foreach (var node in Mcp.OntologyRegistry.AllNodes)
                {
                    if (node.Attribute.IsSystemNode || Mcp.OntologyRegistry.IsSystemNode(node.Kind))
                    {
                        continue;
                    }

                    var count = result.NodeCounts.GetValueOrDefault(node.Kind, 0);
                    if (count > 0)
                    {
                        var layerId = node.Attribute.Layer switch
                        {
                            OntologyConstants.Layers.Physical => 1,
                            OntologyConstants.Layers.ProjectBoundary => 2,
                            OntologyConstants.Layers.Syntactic => 3,
                            OntologyConstants.Layers.Semantic => 4,
                            _ => 4
                        };
                        result.LayerCounts[layerId] = result.LayerCounts.GetValueOrDefault(layerId, 0) + count;
                    }
                }
                result.LayerCounts[5] = result.TotalEdges;

                return result;
            }
            catch
            {
                // Fallback to Cypher below if raw execution fails
            }
        }

        // 1. Node counts by primary label
        try
        {
            var nodeQuery = "MATCH (n) WHERE (labels(n)[0] <> 'Topic' OR (coalesce(n.is_internal, 'false') <> 'true' AND coalesce(n.broker_type, '') <> 'mediatr' AND coalesce(n.broker_type, '') <> 'in-memory')) RETURN labels(n)[0] AS lbl, count(n) AS cnt";
            var nodeJson = await db.ExecuteQueryAsync(nodeQuery, null, ct);

            using var nodeDoc = JsonDocument.Parse(nodeJson);

            long totalNodes = 0;

            foreach (var row in nodeDoc.RootElement.EnumerateArray())
            {
                var cnt = row.TryGetProperty("cnt", out var c) && c.ValueKind == JsonValueKind.Number
                    ? c.GetInt64()
                    : 0;

                string? lbl = null;

                if (row.TryGetProperty("lbl", out var lblProp))
                {
                    if (lblProp.ValueKind == JsonValueKind.String)
                    {
                        lbl = lblProp.GetString();
                    }
                    else if (lblProp.ValueKind == JsonValueKind.Array && lblProp.GetArrayLength() > 0)
                    {
                        lbl = lblProp[0].GetString();
                    }
                }

                if (!string.IsNullOrEmpty(lbl))
                {
                    if (Mcp.OntologyRegistry.IsSystemNode(lbl))
                    {
                        continue;
                    }

                    totalNodes += cnt;

                    result.NodeCounts[lbl] = result.NodeCounts.GetValueOrDefault(lbl, 0) + cnt;

                    if (lbl.Equals("FrontendApp", StringComparison.OrdinalIgnoreCase))
                    {
                        result.NodeCounts["App"] = result.NodeCounts.GetValueOrDefault("App", 0) + cnt;
                    }
                    else if (lbl.Equals("SharedLibrary", StringComparison.OrdinalIgnoreCase))
                    {
                        result.NodeCounts["Library"] = result.NodeCounts.GetValueOrDefault("Library", 0) + cnt;
                    }
                    else if (lbl.Equals("Test", StringComparison.OrdinalIgnoreCase))
                    {
                        result.NodeCounts["TestSuite"] = result.NodeCounts.GetValueOrDefault("TestSuite", 0) + cnt;
                    }
                }
                else
                {
                    totalNodes += cnt;
                }
            }

            result.TotalNodes = totalNodes;

            // 1b. Fallback for legacy graphs where projects were not yet decomposed into separate workload nodes
            if (result.NodeCounts.GetValueOrDefault("Service", 0) == 0 &&
                result.NodeCounts.GetValueOrDefault("App", 0) == 0 &&
                result.NodeCounts.GetValueOrDefault("Project", 0) > 0)
            {
                try
                {
                    var roleQuery =
                        "MATCH (p:Project) RETURN json_extract(p.properties, '$.role') AS role, json_extract(p.properties, '$.is_library') AS is_lib, count(p) AS cnt";
                    var roleJson = await db.ExecuteQueryAsync(roleQuery, null, ct);

                    using var roleDoc = JsonDocument.Parse(roleJson);

                    foreach (var row in roleDoc.RootElement.EnumerateArray())
                    {
                        var role = row.GetStringProp("role");
                        var isLib = row.GetStringProp("is_lib");

                        var cnt = row.TryGetProperty("cnt", out var c) && c.ValueKind == JsonValueKind.Number
                            ? c.GetInt64()
                            : 0;

                        if (role is "FrontendApp" or "App")
                        {
                            result.NodeCounts["App"] = result.NodeCounts.GetValueOrDefault("App", 0) + cnt;
                        }
                        else if (role is "Worker")
                        {
                            result.NodeCounts["Worker"] =
                                result.NodeCounts.GetValueOrDefault("Worker", 0) + cnt;
                        }
                        else if (role is "SharedLibrary" or "Library" || isLib == "true")
                        {
                            result.NodeCounts["Library"] =
                                result.NodeCounts.GetValueOrDefault("Library", 0) + cnt;
                        }
                        else if (role is "CliTool")
                        {
                            result.NodeCounts["CliTool"] =
                                result.NodeCounts.GetValueOrDefault("CliTool", 0) + cnt;
                        }
                        else
                        {
                            result.NodeCounts["Service"] =
                                result.NodeCounts.GetValueOrDefault("Service", 0) + cnt;
                        }
                    }
                }
                catch
                {
                }
            }
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

                var cnt = row.TryGetProperty("cnt", out var c) && c.ValueKind == JsonValueKind.Number
                    ? c.GetInt64()
                    : 0;

                if (!string.IsNullOrEmpty(rel))
                {
                    result.RelationshipCounts[rel] = cnt;
                    totalEdges += cnt;
                }
            }

            result.TotalEdges = totalEdges;
        }
        catch { }

        // 3. Dynamically compute LayerCounts from registered ontology node definitions
        foreach (var node in Mcp.OntologyRegistry.AllNodes)
        {
            if (node.Attribute.IsSystemNode || Mcp.OntologyRegistry.IsSystemNode(node.Kind))
            {
                continue;
            }
            var cnt = result.NodeCounts.GetValueOrDefault(node.Kind, 0);
            if (cnt <= 0) continue;
            var layerId = node.Attribute.Layer switch
            {
                OntologyConstants.Layers.Physical => 1,
                OntologyConstants.Layers.ProjectBoundary => 2,
                OntologyConstants.Layers.Syntactic => 3,
                OntologyConstants.Layers.Semantic => 4,
                _ => 4
            };
            result.LayerCounts[layerId] = result.LayerCounts.GetValueOrDefault(layerId, 0) + cnt;
        }
        result.LayerCounts[5] = result.TotalEdges;

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

        var safeKind = !string.IsNullOrWhiteSpace(kind) && Regex.IsMatch(kind, "^[A-Za-z0-9_]+$")
            ? kind
            : null;

        if (string.Equals(safeKind, "all", StringComparison.OrdinalIgnoreCase))
        {
            safeKind = null;
        }

        if (safeKind != null && Mcp.OntologyRegistry.IsSystemNode(safeKind))
        {
            return result;
        }

        string? serviceId = null;
        string? serviceNameResolved = null;
        if (!string.IsNullOrWhiteSpace(service))
        {
            try
            {
                var normSrv = WorkspaceConventions.NormalizeServiceName(service);
                var sQuery = "MATCH (s) WHERE (s.name = $srv OR s.name = $normSrv OR s.raw_name = $srv OR s.id = $srv OR s.id = 'ws:s:' + $srv OR s.id = 'ws:s:' + $normSrv OR s.id = 'ws:app:' + $srv OR s.id = 'ws:p:' + $srv OR s.id = 'workspace:p:' + $srv OR s.id = 'workspace:service:' + $srv OR s.id = 'workspace:app:' + $srv OR s.id = 'workspace:project:' + $srv) AND (s:Service OR s:App OR s:Worker OR s:CliTool OR s:TestSuite OR s:Project) RETURN s.id AS id, coalesce(s.name, s.id) AS name, labels(s)[0] AS kind ORDER BY CASE WHEN labels(s)[0] IN ['Service', 'App', 'Worker', 'CliTool', 'TestSuite'] THEN 0 ELSE 1 END LIMIT 1";
                var sJson = await db.ExecuteQueryAsync(sQuery, new Dictionary<string, object> { ["srv"] = service, ["normSrv"] = normSrv }, ct);

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

                if (firstRow.ValueKind == JsonValueKind.Object &&
                    firstRow.TryGetProperty("total", out var totProp) && totProp.ValueKind == JsonValueKind.Number)
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
                        Kind = EnumExtensions.ParseNodeKind(string.IsNullOrEmpty(relTypeVal) ? relType : relTypeVal),
                        FilePath = filePath,
                        LineStart = line,
                        Properties = new Dictionary<string, string>
                        {
                            ["source"] = srcName,
                            ["target"] = tgtName,
                            ["type"] = string.IsNullOrEmpty(relTypeVal)
                                ? (relType ?? "RELATIONSHIP")
                                : relTypeVal
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
            layerFilter = "WHERE (n:Project OR n:Package)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Layer3", StringComparison.OrdinalIgnoreCase))
        {
            layerFilter = "WHERE (n:Type OR n:Function OR n:Member)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Layer4", StringComparison.OrdinalIgnoreCase))
        {
            layerFilter = "WHERE (n:Service OR n:App OR n:Worker OR n:Library OR n:CliTool OR n:TestSuite OR n:Test OR n:EntryPoint OR n:Endpoint OR n:Procedure OR n:Database OR n:Table OR n:DataSet OR (n:Topic AND coalesce(n.is_internal, 'false') <> 'true' AND coalesce(n.broker_type, '') <> 'mediatr' AND coalesce(n.broker_type, '') <> 'in-memory') OR n:ExternalService OR n:CloudService OR n:ApiInUse OR n:Query)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "App", StringComparison.OrdinalIgnoreCase) && serviceId == null)
        {
            layerFilter = "WHERE (n:App OR n:FrontendApp)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Worker", StringComparison.OrdinalIgnoreCase) && serviceId == null)
        {
            layerFilter = "WHERE (n:Worker)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Library", StringComparison.OrdinalIgnoreCase) && serviceId == null)
        {
            layerFilter = "WHERE (n:Library OR n:SharedLibrary)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "CliTool", StringComparison.OrdinalIgnoreCase) && serviceId == null)
        {
            layerFilter = "WHERE (n:CliTool)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Service", StringComparison.OrdinalIgnoreCase) && serviceId == null)
        {
            layerFilter = "WHERE (n:Service)";
            safeKind = null;
        }
        else if (string.Equals(safeKind, "Topic", StringComparison.OrdinalIgnoreCase) && serviceId == null)
        {
            layerFilter = "WHERE (coalesce(n.is_internal, 'false') <> 'true' AND coalesce(n.broker_type, '') <> 'mediatr' AND coalesce(n.broker_type, '') <> 'in-memory')";
        }
        else if ((string.Equals(safeKind, "TestSuite", StringComparison.OrdinalIgnoreCase) || string.Equals(safeKind, "Test", StringComparison.OrdinalIgnoreCase)) && serviceId == null)
        {
            layerFilter = "WHERE (n:TestSuite OR n:Test)";
            safeKind = null;
        }

        if (safeKind == null && layerFilter == null && serviceId == null)
        {
            // Default macro filter for unconstrained / 'all' queries (prevents pulling thousands of raw AST symbols)
            layerFilter = "WHERE (n:Service OR n:App OR n:Worker OR n:Library OR n:CliTool OR n:TestSuite OR n:Test OR n:EntryPoint OR n:Endpoint OR n:Database OR n:Table OR (n:Topic AND coalesce(n.is_internal, 'false') <> 'true' AND coalesce(n.broker_type, '') <> 'mediatr' AND coalesce(n.broker_type, '') <> 'in-memory') OR n:ExternalService OR n:CloudService)";
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
                matchClause = $"MATCH (s)-[:CONTAINS|EXPOSED_BY]-(n:Endpoint) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}')";
            }
            else if (string.Equals(safeKind, "Database", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (s)-[:USES_DB|CONTAINS]-(n:Database) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}')";
            }
            else if (string.Equals(safeKind, "Topic", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (s)-[:PUBLISHES_TO|SUBSCRIBED_BY|SUBSCRIBES_TO|TRIGGERS]-(n:Topic) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}') AND (coalesce(n.is_internal, 'false') <> 'true' AND coalesce(n.broker_type, '') <> 'mediatr' AND coalesce(n.broker_type, '') <> 'in-memory')";
            }
            else if (string.Equals(safeKind, "ExternalService", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (s)-[:CALLS_ENDPOINT|SERVICE_CALL|INTEGRATES_WITH]->(n) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}') AND (n:ExternalService OR n:CloudService)";
            }
            else if (string.Equals(safeKind, "Service", StringComparison.OrdinalIgnoreCase) || string.Equals(safeKind, "App", StringComparison.OrdinalIgnoreCase))
            {
                matchClause = $"MATCH (n) WHERE (n.id = '{sIdEsc}' OR n.name = '{sNameEsc}')";
            }
            else
            {
                matchClause = $"MATCH (s)-[r]-(n) WHERE (s.id = '{sIdEsc}' OR s.name = '{sNameEsc}') AND (n:Endpoint OR n:Database OR n:Topic OR n:ExternalService OR n:CloudService OR n:File OR n:Type)";
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

            if (firstRow.ValueKind == JsonValueKind.Object && firstRow.TryGetProperty("total", out var totProp) &&
                totProp.ValueKind == JsonValueKind.Number)
            {
                result.Total = totProp.GetInt64();
            }
        }
        catch { }

        var dataQuery = $"{matchClause} {whereClause}RETURN DISTINCT n.id AS id, n.name AS name, n.file_path AS file_path, n.path AS path, n.line AS line, labels(n) AS lbl, n.framework AS framework, n.method AS method, n.route AS route, n.protocol AS protocol, json_extract(n.properties, '$.role') AS role ORDER BY n.name ASC SKIP {offset} LIMIT {limit}";


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
                    else if (lp.ValueKind == JsonValueKind.String &&
                             int.TryParse(lp.GetString(), out var parsedLine))
                    {
                        line = parsedLine;
                    }
                }

                var role = row.GetStringProp("role");
                string nodeKind = safeKind ?? "";

                if (string.IsNullOrEmpty(nodeKind) && row.TryGetProperty("lbl", out var lblProp) &&
                    lblProp.ValueKind == JsonValueKind.Array)
                {
                    nodeKind = lblProp.EnumerateArray().FirstOrDefault().GetString() ?? "";
                }

                if (Mcp.OntologyRegistry.IsSystemNode(nodeKind))
                {
                    continue;
                }

                if (nodeKind == "Project" && !string.IsNullOrEmpty(role) &&
                    !string.Equals(kind, "Project", StringComparison.OrdinalIgnoreCase))
                {
                    nodeKind = role is "FrontendApp" ? "App" : role is "SharedLibrary" ? "Library" : role;
                }

                var nodeDto = new GraphNodeDto
                {
                    Id = id,
                    Name = name,
                    Kind = EnumExtensions.ParseNodeKind(nodeKind),
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

    public async Task<NodeUsagesResponseDto> GetNodeUsagesAsync(string nodeId, CancellationToken ct = default)
    {
        var response = new NodeUsagesResponseDto { TargetId = nodeId };
        if (string.IsNullOrWhiteSpace(nodeId)) return response;

        // 1. Resolve target node details (Name, Kind, discovery file)
        var targetQuery = "MATCH (t) WHERE t.id = $id OR t.name = $id RETURN t.id AS id, t.name AS name, labels(t)[0] AS kind, coalesce(t.file_path, t.path) AS file_path, t.line AS line LIMIT 1";
        string? targetName = null;
        string? targetKind = null;
        string? declFile = null;
        int? declLine = null;

        try
        {
            var tJson = await db.ExecuteQueryAsync(targetQuery, new Dictionary<string, object> { ["id"] = nodeId }, ct);

            using var tDoc = JsonDocument.Parse(tJson);

            var first = tDoc.RootElement.EnumerateArray().FirstOrDefault();

            if (first.ValueKind == JsonValueKind.Object)
            {
                response.TargetId = first.GetStringProp("id", nodeId);
                response.TargetName = first.GetStringProp("name", nodeId);
                response.TargetKind = first.GetStringProp("kind", "");
                targetName = response.TargetName;
                targetKind = response.TargetKind;
                declFile = first.GetStringProp("file_path");

                if (first.TryGetProperty("line", out var lp) && lp.ValueKind == JsonValueKind.Number)
                {
                    declLine = lp.GetInt32();
                }
            }
        }
        catch { }

        response.TargetName = targetName ?? nodeId;
        response.TargetKind = targetKind ?? "";

        // 2. Query all relationships connected to target (incoming and outgoing)
        // Exclude system/structural nodes like SemanticStructure or Workspace
        var usagesQuery = """
            MATCH (src)-[r]-(tgt)
            WHERE (tgt.id = $id OR tgt.name = $name)
              AND NOT (src:SemanticStructure OR src:Workspace OR src:Layer1 OR src:Layer2 OR src:Layer3 OR src:Layer4)
            RETURN DISTINCT
              src.id AS src_id,
              src.name AS src_name,
              labels(src)[0] AS src_kind,
              r.kind AS rel_kind,
              coalesce(src.file_path, src.path) AS file_path,
              src.line AS line,
              json_extract(src.properties, '$.service') AS service,
              json_extract(src.properties, '$.project_id') AS project_id
            """;

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var paramsDict = new Dictionary<string, object>
            {
                ["id"] = response.TargetId,
                ["name"] = response.TargetName
            };
            var uJson = await db.ExecuteQueryAsync(usagesQuery, paramsDict, ct);

            using var uDoc = JsonDocument.Parse(uJson);

            foreach (var row in uDoc.RootElement.EnumerateArray())
            {
                var srcId = row.GetStringProp("src_id");
                if (string.IsNullOrEmpty(srcId) || srcId == response.TargetId) continue;

                var srcName = row.GetStringProp("src_name", srcId);
                var srcKind = row.GetStringProp("src_kind", "");
                var relKind = row.GetStringProp("rel_kind", "USES");
                var filePath = row.GetStringProp("file_path");
                var service = row.GetStringProp("service");

                if (string.IsNullOrEmpty(service))
                {
                    service = row.GetStringProp("project_id");
                }

                int? line = null;

                if (row.TryGetProperty("line", out var lp))
                {
                    if (lp.ValueKind == JsonValueKind.Number) line = lp.GetInt32();
                    else if (lp.ValueKind == JsonValueKind.String && int.TryParse(lp.GetString(), out var pl))
                        line = pl;
                }

                var key = $"{srcId}:{relKind}:{filePath}:{line}";
                if (!seenKeys.Add(key)) continue;

                response.Usages.Add(new NodeUsageDto
                {
                    SourceId = srcId,
                    SourceName = srcName,
                    SourceKind = srcKind,
                    Relationship = relKind,
                    FilePath = string.IsNullOrEmpty(filePath) ? null : filePath,
                    LineStart = line,
                    ServiceName = string.IsNullOrEmpty(service) ? null : service
                });
            }
        }
        catch { }

        // Also if the target node itself has a declaration/discovery file, include it
        if (!string.IsNullOrEmpty(declFile))
        {
            var key = $"decl:{declFile}:{declLine}";
            if (seenKeys.Add(key))
            {
                response.Usages.Insert(0, new NodeUsageDto
                {
                    SourceId = response.TargetId,
                    SourceName = Path.GetFileName(declFile),
                    SourceKind = "Config",
                    Relationship = "DECLARED_IN",
                    FilePath = declFile,
                    LineStart = declLine
                });
            }
        }

        return response;
    }

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
                l2Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 2 });
            }
            else if (attr.Layer == OntologyConstants.Layers.Syntactic)
            {
                l3Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 3 });
            }
            else if (attr.Layer == OntologyConstants.Layers.Semantic)
            {
                if (kind is "DataSet" || (kind is "Procedure" or "Query" or "EntryPoint" or "ApiInUse" && count == 0))
                {
                    continue;
                }
                l4Categories.Add(new() { Kind = kind, Label = attr.PluralLabel, Icon = attr.Icon, Count = count, LayerId = 4 });
            }
        }

        var topRels = new[] { "CALLS", "DEPENDS_ON", "DEPLOYS", "EXPOSED_BY", "TRIGGERS", "QUERIED_BY", "PUBLISHED_BY", "SUBSCRIBED_BY", "INTEGRATES_WITH", "USES_DB", "IMPLEMENTS", "INHERITS_FROM", "USES_TYPE" };
        var l5Categories = new List<OntologyCategoryDto>();
        foreach (var rel in topRels)
        {
            var cnt = relCounts.GetValueOrDefault(rel, 0);
            if (cnt > 0 || rel is "CALLS" or "DEPENDS_ON" or "INTEGRATES_WITH" or "USES_DB" or "DEPLOYS")
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
            TotalNodes = l1Categories.Sum(c => c.Count) + l2Categories.Sum(c => c.Count) + l3Categories.Sum(c => c.Count) + l4Categories.Sum(c => c.Count),
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
                            else if (tgtName.StartsWith("environment.", StringComparison.OrdinalIgnoreCase) ||
                                     tgtName.StartsWith("env.", StringComparison.OrdinalIgnoreCase))
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
                        else if (tgtName.StartsWith("environment.", StringComparison.OrdinalIgnoreCase) ||
                                 tgtName.StartsWith("env.", StringComparison.OrdinalIgnoreCase))
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
                                  clean.StartsWith(k, StringComparison.OrdinalIgnoreCase) ||
                                  k.StartsWith(clean, StringComparison.OrdinalIgnoreCase)))
            {
                return v;
            }
        }
        return null;
    }
}
