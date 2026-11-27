using System.Text.Json;
using CodeExplorer.Core.Diagrams;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository : IDisposable
{
    private static readonly JsonSerializerOptions CompactJsonOptions = new() { WriteIndented = false };
    private readonly ProjectQueryManager _queryManager;
    private readonly IGraphClient _defaultDbClient;
    private readonly bool _enableFileWatchers;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IGraphClient> _clientCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, WorkspaceIndexer> _indexerCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, FileWatcher> _watcherCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _macroCache = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _defaultClientNonEmpty;

    public void InvalidateCache()
    {
        _macroCache.Clear();
        _defaultClientNonEmpty = false;
    }

    public string? DefaultWorkspacePath { get; }

    public const string StandbyMessageYaml = "status: standby\nmessage: \"CodeExplorer is running in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls, set the WORKSPACE_ROOT environment variable, or launch 'ce mcp --root <path>'.\"";

    public CodeExplorerRepository(
        IGraphClient dbClient,
        ProjectQueryManager? queryManager = null,
        string? defaultWorkspacePath = null,
        bool enableFileWatchers = false)
    {
        _defaultDbClient = dbClient;
        _queryManager = queryManager ?? new();
        DefaultWorkspacePath = defaultWorkspacePath;
        _enableFileWatchers = enableFileWatchers;
    }

    public WorkspaceIndexer GetOrCreateIndexer(string workspaceRoot, IGraphClient client)
    {
        var normRoot = Path.GetFullPath(workspaceRoot);
        return _indexerCache.GetOrAdd(normRoot, _ => new WorkspaceIndexer(client));
    }

    public bool HasActiveWatcher(string workspaceRoot)
    {
        var normRoot = Path.GetFullPath(workspaceRoot);
        return _watcherCache.ContainsKey(normRoot);
    }

    private async Task EnsureWatcherAndFlushAsync(string workspaceRoot, IGraphClient client)
    {
        if (!_enableFileWatchers || !Directory.Exists(workspaceRoot))
        {
            return;
        }

        var normRoot = Path.GetFullPath(workspaceRoot);
        var indexer = GetOrCreateIndexer(normRoot, client);
        var isNew = false;

        var watcher = _watcherCache.GetOrAdd(normRoot, root =>
        {
            isNew = true;
            var w = new FileWatcher(root, async batch =>
            {
                var watchResult = await indexer.IndexAsync(
                    root,
                    root,
                    enableIntentAnalysis: false,
                    knownChangedFiles: batch);
                if (watchResult.HasChanges)
                {
                    InvalidateCache();
                }
            });
            w.Start();
            return w;
        });

        if (isNew)
        {
            return;
        }

        if (watcher.HasPendingOrRunningChanges)
        {
            await watcher.FlushAsync();
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watcherCache.Values)
        {
            watcher.Dispose();
        }
        _watcherCache.Clear();
        GC.SuppressFinalize(this);
    }

    public async Task<IGraphClient> ResolveClientAsync(string? workspacePath)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            var ws = WorkspaceLocator.Find(workspacePath);
            if (ws != null && File.Exists(ws.DbPath))
            {
                var client = _clientCache.GetOrAdd(ws.DbPath, path => new SqliteGraphClient(path));
                await EnsureWatcherAndFlushAsync(ws.RootDirectory, client);
                return client;
            }

            var hasDifferentDefault = !string.IsNullOrWhiteSpace(DefaultWorkspacePath) &&
                !PathsEqualNormalized(DefaultWorkspacePath, workspacePath);
            if (hasDifferentDefault || await IsEmptyStandbyAsync(_defaultDbClient))
            {
                throw new InvalidOperationException(
                    $"No indexed CodeExplorer workspace (.codeexplorer/graph.db) found at '{workspacePath}'. Run 'ce init' and 'ce scan' in that workspace first.");
            }
        }

        if (!string.IsNullOrWhiteSpace(DefaultWorkspacePath))
        {
            await EnsureWatcherAndFlushAsync(DefaultWorkspacePath, _defaultDbClient);
        }

        return _defaultDbClient;
    }

    private static bool PathsEqualNormalized(string pathA, string pathB)
    {
        var normA = Path.GetFullPath(pathA).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normB = Path.GetFullPath(pathB).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsEmptyStandbyAsync(IGraphClient client)
    {
        if (client != _defaultDbClient) return false;
        if (_defaultClientNonEmpty || !string.IsNullOrWhiteSpace(DefaultWorkspacePath)) return false;
        try
        {
            const string hasAnyNodeQuery = "MATCH (n) RETURN n.id LIMIT 1";
            var res = await client.ExecuteCypherAsync(hasAnyNodeQuery);
            if (res.Count > 0)
            {
                _defaultClientNonEmpty = true;
                return false;
            }
            return true;
        }
        catch
        {
            // Ignore transient schema/connection errors on non-standard mocks
        }
        return false;
    }

    private Task<string> ExecuteAndFormatQueryAsync(string query, object? parameters, CancellationToken cancellationToken)
        => ExecuteAndFormatQueryAsync(query, parameters, null, cancellationToken);

    private async Task<string> ExecuteAndFormatQueryAsync(string query, object? parameters = null, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var res = await client.ExecuteCypherAsync(query, parameters, cancellationToken);
        return ToonYamlSerializer.SerializeYaml(res);
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
            return StandbyMessageYaml;
        }

        const string queryArchOverview = "get_architecture_overview";
        const string colProjects = "projects";
        var overviewQuery = Queries.Get(queryArchOverview);
        var overviewRes = await client.ExecuteCypherAsync(overviewQuery, null, cancellationToken);

        long totalNodes = 0;
        long totalFiles = 0;
        long totalRels = 0;
        const string totalNodesQuery = "MATCH (n) RETURN count(n) AS cnt;";
        const string totalFilesQuery = "MATCH (f:File) RETURN count(f) AS cnt;";
        const string totalRelsQuery = "MATCH (p1:Project)-[:DEPENDS_ON]->(p2:Project) RETURN count(*) AS cnt;";
        const string colWorkspace = "workspace";
        const string colPath = "path";
        const string colDatabases = "databases";
        const string colExternalServices = "externalServices";

        try
        {
            totalNodes = await client.QueryScalarAsync<long>(totalNodesQuery, null, cancellationToken);
        }
        catch
        {
            // ignored: optional telemetry count on partial/mock graph backends
        }

        try
        {
            totalFiles = await client.QueryScalarAsync<long>(totalFilesQuery, null, cancellationToken);
        }
        catch
        {
            // ignored: optional telemetry count on partial/mock graph backends
        }

        try
        {
            totalRels = await client.QueryScalarAsync<long>(totalRelsQuery, null, cancellationToken);
        }
        catch
        {
            // ignored: optional telemetry count on partial/mock graph backends
        }

        if (overviewRes.Count == 0)
        {
            return ToonYamlSerializer.SerializeYaml(overviewRes);
        }

        var row = overviewRes.Rows[0];
        var wsName = row.GetStringProp(colWorkspace, colWorkspace);
        var wsPath = row.GetStringProp(colPath);

        var rawProjects = new List<Dictionary<string, string>>();

        if (row.TryGetProperty(colProjects, out var projProp) && projProp.Kind == CypherValueKind.Array)
        {
            foreach (var p in projProp.AsArray())
            {
                var pName = p.TryGetProperty(ColName, out var n) ? n.AsString() : "";
                var pLang = p.TryGetProperty(ColLanguage, out var l) ? l.AsString() : "";
                var pPath = p.TryGetProperty(colPath, out var pt) ? pt.AsString() : "";

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

        var databases = row.GetStringListProp(colDatabases).Where(s => !string.IsNullOrEmpty(s)).ToList();
        var externalServices = row.GetStringListProp(colExternalServices).Where(s => !string.IsNullOrEmpty(s)).ToList();

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

        return ToonYamlSerializer.SerializeObject(new { results = overviewResult });
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

        const string queryArchMapProject = "get_architecture_map_project";
        const string queryArchMapWorkspace = "get_architecture_map_workspace";
        string resultYaml;
        if (!string.IsNullOrEmpty(projectName))
        {
            var query = Queries.Get(queryArchMapProject);
            resultYaml = await ExecuteAndFormatQueryAsync(query, new Dictionary<string, string> { ["projectName"] = projectName }, workspacePath, cancellationToken);
        }
        else
        {
            resultYaml = await ExecuteAndFormatQueryAsync(Queries.Get(queryArchMapWorkspace), null, workspacePath, cancellationToken);
        }

        try
        {
            var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
            var savedQueries = _queryManager.ListQueries(effectiveWsPath);
            if (savedQueries.Count > 0)
            {
                var parsed = ToonYamlSerializer.ParseYamlOrJson(resultYaml);
                var formatted = ToonYamlSerializer.SerializeObject(
                    new
                    {
                        results = parsed,
                        saved_project_queries = savedQueries.Select(q => new
                        {
                            name = q.Name,
                            description = q.Description,
                            parameters = q.Parameters,
                            returns = q.Returns,
                            tags = q.Tags
                        })
                    });
                _macroCache[cacheKey] = formatted;
                return formatted;
            }
        }
        catch
        {
            // Do not fail architecture map if project query listing fails
        }

        _macroCache[cacheKey] = resultYaml;
        return resultYaml;
    }


    public async Task<string> GetArchitectureViewAsync(
        string level = "c1",
        string? scope = null,
        bool includeLibraries = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var normalizedLevel = level.Trim().ToLowerInvariant();
        var engine = new ArchitectureViewEngine(client);

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

        var graph = await engine.GetViewAsync(new ArchitectureViewRequest
        {
            ViewType = viewType,
            Scope = scope,
            IncludeLibraries = includeLibraries
        }, cancellationToken);

        return ArchitectureViewEngine.ToToon(graph);
    }

    public async Task<string> GetDomainArchitectureAsync(
        bool includeLibraries = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var engine = new ArchitectureViewEngine(client);
        var domainDto = await engine.GetDomainArchitectureAsync(includeLibraries, cancellationToken);
        return ArchitectureViewEngine.ToToon(domainDto);
    }

    public async Task<string> GetBoundedContextsAsync(
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var engine = new ArchitectureViewEngine(client);
        var mapDto = await engine.GetBoundedContextMapAsync(ct: cancellationToken);
        return ToonYamlSerializer.SerializeObject(mapDto);
    }

    public async Task<string> GetServiceContractsAsync(
        string serviceName,
        string direction = "all",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var engine = new ArchitectureViewEngine(client);
        var contract = await engine.GetServiceContractsAsync(serviceName, direction, cancellationToken);
        return ArchitectureViewEngine.ToToon(contract);
    }

    public async Task<string> TraceCrossServiceFlowAsync(
        string startService,
        string? entryPoint = null,
        int maxDepth = 3,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var engine = new ArchitectureViewEngine(client);
        var flow = await engine.TraceCrossServiceFlowAsync(startService, entryPoint, maxDepth, cancellationToken);
        return ToonYamlSerializer.SerializeObject(flow);
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
            var ws = await _defaultDbClient.AsRepository().GetWorkspaceAsync();
            if (!string.IsNullOrEmpty(ws?.Path))
            {
                return Path.GetFullPath(ws.Path);
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
            return StandbyMessageYaml;
        }

        return await DiagramExporter.ExportAsync(client, format, type, projectFilter, includeLibraries, cancellationToken);
    }

}
