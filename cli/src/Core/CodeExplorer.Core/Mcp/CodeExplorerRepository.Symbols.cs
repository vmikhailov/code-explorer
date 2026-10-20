using System.Text.Json;
using CodeExplorer.Core.Analysis.Testing;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Cypher.Parser;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private static string FormatFindSymbol(string rawJson, string format, string name, int limit)
    {
        using var doc = JsonDocument.Parse(rawJson);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = doc.RootElement }, CompactJsonOptions);
        }

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeYaml(doc.RootElement);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(doc.RootElement);
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return $"No symbols found matching '{name}'.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("| Kind | Name | Symbol | File | Lines |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
        var count = 0;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (count++ >= limit) break;

            var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            var symName = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var fullName = item.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "";
            var filePath = item.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";

            var startLine = item.TryGetProperty("startLine", out var sl) && sl.ValueKind == JsonValueKind.Number &&
                            sl.TryGetInt64(out var slVal)
                ? slVal
                : 0;

            var endLine = item.TryGetProperty("endLine", out var el) && el.ValueKind == JsonValueKind.Number &&
                          el.TryGetInt64(out var elVal)
                ? elVal
                : 0;

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

        var parameters = new Dictionary<string, object>
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
        var rawJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);

        if ((rawJson == "[]" || rawJson.StartsWith("[]")) && client is SqliteGraphClient sqliteClient)
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
                    var mapped = matches.Select(m => new
                    {
                        type = m.Kind,
                        name = m.Name,
                        fullName = m.FullName,
                        filePath = m.Path ?? "",
                        startLine = m.StartLine,
                        endLine = 0
                    });
                    rawJson = JsonSerializer.Serialize(mapped);
                }
            }
            catch
            {
                // Fallback gracefully on search error
            }
        }

        return FormatFindSymbol(rawJson, format, name, effectiveLimit);
    }

    private static string FormatCallChain(string rawJson, string format, string startFunction, string endFunction, int maxDepth)
    {
        using var doc = JsonDocument.Parse(rawJson);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { results = doc.RootElement }, CompactJsonOptions);
        }

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeYaml(doc.RootElement);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            return ToonYamlSerializer.SerializeToon(doc.RootElement);
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return $"No call chain found between '{startFunction}' and '{endFunction}' within depth {maxDepth}.";
        }

        if (format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");
            var pathIndex = 0;

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.TryGetProperty("chain", out var chainProp) && chainProp.ValueKind == JsonValueKind.Array)
                {
                    var nodes = chainProp.EnumerateArray().ToList();

                    for (var i = 0; i < nodes.Count - 1; i++)
                    {
                        var fromName =
                            nodes[i].ValueKind == JsonValueKind.Object &&
                            nodes[i].TryGetProperty("name", out var fn)
                                ? fn.GetString()
                                : (nodes[i].ValueKind == JsonValueKind.String ? nodes[i].GetString() : $"Step{i}");

                        var toName =
                            nodes[i + 1].ValueKind == JsonValueKind.Object &&
                            nodes[i + 1].TryGetProperty("name", out var tn)
                                ? tn.GetString()
                                : (nodes[i + 1].ValueKind == JsonValueKind.String
                                    ? nodes[i + 1].GetString()
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

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (doc.RootElement.GetArrayLength() > 1)
            {
                md.AppendLine($"#### Path {pIdx++}:");
            }

            if (row.TryGetProperty("chain", out var chainProp) && chainProp.ValueKind == JsonValueKind.Array)
            {
                var step = 1;

                foreach (var node in chainProp.EnumerateArray())
                {
                    if (node.ValueKind == JsonValueKind.Object)
                    {
                        var name = node.TryGetProperty("name", out var n) ? n.GetString() : "Function";
                        var symbol = node.TryGetProperty("symbol", out var s) ? s.GetString() : null;
                        var file = node.TryGetProperty("file_path", out var f) ? f.GetString() : null;
                        var line = node.TryGetProperty("start_line", out var l) ? l.ToString() : null;

                        var loc = !string.IsNullOrEmpty(file)
                            ? $" *({file}{(line != null ? ":" + line : "")})*"
                            : "";
                        md.AppendLine($"{step++}. `{symbol ?? name}`{loc}");
                    }
                    else if (node.ValueKind == JsonValueKind.String)
                    {
                        var str = node.GetString() ?? "Function";
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
        var parameters = new Dictionary<string, object>
        {
            ["startFunction"] = startResolved,
            ["endFunction"] = endResolved
        };
        var rawJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
        return FormatCallChain(rawJson, format, startFunction, endFunction, depth);
    }

    private static async Task<string> ResolveFunctionSymbolAsync(IGraphClient client, string funcIdentifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(funcIdentifier)) return funcIdentifier;

        try
        {
            // 1. Direct match by id or symbol
            var directRows = await client.ExecuteQueryAsync(
                "MATCH (n:Function) WHERE n.id = $id OR n.symbol = $id RETURN n.symbol AS symbol LIMIT 1",
                new Dictionary<string, object> { ["id"] = funcIdentifier },
                cancellationToken);
            using (var doc = JsonDocument.Parse(directRows))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    if (doc.RootElement[0].TryGetProperty("symbol", out var symProp))
                    {
                        var sym = symProp.GetString();
                        if (!string.IsNullOrEmpty(sym)) return sym;
                    }
                }
            }

            // 2. Exact match by name, graphql_field, or graphql_operation
            var nameRows = await client.ExecuteQueryAsync(
                "MATCH (n:Function) WHERE n.name = $name OR n.graphql_field = $name OR n.graphql_operation = $name RETURN n.symbol AS symbol LIMIT 5",
                new Dictionary<string, object> { ["name"] = funcIdentifier },
                cancellationToken);
            using (var doc = JsonDocument.Parse(nameRows))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var len = doc.RootElement.GetArrayLength();
                    if (len == 1)
                    {
                        if (doc.RootElement[0].TryGetProperty("symbol", out var symProp))
                        {
                            var sym = symProp.GetString();
                            if (!string.IsNullOrEmpty(sym)) return sym;
                        }
                    }
                    else if (len > 1)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            if (doc.RootElement[i].TryGetProperty("symbol", out var symProp))
                            {
                                var sym = symProp.GetString();
                                if (!string.IsNullOrEmpty(sym) &&
                                    (sym.EndsWith($":{funcIdentifier}", StringComparison.OrdinalIgnoreCase) ||
                                     sym.EndsWith($".{funcIdentifier}", StringComparison.OrdinalIgnoreCase) ||
                                     sym.EndsWith($":{funcIdentifier}.resolve", StringComparison.OrdinalIgnoreCase)))
                                {
                                    return sym;
                                }
                            }
                        }
                        if (doc.RootElement[0].TryGetProperty("symbol", out var firstProp))
                        {
                            var firstSym = firstProp.GetString();
                            if (!string.IsNullOrEmpty(firstSym)) return firstSym;
                        }
                    }
                }
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
                var dotRows = await client.ExecuteQueryAsync(
                    "MATCH (n:Function) WHERE (n.name = $mName OR n.symbol CONTAINS $mName) AND (n.symbol CONTAINS $cName OR n.file_path CONTAINS $cName) RETURN n.symbol AS symbol LIMIT 5",
                    new Dictionary<string, object> { ["mName"] = dotMethod, ["cName"] = dotClass },
                    cancellationToken);
                using (var doc = JsonDocument.Parse(dotRows))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        if (doc.RootElement[0].TryGetProperty("symbol", out var symProp))
                        {
                            var sym = symProp.GetString();
                            if (!string.IsNullOrEmpty(sym)) return sym;
                        }
                    }
                }
            }

            // 4. Fallback search via contains on name or symbol
            var fuzzyRows = await client.ExecuteQueryAsync(
                "MATCH (n:Function) WHERE n.name CONTAINS $name OR n.symbol CONTAINS $name RETURN n.symbol AS symbol LIMIT 5",
                new Dictionary<string, object> { ["name"] = funcIdentifier },
                cancellationToken);
            using (var doc = JsonDocument.Parse(fuzzyRows))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    if (doc.RootElement[0].TryGetProperty("symbol", out var symProp))
                    {
                        var sym = symProp.GetString();
                        if (!string.IsNullOrEmpty(sym)) return sym;
                    }
                }
            }
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
        var parameters = new Dictionary<string, object>
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
        var parameters = new Dictionary<string, object>
        {
            ["symbolName"] = symbolName
        };
        var rawJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);

        if ((rawJson == "[]" || rawJson.StartsWith("[]")) && client is SqliteGraphClient sqliteClient)
        {
            try
            {
                var engine = new Analysis.IdentifierSearchEngine();
                var resolved = await engine.ResolveIdentifierAsync(sqliteClient.Connection, symbolName, ct: cancellationToken);
                if (resolved != null && resolved.Score >= 0.70f && resolved.NodeId != symbolName && resolved.Name != symbolName)
                {
                    parameters["symbolName"] = resolved.NodeId;
                    var retryJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
                    if (retryJson != "[]" && !retryJson.StartsWith("[]"))
                    {
                        rawJson = retryJson;
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
            using var jsonDoc = JsonDocument.Parse(rawJson);
            return JsonSerializer.Serialize(new { results = jsonDoc.RootElement }, CompactJsonOptions);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            using var jsonDoc = JsonDocument.Parse(rawJson);
            return ToonYamlSerializer.SerializeToon(jsonDoc.RootElement);
        }

        return FormatImpactMarkdown(rawJson, symbolName);
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
        var parameters = new Dictionary<string, object>
        {
            ["target"] = target,
            ["includeTests"] = includeTests
        };
        var rawJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);

        if ((rawJson == "[]" || rawJson.StartsWith("[]")) && client is SqliteGraphClient sqliteClient)
        {
            try
            {
                var engine = new Analysis.IdentifierSearchEngine();
                var resolved = await engine.ResolveIdentifierAsync(sqliteClient.Connection, target, ct: cancellationToken);
                if (resolved != null && resolved.Score >= 0.70f && resolved.NodeId != target && resolved.Name != target)
                {
                    parameters["target"] = resolved.NodeId;
                    var retryJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
                    if (retryJson != "[]" && !retryJson.StartsWith("[]"))
                    {
                        rawJson = retryJson;
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
            using var jsonDoc = JsonDocument.Parse(rawJson);
            return JsonSerializer.Serialize(new { results = jsonDoc.RootElement }, CompactJsonOptions);
        }

        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
        {
            using var jsonDoc = JsonDocument.Parse(rawJson);
            return ToonYamlSerializer.SerializeToon(jsonDoc.RootElement);
        }

        return FormatCallersMarkdown(rawJson, target, includeTests);
    }

    private static string FormatCallersMarkdown(string rawJson, string target, bool includeTests)
    {
        using var doc = JsonDocument.Parse(rawJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return $"No callers or incoming references found for '{target}'" + (includeTests ? "." : " (test callers excluded; pass --tests to include tests).");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"### Callers & Inbound References for `{target}`\n");
        sb.AppendLine($"**Total Inbound Callers**: {doc.RootElement.GetArrayLength()}\n");
        sb.AppendLine("| Target | Caller | Kind | Relation | Project / Service | Location (File:Line) |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- |");

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var targetName = item.TryGetProperty("targetName", out var tn) ? tn.GetString() ?? "" : target;
            var callerName = item.TryGetProperty("callerName", out var cn) ? cn.GetString() ?? "" : "";
            var callerKind = item.TryGetProperty("callerKind", out var ck) ? ck.GetString() ?? "" : "";
            var relKind = item.TryGetProperty("relKind", out var rk) ? rk.GetString() ?? "" : "";
            var project = item.TryGetProperty("projectName", out var p) ? p.GetString() ?? "" : "";
            var loc = item.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "";

            sb.AppendLine($"| `{targetName}` | `{callerName}` | {callerKind} | {relKind} | {project} | `{loc}` |");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatImpactMarkdown(string rawJson, string symbolName)
    {
        using var doc = JsonDocument.Parse(rawJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return $"No direct impact found for symbol '{symbolName}'. No callers, references, or dependent services identified.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"### Blast Radius & Impact Analysis for `{symbolName}`\n");
        sb.AppendLine($"**Total Dependents**: {doc.RootElement.GetArrayLength()}\n");
        sb.AppendLine("| Kind | Dependent | Relationship | Location (File:Line) | Symbol |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var depType = item.TryGetProperty("dependentType", out var dt) ? dt.GetString() ?? "" : "";
            var depName = item.TryGetProperty("dependentName", out var dn) ? dn.GetString() ?? "" : "";
            var rel = item.TryGetProperty("relationship", out var r) ? r.GetString() ?? "" : "";
            var loc = item.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
            var sym = item.TryGetProperty("dependentSymbol", out var ds) ? ds.GetString() ?? "" : "";

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
