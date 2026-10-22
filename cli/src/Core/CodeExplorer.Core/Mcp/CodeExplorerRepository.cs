using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Diagrams;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private static readonly JsonSerializerOptions CompactJsonOptions = new() { WriteIndented = false };
    private const string GateCatalogTableHeader = "| ID | Category | Platform | Scope | Dimension | Name | Severity | Source |";
    private const string GateCatalogTableSeparator = "| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |";
    private const string GateSuiteTableHeader = "| ID | Category | Platform | Scope | Dimension | Gate Name | Status | Violations |";
    private const string GateSuiteTableSeparator = "| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |";
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

            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0 &&
                doc.RootElement[0].TryGetProperty("cnt", out var pCnt))
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

        if (overviewDoc.RootElement.ValueKind != JsonValueKind.Array ||
            overviewDoc.RootElement.GetArrayLength() == 0)
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

        var layerOrder = new[]
        {
            "Core / Domain", "Services / Backend", "UI / Presentation", "Infrastructure / Data", "Other",
            "Tests"
        };

        var groupedLayers = layerOrder.Select(layer =>
        {
            var items = rawProjects.Where(p => p["layer"] == layer)
                .Select(p => new { name = p["name"], language = p["language"], path = p["path"] }).ToList();
            return new { layer, count = items.Count, projects = items };
        }).Where(g => g.count > 0).ToList();

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

                var formatted = JsonSerializer.Serialize(
                    new
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


    public async Task<string> GetArchitectureViewAsync(
        string level = "c1",
        string? scope = null,
        bool includeLibraries = false,
        string format = "markdown",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var normalizedLevel = level.Trim().ToLowerInvariant();
        var engine = new Analysis.ArchitectureViewEngine(client);

        var viewType = normalizedLevel switch
        {
            "c2" or "service" or "flow" or "service-flow" => Analysis.ArchitectureViewType.ServiceFlow,
            "c3" or "component" => Analysis.ArchitectureViewType.Component,
            "project-map" or "projectmap" or "project" or "modules" => Analysis.ArchitectureViewType.ProjectMap,
            "domain" or "domain-map" or "domains" => Analysis.ArchitectureViewType.DomainMap,
            "contexts" or "context" or "bounded-context" or "bounded-contexts" or "context-map" => Analysis.ArchitectureViewType.BoundedContexts,
            "tiers" or "tiered" => Analysis.ArchitectureViewType.Tiers,
            _ => Analysis.ArchitectureViewType.SystemContext
        };

        var graph = await engine.GetViewAsync(new Analysis.ArchitectureViewRequest
        {
            ViewType = viewType,
            Scope = scope,
            IncludeLibraries = includeLibraries
        }, cancellationToken);

        var title = $"{viewType} Architecture View";
        return Analysis.ArchitectureViewEngine.SerializeGraph(graph, format, title);
    }

    public async Task<string> GetDomainArchitectureAsync(
        bool includeLibraries = false,
        string format = "markdown",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var engine = new Analysis.ArchitectureViewEngine(client);
        var domainDto = await engine.GetDomainArchitectureAsync(includeLibraries, cancellationToken);
        return Analysis.ArchitectureViewEngine.SerializeDomainArchitecture(domainDto, format);
    }

    public async Task<string> GetBoundedContextsAsync(
        string format = "markdown",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var engine = new Analysis.ArchitectureViewEngine(client);
        var mapDto = await engine.GetBoundedContextMapAsync(ct: cancellationToken);
        return Analysis.ArchitectureViewEngine.SerializeBoundedContextMap(mapDto, format);
    }

    public async Task<string> GetServiceContractsAsync(
        string serviceName,
        string direction = "all",
        string format = "markdown",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var engine = new Analysis.ArchitectureViewEngine(client);
        var contract = await engine.GetServiceContractsAsync(serviceName, direction, cancellationToken);
        return Analysis.ArchitectureViewEngine.SerializeServiceContract(contract, format);
    }

    public async Task<string> TraceCrossServiceFlowAsync(
        string startService,
        string? entryPoint = null,
        int maxDepth = 3,
        string format = "markdown",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var engine = new Analysis.ArchitectureViewEngine(client);
        var flow = await engine.TraceCrossServiceFlowAsync(startService, entryPoint, maxDepth, cancellationToken);
        return Analysis.ArchitectureViewEngine.SerializeCrossServiceFlow(flow, format);
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


    public async Task<string> ExportArchitectureDiagramAsync(
        string format = "mermaid",
        string type = "architecture",
        string? projectFilter = null,
        bool includeLibraries = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        return await DiagramExporter.ExportAsync(client, format, type, projectFilter, includeLibraries, cancellationToken);
    }

}
