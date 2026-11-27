namespace CodeExplorer.Core.Mcp;

internal static class McpLineageFormatter
{
    private const int MaxEntryPointsCap = 100;
    private const int MaxTablesCap = 10;
    private const int MaxLineageAccessorsCap = 30;
    private const int MaxUpstreamCallersCap = 15;
    private const int MaxQueriesCap = 15;
    private const int MaxQueryTextLength = 120;
    private const string ScopeRuntime = "runtime";
    private const string ScopeBuild = "build";
    private const string ColProject = "project";
    private const string ColDependency = "dependency";
    private const string ColDependencyType = "dependencyType";
    private const string ColVersion = "version";
    private const string ColScope = "scope";
    private const string ColInbound = "inboundProject";
    private const string ColId = "id";
    private const string ColTableName = "tableName";
    private const string ColKind = "kind";
    private const string ColName = "name";
    private const string ColOwnerName = "ownerName";
    private const string ColExtra = "extra";
    private const string ColFilePath = "filePath";
    private const string ColStartLine = "startLine";
    private const string ColEntryPoint = "entryPoint";
    private const string ColClassName = "className";
    private const string ColRoute = "route";
    private const string ColHttpMethod = "httpMethod";
    private const string ColProtocol = "protocol";
    private const string ColRequestType = "requestType";
    private const string ColResponseType = "responseType";
    private const string KindEntity = "ENTITY";
    private const string KindWriter = "WRITER";
    private const string KindReader = "READER";
    private const string KindCaller = "CALLER";
    private const string KindQuery = "QUERY";
    private const string KindMeta = "META";
    private const string KindFkOut = "FK_OUT";
    private const string KindFkIn = "FK_IN";
    private const string KindView = "VIEW";
    private const string KindProc = "PROC";
    private const string KindTrigger = "TRIGGER";
    private const string SingleQuote = "'";
    private const string EscapedSingleQuote = "''";
    private const string CommaSep = ", ";

    private const string AllDepsQuery =
        "MATCH (p:Project)-[r:DEPENDS_ON]->(dep) " +
        "RETURN p.name AS project, dep.name AS dependency, labels(dep)[0] AS dependencyType, " +
        "coalesce(dep.version, r.version, '') AS version, coalesce(r.dependency_type, '') AS scope";

    private const string FilteredOutboundDepsQuery =
        "MATCH (p:Project)-[r:DEPENDS_ON]->(dep) " +
        "WHERE toLower(p.name) CONTAINS toLower($projectFilter) " +
        "RETURN p.name AS project, dep.name AS dependency, labels(dep)[0] AS dependencyType, " +
        "coalesce(dep.version, r.version, '') AS version, coalesce(r.dependency_type, '') AS scope";

    private const string FilteredInboundDepsQuery =
        "MATCH (inProj:Project)-[:DEPENDS_ON]->(p:Project) " +
        "WHERE toLower(p.name) CONTAINS toLower($projectFilter) " +
        "RETURN p.name AS project, inProj.name AS inboundProject";

    private const string ExactTableIdsQuery =
        "MATCH (t:Table) WHERE t.name = $tableName OR toLower(t.name) = toLower($tableName) RETURN t.id AS id LIMIT 10";

    private const string SubstringTableIdsQuery =
        "MATCH (t:Table) WHERE $tableName = '' OR $tableName = '*' OR toLower(t.name) CONTAINS toLower($tableName) RETURN t.id AS id LIMIT 10";

