using CodeExplorer.Core.Analysis.Testing;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private const string SymTypeFunction = "Function";
    private const string SymTypeClass = "Class";
    private const string SymTypeInterface = "Interface";
    private const string SymTypeVariable = "Variable";
    private const string SymTypeMember = "Member";
    private const string KindType = "Type";
    private const string ColFullName = "fullName";
    private const string ColFilePathCamel = "filePath";
    private const string ColStartLineCamel = "startLine";
    private const string ColEndLineCamel = "endLine";
    private const string LimitTenClause = "LIMIT 10";
    private const string DepthPlaceholder = "{depth}";
    private const string QueryFindSymbolFunction = "find_symbol_function";
    private const string QueryFindSymbolClass = "find_symbol_class";
    private const string QueryFindSymbolInterface = "find_symbol_interface";
    private const string QueryFindSymbolAll = "find_symbol_all";
    private const string QueryGetCallChain = "get_call_chain";
    private const string QueryResolveCallTarget = "resolve_call_target";
    private const string QueryAnalyzeCodeImpact = "analyze_code_impact";
    private const string FindTypeSymbolsQuery =
        "MATCH (n:Type) WHERE n.name IS NOT NULL AND n.name CONTAINS $name " +
        "OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File) " +
        "RETURN CASE WHEN n.kind = 'class' THEN 'Class' WHEN n.kind = 'interface' THEN 'Interface' ELSE coalesce(n.kind, 'Type') END AS type, " +
        "n.name AS name, n.symbol AS fullName, coalesce(f.path, n.file_path, n.path) AS filePath, " +
        "coalesce(n.start_line, 0) AS startLine, coalesce(n.end_line, 0) AS endLine LIMIT 10";
    private const string FindMemberQueryTemplate =
        "MATCH (n:Member) WHERE n.name IS NOT NULL AND n.name CONTAINS $name " +
        "OPTIONAL MATCH (owner:Type)-[:HAS_MEMBER]->(n) " +
        "OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File) " +
        "RETURN 'Member' AS type, n.name AS name, " +
        "CASE WHEN owner IS NOT NULL AND owner.name IS NOT NULL THEN (owner.name + '.' + n.name) ELSE n.name END AS fullName, " +
        "coalesce(f.path, n.file_path, n.path) AS filePath, " +
        "coalesce(n.start_line, 0) AS startLine, coalesce(n.end_line, 0) AS endLine LIMIT 10";

    public async Task<string> FindSymbolAsync(string name, string? symbolType = null, int limit = 50, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var effectiveWsRoot = !string.IsNullOrWhiteSpace(workspacePath) ? workspacePath : DefaultWorkspacePath;
        var parameters = new Dictionary<string, string>
        {
            [ColName] = name
        };

        var query = symbolType switch
        {
            SymTypeFunction => Queries.Get(QueryFindSymbolFunction),
            SymTypeClass => Queries.Get(QueryFindSymbolClass),
            SymTypeInterface => Queries.Get(QueryFindSymbolInterface),
            KindType => FindTypeSymbolsQuery,
            SymTypeVariable or SymTypeMember => FindMemberQueryTemplate,
            _ => Queries.Get(QueryFindSymbolAll)
        };

        var effectiveLimit = Math.Clamp(limit, 1, 500);
        query = query.Replace(LimitTenClause, $"LIMIT {effectiveLimit}", StringComparison.Ordinal);
        var res = await client.ExecuteCypherAsync(query, parameters, cancellationToken);

        if (res.Count == 0 && string.IsNullOrEmpty(symbolType))
        {
            var memberFallback = FindMemberQueryTemplate.Replace(LimitTenClause, $"LIMIT {effectiveLimit}", StringComparison.Ordinal);
            res = await client.ExecuteCypherAsync(memberFallback, parameters, cancellationToken);
        }

        if (res.Count == 0 && (string.IsNullOrEmpty(symbolType) || symbolType == SymTypeFunction))
        {
            var gqlQuery =
                "MATCH (n:Function) WHERE n.graphql_field CONTAINS $name OR n.graphql_operation CONTAINS $name " +
                "OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File) " +
                "RETURN 'Function' AS type, n.name AS name, n.symbol AS fullName, " +
                "coalesce(f.path, n.file_path, n.path) AS filePath, " +
                $"coalesce(n.start_line, 0) AS startLine, coalesce(n.end_line, 0) AS endLine LIMIT {effectiveLimit}";
            res = await client.ExecuteCypherAsync(gqlQuery, parameters, cancellationToken);
        }

        if (res.Count == 0 && client.SupportsIdentifierSearch)
        {
            try
            {
                var kindFilter = symbolType switch
                {
                    SymTypeFunction => SymTypeFunction,
                    SymTypeClass or SymTypeInterface => KindType,
                    SymTypeVariable or SymTypeMember => SymTypeMember,
                    _ => null
                };
                var matches = await client.SearchIdentifiersAsync(name, kindFilter, effectiveLimit, cancellationToken);
                var filtered = matches.Where(m => IsDeterministicSymbolMatch(m, name)).ToList();
                if (filtered.Count > 0)
                {
                    string[] cols = [ColType, ColName, ColFullName, ColFilePathCamel, ColStartLineCamel, ColEndLineCamel];
                    var rows = filtered.Select(m => new CypherRow(cols,
                    [
                        CypherValue.FromString(m.Kind.ToString()),
                        CypherValue.FromString(m.Name),
                        CypherValue.FromString(McpResultFormatter.CleanSymbolDisplay(m.FullName, m.Name)),
                        CypherValue.FromString(McpResultFormatter.CleanRelativePath(m.Path, effectiveWsRoot)),
                        CypherValue.FromInt64(m.StartLine),
                        CypherValue.FromInt64(0)
                    ])).ToList();
                    res = new CypherQueryResult(cols, rows);
                }
            }
            catch
            {
                // Fallback gracefully on identifier index error
            }
        }
        else if (res.Count > 0)
        {
            res = NormalizeSymbolQueryFilePaths(res, effectiveWsRoot);
        }

        return ToonYamlSerializer.SerializeYaml(res);
    }

    private static bool IsDeterministicSymbolMatch(IdentifierMatch match, string rawQuery)
    {
        var q = rawQuery.Trim();
        if (q.Length == 0) return false;

        if (match.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            match.FullName.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tokens = IdentifierVectorEncoder.Tokenize(q);
        return tokens.Count >= 2 && tokens.All(t => match.FullName.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static CypherQueryResult NormalizeSymbolQueryFilePaths(CypherQueryResult result, string? workspaceRoot)
    {
        var cols = result.Columns as string[] ?? result.Columns.ToArray();
        var fileColIdx = Array.IndexOf(cols, ColFilePathCamel);
        var fullNameIdx = Array.IndexOf(cols, ColFullName);
        if ((fileColIdx < 0 && fullNameIdx < 0) || result.Rows.Count == 0) return result;

        var normalizedRows = new List<CypherRow>(result.Rows.Count);
        foreach (var row in result.Rows)
        {
            var values = new CypherValue[cols.Length];
            var symName = row.GetStringProp(ColName);
            for (var i = 0; i < cols.Length; i++)
            {
                var val = row[cols[i]];
                if (i == fileColIdx && val.Kind == CypherValueKind.String)
                {
                    val = CypherValue.FromString(McpResultFormatter.CleanRelativePath(val.AsString(), workspaceRoot));
                }
                else if (i == fullNameIdx && val.Kind == CypherValueKind.String)
                {
                    val = CypherValue.FromString(McpResultFormatter.CleanSymbolDisplay(val.AsString(), symName));
                }
                values[i] = val;
            }
            normalizedRows.Add(new CypherRow(cols, values));
        }

        return new CypherQueryResult(cols, normalizedRows);
    }

    public async Task<string> GetCallChainAsync(string startFunction, string endFunction, int maxDepth = 5, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var startResolved = await ResolveFunctionSymbolAsync(client, startFunction, cancellationToken);
        var endResolved = await ResolveFunctionSymbolAsync(client, endFunction, cancellationToken);

        var depth = Math.Max(1, Math.Min(10, maxDepth));
        var query = Queries.Get(QueryGetCallChain).Replace(DepthPlaceholder, depth.ToString());
        var parameters = new Dictionary<string, string>
        {
            ["startFunction"] = startResolved,
            ["endFunction"] = endResolved
        };
        var res = await client.ExecuteCypherAsync(query, parameters, cancellationToken);
        return ToonYamlSerializer.SerializeYaml(res);
    }

    private static async Task<string> ResolveFunctionSymbolAsync(IGraphClient client, string funcIdentifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(funcIdentifier)) return funcIdentifier;

        try
        {
            const string directSymQuery = "MATCH (n:Function) WHERE n.id = $id OR n.symbol = $id RETURN n.symbol AS symbol LIMIT 1";
            const string nameMatchesQuery = "MATCH (n:Function) WHERE n.name = $name OR n.graphql_field = $name OR n.graphql_operation = $name RETURN n.symbol AS symbol LIMIT 5";
            const string dotSymQuery = "MATCH (n:Function) WHERE (n.name = $mName OR n.symbol CONTAINS $mName) AND (n.symbol CONTAINS $cName OR n.file_path CONTAINS $cName) RETURN n.symbol AS symbol LIMIT 5";
            const string fuzzySymQuery = "MATCH (n:Function) WHERE n.name CONTAINS $name OR n.symbol CONTAINS $name RETURN n.symbol AS symbol LIMIT 5";

            // 1. Direct match by id or symbol
            var directSym = await client.QueryFirstOrDefaultAsync<string>(
                directSymQuery,
                new Dictionary<string, string> { ["id"] = funcIdentifier },
                cancellationToken);
            if (!string.IsNullOrEmpty(directSym)) return directSym;

            // 2. Exact match by name, graphql_field, or graphql_operation
            var nameMatches = await client.QueryAsync<string>(
                nameMatchesQuery,
                new Dictionary<string, string> { ["name"] = funcIdentifier },
                cancellationToken);
            if (nameMatches.Count == 1 && !string.IsNullOrEmpty(nameMatches[0]))
            {
                return nameMatches[0];
            }
            if (nameMatches.Count > 1)
            {
                foreach (var sym in nameMatches)
                {
                    if (!string.IsNullOrEmpty(sym) &&
                        (sym.EndsWith($":{funcIdentifier}", StringComparison.OrdinalIgnoreCase) ||
                         sym.EndsWith($".{funcIdentifier}", StringComparison.OrdinalIgnoreCase) ||
                         sym.EndsWith($":{funcIdentifier}.resolve", StringComparison.OrdinalIgnoreCase)))
                    {
                        return sym;
                    }
                }
                if (!string.IsNullOrEmpty(nameMatches[0])) return nameMatches[0];
            }

            // 3. Match qualified name like Class.Method or symbol suffix
            if (funcIdentifier.Contains('.'))
            {
                var dotMethod = funcIdentifier[(funcIdentifier.LastIndexOf('.') + 1)..];
                var dotClass = funcIdentifier[..funcIdentifier.LastIndexOf('.')];
                if (dotClass.Contains('.'))
                {
                    dotClass = dotClass[(dotClass.LastIndexOf('.') + 1)..];
                }
                var dotSym = await client.QueryFirstOrDefaultAsync<string>(
                    dotSymQuery,
                    new Dictionary<string, string> { ["mName"] = dotMethod, ["cName"] = dotClass },
                    cancellationToken);
                if (!string.IsNullOrEmpty(dotSym)) return dotSym;
            }

            // 4. Fallback search via contains on name or symbol
            var fuzzySym = await client.QueryFirstOrDefaultAsync<string>(
                fuzzySymQuery,
                new Dictionary<string, string> { ["name"] = funcIdentifier },
                cancellationToken);
            if (!string.IsNullOrEmpty(fuzzySym)) return fuzzySym;
        }
        catch
        {
            // Fallback to original identifier if resolution fails
        }

        return funcIdentifier;
    }

    public async Task<string> ResolveCallTargetAsync(string interfaceName, string methodName, string? workspacePath = null, CancellationToken cancellationToken = default, bool includeTests = false)
    {
        var query = Queries.Get(QueryResolveCallTarget);
        var parameters = new Dictionary<string, string>
        {
            ["interfaceName"] = interfaceName,
            ["methodName"] = methodName
        };
        var formatted = await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(formatted) && !string.Equals(formatted.Trim(), "[]", StringComparison.Ordinal))
        {
            return formatted;
        }

        var qualifiedCallers = await GetCallersAsync($"{interfaceName}.{methodName}", includeTests, workspacePath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(qualifiedCallers) && !string.Equals(qualifiedCallers.Trim(), "[]", StringComparison.Ordinal))
        {
            return qualifiedCallers;
        }

        return formatted;
    }

    public async Task<string> AnalyzeCodeImpactAsync(string symbolName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var wsRoot = !string.IsNullOrWhiteSpace(workspacePath) ? workspacePath : DefaultWorkspacePath;
        return await McpImpactFormatter.AnalyzeCodeImpactYamlAsync(client, symbolName, wsRoot, cancellationToken);
    }

    public async Task<string> SearchIdentifiersAsync(
        string query,
        string? kind = null,
        int limit = 15,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        if (!client.SupportsIdentifierSearch)
        {
            return "Search is only supported on SQLite workspaces.";
        }

        var fetchCount = Math.Clamp(limit * 2, 1, 300);
        var results = await client.SearchIdentifiersAsync(query, kind, fetchCount, cancellationToken);
        var wsRoot = !string.IsNullOrWhiteSpace(workspacePath) ? workspacePath : DefaultWorkspacePath;
        return McpResultFormatter.FormatHybridMatches(results, wsRoot, kind, Math.Clamp(limit, 1, 200));
    }

    public async Task<string> GetAffectedTestsAsync(
        string? filePath = null,
        string? symbolName = null,
        IReadOnlyList<string>? filePaths = null,
        string? gitDiff = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var changedFilesList = new List<string>();
        if (!string.IsNullOrWhiteSpace(filePath)) changedFilesList.Add(filePath);
        if (filePaths != null) changedFilesList.AddRange(filePaths);

        var symList = !string.IsNullOrWhiteSpace(symbolName) ? new[] { symbolName } : null;
        var wsRoot = !string.IsNullOrWhiteSpace(workspacePath) ? workspacePath : DefaultWorkspacePath;

        var req = new TestImpactRequest(
            ChangedFiles: changedFilesList.Count > 0 ? changedFilesList : null,
            GitDiff: gitDiff,
            SymbolNames: symList,
            WorkspaceRoot: wsRoot
        );

        var report = await client.AnalyzeTestImpactAsync(req, cancellationToken);
        if (report != null)
        {
            return McpResultFormatter.FormatAffectedTests(report, wsRoot);
        }

        var query = Queries.Get(Queries.QueryGetAffectedTests);
        var parameters = new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["symbolName"] = symbolName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    public async Task<string> GetTestCoverageAsync(
        string? projectName = null,
        string? pathPrefix = null,
        string? status = null,
        int? limit = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var wsRoot = !string.IsNullOrWhiteSpace(workspacePath) ? workspacePath : DefaultWorkspacePath;
        var report = await client.AnalyzeTestCoverageAsync(new TestCoverageFilter(projectName, pathPrefix, status, limit), cancellationToken);
        if (report != null)
        {
            return McpResultFormatter.FormatTestCoverage(report, wsRoot);
        }

        var query = Queries.Get(Queries.QueryGetTestCoverage);
        var parameters = new Dictionary<string, object?>
        {
            ["projectName"] = projectName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }
}
