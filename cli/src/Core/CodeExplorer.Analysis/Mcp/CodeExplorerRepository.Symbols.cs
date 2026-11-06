using System.Text.Json;
using CodeExplorer.Core.Analysis.Testing;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private static string FormatFindSymbol(CypherQueryResult res, string format, string name, int limit)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = res.Rows }, CompactJsonOptions);
        }

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeYaml(res);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(res);
        }

        if (res.Count == 0)
        {
            return $"No symbols found matching '{name}'.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("| Kind | Name | Symbol | File | Lines |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
        var count = 0;

        foreach (var item in res.Rows)
        {
            if (count++ >= limit) break;

            const string colFullName = "fullName";
            const string colFilePath = "filePath";
            var type = item.GetStringProp(ColType);
            var symName = item.GetStringProp(ColName);
            var fullName = item.GetStringProp(colFullName);
            var filePath = item.GetStringProp(colFilePath);
            var startLine = item.GetLongProp(ColStartLine);
            var endLine = item.GetLongProp(ColEndLine);

            var linesStr = startLine > 0
                ? (endLine > startLine ? $"L{startLine}-{endLine}" : $"L{startLine}")
                : "-";
            sb.AppendLine($"| {type} | `{symName}` | `{fullName}` | `{filePath}` | {linesStr} |");
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> FindSymbolAsync(string name, string? symbolType = null, string format = "markdown", int limit = 50, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
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

        return FormatFindSymbol(res, format, name, effectiveLimit);
    }

    private static string FormatCallChain(CypherQueryResult res, string format, string startFunction, string endFunction, int maxDepth)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = res.Rows }, CompactJsonOptions);
        }

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeYaml(res);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(res);
        }

        if (res.Count == 0)
        {
            return $"No call chain found between '{startFunction}' and '{endFunction}' within depth {maxDepth}.";
        }

        if (format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");
            var pathIndex = 0;

            foreach (var row in res.Rows)
            {
                if (row.TryGetProperty("chain", out var chainProp) && chainProp.Kind == CypherValueKind.Array)
                {
                    var nodes = chainProp.AsArray();

                    for (var i = 0; i < nodes.Count - 1; i++)
                    {
                        var fromName =
                            nodes[i].Kind == CypherValueKind.Object &&
                            nodes[i].TryGetProperty("name", out var fn)
                                ? fn.AsString()
                                : (nodes[i].Kind == CypherValueKind.String ? nodes[i].AsString() : $"Step{i}");

                        var toName =
                            nodes[i + 1].Kind == CypherValueKind.Object &&
                            nodes[i + 1].TryGetProperty("name", out var tn)
                                ? tn.AsString()
                                : (nodes[i + 1].Kind == CypherValueKind.String
                                    ? nodes[i + 1].AsString()
                                    : $"Step{i + 1}");
                        var fromId = $"p{pathIndex}_n{i}";
                        var toId = $"p{pathIndex}_n{i + 1}";
                        sb.AppendLine($"    {fromId}[\"{fromName}()\"] --> {toId}[\"{toName}()\"]");
                    }

                    pathIndex++;
                }
            }

            sb.Append("```");
            return sb.ToString();
        }

        // Markdown format
        var md = new System.Text.StringBuilder();
        md.AppendLine($"### Call Chain: `{startFunction}` → `{endFunction}`\n");
        var pIdx = 1;

        foreach (var row in res.Rows)
        {
            if (res.Count > 1)
            {
                md.AppendLine($"#### Path {pIdx++}:");
            }

            if (row.TryGetProperty("chain", out var chainProp) && chainProp.Kind == CypherValueKind.Array)
            {
                const string defaultFuncLabel = "Function";
                var step = 1;

                foreach (var node in chainProp.AsArray())
                {
                    if (node.Kind == CypherValueKind.Object)
                    {
                        var name = node.TryGetProperty(ColName, out var n) ? n.AsString(defaultFuncLabel) : defaultFuncLabel;
                        var symbol = node.TryGetProperty("symbol", out var s) ? s.AsNullableString() : null;
                        var file = node.TryGetProperty("file_path", out var f) ? f.AsNullableString() : null;
                        var line = node.TryGetProperty("start_line", out var l) ? l.AsNullableString() : null;

                        var loc = !string.IsNullOrEmpty(file)
                            ? $" *({file}{(line != null ? ":" + line : "")})*"
                            : "";
                        md.AppendLine($"{step++}. `{symbol ?? name}`{loc}");
                    }
                    else if (node.Kind == CypherValueKind.String)
                    {
                        var str = node.AsString(defaultFuncLabel);
                        md.AppendLine($"{step++}. `{str}`");
                    }
                }
            }

            md.AppendLine();
        }

        return md.ToString().TrimEnd();
    }

    public async Task<string> GetCallChainAsync(string startFunction, string endFunction, int maxDepth = 5, string format = "markdown", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
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
        return FormatCallChain(res, format, startFunction, endFunction, depth);
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

    public async Task<string> AnalyzeCodeImpactAsync(string symbolName, string format = "markdown", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
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

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = res.Rows }, CompactJsonOptions);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(res);
        }

        return FormatImpactMarkdown(res, symbolName);
    }

    public async Task<string> SearchIdentifiersAsync(
        string query,
        string? kind = null,
        int limit = 15,
        string format = "markdown",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        if (client is not SqliteGraphClient sqliteClient)
        {
            return "Search is only supported on SQLite workspaces.";
        }

        var engine = new Analysis.IdentifierSearchEngine();
        var results = await engine.SearchAsync(sqliteClient.Connection, query, kind, limit, ct: cancellationToken);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        }

        if (results.Count == 0)
        {
            return $"No identifiers found matching '{query}'.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"### Code Identifier Search Results for `{query}`\n");
        sb.AppendLine($"**Matches Found**: {results.Count}\n");
        sb.AppendLine("| Score | Kind | Name | Full Name / Route | Location (File:Line) |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");

        foreach (var r in results)
        {
            var loc = !string.IsNullOrEmpty(r.Path) ? (r.StartLine > 0 ? $"{r.Path}:{r.StartLine}" : r.Path) : "-";
            sb.AppendLine($"| {r.Score:F2} | `{r.Kind}` | `{r.Name}` | `{r.FullName}` | `{loc}` |");
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetCallersAsync(
        string target,
        string format = "markdown",
        bool includeTests = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var query = Queries.Get("get_callers");
        var parameters = new Dictionary<string, object?>
        {
            ["target"] = target,
            ["includeTests"] = includeTests
        };
        var res = await client.ExecuteCypherAsync(query, parameters, cancellationToken);

        if (res.Count == 0 && client is SqliteGraphClient sqliteClient)
        {
            try
            {
                var engine = new Analysis.IdentifierSearchEngine();
                var resolved = await engine.ResolveIdentifierAsync(sqliteClient.Connection, target, ct: cancellationToken);
                if (resolved != null && resolved.Score >= 0.70f && resolved.NodeId != target && resolved.Name != target)
                {
                    parameters["target"] = resolved.NodeId;
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

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = res.Rows }, CompactJsonOptions);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(res);
        }

        return FormatCallersMarkdown(res, target, includeTests);
    }

    private static string FormatCallersMarkdown(CypherQueryResult res, string target, bool includeTests)
    {
        if (res.Count == 0)
        {
            return $"No callers or incoming references found for '{target}'" + (includeTests ? "." : " (test callers excluded; pass --tests to include tests).");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"### Callers & Inbound References for `{target}`\n");
        sb.AppendLine($"**Total Inbound Callers**: {res.Count}\n");
        sb.AppendLine("| Target | Caller | Kind | Relation | Project / Service | Location (File:Line) |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- |");

        const string colTargetName = "targetName";
        const string colCallerName = "callerName";
        const string colCallerKind = "callerKind";
        const string colRelKind = "relKind";
        const string colProjectName = "projectName";
        const string colLocation = "location";

        foreach (var item in res.Rows)
        {
            var targetName = item.GetStringProp(colTargetName, target);
            var callerName = item.GetStringProp(colCallerName);
            var callerKind = item.GetStringProp(colCallerKind);
            var relKind = item.GetStringProp(colRelKind);
            var project = item.GetStringProp(colProjectName);
            var loc = item.GetStringProp(colLocation);

            sb.AppendLine($"| `{targetName}` | `{callerName}` | {callerKind} | {relKind} | {project} | `{loc}` |");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatImpactMarkdown(CypherQueryResult res, string symbolName)
    {
        if (res.Count == 0)
        {
            return $"No direct impact found for symbol '{symbolName}'. No callers, references, or dependent services identified.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"### Blast Radius & Impact Analysis for `{symbolName}`\n");
        sb.AppendLine($"**Total Dependents**: {res.Count}\n");
        sb.AppendLine("| Kind | Dependent | Relationship | Location (File:Line) | Symbol |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");

        const string colDependentType = "dependentType";
        const string colDependentName = "dependentName";
        const string colRelationship = "relationship";
        const string colFilePath = "filePath";
        const string colDependentSymbol = "dependentSymbol";

        foreach (var item in res.Rows)
        {
            var depType = item.GetStringProp(colDependentType);
            var depName = item.GetStringProp(colDependentName);
            var rel = item.GetStringProp(colRelationship);
            var loc = item.GetStringProp(colFilePath);
            var sym = item.GetStringProp(colDependentSymbol);

            sb.AppendLine($"| {depType} | `{depName}` | {rel} | `{loc}` | `{sym}` |");
        }

        return sb.ToString().TrimEnd();
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
            return TestIntelligenceService.FormatImpactMarkdown(report);
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
            return TestIntelligenceService.FormatCoverageMarkdown(report);
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