    private static string BuildLineageUnionByIdsQuery(string idListLiteral)
    {
        var anchor = $"MATCH (t:Table) WHERE t.id IN [{idListLiteral}] ";
        return anchor + "OPTIONAL MATCH (t)-[:IMPLEMENTED_BY]->(e:Type) " +
               "RETURN t.name AS tableName, 'ENTITY' AS kind, coalesce(e.name, '') AS name, '' AS ownerName, '' AS extra, coalesce(e.file_path, e.path, '') AS filePath, 0 AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[r:WRITES_TO]-(fn:Function) OPTIONAL MATCH (fn)<-[:HAS_METHOD]-(owner:Type) " +
               "RETURN t.name AS tableName, 'WRITER' AS kind, coalesce(fn.name, '') AS name, coalesce(owner.name, '') AS ownerName, coalesce(r.operation, 'WRITE') AS extra, coalesce(fn.file_path, fn.path, r.file_path, '') AS filePath, coalesce(fn.start_line, r.line, 0) AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[r:WRITES_TO]-(q:Query) OPTIONAL MATCH (q)-[:ACCESSED_BY]->(fn:Function) OPTIONAL MATCH (fn)<-[:HAS_METHOD]-(owner:Type) OPTIONAL MATCH (q)-[:ACCESSED_BY]->(ownerType:Type) " +
               "RETURN t.name AS tableName, 'WRITER' AS kind, coalesce(fn.name, q.name, '') AS name, coalesce(owner.name, ownerType.name, '') AS ownerName, coalesce(q.verb, q.operation, r.operation, 'WRITE') AS extra, coalesce(fn.file_path, fn.path, q.file_path, q.path, r.file_path, '') AS filePath, coalesce(fn.start_line, q.line, q.start_line, r.line, 0) AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[r:READS_FROM]-(fn:Function) OPTIONAL MATCH (fn)<-[:HAS_METHOD]-(owner:Type) " +
               "RETURN t.name AS tableName, 'READER' AS kind, coalesce(fn.name, '') AS name, coalesce(owner.name, '') AS ownerName, '' AS extra, coalesce(fn.file_path, fn.path, r.file_path, '') AS filePath, coalesce(fn.start_line, r.line, 0) AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[r:READS_FROM]-(q:Query) OPTIONAL MATCH (q)-[:ACCESSED_BY]->(fn:Function) OPTIONAL MATCH (fn)<-[:HAS_METHOD]-(owner:Type) OPTIONAL MATCH (q)-[:ACCESSED_BY]->(ownerType:Type) " +
               "RETURN t.name AS tableName, 'READER' AS kind, coalesce(fn.name, q.name, '') AS name, coalesce(owner.name, ownerType.name, '') AS ownerName, '' AS extra, coalesce(fn.file_path, fn.path, q.file_path, q.path, r.file_path, '') AS filePath, coalesce(fn.start_line, q.line, q.start_line, r.line, 0) AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)-[:ACCESSED_BY|QUERIED_BY]->(fn:Function) OPTIONAL MATCH (fn)<-[:HAS_METHOD]-(owner:Type) " +
               "RETURN t.name AS tableName, 'READER' AS kind, coalesce(fn.name, '') AS name, coalesce(owner.name, '') AS ownerName, '' AS extra, coalesce(fn.file_path, fn.path, '') AS filePath, coalesce(fn.start_line, 0) AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[:READS_FROM|WRITES_TO]-(fn:Function) OPTIONAL MATCH (fn)<-[:CALLS]-(caller:Function) OPTIONAL MATCH (caller)<-[:HAS_METHOD]-(owner:Type) " +
               "RETURN t.name AS tableName, 'CALLER' AS kind, coalesce(caller.name, '') AS name, coalesce(owner.name, '') AS ownerName, '' AS extra, coalesce(caller.file_path, caller.path, '') AS filePath, coalesce(caller.start_line, 0) AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[:READS_FROM|WRITES_TO]-(q:Query) OPTIONAL MATCH (q)-[:ACCESSED_BY]->(fn:Function) OPTIONAL MATCH (fn)<-[:CALLS]-(caller:Function) OPTIONAL MATCH (caller)<-[:HAS_METHOD]-(owner:Type) " +
               "RETURN t.name AS tableName, 'CALLER' AS kind, coalesce(caller.name, '') AS name, coalesce(owner.name, '') AS ownerName, '' AS extra, coalesce(caller.file_path, caller.path, '') AS filePath, coalesce(caller.start_line, 0) AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)-[:ACCESSED_BY|QUERIED_BY]->(fn:Function) OPTIONAL MATCH (fn)<-[:CALLS]-(caller:Function) OPTIONAL MATCH (caller)<-[:HAS_METHOD]-(owner:Type) " +
               "RETURN t.name AS tableName, 'CALLER' AS kind, coalesce(caller.name, '') AS name, coalesce(owner.name, '') AS ownerName, '' AS extra, coalesce(caller.file_path, caller.path, '') AS filePath, coalesce(caller.start_line, 0) AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[r:READS_FROM|WRITES_TO]-(q:Query) " +
               "RETURN t.name AS tableName, 'QUERY' AS kind, coalesce(q.name, '') AS name, '' AS ownerName, coalesce(q.query_text, '') AS extra, coalesce(q.path, q.file_path, r.file_path, '') AS filePath, coalesce(q.line, q.start_line, r.line, 0) AS startLine UNION ALL " +
               anchor +
               "RETURN t.name AS tableName, 'META' AS kind, coalesce(t.sub_kind, '') AS name, '' AS ownerName, coalesce(t.columns, '') AS extra, coalesce(t.path, '') AS filePath, 0 AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)-[r:REFERENCES]->(parent:Table) " +
               "RETURN t.name AS tableName, 'FK_OUT' AS kind, coalesce(parent.name, '') AS name, '' AS ownerName, coalesce(r.operation, '') AS extra, '' AS filePath, 0 AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[r:REFERENCES]-(child:Table) " +
               "RETURN t.name AS tableName, 'FK_IN' AS kind, coalesce(child.name, '') AS name, '' AS ownerName, coalesce(r.operation, '') AS extra, '' AS filePath, 0 AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[:READS_FROM]-(v:Table) " +
               "RETURN t.name AS tableName, 'VIEW' AS kind, coalesce(v.name, '') AS name, coalesce(v.sub_kind, 'view') AS ownerName, '' AS extra, coalesce(v.path, '') AS filePath, 0 AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[:READS_FROM]-(p:Procedure) " +
               "RETURN t.name AS tableName, 'PROC' AS kind, coalesce(p.name, '') AS name, coalesce(p.sub_kind, 'procedure') AS ownerName, 'READ' AS extra, coalesce(p.path, '') AS filePath, 0 AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)<-[:WRITES_TO]-(p:Procedure) " +
               "RETURN t.name AS tableName, 'PROC' AS kind, coalesce(p.name, '') AS name, coalesce(p.sub_kind, 'procedure') AS ownerName, 'WRITE' AS extra, coalesce(p.path, '') AS filePath, 0 AS startLine UNION ALL " +
               anchor + "OPTIONAL MATCH (t)-[r:TRIGGERS]->(p:Procedure) " +
               "RETURN t.name AS tableName, 'TRIGGER' AS kind, coalesce(p.name, '') AS name, coalesce(p.sub_kind, 'trigger') AS ownerName, coalesce(r.operation, p.trigger_event, '') AS extra, coalesce(p.path, '') AS filePath, 0 AS startLine";
    }

