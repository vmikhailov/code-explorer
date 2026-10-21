using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Protocol;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private static string? InferSqlVerb(string? verbProp, string? queryText, string? name = null)
    {
        if (!string.IsNullOrWhiteSpace(verbProp) && !verbProp.Equals("SQL", StringComparison.OrdinalIgnoreCase))
        {
            return verbProp.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            var firstWord = queryText.TrimStart().Split([' ', '\r', '\n', '\t', '('], 2)[0].ToUpperInvariant();
            if (firstWord is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "MERGE" or "CREATE" or "DROP" or "ALTER" or "EXEC" or "EXECUTE")
            {
                return firstWord == "EXECUTE" ? "EXEC" : firstWord;
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            foreach (var candidate in new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "EXEC" })
            {
                if (name.StartWithIC(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static int? ParseLineProp(IReadOnlyDictionary<string, string> relProps, string propKey, JsonElement row, string rowKey)
    {
        if (relProps.TryGetValue(propKey, out var raw) && int.TryParse(raw, out var parsed) && parsed > 0)
        {
            return parsed;
        }
        var fromRow = row.GetNullableIntProp(rowKey);
        return fromRow is > 0 ? fromRow : null;
    }

    private static bool QueryTextReferencesSchema(string? queryText, string schema)
    {
        if (string.IsNullOrWhiteSpace(queryText) || string.IsNullOrWhiteSpace(schema)) return false;
        var pattern = $@"(?<![a-zA-Z0-9_])(?:""{Regex.Escape(schema)}""|\[{Regex.Escape(schema)}\]|`{Regex.Escape(schema)}`|\b{Regex.Escape(schema)})\s*\.\s*(?:""?[a-zA-Z0-9_]|\[|`|[a-zA-Z0-9_])";
        return Regex.IsMatch(queryText, pattern, RegexOptions.IgnoreCase);
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

    public Task<DatabaseEvidenceResponseDto> GetDatabaseEvidenceAsync(
        string databaseId,
        string? serviceId = null,
        CancellationToken ct = default) =>
        GetDatabaseEvidenceCoreAsync(databaseId, serviceId, ct, null);

    internal async Task<DatabaseEvidenceResponseDto> GetDatabaseEvidenceCoreAsync(
        string databaseId,
        string? serviceId = null,
        CancellationToken ct = default,
        Dictionary<string, EvidenceScope?>? scopeCache = null)
    {
        var response = new DatabaseEvidenceResponseDto
        {
            DatabaseId = databaseId,
            ServiceId = serviceId
        };
        if (string.IsNullOrWhiteSpace(databaseId)) return response;
        var warnings = new List<string>();

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
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Resolve database '{databaseId}'", ex);
        }

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
        var scope = string.IsNullOrWhiteSpace(serviceId)
            ? null
            : await ResolveServiceScopeAsync(serviceId, ct, scopeCache, warnings);
        string? serviceName = scope?.Name;

        var items = new List<ArchitectureEvidenceItemDto>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool MatchesService(string? filePath, string? itemService, string? nodeId = null) =>
            scope is null || scope.Matches(filePath, itemService, nodeId);

        // 3. Query Nodes (:Query)
        try
        {
            var qQuery = """
                MATCH (q:Query)
                RETURN q.id AS id, q.name AS name, coalesce(q.file_path, q.path) AS path,
                       coalesce(q.start_line, q.line) AS line, coalesce(q.end_line, q.line_end) AS line_end,
                       json_extract(q.properties, '$.query_text') AS query_text,
                       json_extract(q.properties, '$.verb') AS verb,
                       json_extract(q.properties, '$.operation') AS operation,
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
            catch (Exception ex)
            {
                RecordEvidenceWarning(warnings, "Query function callers lookup", ex);
            }

            foreach (var row in qDoc.RootElement.EnumerateArray())
            {
                var qId = row.GetStringProp("id");
                var qName = row.GetStringProp("name");
                var qPath = row.GetStringProp("path");
                var qText = row.GetStringProp("query_text");
                var qVerb = row.GetStringProp("operation") is { Length: > 0 } opProp ? opProp : row.GetStringProp("verb");
                var qSchema = row.GetStringProp("schema");
                var qService = row.GetStringProp("service");
                var qCaller = row.GetStringProp("caller");
                if (string.IsNullOrEmpty(qCaller) && !string.IsNullOrEmpty(qId) && fnCallers.TryGetValue(qId, out var fName))
                {
                    qCaller = fName;
                }

                int? lineStart = row.GetNullableIntProp("line");
                int? lineEnd = row.GetNullableIntProp("line_end");
                if (lineStart <= 0) lineStart = null;
                if (lineEnd <= 0) lineEnd = lineStart;

                // Match query to database strictly on schema identifier boundaries:
                bool queryMatchesDb = false;
                var confidence = OntologyConstants.EvidenceConfidence.Inferred;
                if (!string.IsNullOrEmpty(dbSchema) && !dbSchema.Equals("public", StringComparison.OrdinalIgnoreCase) && !dbSchema.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(qSchema))
                    {
                        if (qSchema.Equals(dbSchema, StringComparison.OrdinalIgnoreCase))
                        {
                            queryMatchesDb = true;
                            confidence = OntologyConstants.EvidenceConfidence.Recorded;
                        }
                    }
                    else if (QueryTextReferencesSchema(qText, dbSchema))
                    {
                        queryMatchesDb = true;
                    }
                }
                else if (string.IsNullOrEmpty(qSchema) && !string.IsNullOrEmpty(dbEngine) && !dbEngine.Equals("Database", StringComparison.OrdinalIgnoreCase))
                {
                    if (qName.Contains(dbEngine, StringComparison.OrdinalIgnoreCase) || qId.Contains(dbEngine, StringComparison.OrdinalIgnoreCase))
                    {
                        queryMatchesDb = true;
                    }
                }

                if (!queryMatchesDb) continue;
                if (!MatchesService(qPath, qService, qId)) continue;

                var verb = InferSqlVerb(qVerb, qText, qName) ?? "SQL";
                var snippet = !string.IsNullOrWhiteSpace(qText) ? qText.Trim() : qName;
                var title = $"{verb} {(!string.IsNullOrWhiteSpace(qCaller) ? $"in {qCaller}" : Path.GetFileName(qPath))}";

                var key = $"query:{qPath}:{lineStart}:{snippet}";
                if (seenKeys.Add(key))
                {
                    items.Add(new ArchitectureEvidenceItemDto
                    {
                        Id = qId ?? "",
                        Kind = OntologyConstants.EvidenceKinds.Query,
                        Title = title,
                        Operation = verb,
                        FilePath = qPath ?? "",
                        LineStart = lineStart,
                        LineEnd = lineEnd,
                        Confidence = confidence,
                        CodeSnippet = snippet,
                        CallerSymbol = qCaller,
                        ServiceName = qService ?? serviceName,
                        Reason = $"SQL {verb} statement accessing schema '{dbSchema}'"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Query node scan for '{dbName}'", ex);
        }

        // 4. Query connected Files, Entities, Configs, Migrations, Queries via relationships targeting or originating from Database
        try
        {
            foreach (var (pattern, kinds) in new[]
            {
                ("(src)-[r]->(tgt)", "'USES_DB', 'CONFIGURES', 'READS_FROM', 'WRITES_TO', 'TRANSITIVELY_CALLS'"),
                ("(tgt)-[r]->(src)", "'QUERIED_BY', 'CALLED_BY'")
            })
            {
                var relsQuery = $"""
                    MATCH {pattern}
                    WHERE (tgt.id = $id OR tgt.name = $name OR tgt.id = $rawId)
                      AND r.kind IN [{kinds}]
                      AND NOT (src:Project OR src:Service OR src:Workspace OR src:SemanticStructure)
                    RETURN src.id AS id, src.name AS name, labels(src)[0] AS kind,
                           coalesce(src.file_path, src.path) AS path,
                           coalesce(src.start_line, src.line) AS line,
                           coalesce(src.end_line, src.line_end) AS line_end,
                           r.kind AS rel_kind, r.properties AS properties,
                           json_extract(src.properties, '$.service') AS service,
                           json_extract(src.properties, '$.query_text') AS query_text,
                           json_extract(src.properties, '$.verb') AS verb
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
                    var rProps = row.ExtractProperties();
                    var srcId = row.GetStringProp("id");
                    var srcName = row.GetStringProp("name");
                    var srcKind = row.GetStringProp("kind");
                    var srcPath = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                        ? rfp
                        : row.GetStringProp("path");
                    var relKind = row.GetStringProp("rel_kind");
                    var relOp = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.Operation) ?? "";
                    var relEvKind = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.EvidenceKind) ?? "";
                    var relIsOrm = rProps.GetValueOrDefault("is_orm") ?? "";
                    var srcService = row.GetStringProp("service");

                    int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                    int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                    if (!MatchesService(srcPath, srcService, srcId)) continue;
                    if (string.IsNullOrEmpty(srcPath)) continue;

                    var hasRecordedProvenance = !string.IsNullOrEmpty(relEvKind) || !string.IsNullOrEmpty(relOp);
                    var confidence = hasRecordedProvenance
                        ? OntologyConstants.EvidenceConfidence.Recorded
                        : OntologyConstants.EvidenceConfidence.Inferred;

                    if (srcKind == "Query" || relKind == "QUERIED_BY")
                    {
                        var qText = row.GetStringProp("query_text");
                        var qVerb = row.GetStringProp("verb");
                        var verb = InferSqlVerb(relOp, null) ?? InferSqlVerb(qVerb, qText, srcName) ?? "SQL";
                        var snippet = !string.IsNullOrWhiteSpace(qText) ? qText.Trim() : srcName;
                        var qKey = $"query:{srcPath}:{line}:{snippet}";
                        if (seenKeys.Add(qKey))
                        {
                            items.Add(new ArchitectureEvidenceItemDto
                            {
                                Id = srcId,
                                Kind = OntologyConstants.EvidenceKinds.Query,
                                Title = $"{verb} in {Path.GetFileName(srcPath)}",
                                Operation = verb,
                                FilePath = srcPath,
                                LineStart = line,
                                LineEnd = lineEnd,
                                Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                                CodeSnippet = snippet,
                                CallerSymbol = srcName,
                                ServiceName = srcService ?? serviceName,
                                Reason = $"SQL {verb} query accessing database '{dbName}'"
                            });
                        }
                        continue;
                    }

                    var isMigration = string.Equals(relEvKind, OntologyConstants.EvidenceKinds.Migration, StringComparison.OrdinalIgnoreCase) ||
                                      srcPath.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) ||
                                      srcPath.Contains("\\Migrations\\", StringComparison.OrdinalIgnoreCase) ||
                                      srcPath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
                    var isConfig = string.Equals(relEvKind, OntologyConstants.EvidenceKinds.Config, StringComparison.OrdinalIgnoreCase) ||
                                   relKind == OntologyConstants.Relationships.Configures ||
                                   srcPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                   srcPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                   srcPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                   srcPath.EndsWith(".config", StringComparison.OrdinalIgnoreCase);
                    var isOrm = !isMigration && !isConfig && (
                        string.Equals(relEvKind, OntologyConstants.EvidenceKinds.Orm, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(relIsOrm, "true", StringComparison.OrdinalIgnoreCase) ||
                        srcPath.Contains("Context", StringComparison.OrdinalIgnoreCase) ||
                        srcPath.Contains("Entity", StringComparison.OrdinalIgnoreCase) ||
                        srcPath.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
                        srcPath.Contains("Repository", StringComparison.OrdinalIgnoreCase));

                    string itemKind = !string.IsNullOrEmpty(relEvKind)
                        ? relEvKind
                        : (isMigration ? OntologyConstants.EvidenceKinds.Migration
                            : isConfig ? OntologyConstants.EvidenceKinds.Config
                            : isOrm ? OntologyConstants.EvidenceKinds.Orm
                            : OntologyConstants.EvidenceKinds.Table);
                    string operation = !string.IsNullOrEmpty(relOp)
                        ? relOp
                        : (isMigration ? "MIGRATION" : isConfig ? "CONFIG" : isOrm ? "ORM" : "TABLE");
                    string reason = isMigration ? "Database migration script / snapshot defining tables"
                                  : (isConfig ? "Configuration / connection string declaring database"
                                  : (isOrm ? "ORM entity / repository accessing database" : "Database reference"));

                    var key = $"{itemKind}:{srcPath}:{line}";
                    if (seenKeys.Add(key))
                    {
                        items.Add(new ArchitectureEvidenceItemDto
                        {
                            Id = srcId,
                            Kind = itemKind,
                            Title = $"{srcName} ({operation})",
                            Operation = operation,
                            FilePath = srcPath,
                            LineStart = line,
                            LineEnd = lineEnd,
                            Confidence = isConfig || hasRecordedProvenance
                                ? OntologyConstants.EvidenceConfidence.Recorded
                                : confidence,
                            ServiceName = srcService ?? serviceName,
                            Reason = reason
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Direct database relationship scan for '{dbName}'", ex);
        }

        // 4b. Two-hop traversal: code <-> Table/Procedure <-> Database
        try
        {
            var objects = await FindDatabaseObjectsAsync(resolvedDbId ?? databaseId, dbName ?? databaseId, databaseId, ct);
            foreach (var obj in objects)
            {
                foreach (var (pattern, kinds) in new[]
                {
                    ("(src)-[r]->(obj)", "'PERSISTED_IN', 'READS_FROM', 'WRITES_TO', 'CALLS', 'DEPENDS_ON'"),
                    ("(obj)-[r]->(src)", "'QUERIED_BY', 'CALLED_BY', 'DECLARED_IN'")
                })
                {
                    var usersQuery = $"""
                        MATCH {pattern}
                        WHERE obj.id = $objId
                          AND r.kind IN [{kinds}]
                          AND NOT (src:Project OR src:Service OR src:Workspace OR src:SemanticStructure)
                        RETURN src.id AS id, src.name AS name, labels(src)[0] AS src_kind,
                               coalesce(src.file_path, src.path) AS path,
                               coalesce(src.start_line, src.line) AS line,
                               coalesce(src.end_line, src.line_end) AS line_end,
                               r.kind AS rel_kind, r.properties AS properties,
                               json_extract(src.properties, '$.verb') AS src_verb,
                               json_extract(src.properties, '$.operation') AS src_op,
                               json_extract(src.properties, '$.query_text') AS query_text,
                               json_extract(src.properties, '$.service') AS service
                        """;
                    var usersJson = await db.ExecuteQueryAsync(usersQuery, new Dictionary<string, object> { ["objId"] = obj.Id }, ct);
                    using var usersDoc = JsonDocument.Parse(usersJson);
                    foreach (var row in usersDoc.RootElement.EnumerateArray())
                    {
                        var rProps = row.ExtractProperties();
                        var srcId = row.GetStringProp("id");
                        var path = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.FilePath) is { Length: > 0 } rfp
                            ? rfp
                            : row.GetStringProp("path");
                        if (string.IsNullOrEmpty(path) || !MatchesService(path, row.GetStringProp("service"), srcId)) continue;

                        var relKind = row.GetStringProp("rel_kind");
                        var relOp = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.Operation) ?? "";
                        var relEvKind = rProps.GetValueOrDefault(OntologyConstants.PropertyNames.EvidenceKind) ?? "";
                        var srcVerb = row.GetStringProp("src_op") is { Length: > 0 } sOp ? sOp : row.GetStringProp("src_verb");
                        var queryText = row.GetStringProp("query_text");
                        var srcName = row.GetStringProp("name");

                        int? line = ParseLineProp(rProps, OntologyConstants.PropertyNames.Line, row, "line");
                        int? lineEnd = ParseLineProp(rProps, OntologyConstants.PropertyNames.LineEnd, row, "line_end") ?? line;

                        var isProcedure = obj.Label == OntologyConstants.NodeLabels.Procedure;
                        var explicitSqlVerb = InferSqlVerb(relOp, null) ?? InferSqlVerb(srcVerb, queryText, srcName);

                        var (kind, operation) = relKind switch
                        {
                            "PERSISTED_IN" or "DECLARED_IN" => (OntologyConstants.EvidenceKinds.Orm, !string.IsNullOrEmpty(relOp) ? relOp : "ORM"),
                            "WRITES_TO" => (
                                !string.IsNullOrEmpty(relEvKind) ? relEvKind : OntologyConstants.EvidenceKinds.Query,
                                explicitSqlVerb ?? "INSERT"),
                            "READS_FROM" or "QUERIED_BY" or "DEPENDS_ON" => (
                                !string.IsNullOrEmpty(relEvKind) ? relEvKind : OntologyConstants.EvidenceKinds.Query,
                                explicitSqlVerb ?? "SELECT"),
                            _ => (
                                !string.IsNullOrEmpty(relEvKind) ? relEvKind : (isProcedure ? OntologyConstants.EvidenceKinds.Procedure : OntologyConstants.EvidenceKinds.Query),
                                explicitSqlVerb ?? (isProcedure ? "EXEC" : "SELECT"))
                        };

                        var key = $"{kind}:{path}:{line}:{obj.Id}:{operation}";
                        if (!seenKeys.Add(key)) continue;

                        items.Add(new ArchitectureEvidenceItemDto
                        {
                            Id = srcId,
                            Kind = kind,
                            Operation = operation,
                            Title = $"{srcName} → {obj.Name}",
                            FilePath = path,
                            LineStart = line,
                            LineEnd = lineEnd,
                            Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                             CodeSnippet = !string.IsNullOrWhiteSpace(queryText) ? queryText : null,
                            CallerSymbol = srcName,
                            TargetSymbol = obj.Name,
                            ServiceName = row.GetStringProp("service") is { Length: > 0 } s ? s : serviceName,
                            Reason = $"{relKind} {obj.Label.ToLowerInvariant()} '{obj.Name}' belonging to database '{dbName}'"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RecordEvidenceWarning(warnings, $"Table/procedure traversal for '{dbName}'", ex);
        }

        // Also if target node has a declaration file and not yet added, add it
        if (!string.IsNullOrEmpty(dbDeclPath) && MatchesService(dbDeclPath, serviceName))
        {
            var key = $"config:{dbDeclPath}";
            if (seenKeys.Add(key))
            {
                items.Add(new ArchitectureEvidenceItemDto
                {
                    Id = resolvedDbId ?? databaseId,
                    Kind = OntologyConstants.EvidenceKinds.Config,
                    Title = $"{Path.GetFileName(dbDeclPath)} (CONFIG)",
                    Operation = "CONFIG",
                    FilePath = dbDeclPath,
                    LineStart = null,
                    LineEnd = null,
                    Confidence = OntologyConstants.EvidenceConfidence.Recorded,
                    ServiceName = serviceName,
                    Reason = $"Database declaration configuration ({dbEngine})"
                });
            }
        }

        // Sort items: queries first, then orm/tables, then config, then migrations
        response.Items = items
            .OrderBy(i => i.Kind switch
            {
                "query" or "procedure" => 0,
                "orm" => 1,
                "table" => 2,
                "config" => 3,
                "migration" => 4,
                _ => 5
            })
            .ThenBy(i => i.FilePath)
            .ThenBy(i => i.LineStart ?? int.MaxValue)
            .ToList();

        response.TotalCount = response.Items.Count;
        if (warnings.Count > 0)
        {
            response.Warnings = warnings;
        }
        return response;
    }

}
