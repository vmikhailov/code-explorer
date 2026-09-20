using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Diagrams;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Parser;

namespace CodeExplorer.Core.Mcp;

public class CodeExplorerRepository
{
    private static readonly JsonSerializerOptions CompactJsonOptions = new() { WriteIndented = false };
    private readonly ProjectQueryManager _queryManager;
    private readonly IGraphClient _defaultDbClient;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IGraphClient> _clientCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _macroCache = new(StringComparer.OrdinalIgnoreCase);

    public void InvalidateCache()
    {
        _macroCache.Clear();
    }

    public string? DefaultWorkspacePath { get; }

    public const string StandbyMessageMarkdown = "⚠️ **CodeExplorer Standby Mode**: No workspace is currently bound. Specify the `workspacePath` argument on this tool call, set the `WORKSPACE_ROOT` environment variable, or launch `ce mcp --root <path>`.";
    public const string StandbyMessageJson = "{\"status\":\"standby\",\"message\":\"CodeExplorer is running in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls, set the WORKSPACE_ROOT environment variable, or launch 'ce mcp --root <path>'.\"}";

    public static string GetStandbyMessage(string format)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase)) return StandbyMessageJson;
        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
            return "status: standby\nmessage: \"CodeExplorer is running in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls, set the WORKSPACE_ROOT environment variable, or launch 'ce mcp --root <path>'.\"";
        if (format.Equals("toon", StringComparison.OrdinalIgnoreCase))
            return "status: standby\nmessage: CodeExplorer is running in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls, set the WORKSPACE_ROOT environment variable, or launch 'ce mcp --root <path>'.";
        return StandbyMessageMarkdown;
    }

    public CodeExplorerRepository(
        IGraphClient dbClient,
        ProjectQueryManager? queryManager = null,
        string? defaultWorkspacePath = null)
    {
        _defaultDbClient = dbClient;
        _queryManager = queryManager ?? new();
        DefaultWorkspacePath = defaultWorkspacePath;
    }

    public async Task<IGraphClient> ResolveClientAsync(string? workspacePath)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            var ws = WorkspaceLocator.Find(workspacePath);
            if (ws != null && File.Exists(ws.DbPath))
            {
                return _clientCache.GetOrAdd(ws.DbPath, path => new SqliteGraphClient(path));
            }
        }
        return _defaultDbClient;
    }

    private async Task<bool> IsEmptyStandbyAsync(IGraphClient client)
    {
        if (client != _defaultDbClient) return false;
        if (!string.IsNullOrWhiteSpace(DefaultWorkspacePath)) return false;
        try
        {
            var testJson = await client.ExecuteQueryAsync("MATCH (n) RETURN count(n) AS cnt LIMIT 1;");
            using var doc = JsonDocument.Parse(testJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0 && doc.RootElement[0].TryGetProperty("cnt", out var pCnt))
            {
                return pCnt.GetInt64() == 0;
            }
        }
        catch { }
        return false;
    }

    private Task<string> ExecuteAndFormatQueryAsync(string query, object? parameters, CancellationToken cancellationToken)
        => ExecuteAndFormatQueryAsync(query, parameters, null, cancellationToken);

    private async Task<string> ExecuteAndFormatQueryAsync(string query, object? parameters = null, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageJson;
        }

        var resultJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
        using var doc = JsonDocument.Parse(resultJson);

        return JsonSerializer.Serialize(new { results = doc.RootElement }, CompactJsonOptions);
    }



    public async Task ClearAllAsync()
    {
        InvalidateCache();
        await _defaultDbClient.ClearDatabaseAsync();
    }

    public async Task<bool> ClearWorkspaceAsync(string workspaceIdOrPath)
    {
        InvalidateCache();
        return await _defaultDbClient.ClearWorkspaceAsync(workspaceIdOrPath);
    }

    public async Task<(List<string> Cleared, List<string> NotFound)> ClearWorkspacesAsync(IEnumerable<string> workspaces)
    {
        InvalidateCache();
        var cleared = new List<string>();
        var notFound = new List<string>();
        foreach (var ws in workspaces)
        {
            if (await _defaultDbClient.ClearWorkspaceAsync(ws))
            {
                cleared.Add(ws);
            }
            else
            {
                notFound.Add(ws);
            }
        }
        return (cleared, notFound);
    }

    public async Task<string> GetArchitectureOverviewAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageJson;
        }

        var overviewQuery = Queries.Get("get_architecture_overview");
        var rawJson = await client.ExecuteQueryAsync(overviewQuery, null, cancellationToken);

        long totalNodes = 0;
        long totalFiles = 0;
        long totalRels = 0;

        try
        {
            var nodesCountJson = await client.ExecuteQueryAsync("MATCH (n) RETURN count(n) AS cnt;", null, cancellationToken);
            using var doc = JsonDocument.Parse(nodesCountJson);
            if (doc.RootElement.GetArrayLength() > 0 && doc.RootElement[0].TryGetProperty("cnt", out var pCnt))
                totalNodes = pCnt.GetInt64();
        }
        catch { }

        try
        {
            var filesCountJson = await client.ExecuteQueryAsync("MATCH (f:File) RETURN count(f) AS cnt;", null, cancellationToken);
            using var doc = JsonDocument.Parse(filesCountJson);
            if (doc.RootElement.GetArrayLength() > 0 && doc.RootElement[0].TryGetProperty("cnt", out var pCnt))
                totalFiles = pCnt.GetInt64();
        }
        catch { }

        try
        {
            var relsCountJson = await client.ExecuteQueryAsync("MATCH (p1:Project)-[:DEPENDS_ON]->(p2:Project) RETURN count(*) AS cnt;", null, cancellationToken);
            using var doc = JsonDocument.Parse(relsCountJson);
            if (doc.RootElement.GetArrayLength() > 0 && doc.RootElement[0].TryGetProperty("cnt", out var pCnt))
                totalRels = pCnt.GetInt64();
        }
        catch { }

        using var overviewDoc = JsonDocument.Parse(rawJson);
        if (overviewDoc.RootElement.ValueKind != JsonValueKind.Array || overviewDoc.RootElement.GetArrayLength() == 0)
        {
            return rawJson;
        }

        var row = overviewDoc.RootElement[0];
        var wsName = row.TryGetProperty("workspace", out var wProp) ? wProp.GetString() : "workspace";
        var wsPath = row.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : "";

        var rawProjects = new List<Dictionary<string, string>>();
        if (row.TryGetProperty("projects", out var projProp) && projProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in projProp.EnumerateArray())
            {
                var pName = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var pLang = p.TryGetProperty("language", out var l) ? l.GetString() ?? "" : "";
                var pPath = p.TryGetProperty("path", out var pt) ? pt.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(pName))
                {
                    rawProjects.Add(new Dictionary<string, string>
                    {
                        ["name"] = pName,
                        ["language"] = pLang,
                        ["path"] = pPath,
                        ["layer"] = ClassifyProjectLayer(pName, pPath)
                    });
                }
            }
        }

        var layerOrder = new[] { "Core / Domain", "Services / Backend", "UI / Presentation", "Infrastructure / Data", "Other", "Tests" };
        var groupedLayers = layerOrder
            .Select(layer =>
            {
                var items = rawProjects.Where(p => p["layer"] == layer).Select(p => new { name = p["name"], language = p["language"], path = p["path"] }).ToList();
                return new
                {
                    layer,
                    count = items.Count,
                    projects = items
                };
            })
            .Where(g => g.count > 0)
            .ToList();

        var databases = new List<string>();
        if (row.TryGetProperty("databases", out var dbProp) && dbProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var db in dbProp.EnumerateArray())
            {
                var dbStr = db.GetString();
                if (!string.IsNullOrEmpty(dbStr)) databases.Add(dbStr);
            }
        }

        var externalServices = new List<string>();
        if (row.TryGetProperty("externalServices", out var esProp) && esProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var es in esProp.EnumerateArray())
            {
                var esStr = es.GetString();
                if (!string.IsNullOrEmpty(esStr)) externalServices.Add(esStr);
            }
        }

        var overviewResult = new
        {
            workspace = wsName,
            path = wsPath,
            stats = new
            {
                total_projects = rawProjects.Count,
                total_files = totalFiles,
                total_nodes = totalNodes,
                total_project_dependencies = totalRels
            },
            layers = groupedLayers,
            databases = databases.Distinct().OrderBy(x => x).ToList(),
            external_services = externalServices.Distinct().OrderBy(x => x).ToList()
        };

        return JsonSerializer.Serialize(new { results = overviewResult }, CompactJsonOptions);
    }

    private static string ClassifyProjectLayer(string name, string path)
    {
        var combined = (name + " " + path).ToLowerInvariant();
        if (combined.Contains("test") || combined.Contains("mock") || combined.Contains("spec"))
            return "Tests";
        if (combined.Contains("ui") || combined.Contains("web") || combined.Contains("api") ||
            combined.Contains("client") || combined.Contains("frontend") || combined.Contains("cli"))
            return "UI / Presentation";
        if (combined.Contains("core") || combined.Contains("domain") || combined.Contains("common") ||
            combined.Contains("shared") || combined.Contains("model") || combined.Contains("entity"))
            return "Core / Domain";
        if (combined.Contains("service") || combined.Contains("server") || combined.Contains("backend") ||
            combined.Contains("worker") || combined.Contains("job") || combined.Contains("consumer"))
            return "Services / Backend";
        if (combined.Contains("infra") || combined.Contains("data") || combined.Contains("db") ||
            combined.Contains("database") || combined.Contains("persistence") || combined.Contains("sql") ||
            combined.Contains("repository"))
            return "Infrastructure / Data";
        return "Other";
    }

    public async Task<string> GetArchitectureMapAsync(string? projectName = null, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"arch:{workspacePath ?? ""}:{projectName ?? ""}";
        if (_macroCache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        string resultJson;
        if (!string.IsNullOrEmpty(projectName))
        {
            var query = Queries.Get("get_architecture_map_project");
            resultJson = await ExecuteAndFormatQueryAsync(query, new Dictionary<string, object> { ["projectName"] = projectName }, workspacePath, cancellationToken);
        }
        else
        {
            resultJson = await ExecuteAndFormatQueryAsync(Queries.Get("get_architecture_map_workspace"), new Dictionary<string, object>(), workspacePath, cancellationToken);
        }

        try
        {
            var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
            var savedQueries = _queryManager.ListQueries(effectiveWsPath);
            if (savedQueries.Count > 0)
            {
                using var doc = JsonDocument.Parse(resultJson);
                var formatted = JsonSerializer.Serialize(new
                {
                    results = doc.RootElement.GetProperty("results"),
                    saved_project_queries = savedQueries.Select(q => new
                    {
                        name = q.Name,
                        description = q.Description,
                        parameters = q.Parameters,
                        returns = q.Returns,
                        tags = q.Tags
                    })
                }, CompactJsonOptions);
                _macroCache[cacheKey] = formatted;
                return formatted;
            }
        }
        catch
        {
            // Do not fail architecture map if project query listing fails
        }

        _macroCache[cacheKey] = resultJson;
        return resultJson;
    }

    private static string SanitizeMermaidId(string text)
    {
        return text.Replace(".", "_").Replace("-", "_").Replace(":", "_").Replace("/", "_").Replace(" ", "_");
    }

    private static string FormatDependenciesAll(string rawJson, string format, int limit)
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

        if (format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");
            var count = 0;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (count++ >= limit) break;
                    var proj = item.TryGetProperty("project", out var p) ? p.GetString() : null;
                    var dep = item.TryGetProperty("dependency", out var d) ? d.GetString() : null;
                    if (!string.IsNullOrEmpty(proj) && !string.IsNullOrEmpty(dep))
                    {
                        sb.AppendLine($"    {SanitizeMermaidId(proj)}[\"{proj}\"] --> {SanitizeMermaidId(dep)}[\"{dep}\"]");
                    }
                }
            }
            if (count == 0)
            {
                sb.AppendLine("    %% No project dependencies found");
            }
            sb.Append("```");
            return sb.ToString();
        }

        // Markdown format (default)
        var md = new System.Text.StringBuilder();
        md.AppendLine("### Project Dependencies\n");
        var grouped = new Dictionary<string, List<string>>();
        var total = 0;
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (total++ >= limit) break;
                var proj = item.TryGetProperty("project", out var p) ? p.GetString() : null;
                var dep = item.TryGetProperty("dependency", out var d) ? d.GetString() : null;
                if (!string.IsNullOrEmpty(proj) && !string.IsNullOrEmpty(dep))
                {
                    if (!grouped.TryGetValue(proj, out var list))
                    {
                        list = [];
                        grouped[proj] = list;
                    }
                    list.Add(dep);
                }
            }
        }

        if (grouped.Count == 0)
        {
            md.AppendLine("No project dependencies found in workspace.");
        }
        else
        {
            foreach (var (proj, deps) in grouped)
            {
                md.AppendLine($"- **{proj}** -> {string.Join(", ", deps)}");
            }
        }
        return md.ToString().TrimEnd();
    }

    private static string FormatDependenciesFiltered(string rawJson, string format, string projectFilter)
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

        var outgoing = new List<string>();
        var incoming = new List<string>();
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            var row = doc.RootElement[0];
            if (row.TryGetProperty("outgoingDependencies", out var outProp) && outProp.ValueKind == JsonValueKind.Array)
            {
                outgoing = outProp.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
            }
            if (row.TryGetProperty("incomingDependencies", out var inProp) && inProp.ValueKind == JsonValueKind.Array)
            {
                incoming = inProp.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
            }
        }

        if (format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");
            var pId = SanitizeMermaidId(projectFilter);
            foreach (var o in outgoing)
            {
                sb.AppendLine($"    {pId}[\"{projectFilter}\"] --> {SanitizeMermaidId(o)}[\"{o}\"]");
            }
            foreach (var i in incoming)
            {
                sb.AppendLine($"    {SanitizeMermaidId(i)}[\"{i}\"] --> {pId}[\"{projectFilter}\"]");
            }
            if (outgoing.Count == 0 && incoming.Count == 0)
            {
                sb.AppendLine($"    {pId}[\"{projectFilter}\"]");
                sb.AppendLine("    %% No connected dependencies");
            }
            sb.Append("```");
            return sb.ToString();
        }

        // Markdown format
        var md = new System.Text.StringBuilder();
        md.AppendLine($"### Dependencies for Project: `{projectFilter}`\n");
        md.AppendLine($"- **Outgoing Dependencies** ({outgoing.Count}): {(outgoing.Count > 0 ? string.Join(", ", outgoing) : "none")}");
        md.AppendLine($"- **Incoming Dependencies** ({incoming.Count}): {(incoming.Count > 0 ? string.Join(", ", incoming) : "none")}");
        return md.ToString().TrimEnd();
    }

    public async Task<string> GetProjectDependenciesAsync(string? projectFilter = null, string format = "markdown", int limit = 50, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        if (!string.IsNullOrEmpty(projectFilter))
        {
            var query = Queries.Get("get_project_dependencies_filtered");
            var rawJson = await client.ExecuteQueryAsync(query, new Dictionary<string, object> { ["projectFilter"] = projectFilter }, cancellationToken);
            return FormatDependenciesFiltered(rawJson, format, projectFilter);
        }
        else
        {
            var query = Queries.Get("get_project_dependencies_all");
            var rawJson = await client.ExecuteQueryAsync(query, new Dictionary<string, object>(), cancellationToken);
            return FormatDependenciesAll(rawJson, format, limit);
        }
    }

    private static string FormatFileOutline(string rawJson, string format, string filePath)
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
            return $"No symbols found in outline for '{filePath}'.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"### File Outline: `{filePath}`\n");
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var type = item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "Symbol" : "Symbol";
            var name = item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            var startLine = item.TryGetProperty("startLine", out var sl) && sl.ValueKind == JsonValueKind.Number && sl.TryGetInt64(out var slVal) ? slVal : 0;
            var endLine = item.TryGetProperty("endLine", out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var elVal) ? elVal : 0;

            var linesStr = startLine > 0 ? (endLine > startLine ? $" (L{startLine}-{endLine})" : $" (L{startLine})") : "";
            sb.AppendLine($"- **{type}** `{name}`{linesStr}");
        }
        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetFileOutlineAsync(string filePath, string format = "markdown", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var normalizedPath = filePath.Trim().Replace('\\', '/').TrimStart('/');
        var query = Queries.Get("get_file_outline");
        var parameters = new Dictionary<string, object>
        {
            ["filePath"] = normalizedPath
        };
        var rawJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
        return FormatFileOutline(rawJson, format, normalizedPath);
    }

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
            var startLine = item.TryGetProperty("startLine", out var sl) && sl.ValueKind == JsonValueKind.Number && sl.TryGetInt64(out var slVal) ? slVal : 0;
            var endLine = item.TryGetProperty("endLine", out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var elVal) ? elVal : 0;

            var linesStr = startLine > 0 ? (endLine > startLine ? $"L{startLine}-{endLine}" : $"L{startLine}") : "-";
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
                        var fromName = nodes[i].TryGetProperty("name", out var fn) ? fn.GetString() : $"Step{i}";
                        var toName = nodes[i + 1].TryGetProperty("name", out var tn) ? tn.GetString() : $"Step{i + 1}";
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
                    var name = node.TryGetProperty("name", out var n) ? n.GetString() : "Function";
                    var symbol = node.TryGetProperty("symbol", out var s) ? s.GetString() : null;
                    var file = node.TryGetProperty("file_path", out var f) ? f.GetString() : null;
                    var line = node.TryGetProperty("start_line", out var l) ? l.ToString() : null;

                    var loc = !string.IsNullOrEmpty(file) ? $" *({file}{(line != null ? ":" + line : "")})*" : "";
                    md.AppendLine($"{step++}. `{symbol ?? name}`{loc}");
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

        var depth = Math.Max(1, Math.Min(10, maxDepth));
        var query = Queries.Get("get_call_chain").Replace("{depth}", depth.ToString());
        var parameters = new Dictionary<string, object>
        {
            ["startFunction"] = startFunction,
            ["endFunction"] = endFunction
        };
        var rawJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
        return FormatCallChain(rawJson, format, startFunction, endFunction, depth);
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

    public async Task<string> AnalyzeCodeImpactAsync(string symbolName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var query = Queries.Get("analyze_code_impact");
        var parameters = new Dictionary<string, object>
        {
            ["symbolName"] = symbolName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    public async Task<string> InspectDataLineageAsync(string tableName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var query = Queries.Get("inspect_data_lineage");
        var parameters = new Dictionary<string, object>
        {
            ["tableName"] = tableName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    public async Task<string> GetProjectEntryPointsAsync(string projectName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var query = Queries.Get("get_project_entry_points");
        var parameters = new Dictionary<string, object>
        {
            ["projectName"] = projectName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    private async Task AppendMetricResultsAsync(List<object> results, string queryKey, Dictionary<string, object> parameters, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var query = Queries.Get(queryKey);
        var res = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
        using var doc = JsonDocument.Parse(res);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            results.Add(item.Clone());
        }
    }

    public async Task<string> FindRefactoringOpportunitiesAsync(string projectName, string metricType = "all", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var results = new List<object>();
        var parameters = new Dictionary<string, object> { ["projectName"] = projectName };

        if (metricType is "dead_code" or "all")
            await AppendMetricResultsAsync(results, "find_refactor_dead_code", parameters, workspacePath, cancellationToken);

        if (metricType is "god_objects" or "all")
            await AppendMetricResultsAsync(results, "find_refactor_god_objects", parameters, workspacePath, cancellationToken);

        return JsonSerializer.Serialize(new { results }, CompactJsonOptions);
    }

    public async Task<string> ExecuteCustomReadCypherAsync(string query, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        CypherSecurityValidator.ValidateReadOnly(query);
        return await ExecuteAndFormatQueryAsync(query, new Dictionary<string, object?>(), workspacePath, cancellationToken);
    }

    public async Task<string> GetWorkspaceContentAsync(string? workspacePath = null, string? type = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var parameters = new Dictionary<string, object?>
        {
            ["type"] = string.IsNullOrEmpty(type) ? null : type
        };
        var query = Queries.Get("get_workspace_content");
        return await client.ExecuteQueryAsync(query, parameters, cancellationToken);
    }

    public async Task<string> ExecuteRawQueryAsync(string query, Dictionary<string, object?>? parameters = null, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        return await client.ExecuteQueryAsync(query, parameters, cancellationToken);
    }

    public async Task<string> GetTaxonomyAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"taxonomy:{workspacePath ?? ""}";
        if (_macroCache.TryGetValue(cacheKey, out var cachedTaxonomy))
        {
            return cachedTaxonomy;
        }

        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageJson;
        }

        var parameters = new Dictionary<string, object?>();
        var query = Queries.Get("get_taxonomy_nodes");
        var propQuery = Queries.Get("get_taxonomy_properties");

        var resultJson = await client.ExecuteQueryAsync(query, parameters, cancellationToken);
        var parsedTriplets = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(resultJson) ?? [];

        var propJson = await client.ExecuteQueryAsync(propQuery, parameters, cancellationToken);
        var parsedProperties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(propJson) ?? [];

        var taxonomy = BuildTaxonomy(parsedTriplets, parsedProperties);
        var finalJson = JsonSerializer.Serialize(new { taxonomy }, CompactJsonOptions);
        _macroCache[cacheKey] = finalJson;
        return finalJson;
    }

    public async Task<string> FetchCodeSnippetsAsync(string nodesJson, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        return await FetchCodeSnippetsDirectlyAsync(nodesJson, workspacePath);
    }

    public string GetNodeDefinition(string kind)
    {
        return OntologyRegistry.GetNodeDefinition(kind);
    }
    private static List<McpRAGNode>? ParseRagNodes(string nodesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<McpRAGNode>>(nodesJson);
        }
        catch
        {
            try
            {
                var single = JsonSerializer.Deserialize<McpRAGNode>(nodesJson);
                if (single != null) return [single];
            }
            catch
            {
                var nestedNodes = JsonSerializer.Deserialize<List<NestedMcpRAGNode>>(nodesJson);
                return nestedNodes?.Where(n => n.props != null).Select(n => n.props!).ToList();
            }
        }
        return null;
    }

    private static string? ResolveWorkspaceRoot(string? hostWorkspacePath)
    {
        var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
        if (!string.IsNullOrEmpty(workspaceRoot)) return workspaceRoot;

        if (!string.IsNullOrEmpty(hostWorkspacePath)) return hostWorkspacePath;

        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "CodeExplorer.slnx")) || File.Exists(Path.Combine(current, "CodeExplorer.sln")))
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    private static string FormatCodeSnippet(string[] lines, McpRAGNode node)
    {
        var sIdx = Math.Min(Math.Max(0, node.start_line!.Value), lines.Length);
        var eIdx = Math.Max(sIdx, Math.Min(lines.Length, node.end_line!.Value + 1));
        var snippet = string.Join("\n", lines.Skip(sIdx).Take(eIdx - sIdx));

        var ext = Path.GetExtension(node.file_path!).ToLower().TrimStart('.');
        var lang = ext switch
        {
            "ts" or "tsx" => "typescript",
            "js" or "jsx" => "javascript",
            "cs" => "csharp",
            _ => ext
        };

        return $"### File: `{node.file_path}` (Lines {sIdx + 1}-{eIdx})\n```{lang}\n{snippet}\n```";
    }

    private static async Task<string> ExtractFileSnippetAsync(McpRAGNode node, string? workspaceRoot, string? hostWorkspacePath)
    {
        var relativePath = PathTools.GetRelativePath(node.file_path!, hostWorkspacePath);
        var joinedPath = Path.Combine(workspaceRoot ?? "", relativePath);
        var absPath = Path.GetFullPath(joinedPath);
        var absRoot = Path.GetFullPath(workspaceRoot ?? "");

        if (!absPath.StartsWith(absRoot))
            return $"### Access Denied: `{node.file_path}` is outside the workspace root.";

        if (!File.Exists(absPath))
            return $"### File Not Found: `{node.file_path}`";

        try
        {
            var lines = await File.ReadAllLinesAsync(absPath);
            return FormatCodeSnippet(lines, node);
        }
        catch (Exception ex)
        {
            return $"### Error reading `{node.file_path}`: {ex.Message}";
        }
    }

    private async Task<string> FetchCodeSnippetsDirectlyAsync(string nodesJson, string? hostWorkspacePath)
    {
        List<McpRAGNode>? nodes;
        try
        {
            nodes = ParseRagNodes(nodesJson);
        }
        catch (Exception ex)
        {
            return $"Error parsing nodes JSON: {ex.Message}";
        }

        if (nodes == null || nodes.Count == 0) return "No valid code contexts retrieved.";

        var workspaceRoot = ResolveWorkspaceRoot(hostWorkspacePath);
        var output = new List<string>();

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.file_path) || node.start_line == null || node.end_line == null)
                continue;

            output.Add(await ExtractFileSnippetAsync(node, workspaceRoot, hostWorkspacePath));
        }

        return output.Count == 0 ? "No valid code contexts retrieved." : string.Join("\n\n", output);
    }

    private static void PopulateTaxonomyProperties(
        Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing, HashSet<(string relationship, string source)> incoming)> nodes,
        List<Dictionary<string, string>> properties)
    {
        foreach (var prop in properties)
        {
            if (prop.TryGetValue("label", out var label) && prop.TryGetValue("key", out var key))
            {
                if (!nodes.ContainsKey(label)) nodes[label] = ([], [], []);
                nodes[label].properties.Add(key);
            }
        }
    }

    private static void PopulateTaxonomyTriplets(
        Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing, HashSet<(string relationship, string source)> incoming)> nodes,
        List<Dictionary<string, string>> triplets)
    {
        foreach (var triplet in triplets)
        {
            if (triplet.TryGetValue("fromLabel", out var from) && triplet.TryGetValue("relType", out var rel) &&
                triplet.TryGetValue("toLabel", out var to))
            {
                if (!nodes.ContainsKey(from)) nodes[from] = ([], [], []);
                if (!nodes.ContainsKey(to)) nodes[to] = ([], [], []);

                nodes[from].outgoing.Add((rel, to));
                nodes[to].incoming.Add((rel, from));
            }
        }
    }

    public static object BuildTaxonomy(
        List<Dictionary<string, string>> triplets,
        List<Dictionary<string, string>> properties)
    {
        var nodes = new Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing,
            HashSet<(string relationship, string source)> incoming)>(StringComparer.OrdinalIgnoreCase);

        PopulateTaxonomyProperties(nodes, properties);
        PopulateTaxonomyTriplets(nodes, triplets);

        return nodes.OrderBy(k => k.Key).Select(kvp => new
        {
            label = kvp.Key,
            properties = kvp.Value.properties.OrderBy(p => p).ToList(),
            outgoing = kvp.Value.outgoing.OrderBy(x => x.relationship).Select(x => new { x.relationship, x.target }).ToList(),
            incoming = kvp.Value.incoming.OrderBy(x => x.relationship).Select(x => new { x.relationship, x.source }).ToList()
        }).ToList();
    }

    public async Task<string> ResolveWorkspacePathAsync(string? workspacePath)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            return Path.GetFullPath(workspacePath);
        }

        if (!string.IsNullOrWhiteSpace(DefaultWorkspacePath))
        {
            return Path.GetFullPath(DefaultWorkspacePath);
        }

        var found = WorkspaceLocator.Find();
        if (found != null)
        {
            return found.RootDirectory;
        }

        try
        {
            var allResult = await _defaultDbClient.ExecuteQueryAsync("MATCH (w:Workspace) RETURN w.path AS path LIMIT 1;");
            using var allDoc = JsonDocument.Parse(allResult);
            if (allDoc.RootElement.ValueKind == JsonValueKind.Array && allDoc.RootElement.GetArrayLength() > 0)
            {
                var row = allDoc.RootElement[0];
                if (row.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                {
                    var p = pathProp.GetString();
                    if (!string.IsNullOrEmpty(p)) return Path.GetFullPath(p);
                }
            }
        }
        catch
        {
            // Fallback
        }

        return Directory.GetCurrentDirectory();
    }

    public async Task<string> ListProjectQueriesAsync(string? workspacePath, CancellationToken cancellationToken = default)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
        var queries = _queryManager.ListQueries(effectiveWsPath);
        return JsonSerializer.Serialize(new
        {
            workspace_path = effectiveWsPath,
            queries_count = queries.Count,
            queries = queries.Select(q => new
            {
                name = q.Name,
                description = q.Description,
                parameters = q.Parameters,
                returns = q.Returns,
                tags = q.Tags
            })
        }, CompactJsonOptions);
    }

    public async Task<string> SaveProjectQueryAsync(
        string name,
        string description,
        string cypher,
        string? parametersJson,
        string? returns,
        string? tags,
        string? workspacePath,
        CancellationToken cancellationToken = default)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);

        var paramList = new List<ProjectQueryParameter>();
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<ProjectQueryParameter>>(parametersJson);
                if (parsed != null) paramList = parsed;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parametersJson: {ex.Message}", nameof(parametersJson), ex);
            }
        }

        var tagList = string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var metadata = new ProjectQueryMetadata
        {
            Name = name,
            Description = description,
            Parameters = paramList,
            Returns = returns,
            Tags = tagList
        };

        var saved = _queryManager.SaveQuery(effectiveWsPath, name, cypher, metadata);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Project query '{saved.Name}' successfully validated and saved to .codeexplorer/queries/",
            query = new
            {
                name = saved.Name,
                description = saved.Description,
                parameters = saved.Parameters,
                returns = saved.Returns,
                tags = saved.Tags,
                cypher_file = saved.CypherPath,
                metadata_file = saved.MetadataPath
            }
        }, CompactJsonOptions);
    }

    public async Task<string> ExecuteProjectQueryAsync(string name, string? parametersJson, string? workspacePath, CancellationToken cancellationToken = default)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
        var queryItem = _queryManager.GetQuery(effectiveWsPath, name);
        if (queryItem == null)
        {
            throw new FileNotFoundException(
                $"Project query '{name}' not found in '{_queryManager.GetQueriesDirectory(effectiveWsPath)}'. " +
                "Use 'list_project_queries' to see available queries or 'save_project_query' to register a new one.");
        }

        var paramDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(parametersJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        paramDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parametersJson: {ex.Message}", nameof(parametersJson), ex);
            }
        }

        foreach (var p in queryItem.Parameters)
        {
            if (p.Required && (!paramDict.ContainsKey(p.Name) || paramDict[p.Name] == null))
            {
                throw new ArgumentException($"Required parameter '{p.Name}' is missing for query '{name}'.");
            }
        }

        if (queryItem.Cypher.Contains("$workspaceId") && !paramDict.ContainsKey("workspaceId"))
        {
            paramDict["workspaceId"] = "workspace";
        }

        if (queryItem.Cypher.Contains("$workspaceIdPrefix") && !paramDict.ContainsKey("workspaceIdPrefix"))
        {
            paramDict["workspaceIdPrefix"] = "workspace:";
        }

        return await ExecuteAndFormatQueryAsync(queryItem.Cypher, paramDict, workspacePath, cancellationToken);
    }

    public async Task<string> ExportArchitectureDiagramAsync(
        string format = "mermaid",
        string type = "architecture",
        string? projectFilter = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        return await DiagramExporter.ExportAsync(client, format, type, projectFilter, cancellationToken);
    }

    public async Task<string> InitWorkspaceAsync(
        string? name = null,
        string? workspacePath = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var wsName = string.IsNullOrWhiteSpace(name) ? new DirectoryInfo(targetDir).Name : name.Trim();

        var existing = WorkspaceLocator.Find(targetDir);
        if (existing != null && existing.RootDirectory.Equals(targetDir, StringComparison.OrdinalIgnoreCase) && !force)
        {
            return JsonSerializer.Serialize(new
            {
                status = "exists",
                message = $"Workspace already exists at '{existing.RootDirectory}'. Set force=true to reinitialize.",
                workspacePath = existing.RootDirectory,
                dbPath = existing.DbPath
            }, CompactJsonOptions);
        }

        var ws = WorkspaceLocator.Initialize(targetDir, wsName);
        await using (var client = new SqliteGraphClient(ws.DbPath))
        {
            await client.ExecuteWriteAsync(
                "INSERT INTO nodes (id, kind, properties) VALUES ('workspace', 'Workspace', json_object('id', 'workspace', 'name', @name, 'path', @path)) " +
                "ON CONFLICT(id) DO UPDATE SET properties = json_object('id', 'workspace', 'name', @name, 'path', @path);",
                new Dictionary<string, object?> { ["name"] = wsName, ["path"] = ws.RootDirectory }, cancellationToken);
        }

        return JsonSerializer.Serialize(new
            {
                status = "initialized",
                name = wsName,
                workspacePath = ws.RootDirectory,
                dbPath = ws.DbPath,
                queriesPath = ws.QueriesDirectory
            }, CompactJsonOptions);
    }

    public async Task<string> ScanWorkspaceAsync(
        string? path = null,
        bool clear = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(path ?? workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null)
        {
            var defaultName = new DirectoryInfo(targetDir).Name;
            ws = WorkspaceLocator.Initialize(targetDir, defaultName);
        }

        var client = await ResolveClientAsync(ws.RootDirectory);
        if (clear)
        {
            await client.ClearWorkspaceAsync(targetDir);
        }

        var indexer = new WorkspaceIndexer(client);
        var (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(targetDir, ws.RootDirectory, clear: false, cancellationToken: cancellationToken);

        InvalidateCache();

        return JsonSerializer.Serialize(new
        {
            status = "success",
            workspacePath = ws.RootDirectory,
            scannedPath = targetDir,
            nodesCount,
            relationshipsCount = relsCount,
            nodesByKind
        }, CompactJsonOptions);
    }

    public async Task<string> GetWorkspaceStatusAsync(
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null || !File.Exists(ws.DbPath))
        {
            return JsonSerializer.Serialize(new
            {
                status = "not_initialized",
                message = $"No indexed workspace found for '{targetDir}'. Call scan_workspace or init_workspace first."
            }, CompactJsonOptions);
        }

        var fileInfo = new FileInfo(ws.DbPath);
        var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
        var client = await ResolveClientAsync(ws.RootDirectory);

        var projResult = await client.ExecuteQueryAsync("MATCH (p:Project) RETURN p.name AS name, p.language AS language, p.project_type AS type, p.path AS path", null, cancellationToken);
        using var projDoc = JsonDocument.Parse(projResult);

        var countResult = await client.ExecuteQueryAsync("MATCH (n) RETURN labels(n)[0] AS kind, count(n) AS count ORDER BY count DESC", null, cancellationToken);
        using var countDoc = JsonDocument.Parse(countResult);

        var customQueriesCount = Directory.Exists(ws.QueriesDirectory)
            ? Directory.GetFiles(ws.QueriesDirectory, "*.cypher").Length
            : 0;

        return JsonSerializer.Serialize(new
        {
            status = "ready",
            workspacePath = ws.RootDirectory,
            dbPath = ws.DbPath,
            databaseSizeMb = Math.Round(sizeMb, 2),
            projects = projDoc.RootElement,
            nodeCounts = countDoc.RootElement,
            customQueriesCount
        }, CompactJsonOptions);
    }

    public async Task<string> ClearWorkspaceIndexAsync(
        string? path = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(path ?? workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null || !File.Exists(ws.DbPath))
        {
            return JsonSerializer.Serialize(new
            {
                status = "not_found",
                message = "Workspace or database not found."
            }, CompactJsonOptions);
        }

        var client = await ResolveClientAsync(ws.RootDirectory);
        if (string.IsNullOrWhiteSpace(path) || path.TrimEnd('/', '\\').Equals(ws.RootDirectory.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
        {
            await client.ClearDatabaseAsync();
        }
        else
        {
            await client.ClearWorkspaceAsync(targetDir);
        }

        InvalidateCache();

        return JsonSerializer.Serialize(new
        {
            status = "cleared",
            targetPath = targetDir
        }, CompactJsonOptions);
    }

    public async Task<string> IngestGraphDataAsync(
        string nodesJson,
        string? relationshipsJson = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageJson;
        }

        using var nodesDoc = JsonDocument.Parse(nodesJson);
        int nodesIngested = 0;
        int relsIngested = 0;

        if (nodesDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodesDoc.RootElement.EnumerateArray())
            {
                if (node.TryGetProperty("id", out var idProp) && node.TryGetProperty("kind", out var kindProp))
                {
                    var id = idProp.GetString()!;
                    var kind = kindProp.GetString()!;
                    var props = node.TryGetProperty("properties", out var p) ? p.GetRawText() : "{}";
                    await client.ExecuteWriteAsync(
                        "INSERT INTO nodes (id, kind, properties) VALUES (@id, @kind, json(@props)) ON CONFLICT(id) DO UPDATE SET kind = @kind, properties = json(@props);",
                        new Dictionary<string, object?> { ["id"] = id, ["kind"] = kind, ["props"] = props }, cancellationToken);
                    nodesIngested++;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(relationshipsJson))
        {
            using var relsDoc = JsonDocument.Parse(relationshipsJson);
            if (relsDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var rel in relsDoc.RootElement.EnumerateArray())
                {
                    var kind = rel.TryGetProperty("kind", out var kProp) ? kProp.GetString() : (rel.TryGetProperty("type", out var tProp) ? tProp.GetString() : null);
                    if (!string.IsNullOrEmpty(kind) && rel.TryGetProperty("from_id", out var fProp) && rel.TryGetProperty("to_id", out var toProp))
                    {
                        var fromId = fProp.GetString()!;
                        var toId = toProp.GetString()!;
                        var props = rel.TryGetProperty("properties", out var p) ? p.GetRawText() : "{}";
                        await client.ExecuteWriteAsync(
                            "INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties) VALUES (@fromId, @toId, @kind, json(@props));",
                            new Dictionary<string, object?> { ["kind"] = kind, ["fromId"] = fromId, ["toId"] = toId, ["props"] = props }, cancellationToken);
                        relsIngested++;
                    }
                }
            }
        }

        InvalidateCache();

        return JsonSerializer.Serialize(new
        {
            status = "success",
            nodesIngested,
            relationshipsIngested = relsIngested
        }, CompactJsonOptions);
    }
}