    private static readonly string[] DepEdgeColumns = ["project", "dependency", "dependencyType", "version", "scope"];
    private static readonly string[] EntryPointColumns = ["protocol", "httpMethod", "route", "entryPoint", "className", "requestType", "responseType", "filePath", "startLine"];

    private static readonly HashSet<string> BuildDevNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "build", "test", "src", "jest", "vitest", "ts-jest", "ts-node", "typescript", "esbuild",
        "prettier", "eslint", "rimraf", "supertest", "msw", "testcontainers"
    };

    public static async Task<string> GetProjectDependenciesYamlAsync(
        IGraphClient client,
        string? projectFilter,
        int limit,
        string? type,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 500);
        var normType = (type ?? "all").Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(projectFilter))
        {
            return await FormatFilteredProjectDepsAsync(client, projectFilter.Trim(), effectiveLimit, normType, cancellationToken);
        }

        var res = await client.ExecuteCypherAsync(AllDepsQuery, null, cancellationToken);
        var filtered = res.Rows
            .Where(r => MatchesDependencyScope(r.GetStringProp(ColDependency), r.GetStringProp(ColDependencyType), r.GetStringProp(ColScope), normType))
            .ToList();

        var rows = filtered
            .Take(effectiveLimit)
            .Select(r => new CypherRow(DepEdgeColumns,
            [
                CypherValue.FromString(r.GetStringProp(ColProject)),
                CypherValue.FromString(r.GetStringProp(ColDependency)),
                CypherValue.FromString(r.GetStringProp(ColDependencyType)),
                CypherValue.FromString(r.GetStringProp(ColVersion)),
                CypherValue.FromString(r.GetStringProp(ColScope))
            ]))
            .ToList();

        return ToonYamlSerializer.SerializeYaml(new CypherQueryResult(DepEdgeColumns, rows));
    }

    private static async Task<string> FormatFilteredProjectDepsAsync(
        IGraphClient client,
        string projectFilter,
        int limit,
        string normType,
        CancellationToken cancellationToken)
    {
        var queryParams = new Dictionary<string, string> { ["projectFilter"] = projectFilter };
        var outRes = await client.ExecuteCypherAsync(FilteredOutboundDepsQuery, queryParams, cancellationToken);
        var inRes = await client.ExecuteCypherAsync(FilteredInboundDepsQuery, queryParams, cancellationToken);

        var projectNames = outRes.Rows.Select(r => r.GetStringProp(ColProject))
            .Concat(inRes.Rows.Select(r => r.GetStringProp(ColProject)))
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => string.Equals(n, projectFilter, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (projectNames.Count == 0)
        {
            return "[]";
        }

        var items = new List<object>();
        foreach (var proj in projectNames)
        {
            var projOut = outRes.Rows
                .Where(r => string.Equals(r.GetStringProp(ColProject), proj, StringComparison.OrdinalIgnoreCase))
                .Where(r => MatchesDependencyScope(r.GetStringProp(ColDependency), r.GetStringProp(ColDependencyType), r.GetStringProp(ColScope), normType))
                .Select(r => r.GetStringProp(ColDependency))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToList();

            var projIn = inRes.Rows
                .Where(r => string.Equals(r.GetStringProp(ColProject), proj, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.GetStringProp(ColInbound))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToList();

            var shownOut = projOut.Take(limit).ToList();
            items.Add(new
            {
                project = proj,
                totalOutgoing = projOut.Count,
                moreOutgoing = projOut.Count > shownOut.Count ? projOut.Count - shownOut.Count : 0,
                outgoingDependencies = shownOut,
                incomingDependencies = projIn
            });
        }

        return ToonYamlSerializer.SerializeObject(items);
    }

    public static string FormatEntryPointsYaml(CypherQueryResult rawResult, string? workspaceRoot)
    {
        var deduped = new Dictionary<(string Entry, string Proto, string Method, string Route, long Line), (string ClassName, string ReqType, string RespType, string FilePath)>();
        foreach (var row in rawResult.Rows)
        {
            var entry = row.GetStringProp(ColEntryPoint);
            var proto = row.GetStringProp(ColProtocol);
            var method = row.GetStringProp(ColHttpMethod);
            var route = row.GetStringProp(ColRoute);
            var line = row.GetLongProp(ColStartLine);
            var cls = row.GetStringProp(ColClassName);
            var reqT = row.GetStringProp(ColRequestType);
            var respT = row.GetStringProp(ColResponseType);
            var fp = McpResultFormatter.CleanRelativePath(row.GetStringProp(ColFilePath), workspaceRoot);

            var key = (entry, proto, method, route, line);
            if (!deduped.TryGetValue(key, out var existing)
                || (string.IsNullOrEmpty(existing.ClassName) && !string.IsNullOrEmpty(cls))
                || (string.IsNullOrEmpty(existing.FilePath) && !string.IsNullOrEmpty(fp)))
            {
                deduped[key] = (
                    !string.IsNullOrEmpty(cls) ? cls : (existing.ClassName ?? string.Empty),
                    !string.IsNullOrEmpty(reqT) ? reqT : (existing.ReqType ?? string.Empty),
                    !string.IsNullOrEmpty(respT) ? respT : (existing.RespType ?? string.Empty),
                    !string.IsNullOrEmpty(fp) ? fp : (existing.FilePath ?? string.Empty));
            }
        }

        var ordered = deduped
            .OrderBy(kv => kv.Value.FilePath, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.Line)
            .ThenBy(kv => kv.Key.Entry, StringComparer.Ordinal)
            .ToList();

        var shown = ordered
            .Take(MaxEntryPointsCap)
            .Select(kv => new CypherRow(EntryPointColumns,
            [
                CypherValue.FromString(kv.Key.Proto),
                CypherValue.FromString(kv.Key.Method),
                CypherValue.FromString(kv.Key.Route),
                CypherValue.FromString(kv.Key.Entry),
                CypherValue.FromString(kv.Value.ClassName),
                CypherValue.FromString(kv.Value.ReqType),
                CypherValue.FromString(kv.Value.RespType),
                CypherValue.FromString(kv.Value.FilePath),
                CypherValue.FromInt64(kv.Key.Line)
            ]))
            .ToList();

        var tableYaml = ToonYamlSerializer.SerializeYaml(new CypherQueryResult(EntryPointColumns, shown));
        if (ordered.Count <= MaxEntryPointsCap)
        {
            return tableYaml;
        }

        return $"totalEntryPoints: {ordered.Count}\nmoreEntryPoints: {ordered.Count - shown.Count}\n{tableYaml}";
    }

    public static async Task<string> InspectDataLineageYamlAsync(
        IGraphClient client,
        string? tableName,
        string? workspaceRoot,
        CancellationToken cancellationToken)
    {
        var trimmed = tableName?.Trim() ?? string.Empty;
        var tableParams = new Dictionary<string, string?> { ["tableName"] = trimmed };
        var idsRes = !string.IsNullOrEmpty(trimmed) && trimmed != "*"
            ? await client.ExecuteCypherAsync(ExactTableIdsQuery, tableParams, cancellationToken)
            : new CypherQueryResult([], []);

        if (idsRes.Count == 0)
        {
            idsRes = await client.ExecuteCypherAsync(SubstringTableIdsQuery, tableParams, cancellationToken);
        }

        var tableIds = idsRes.Rows
            .Select(r => r.GetStringProp(ColId))
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTablesCap)
            .Select(id => $"'{id.Replace(SingleQuote, EscapedSingleQuote)}'")
            .ToList();

        if (tableIds.Count == 0)
        {
            return "[]";
        }

        var res = await client.ExecuteCypherAsync(BuildLineageUnionByIdsQuery(string.Join(CommaSep, tableIds)), null, cancellationToken);
        var grouped = res.Rows
            .GroupBy(r => r.GetStringProp(ColTableName), StringComparer.Ordinal)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxTablesCap)
            .Select(g => BuildGroupedTableLineage(g.Key, g, workspaceRoot))
            .ToList();

        return ToonYamlSerializer.SerializeObject(grouped);
    }

    private static object BuildGroupedTableLineage(
        string tableName,
        IEnumerable<CypherRow> rows,
        string? workspaceRoot)
    {
        var rowList = rows.ToList();
        var entities = rowList
            .Where(r => r.GetStringProp(ColKind) == KindEntity)
            .Select(r => (Name: r.GetStringProp(ColName), Path: McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot)))
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Distinct()
            .Select(e => string.IsNullOrEmpty(e.Path) ? e.Name : $"{e.Name} ({e.Path})")
            .ToList();

        var writers = FormatAccessorList(rowList.Where(r => r.GetStringProp(ColKind) == KindWriter), workspaceRoot, includeOperation: true, MaxLineageAccessorsCap);
        var readers = FormatAccessorList(rowList.Where(r => r.GetStringProp(ColKind) == KindReader), workspaceRoot, includeOperation: false, MaxLineageAccessorsCap);
        var upstream = FormatAccessorList(rowList.Where(r => r.GetStringProp(ColKind) == KindCaller), workspaceRoot, includeOperation: false, MaxUpstreamCallersCap);
        var queries = FormatQueriesList(rowList.Where(r => r.GetStringProp(ColKind) == KindQuery), tableName, workspaceRoot);

        string? subKind = null;
        string? columns = null;
        foreach (var r in rowList)
        {
            if (r.GetStringProp(ColKind) == KindMeta)
            {
                subKind = r.GetStringProp(ColName);
                columns = r.GetStringProp(ColExtra);
                break;
            }
        }

        var referencesTables = rowList
            .Where(r => r.GetStringProp(ColKind) == KindFkOut)
            .Select(r => FormatFkEntry(r.GetStringProp(ColName), r.GetStringProp(ColExtra)))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var referencedByTables = rowList
            .Where(r => r.GetStringProp(ColKind) == KindFkIn)
            .Select(r => FormatFkEntry(r.GetStringProp(ColName), r.GetStringProp(ColExtra)))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dependentViews = rowList
            .Where(r => r.GetStringProp(ColKind) == KindView)
            .Select(r => r.GetStringProp(ColName))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var procedures = rowList
            .Where(r => r.GetStringProp(ColKind) == KindProc)
            .Select(r =>
            {
                var name = r.GetStringProp(ColName);
                if (string.IsNullOrEmpty(name)) return string.Empty;
                var kind = r.GetStringProp(ColOwnerName);
                var op = r.GetStringProp(ColExtra);
                return $"{name} ({kind}, {op})";
            })
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var triggers = rowList
            .Where(r => r.GetStringProp(ColKind) == KindTrigger)
            .Select(r =>
            {
                var name = r.GetStringProp(ColName);
                if (string.IsNullOrEmpty(name)) return string.Empty;
                var ev = r.GetStringProp(ColExtra);
                return string.IsNullOrEmpty(ev) ? name : $"{name} ({ev})";
            })
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, object>
        {
            ["tableName"] = tableName,
            ["entities"] = entities,
            ["writersCount"] = writers.TotalCount,
            ["writers"] = writers.Items,
            ["readersCount"] = readers.TotalCount,
            ["readers"] = readers.Items,
            ["upstreamCallersCount"] = upstream.TotalCount,
            ["upstreamCallers"] = upstream.Items,
            ["queries"] = queries
        };

        if (!string.IsNullOrEmpty(subKind)) result["subKind"] = subKind;
        if (!string.IsNullOrEmpty(columns)) result["columns"] = columns;
        if (referencesTables.Count > 0) result["referencesTables"] = referencesTables;
        if (referencedByTables.Count > 0) result["referencedByTables"] = referencedByTables;
        if (dependentViews.Count > 0) result["dependentViews"] = dependentViews;
        if (procedures.Count > 0) result["procedures"] = procedures;
        if (triggers.Count > 0) result["triggers"] = triggers;

        return result;
    }

    private static string FormatFkEntry(string targetName, string context) =>
        string.IsNullOrEmpty(targetName) ? string.Empty :
        string.IsNullOrEmpty(context) ? targetName : $"{targetName} ({context})";

    private static List<object> FormatQueriesList(IEnumerable<CypherRow> rows, string tableName, string? workspaceRoot) =>
        rows.Select(r =>
            {
                var qText = r.GetStringProp(ColExtra).Replace("\r", " ").Replace("\n", " ").Trim();
                if (qText.Length > MaxQueryTextLength) qText = qText[..MaxQueryTextLength] + "...";
                var cleanPath = McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot);
                var line = r.GetLongProp(ColStartLine);
                var locPath = !string.IsNullOrEmpty(cleanPath) && line > 0 ? $"{cleanPath}:{line}" : cleanPath;
                return (Name: r.GetStringProp(ColName), Path: locPath, Sql: qText);
            })
            .Where(q => (!string.IsNullOrEmpty(q.Sql) || !string.IsNullOrEmpty(q.Name)) && IsQueryRelevantToTable(q.Sql, tableName))
            .Distinct()
            .Take(MaxQueriesCap)
            .Select(q => (object)new { name = q.Name, filePath = q.Path, sql = q.Sql })
            .ToList();

    private static bool IsQueryRelevantToTable(string sql, string tableName)
    {
        if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(tableName))
        {
            return true;
        }

        if (sql.Contains(tableName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = sql.TrimStart();
        if (trimmed.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("DELETE ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("REPLACE ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static (int TotalCount, List<string> Items) FormatAccessorList(
        IEnumerable<CypherRow> rows,
        string? workspaceRoot,
        bool includeOperation,
        int maxCap)
    {
        var entries = new List<(bool IsTest, string Formatted)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in rows)
        {
            var fn = r.GetStringProp(ColName);
            if (string.IsNullOrEmpty(fn)) continue;
            var owner = r.GetStringProp(ColOwnerName);
            var qualified = string.IsNullOrEmpty(owner) ? fn : $"{owner}.{fn}";
            var path = McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot);
            var line = r.GetLongProp(ColStartLine);
            var loc = string.IsNullOrEmpty(path) ? string.Empty : (line > 0 ? $"{path}:{line}" : path);
            var op = includeOperation ? r.GetStringProp(ColExtra) : string.Empty;

            var label = !string.IsNullOrEmpty(op) && !string.IsNullOrEmpty(loc)
                ? $"{qualified} [{op}] ({loc})"
                : (!string.IsNullOrEmpty(loc) ? $"{qualified} ({loc})" : qualified);

            if (seen.Add(label))
            {
                var isTest = path.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
                    || path.Contains(".test.", StringComparison.OrdinalIgnoreCase)
                    || fn.StartsWith("Jest:", StringComparison.Ordinal);
                entries.Add((isTest, label));
            }
        }

        var sorted = entries
            .OrderBy(e => e.IsTest ? 1 : 0)
            .ThenBy(e => e.Formatted, StringComparer.Ordinal)
            .Select(e => e.Formatted)
            .ToList();

        return (sorted.Count, sorted.Take(maxCap).ToList());
    }

    private static bool MatchesDependencyScope(string depName, string depKind, string scope, string normType)
    {
        if (normType is not (ScopeRuntime or ScopeBuild))
        {
            return true;
        }

        var isBuildOrDev = scope.Equals("dev", StringComparison.OrdinalIgnoreCase)
            || scope.Equals("build", StringComparison.OrdinalIgnoreCase)
            || scope.Equals("test", StringComparison.OrdinalIgnoreCase)
            || depName.StartsWith("@types/", StringComparison.OrdinalIgnoreCase)
            || (depKind != "Project" && BuildDevNames.Contains(depName));

        return normType == ScopeBuild ? isBuildOrDev : !isBuildOrDev;
    }
}
