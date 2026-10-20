using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
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

    public async Task<DatabaseEvidenceResponseDto> GetDatabaseEvidenceAsync(
        string databaseId,
        string? serviceId = null,
        CancellationToken ct = default)
    {
        var response = new DatabaseEvidenceResponseDto
        {
            DatabaseId = databaseId,
            ServiceId = serviceId
        };
        if (string.IsNullOrWhiteSpace(databaseId)) return response;

        // 1. Resolve target Database node details
        var dbQuery = """
            MATCH (d:Database)
            WHERE d.id = $id OR d.name = $id OR d.id ENDS WITH (':' + $id)
            RETURN d.id AS id, d.name AS name, d.engine AS engine, d.schema AS schema, d.db_type AS db_type, coalesce(d.file_path, d.path) AS path
            LIMIT 1
            """;
        
        string dbName = databaseId;
        string dbEngine = "";
        string dbSchema = "";
        string? dbDeclPath = null;
        string resolvedDbId = databaseId;

        try
        {
            var dbJson = await db.ExecuteQueryAsync(dbQuery, new Dictionary<string, object> { ["id"] = databaseId }, ct);
            using var dbDoc = JsonDocument.Parse(dbJson);
            var firstDb = dbDoc.RootElement.EnumerateArray().FirstOrDefault();
            if (firstDb.ValueKind == JsonValueKind.Object)
            {
                resolvedDbId = firstDb.GetStringProp("id", databaseId);
                dbName = firstDb.GetStringProp("name", databaseId);
                dbEngine = firstDb.GetStringProp("engine", "");
                dbSchema = firstDb.GetStringProp("schema", "");
                dbDeclPath = firstDb.GetStringProp("path", "");
            }
        }
        catch { }

        if (string.IsNullOrEmpty(dbName))
        {
            dbName = databaseId;
        }
        if (string.IsNullOrEmpty(dbSchema) && dbName.Contains('.'))
        {
            dbSchema = dbName.Split('.', 2)[1];
        }
        if (string.IsNullOrEmpty(dbEngine))
        {
            dbEngine = dbName.Contains('.') ? dbName.Split('.', 2)[0] : "Database";
        }

        response.DatabaseId = resolvedDbId ?? "";
        response.DatabaseName = dbName ?? databaseId;
        response.Engine = dbEngine ?? "Database";
        response.Schema = dbSchema ?? "";

        // 2. Resolve Service/Project scope if specified
        string? servicePath = null;
        string? serviceName = null;
        if (!string.IsNullOrWhiteSpace(serviceId))
        {
            try
            {
                var sQuery = """
                    MATCH (s)
                    WHERE (s:Project OR s:Service OR s:Worker OR s:App)
                      AND (s.id = $sId OR s.name = $sId OR s.id ENDS WITH (':' + $sId))
                    RETURN s.id AS id, s.name AS name, coalesce(s.file_path, s.path) AS path
                    LIMIT 1
                    """;
                var sJson = await db.ExecuteQueryAsync(sQuery, new Dictionary<string, object> { ["sId"] = serviceId }, ct);
                using var sDoc = JsonDocument.Parse(sJson);
                var firstS = sDoc.RootElement.EnumerateArray().FirstOrDefault();
                if (firstS.ValueKind == JsonValueKind.Object)
                {
                    serviceName = firstS.GetStringProp("name", serviceId);
                    servicePath = firstS.GetStringProp("path");
                }
            }
            catch { }
            if (string.IsNullOrEmpty(serviceName)) serviceName = serviceId;
        }

        var items = new List<DatabaseEvidenceItemDto>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Helper to check if file/evidence belongs to requested service
        bool MatchesService(string? filePath, string? itemService)
        {
            if (string.IsNullOrWhiteSpace(serviceId)) return true;
            if (!string.IsNullOrEmpty(itemService) && (itemService.Equals(serviceName, StringComparison.OrdinalIgnoreCase) || itemService.Equals(serviceId, StringComparison.OrdinalIgnoreCase)))
                return true;
            if (!string.IsNullOrEmpty(filePath))
            {
                if (!string.IsNullOrEmpty(servicePath) && (filePath.StartWithIC(servicePath) || filePath.Contains(servicePath, StringComparison.OrdinalIgnoreCase)))
                    return true;
                if (!string.IsNullOrEmpty(serviceName) && filePath.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // 3. Query Nodes (:Query)
        try
        {
            var qQuery = """
                MATCH (q:Query)
                RETURN q.id AS id, q.name AS name, coalesce(q.file_path, q.path) AS path,
                       q.line AS line, q.line_end AS line_end,
                       json_extract(q.properties, '$.query_text') AS query_text,
                       json_extract(q.properties, '$.verb') AS verb,
                       json_extract(q.properties, '$.schema') AS schema,
                       json_extract(q.properties, '$.service') AS service,
                       json_extract(q.properties, '$.caller') AS caller
                """;
            var qJson = await db.ExecuteQueryAsync(qQuery, null, ct);
            using var qDoc = JsonDocument.Parse(qJson);

            // Also find callers from (fn:Function)-[:DECLARES]->(q:Query)
            var fnCallersQuery = """
                MATCH (fn:Function)-[:DECLARES]->(q:Query)
                RETURN q.id AS q_id, fn.name AS fn_name
                """;
            var fnCallers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var fnJson = await db.ExecuteQueryAsync(fnCallersQuery, null, ct);
                using var fnDoc = JsonDocument.Parse(fnJson);
                foreach (var row in fnDoc.RootElement.EnumerateArray())
                {
                    var qId = row.GetStringProp("q_id");
                    var fnName = row.GetStringProp("fn_name");
                    if (!string.IsNullOrEmpty(qId) && !string.IsNullOrEmpty(fnName) && !fnCallers.ContainsKey(qId))
                    {
                        fnCallers[qId] = fnName;
                    }
                }
            }
            catch { }

            foreach (var row in qDoc.RootElement.EnumerateArray())
            {
                var qId = row.GetStringProp("id");
                var qName = row.GetStringProp("name");
                var qPath = row.GetStringProp("path");
                var qText = row.GetStringProp("query_text");
                var qVerb = row.GetStringProp("verb");
                var qSchema = row.GetStringProp("schema");
                var qService = row.GetStringProp("service");
                var qCaller = row.GetStringProp("caller");
                if (string.IsNullOrEmpty(qCaller) && !string.IsNullOrEmpty(qId) && fnCallers.TryGetValue(qId, out var fName))
                {
                    qCaller = fName;
                }

                int lineStart = 0;
                int lineEnd = 0;
                if (row.TryGetProperty("line", out var lp) && lp.ValueKind == JsonValueKind.Number)
                    lineStart = lp.GetInt32();
                if (row.TryGetProperty("line_end", out var lep) && lep.ValueKind == JsonValueKind.Number)
                    lineEnd = lep.GetInt32();
                if (lineEnd <= 0) lineEnd = lineStart;

                // Match query to database:
                bool queryMatchesDb = false;
                if (!string.IsNullOrEmpty(dbSchema) && !dbSchema.Equals("public", StringComparison.OrdinalIgnoreCase) && !dbSchema.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(qSchema) && qSchema.Equals(dbSchema, StringComparison.OrdinalIgnoreCase))
                        queryMatchesDb = true;
                    else if (!string.IsNullOrEmpty(qText) && (qText.Contains(dbSchema + ".", StringComparison.OrdinalIgnoreCase) || qText.Contains(dbSchema + "\"", StringComparison.OrdinalIgnoreCase) || qText.Contains("\"" + dbSchema + "\"", StringComparison.OrdinalIgnoreCase)))
                        queryMatchesDb = true;
                }
                else if (!string.IsNullOrEmpty(dbEngine) && !dbEngine.Equals("Database", StringComparison.OrdinalIgnoreCase))
                {
                    if (qName.Contains(dbEngine, StringComparison.OrdinalIgnoreCase) || qId.Contains(dbEngine, StringComparison.OrdinalIgnoreCase))
                        queryMatchesDb = true;
                }

                if (!queryMatchesDb) continue;
                if (!MatchesService(qPath, qService)) continue;

                var verb = !string.IsNullOrWhiteSpace(qVerb) ? qVerb.ToUpperInvariant() : "SQL";
                if (verb == "SQL" && !string.IsNullOrWhiteSpace(qText))
                {
                    var firstWord = qText.TrimStart().Split([' ', '\r', '\n', '\t'], 2)[0].ToUpperInvariant();
                    if (firstWord is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "CREATE" or "DROP" or "ALTER" or "EXEC" or "EXECUTE")
                    {
                        verb = firstWord;
                    }
                }
                else if (verb == "SQL" && qName.StartWithIC("SELECT")) verb = "SELECT";
                else if (verb == "SQL" && qName.StartWithIC("INSERT")) verb = "INSERT";
                else if (verb == "SQL" && qName.StartWithIC("UPDATE")) verb = "UPDATE";
                else if (verb == "SQL" && qName.StartWithIC("DELETE")) verb = "DELETE";

                var snippet = !string.IsNullOrWhiteSpace(qText) ? qText.Trim() : qName;
                var title = $"{verb} {(!string.IsNullOrWhiteSpace(qCaller) ? $"in {qCaller}" : Path.GetFileName(qPath))}";

                var key = $"query:{qPath}:{lineStart}:{snippet}";
                if (seenKeys.Add(key))
                {
                    items.Add(new DatabaseEvidenceItemDto
                    {
                        Id = qId ?? "",
                        Kind = "query",
                        Title = title,
                        Operation = verb,
                        FilePath = qPath ?? "",
                        LineStart = lineStart,
                        LineEnd = lineEnd,
                        CodeSnippet = snippet,
                        CallerSymbol = qCaller,
                        ServiceName = qService ?? serviceName,
                        Reason = $"SQL {verb} statement directly accessing schema '{dbSchema}'"
                    });
                }
            }
        }
        catch { }

        // 4. Query connected Files, Entities, Configs, Migrations via relationships targeting Database
        try
        {
            var relsQuery = """
                MATCH (src)-[r]->(tgt)
                WHERE (tgt.id = $id OR tgt.name = $name OR tgt.id = $rawId)
                  AND r.kind IN ['USES_DB', 'CONFIGURES', 'READS_FROM', 'WRITES_TO']
                  AND NOT (src:Project OR src:Service OR src:Workspace OR src:SemanticStructure)
                RETURN src.id AS id, src.name AS name, labels(src)[0] AS kind,
                       coalesce(src.file_path, src.path) AS path, src.line AS line,
                       r.kind AS rel_kind,
                       json_extract(src.properties, '$.service') AS service
                """;
            var paramsDict = new Dictionary<string, object>
            {
                ["id"] = resolvedDbId ?? databaseId,
                ["name"] = dbName ?? databaseId,
                ["rawId"] = databaseId
            };
            var relsJson = await db.ExecuteQueryAsync(relsQuery, paramsDict, ct);
            using var relsDoc = JsonDocument.Parse(relsJson);

            foreach (var row in relsDoc.RootElement.EnumerateArray())
            {
                var srcId = row.GetStringProp("id");
                var srcName = row.GetStringProp("name");
                var srcKind = row.GetStringProp("kind");
                var srcPath = row.GetStringProp("path");
                var relKind = row.GetStringProp("rel_kind");
                var srcService = row.GetStringProp("service");

                int line = 0;
                if (row.TryGetProperty("line", out var lp) && lp.ValueKind == JsonValueKind.Number)
                    line = lp.GetInt32();

                if (!MatchesService(srcPath, srcService)) continue;
                if (string.IsNullOrEmpty(srcPath)) continue;

                var isMigration = srcPath.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) ||
                                  srcPath.Contains("\\Migrations\\", StringComparison.OrdinalIgnoreCase) ||
                                  srcPath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
                var isConfig = srcPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                               srcPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                               srcPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                               srcPath.EndsWith(".config", StringComparison.OrdinalIgnoreCase);
                var isOrm = !isMigration && !isConfig && (
                    srcPath.Contains("Context", StringComparison.OrdinalIgnoreCase) ||
                    srcPath.Contains("Entity", StringComparison.OrdinalIgnoreCase) ||
                    srcPath.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
                    srcPath.Contains("Repository", StringComparison.OrdinalIgnoreCase) ||
                    srcPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    srcPath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase));

                string itemKind = isMigration ? "migration" : (isConfig ? "config" : (isOrm ? "orm" : "table"));
                string operation = isMigration ? "MIGRATION" : (isConfig ? "CONFIG" : (isOrm ? "ORM" : "TABLE"));
                string reason = isMigration ? "Database migration script / snapshot defining tables"
                              : (isConfig ? "Configuration / connection string declaring database"
                              : (isOrm ? "ORM entity / DbContext mapping accessing database" : "Table reference"));

                var key = $"{itemKind}:{srcPath}:{line}";
                if (seenKeys.Add(key))
                {
                    items.Add(new DatabaseEvidenceItemDto
                    {
                        Id = srcId,
                        Kind = itemKind,
                        Title = $"{srcName} ({operation})",
                        Operation = operation,
                        FilePath = srcPath,
                        LineStart = line > 0 ? line : 1,
                        LineEnd = line > 0 ? line : 1,
                        ServiceName = srcService ?? serviceName,
                        Reason = reason
                    });
                }
            }
        }
        catch { }

        // Also if target node has a declaration file and not yet added, add it
        if (!string.IsNullOrEmpty(dbDeclPath) && MatchesService(dbDeclPath, serviceName))
        {
            var key = $"config:{dbDeclPath}:1";
            if (seenKeys.Add(key))
            {
                items.Add(new DatabaseEvidenceItemDto
                {
                    Id = resolvedDbId ?? databaseId,
                    Kind = "config",
                    Title = $"{Path.GetFileName(dbDeclPath)} (CONFIG)",
                    Operation = "CONFIG",
                    FilePath = dbDeclPath,
                    LineStart = 1,
                    LineEnd = 1,
                    ServiceName = serviceName,
                    Reason = $"Database declaration configuration ({dbEngine})"
                });
            }
        }

        // Sort items: queries first, then orm/tables, then config, then migrations
        response.Items = items
            .OrderBy(i => i.Kind switch
            {
                "query" => 0,
                "orm" => 1,
                "table" => 2,
                "config" => 3,
                "migration" => 4,
                _ => 5
            })
            .ThenBy(i => i.FilePath)
            .ThenBy(i => i.LineStart)
            .ToList();

        response.TotalCount = response.Items.Count;
        return response;
    }

}
