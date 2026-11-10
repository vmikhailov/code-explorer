using System.Text.Json;
using CodeExplorer.Core.Analysis.Testing;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    public async Task<string> FindSymbolAsync(string name, string? symbolType = null, int limit = 50, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var parameters = new Dictionary<string, string>
        {
            ["name"] = name
        };

        string query;
        if (symbolType == "Function")
        {
            query = Queries.Get("find_symbol_function");
        }
        else if (symbolType == "Class")
        {
            query = Queries.Get("find_symbol_class");
        }
        else if (symbolType == "Interface")
        {
            query = Queries.Get("find_symbol_interface");
        }
        else
        {
            query = Queries.Get("find_symbol_all");
        }

        var effectiveLimit = Math.Clamp(limit, 1, 500);
        query = query.Replace("LIMIT 10", $"LIMIT {effectiveLimit}");
        var res = await client.ExecuteCypherAsync(query, parameters, cancellationToken);

        if (res.Count == 0 && (string.IsNullOrEmpty(symbolType) || symbolType == "Function"))
        {
            var gqlQuery =
                "MATCH (n:Function) WHERE n.graphql_field CONTAINS $name OR n.graphql_operation CONTAINS $name " +
                "OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File) OPTIONAL MATCH (w:Workspace) " +
                "RETURN 'Function' AS type, n.name AS name, n.symbol AS fullName, " +
                "CASE WHEN f IS NOT NULL AND w IS NOT NULL THEN w.path + '/' + f.path ELSE coalesce(n.file_path, f.path) END AS filePath, " +
                $"coalesce(n.start_line, 0) AS startLine, coalesce(n.end_line, 0) AS endLine LIMIT {effectiveLimit}";
            res = await client.ExecuteCypherAsync(gqlQuery, parameters, cancellationToken);
        }

        if (res.Count == 0 && client is SqliteGraphClient sqliteClient)
        {
            try
            {
                var engine = new Analysis.IdentifierSearchEngine();
                var kindFilter = symbolType switch
                {
                    "Function" => "Function",
                    "Class" => "Type",
                    "Interface" => "Type",
                    _ => null
                };
                var matches = await engine.SearchAsync(sqliteClient.Connection, name, kindFilter, effectiveLimit, ct: cancellationToken);
                if (matches.Count > 0)
                {
                    string[] cols = ["type", "name", "fullName", "filePath", "startLine", "endLine"];
                    var rows = matches.Select(m => new CypherRow(cols,
                    [
                        CypherValue.FromString(m.Kind.ToString()),
                        CypherValue.FromString(m.Name),
                        CypherValue.FromString(m.FullName),
                        CypherValue.FromString(m.Path ?? ""),
                        CypherValue.FromInt64(m.StartLine),
                        CypherValue.FromInt64(0)
                    ])).ToList();
                    res = new CypherQueryResult(cols, rows);
                }
            }
            catch
            {
                // Fallback gracefully on search error
            }
        }

        return ToonYamlSerializer.SerializeYaml(res);
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
        var query = Queries.Get("get_call_chain").Replace("{depth}", depth.ToString());
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

    public async Task<string> ResolveCallTargetAsync(string interfaceName, string methodName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var query = Queries.Get("resolve_call_target");
        var parameters = new Dictionary<string, string>
        {
            ["interfaceName"] = interfaceName,
            ["methodName"] = methodName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    public async Task<string> AnalyzeCodeImpactAsync(string symbolName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var query = Queries.Get("analyze_code_impact");
        var parameters = new Dictionary<string, string>
        {
            ["symbolName"] = symbolName
        };
        var res = await client.ExecuteCypherAsync(query, parameters, cancellationToken);

        if (res.Count == 0 && client is SqliteGraphClient sqliteClient)
        {
            try
            {
                var engine = new Analysis.IdentifierSearchEngine();
                var resolved = await engine.ResolveIdentifierAsync(sqliteClient.Connection, symbolName, ct: cancellationToken);
                if (resolved != null && resolved.Score >= 0.70f && resolved.NodeId != symbolName && resolved.Name != symbolName)
                {
                    parameters["symbolName"] = resolved.NodeId;
                    var retryRes = await client.ExecuteCypherAsync(query, parameters, cancellationToken);
                    if (retryRes.Count > 0)
                    {
                        res = retryRes;
                    }
                }
            }
            catch
            {
                // Fallback gracefully
            }
        }

        return ToonYamlSerializer.SerializeYaml(res);
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

        if (client is not SqliteGraphClient sqliteClient)
        {
            return "Search is only supported on SQLite workspaces.";
        }

        var engine = new Analysis.IdentifierSearchEngine();
        var results = await engine.SearchAsync(sqliteClient.Connection, query, kind, limit, ct: cancellationToken);
        return ToonYamlSerializer.SerializeObject(results);
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
        if (client is SqliteGraphClient sqliteClient)
        {
            var changedFilesList = new List<string>();
            if (!string.IsNullOrWhiteSpace(filePath)) changedFilesList.Add(filePath);
            if (filePaths != null) changedFilesList.AddRange(filePaths);

            var symList = !string.IsNullOrWhiteSpace(symbolName) ? new[] { symbolName } : null;
            var wsRoot = !string.IsNullOrWhiteSpace(workspacePath) ? workspacePath : DefaultWorkspacePath;

            var service = new TestIntelligenceService(sqliteClient);
            var req = new TestImpactRequest(
                ChangedFiles: changedFilesList.Count > 0 ? changedFilesList : null,
                GitDiff: gitDiff,
                SymbolNames: symList,
                WorkspaceRoot: wsRoot
            );

            var report = await service.AnalyzeImpactAsync(req, cancellationToken);
            return ToonYamlSerializer.SerializeObject(report);
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
        if (client is SqliteGraphClient sqliteClient)
        {
            var service = new TestIntelligenceService(sqliteClient);
            var report = await service.AnalyzeCoverageAsync(new TestCoverageFilter(projectName, pathPrefix, status, limit), cancellationToken);
            return ToonYamlSerializer.SerializeObject(report);
        }

        var query = Queries.Get(Queries.QueryGetTestCoverage);
        var parameters = new Dictionary<string, object?>
        {
            ["projectName"] = projectName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }


    private static string ExtractCallee(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return "(unknown)";
        var idx = target.IndexOf('(');
        return idx > 0 ? target[..idx].Trim() : target.Trim();
    }
}
