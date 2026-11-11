using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CodeExplorer.Core.Mcp;

[McpServerToolType]
public partial class McpGraphHandler(
    CodeExplorerRepository repository,
    IHttpContextAccessor httpContextAccessor,
    ILogger<McpGraphHandler>? logger = null,
    McpServer? mcpServer = null)
{
    private const string QueryParamWs = "ws";
    private const string QueryParamWorkspacePath = "workspacePath";
    private const string AsyncMethodSuffix = "Async";
    private const string RootListSeparator = ", ";
    private const string RootsListFailedLogMessage = "[MCP] Client roots/list request failed or timed out; falling back to standby.";
    private static readonly TimeSpan ClientRootsRequestTimeout = TimeSpan.FromSeconds(2);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, CachedClientRoots> SessionRootsCache = new();

    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;
    internal Func<CancellationToken, Task<IReadOnlyList<string>>>? CustomRootsProvider { get; set; }

    private sealed class CachedClientRoots
    {
        public bool HandlerRegistered;
        public volatile bool IsPopulated;
        public IReadOnlyList<string> RawUris = [];
    }

    internal async Task<string?> ResolveWorkspacePathAsync(string? explicitWorkspace = null, CancellationToken cancellationToken = default)
    {
        var ws = TryResolveImmediateWorkspacePath(explicitWorkspace);
        if (string.IsNullOrEmpty(ws))
        {
            ws = await TryResolveWorkspaceFromClientRootsAsync(cancellationToken);
        }

        if (!string.IsNullOrEmpty(ws))
        {
            WorkspaceConventions.LoadFromWorkspace(ws);
        }
        return ws;
    }

    private string? TryResolveImmediateWorkspacePath(string? explicitWorkspace)
    {
        if (!string.IsNullOrWhiteSpace(explicitWorkspace))
        {
            return explicitWorkspace;
        }

        var httpContext = httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            var workspacePath = httpContext.Request.Query[QueryParamWs].ToString();
            if (string.IsNullOrEmpty(workspacePath))
            {
                workspacePath = httpContext.Request.Query[QueryParamWorkspacePath].ToString();
            }
            if (!string.IsNullOrEmpty(workspacePath))
            {
                return workspacePath;
            }
        }

        return repository.DefaultWorkspacePath;
    }

    private async Task<string?> TryResolveWorkspaceFromClientRootsAsync(CancellationToken cancellationToken)
    {
        var rawUris = await FetchClientRootUrisAsync(cancellationToken);
        if (rawUris.Count == 0)
        {
            return null;
        }

        var localDirs = new List<string>(rawUris.Count);
        var indexedWorkspaces = new List<WorkspaceInfo>();
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawUri in rawUris)
        {
            var localPath = ConvertRootUriToLocalPath(rawUri);
            if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
            {
                continue;
            }

            localDirs.Add(localPath);
            var found = WorkspaceLocator.Find(localPath);
            if (found != null && File.Exists(found.DbPath))
            {
                var normRoot = Path.GetFullPath(found.RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (seenRoots.Add(normRoot))
                {
                    indexedWorkspaces.Add(found);
                }
            }
        }

        if (indexedWorkspaces.Count == 1)
        {
            return indexedWorkspaces[0].RootDirectory;
        }

        if (indexedWorkspaces.Count > 1)
        {
            var names = string.Join(RootListSeparator, indexedWorkspaces.Select(w => w.RootDirectory));
            throw new InvalidOperationException(
                $"Multiple indexed CodeExplorer workspaces are open in client roots ({names}). Specify 'workspacePath' on the tool call to select one.");
        }

        return localDirs.Count == 1 ? localDirs[0] : null;
    }

    private async Task<IReadOnlyList<string>> FetchClientRootUrisAsync(CancellationToken cancellationToken)
    {
        if (CustomRootsProvider != null)
        {
            return await CustomRootsProvider(cancellationToken);
        }

        if (mcpServer?.ClientCapabilities?.Roots == null)
        {
            return [];
        }

        var cacheEntry = SessionRootsCache.GetOrCreateValue(mcpServer);
        lock (cacheEntry)
        {
            if (!cacheEntry.HandlerRegistered)
            {
                cacheEntry.HandlerRegistered = true;
                mcpServer.RegisterNotificationHandler(NotificationMethods.RootsListChangedNotification, (_, _) =>
                {
                    cacheEntry.IsPopulated = false;
                    return ValueTask.CompletedTask;
                });
            }
        }

        if (cacheEntry.IsPopulated)
        {
            return cacheEntry.RawUris;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ClientRootsRequestTimeout);
            var rootsResult = await mcpServer.RequestRootsAsync(new ListRootsRequestParams(), cts.Token);
            var uris = rootsResult.Roots?
                .Select(r => r.Uri)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToArray() ?? [];
            cacheEntry.RawUris = uris;
            cacheEntry.IsPopulated = true;
            return uris;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, RootsListFailedLogMessage);
            return [];
        }
    }

    private const string FileUriSchemePrefix = "file:///";
    private const string EncodedColonUpper = "%3A";
    private const string EncodedColonLower = "%3a";
    private const string ColonString = ":";

    internal static string? ConvertRootUriToLocalPath(string rawUri)
    {
        if (string.IsNullOrWhiteSpace(rawUri))
        {
            return null;
        }

        var normalizedUri = rawUri;
        if (normalizedUri.StartsWith(FileUriSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedUri = normalizedUri
                .Replace(EncodedColonUpper, ColonString, StringComparison.Ordinal)
                .Replace(EncodedColonLower, ColonString, StringComparison.Ordinal);
        }

        if (Uri.TryCreate(normalizedUri, UriKind.Absolute, out var parsed) && parsed.IsFile)
        {
            var localPath = parsed.LocalPath;
            if (OperatingSystem.IsWindows() &&
                localPath.Length >= 3 &&
                (localPath[0] == '\\' || localPath[0] == '/') &&
                char.IsAsciiLetter(localPath[1]) &&
                localPath[2] == ':')
            {
                return localPath[1..];
            }
            return localPath;
        }

        return rawUri;
    }

    private Task<CallToolResult> ExecuteForWorkspaceAsync(
        string? explicitWorkspace,
        Func<string?, Task<string>> action,
        CancellationToken cancellationToken,
        [CallerMemberName] string toolName = "")
    {
        return ExecuteAsync(async () =>
        {
            var resolvedWs = await ResolveWorkspacePathAsync(explicitWorkspace, cancellationToken);
            return await action(resolvedWs);
        }, toolName);
    }

    private static CallToolResult WrapResult(string text)
    {
        return new CallToolResult
        {
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } }
        };
    }

    private static CallToolResult WrapError(string message)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock> { new TextContentBlock { Text = message } }
        };
    }

    private static CallToolResult WrapError(Exception ex) => WrapError(ex.Message);

    private static string NormalizeToolName(string name)
    {
        if (name.EndsWith(AsyncMethodSuffix, StringComparison.Ordinal))
            name = name[..^5];
        return name;
    }

    private async Task<CallToolResult> ExecuteAsync(Func<Task<string>> action, [CallerMemberName] string toolName = "")
    {
        var sw = Stopwatch.StartNew();
        var normalizedName = NormalizeToolName(toolName);
        try
        {
            var result = await action();
            sw.Stop();
            _logger.LogInformation("[MCP] Tool {ToolName} completed in {ElapsedMs:F1}ms", normalizedName, sw.Elapsed.TotalMilliseconds);
            return WrapResult(result);
        }
        catch (TimeoutException ex)
        {
            sw.Stop();
            _logger.LogWarning("[MCP] Tool {ToolName} timed out after {ElapsedMs:F1}ms: {Message}", normalizedName, sw.Elapsed.TotalMilliseconds, ex.Message);
            return WrapError($"Query timed out: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning("[MCP] Tool {ToolName} was cancelled after {ElapsedMs:F1}ms", normalizedName, sw.Elapsed.TotalMilliseconds);
            return WrapError("Operation was cancelled.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[MCP] Tool {ToolName} failed after {ElapsedMs:F1}ms: {Message}", normalizedName, sw.Elapsed.TotalMilliseconds, ex.Message);
            return WrapError(ex);
        }
    }

    private CallToolResult Execute(Func<string> action, [CallerMemberName] string toolName = "")
    {
        var sw = Stopwatch.StartNew();
        var normalizedName = NormalizeToolName(toolName);
        try
        {
            var result = action();
            sw.Stop();
            _logger.LogInformation("[MCP] Tool {ToolName} completed in {ElapsedMs:F1}ms", normalizedName, sw.Elapsed.TotalMilliseconds);
            return WrapResult(result);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[MCP] Tool {ToolName} failed after {ElapsedMs:F1}ms: {Message}", normalizedName, sw.Elapsed.TotalMilliseconds, ex.Message);
            return WrapError(ex);
        }
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Architecture view of workspace structure ('c1', 'c2', 'c3', 'project-map', 'tiers').")]
    public async Task<CallToolResult> GetArchitectureViewAsync(
        [Description("View level: 'c1', 'c2', 'c3', 'project-map' (internal modules, lifted calls & cycles of one project), or 'tiers'. Default: 'c1'.")] string level = "c1",
        [Description("Optional project/service name filter. For 'project-map': project name, id, or path (defaults to the largest project).")] string? scope = null,
        [Description("Include shared libraries. Default: false.")] bool includeLibraries = false,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetArchitectureViewAsync(level, scope, includeLibraries, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> GetBoundedContextsAsync(
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetBoundedContextsAsync(ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("High-level architecture overview grouped by semantic layers (Core, Services, UI, Infra, Tests).")]
    public async Task<CallToolResult> GetArchitectureOverviewAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetArchitectureOverviewAsync(ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Infrastructure map: workspace folders, projects, internal folders, and databases.")]
    public async Task<CallToolResult> GetArchitectureMapAsync(
        [Description("Optional project filter.")] string? projectName = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetArchitectureMapAsync(projectName, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Project dependency graph with runtime vs build filtering.")]
    public async Task<CallToolResult> GetProjectDependenciesAsync(
        [Description("Optional project name filter.")] string? projectFilter = null,
        [Description("Max results (default: 50).")] int limit = 50,
        [Description("Type: 'all', 'runtime', or 'build'. Default: 'all'.")] string type = "all",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetProjectDependenciesAsync(projectFilter, limit, type, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Ingress and egress contracts for a service (endpoints, RPC, publishers, subscribers).")]
    public async Task<CallToolResult> GetServiceContractsAsync(
        [Description("Service or project name.")] string serviceName,
        [Description("Direction: 'ingress', 'egress', or 'all'. Default: 'all'.")] string direction = "all",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return WrapError("Missing 'serviceName' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetServiceContractsAsync(serviceName, direction, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Trace distributed flow across microservices starting from an endpoint or topic.")]
    public async Task<CallToolResult> TraceCrossServiceFlowAsync(
        [Description("Starting service name.")] string startService,
        [Description("Optional entry point URL or method name.")] string? entryPoint = null,
        [Description("Max traversal depth (1-10, default: 3).")] int maxDepth = 3,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(startService))
        {
            return WrapError("Missing 'startService' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.TraceCrossServiceFlowAsync(startService, entryPoint, maxDepth, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Extract file structural outline without reading source text.")]
    public async Task<CallToolResult> GetFileOutlineAsync(
        [Description("File path relative to workspace or absolute.")] string filePath,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return WrapError("Missing 'filePath' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetFileOutlineAsync(filePath, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Search graph for code symbols (Class, Interface, Function, Variable).")]
    public async Task<CallToolResult> FindSymbolAsync(
        [Description("Symbol name or partial name.")] string name,
        [Description("Optional type: 'Class', 'Interface', 'Function', 'Variable'.")] string? symbolType = null,
        [Description("Max results (default: 50).")] int limit = 50,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return WrapError("Missing 'name' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.FindSymbolAsync(name, symbolType, limit, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Trace sequential call graph between start and end functions.")]
    public async Task<CallToolResult> GetCallChainAsync(
        [Description("Originating function name.")] string startFunction,
        [Description("Destination function name.")] string endFunction,
        [Description("Max depth (1-10, default: 5).")] int maxDepth = 5,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(startFunction) || string.IsNullOrEmpty(endFunction))
        {
            return WrapError("Missing 'startFunction' or 'endFunction' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetCallChainAsync(startFunction, endFunction, maxDepth, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Find concrete implementations of an interface method (DI resolution).")]
    public async Task<CallToolResult> ResolveCallTargetAsync(
        [Description("Interface name.")] string interfaceName,
        [Description("Method name.")] string methodName,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interfaceName) || string.IsNullOrEmpty(methodName))
        {
            return WrapError("Missing 'interfaceName' or 'methodName' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ResolveCallTargetAsync(interfaceName, methodName, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Find all callers, inbound references, service consumers, or table/topic accessors for a target (functions, microservices, database tables, messaging topics). Prioritize this tool over grep for discovering callers.")]
    public async Task<CallToolResult> GetCallersAsync(
        [Description("Target name (Function, Service, Project, Table, Topic, or Type).")] string target,
        [Description("Include test callers (default: false).")] bool includeTests = false,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(target))
        {
            return WrapError("Missing 'target' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetCallersAsync(target, includeTests, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Fast hybrid vector and trigram search across code identifiers, routes, tables, and topics. Finds exact or fuzzy matches with sub-millisecond ranking.")]
    public async Task<CallToolResult> SearchIdentifiersAsync(
        [Description("Search query (symbol name, route, or colloquial keyword).")] string query,
        [Description("Optional filter by node kind ('Type', 'Function', 'Member', 'Endpoint', 'Table', 'Topic', 'Project').")] string? kind = null,
        [Description("Maximum results to return (default: 15).")] int limit = 15,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            return WrapError("Missing 'query' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.SearchIdentifiersAsync(query, kind, limit, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Blast-radius impact analysis: all callers, dependents, and references affected by modifying a symbol (Class, Interface, Function, Service, Table). Prioritize over grep.")]
    public async Task<CallToolResult> AnalyzeCodeImpactAsync(
        [Description("Class, interface, function, service, or table name.")] string symbolName,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(symbolName))
        {
            return WrapError("Missing 'symbolName' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.AnalyzeCodeImpactAsync(symbolName, ws, cancellationToken), cancellationToken);
    }
}
