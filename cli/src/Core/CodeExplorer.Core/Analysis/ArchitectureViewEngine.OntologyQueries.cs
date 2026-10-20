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

}
