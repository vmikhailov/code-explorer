using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    private static readonly ConditionalWeakTable<object, CachedClientRoots> SessionRootsCache = new();

    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;
    internal Func<CancellationToken, Task<IReadOnlyList<string>>>? CustomRootsProvider { get; set; }

    private sealed class CachedClientRoots
    {
        public bool HandlerRegistered;
        public volatile bool IsPopulated;
        public IReadOnlyList<string> RawUris = [];
        public volatile string? StickyWorkspacePath;
    }

    private CachedClientRoots GetSessionState() => SessionRootsCache.GetOrCreateValue((object?)mcpServer ?? this);

    private void RememberStickyWorkspaceIfIndexed(string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || !Directory.Exists(candidatePath))
        {
            return;
        }

        var found = WorkspaceLocator.Find(candidatePath);
        if (found != null && File.Exists(found.DbPath))
        {
            GetSessionState().StickyWorkspacePath = found.RootDirectory;
        }
    }

    private string? TryGetValidStickyWorkspacePath()
    {
        var sticky = GetSessionState().StickyWorkspacePath;
        if (string.IsNullOrWhiteSpace(sticky) || !Directory.Exists(sticky))
        {
            return null;
        }

        var found = WorkspaceLocator.Find(sticky);
        return found != null && File.Exists(found.DbPath) ? found.RootDirectory : null;
    }

    internal async Task<string?> ResolveWorkspacePathAsync(string? explicitWorkspace = null, CancellationToken cancellationToken = default)
    {
        var explicitWs = TryResolveExplicitWorkspacePath(explicitWorkspace);
        string? ws;
        if (!string.IsNullOrEmpty(explicitWs))
        {
            RememberStickyWorkspaceIfIndexed(explicitWs);
            ws = explicitWs;
        }
        else
        {
            ws = repository.DefaultWorkspacePath;
            if (string.IsNullOrEmpty(ws))
            {
                ws = await TryResolveWorkspaceFromClientRootsAsync(cancellationToken);
            }
            if (string.IsNullOrEmpty(ws))
            {
                ws = TryGetValidStickyWorkspacePath();
            }
        }

        if (!string.IsNullOrEmpty(ws))
        {
            WorkspaceConventions.LoadFromWorkspace(ws);
        }
        return ws;
    }

    private string? TryResolveExplicitWorkspacePath(string? explicitWorkspace)
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

        return null;
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
            var output = await action(resolvedWs);
            if (!string.IsNullOrWhiteSpace(explicitWorkspace) && !string.IsNullOrWhiteSpace(resolvedWs))
            {
                RememberStickyWorkspaceIfIndexed(resolvedWs);
            }
            return output;
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

    private const string OperationCancelledError = "Operation was cancelled.";
    private const string MissingServiceNameError = "Missing 'serviceName' argument.";
    private const string MissingStartServiceError = "Missing 'startService' argument.";
    private const string MissingFilePathError = "Missing 'filePath' argument.";
    private const string MissingNameError = "Missing 'name' argument.";
    private const string MissingStartOrEndFunctionError = "Missing 'startFunction' or 'endFunction' argument.";
    private const string MissingInterfaceOrMethodNameError = "Missing 'interfaceName' or 'methodName' argument.";
    private const string MissingTargetError = "Missing 'target' argument.";
    private const string MissingQueryError = "Missing 'query' argument.";
    private const string MissingSymbolNameError = "Missing 'symbolName' argument.";

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
            return WrapError(OperationCancelledError);
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
    [McpServerTool(Title = "Get Architecture Topology View", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Project the workspace architecture graph at a selected zoom level: 'c1' (system context: services, databases, topics, external APIs), 'c2' (inter-service communication flows), 'c3' or 'project-map' (internal modules, lifted calls, and circular dependency cycles within one project), or 'tiers' (Clean Architecture layers). Use when inspecting topological layers, inter-service edges, or internal module cycles; for a flat workspace inventory of projects and endpoints use get_architecture_map, and for renderable Mermaid or C4 diagram syntax use export_architecture_diagram. Requires an indexed .codeexplorer/graph.db; scope filters 'c1'/'c2'/'tiers' by service or selects the target project in 'c3'/'project-map' (defaulting to the largest project), while includeLibraries adds shared class libraries to 'c1'/'c2'/'tiers', returning YAML nodes, directed edges, and cycle diagnostics.")]
    public async Task<CallToolResult> GetArchitectureViewAsync(
        [AllowedValues("c1", "c2", "c3", "project-map", "tiers")]
        [Description("Abstraction level to project: 'c1' (system context), 'c2' (service communication), 'c3' or 'project-map' (internal modules and cycles of one project), or 'tiers' (architectural layers). Default: 'c1'.")] string level = "c1",
        [Description("Optional project or service filter (name, node ID, or relative path). For 'c3' / 'project-map', selects the target project (defaults to the largest project when omitted).")] string? scope = null,
        [Description("Whether to include shared class libraries alongside deployable services in 'c1', 'c2', and 'tiers' views. Default: false.")] bool includeLibraries = false,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
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
    public async Task<CallToolResult> GetArchitectureOverviewAsync(
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetArchitectureOverviewAsync(ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get Workspace Architecture Map", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Retrieve a consolidated workspace architecture inventory listing all projects, programming languages, inter-project dependencies, databases, ingress endpoints, egress external services, and saved project queries. Use for initial repository orientation or scoping a single project's boundary summary; for zoomed C1/C2/C3 topology projections and cycle analysis use get_architecture_view, and for filtered runtime vs build package edges use get_project_dependencies. Reads the indexed .codeexplorer/graph.db without scanning source files; when projectName is provided, narrows the inventory to matching projects and their direct neighbors, returning a bounded YAML summary (<1,000 tokens) or a standby notice if unindexed.")]
    public async Task<CallToolResult> GetArchitectureMapAsync(
        [Description("Optional case-insensitive project name or substring to scope the inventory to matching projects and their immediate dependencies.")] string? projectName = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetArchitectureMapAsync(projectName, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get Project and Package Dependencies", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List directed project-to-project references and external package dependencies (NuGet, npm, PyPI, Go, Maven, Cargo, and Terraform modules) from the workspace graph. Use when auditing package versions or project-level coupling; for symbol-level refactoring blast radius use analyze_code_impact, and for a full workspace overview use get_architecture_map. Requires an indexed .codeexplorer/graph.db; combines projectFilter (matching source or target project name) with type ('runtime' vs 'build' vs 'all') and truncates output at limit edges, returning YAML rows with source project, target project/package, version, and dependency scope.")]
    public async Task<CallToolResult> GetProjectDependenciesAsync(
        [Description("Optional case-insensitive project name substring to filter inbound and outbound dependencies for matching projects.")] string? projectFilter = null,
        [Range(1, 500)]
        [Description("Maximum number of dependency edges to return (1 to 500, default: 50).")] int limit = 50,
        [AllowedValues("all", "runtime", "build")]
        [Description("Dependency scope filter: 'all', 'runtime' (production references only), or 'build' (test/tooling/dev dependencies). Default: 'all'.")] string type = "all",
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetProjectDependenciesAsync(projectFilter, limit, type, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get Service Ingress and Egress Contracts", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Retrieve external communication contracts for a named service or project, covering HTTP/REST routes, gRPC services, GraphQL resolvers, message queue publishers, and topic subscribers. Use when inspecting both inbound API surface and outbound calls/messages of a service; to list internal entry-point handlers only (without outbound egress contracts) use get_project_entry_points, and to trace multi-hop cross-service paths use trace_cross_service_flow. Queries the indexed .codeexplorer/graph.db where serviceName matches case-insensitively and direction ('ingress', 'egress', or 'all') selects which contract sections are populated, returning YAML lists of endpoints, external calls, and broker topics (or an empty contract list if none exist).")]
    public async Task<CallToolResult> GetServiceContractsAsync(
        [Description("Required service or project name (case-insensitive exact or substring match, e.g. 'OrderService' or 'WebApi').")] string serviceName,
        [AllowedValues("all", "ingress", "egress")]
        [Description("Contract direction filter: 'ingress' (exposed endpoints and subscribed topics), 'egress' (outbound HTTP/gRPC calls and published topics), or 'all'. Default: 'all'.")] string direction = "all",
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return WrapError(MissingServiceNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetServiceContractsAsync(serviceName, direction, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Trace Cross-Service Distributed Flow", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Trace distributed request and event propagation paths across microservices, HTTP clients, gRPC channels, and message broker topics starting from startService up to maxDepth cross-service hops. Use for multi-service flows across process boundaries; for intra-process function-to-function call paths between two methods use get_call_chain, and for a single service's static contracts use get_service_contracts. Requires an indexed .codeexplorer/graph.db; when entryPoint is provided it anchors traversal to matching routes or handlers inside startService, and maxDepth (clamped to 1..10) bounds hop recursion, returning YAML hop chains with source service, protocol, route/topic, and target service (or an empty trace if no outbound cross-service calls exist).")]
    public async Task<CallToolResult> TraceCrossServiceFlowAsync(
        [Description("Required originating service or project name where the distributed trace begins (e.g. 'ApiGateway').")] string startService,
        [Description("Optional entry point URL route substring, topic name, or handler method name within startService to anchor the trace (e.g. '/api/orders' or 'CreateOrder').")] string? entryPoint = null,
        [Range(1, 10)]
        [Description("Maximum cross-service hop depth to traverse (integer 1 to 10, default: 3).")] int maxDepth = 3,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(startService))
        {
            return WrapError(MissingStartServiceError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.TraceCrossServiceFlowAsync(startService, entryPoint, maxDepth, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get File or Directory Structural Outline", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Extract the structural AST outline of a single source file (declared types, functions, members, and 1-based line ranges) or a directory (immediate subfolders, files, line counts, and top-level symbols) without reading raw source text. Use to inspect file or folder structure before fetching code ranges; to read actual source lines use fetch_code_snippets, and to search declarations across the workspace by name use find_symbol. Queries the indexed .codeexplorer/graph.db (switching output shape automatically based on whether filePath resolves to a file or directory, with '.' targeting the workspace root) and returns YAML symbol trees or folder summaries without truncating large files.")]
    public async Task<CallToolResult> GetFileOutlineAsync(
        [Description("Required file or directory path to outline (workspace-relative path, absolute path, or '.' for the workspace root).")] string filePath,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return WrapError(MissingFilePathError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetFileOutlineAsync(filePath, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Find Code Symbols by Name", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Search the indexed workspace graph for declared code symbols ('Class', 'Interface', 'Function', or 'Variable'/'Member') matching an exact or substring identifier name. Use when locating code declarations by known or partial name; for fuzzy trigram and semantic vector search across routes, database tables, topics, and keywords use search_identifiers, and for listing all symbols inside a known file path use get_file_outline. Queries .codeexplorer/graph.db case-insensitively (prioritizing exact matches over substring matches, filtered by symbolType when specified, and truncated at limit rows) and returns YAML records with symbol kind, qualified name, file_path, start_line, and end_line (or an empty list if none match).")]
    public async Task<CallToolResult> FindSymbolAsync(
        [Description("Required symbol name or case-insensitive substring to match (e.g. 'OrderController' or 'ValidateToken').")] string name,
        [AllowedValues("Class", "Interface", "Function", "Variable", "Member")]
        [Description("Optional symbol category filter: 'Class', 'Interface', 'Function', 'Variable', or 'Member' ('Variable' and 'Member' are aliases). Omit to search all symbol kinds.")] string? symbolType = null,
        [Range(1, 500)]
        [Description("Maximum number of matching symbols to return (1 to 500, default: 50).")] int limit = 50,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return WrapError(MissingNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.FindSymbolAsync(name, symbolType, limit, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Trace Function-to-Function Call Chain", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Find directed function-to-function call paths connecting a specific startFunction to a specific endFunction within the indexed code graph up to maxDepth hops. Use only when both the starting caller and destination callee are known; to list all direct 1-hop callers of a single target use get_callers, for multi-hop transitive blast radius of one symbol use analyze_code_impact, and for cross-service HTTP/messaging flows use trace_cross_service_flow. Evaluates bounded shortest-path traversal (clamped to 1..10 hops) in .codeexplorer/graph.db without modifying state and returns all discovered call chains in YAML with ordered function names, file paths, and line numbers (or an empty result if no path connects them within maxDepth).")]
    public async Task<CallToolResult> GetCallChainAsync(
        [Description("Required originating caller function or method name (exact or qualified name, e.g. 'SubmitOrder' or 'OrderController.Post').")] string startFunction,
        [Description("Required destination callee function or method name (exact or qualified name, e.g. 'ChargeCard' or 'PaymentGateway.Execute').")] string endFunction,
        [Range(1, 10)]
        [Description("Maximum call-graph hop depth to search between startFunction and endFunction (integer 1 to 10, default: 5).")] int maxDepth = 5,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(startFunction) || string.IsNullOrEmpty(endFunction))
        {
            return WrapError(MissingStartOrEndFunctionError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetCallChainAsync(startFunction, endFunction, maxDepth, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Resolve Interface Method Implementations", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Resolve concrete class implementations and dependency-injection targets for an abstract or interface method identified by interfaceName and methodName. Use when tracing outbound polymorphic dispatch from an interface contract to its concrete classes; to find inbound callers invoking a method use get_callers, and to trace multi-hop paths between two functions use get_call_chain. Queries IMPLEMENTS, INHERITS_FROM, and HAS_METHOD edges in the indexed .codeexplorer/graph.db (matching both interfaceName and methodName as a required pair) and returns a complete YAML list of implementing classes, method signatures, file_path, and 1-based line ranges (or an empty list if no concrete implementation is indexed).")]
    public async Task<CallToolResult> ResolveCallTargetAsync(
        [Description("Required interface or abstract base type name (e.g. 'IPaymentProcessor').")] string interfaceName,
        [Description("Required method name declared on the interface or abstract type (e.g. 'ProcessPaymentAsync').")] string methodName,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interfaceName) || string.IsNullOrEmpty(methodName))
        {
            return WrapError(MissingInterfaceOrMethodNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ResolveCallTargetAsync(interfaceName, methodName, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Find Inbound Callers and Consumers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all direct 1-hop inbound callers, references, service consumers, or table/topic accessors targeting a named function, type, service, project, database table, or messaging topic. Use when inspecting immediate 1-hop consumers of a target; for multi-hop transitive blast-radius analysis across downstream dependents use analyze_code_impact, for point-to-point paths between two functions use get_call_chain, and to resolve concrete implementations of an interface method use resolve_call_target. Queries inbound CALLS, ACCESSED_BY, and DEPENDS_ON edges in .codeexplorer/graph.db (filtering out unit/integration test projects when includeTests=false) and returns YAML records with caller symbol, kind, file_path, and line number.")]
    public async Task<CallToolResult> GetCallersAsync(
        [Description("Required target name (function, class/interface, service, project, database table, or messaging topic).")] string target,
        [Description("Whether to include callers located in unit/integration test projects. Default: false (production callers only).")] bool includeTests = false,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(target))
        {
            return WrapError(MissingTargetError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetCallersAsync(target, includeTests, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Hybrid Fuzzy and Vector Identifier Search", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Perform hybrid trigram and semantic vector search across code identifiers, HTTP routes, database tables, messaging topics, and projects using exact, fuzzy, or natural-language keywords. Use for exploratory discovery when the exact symbol name is unknown or when searching across routes, tables, and topics simultaneously; for strict substring lookup on code declarations ('Class', 'Interface', 'Function', 'Variable') use find_symbol, and for outlining a known file use get_file_outline. Scores candidates in the indexed .codeexplorer/graph.db by combined lexical and vector similarity (restricted to kind when provided and capped at limit rows) and returns ranked YAML matches with similarity score, node kind, qualified name, file_path, and line range.")]
    public async Task<CallToolResult> SearchIdentifiersAsync(
        [Description("Required search query (partial identifier, URL route fragment, table name, or domain keyword).")] string query,
        [AllowedValues("Type", "Function", "Member", "Endpoint", "Table", "Topic", "Project")]
        [Description("Optional ontology node kind filter: 'Type', 'Function', 'Member', 'Endpoint', 'Table', 'Topic', or 'Project'. Omit to search across all kinds.")] string? kind = null,
        [Range(1, 200)]
        [Description("Maximum number of ranked matches to return (1 to 200, default: 15).")] int limit = 15,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            return WrapError(MissingQueryError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.SearchIdentifiersAsync(query, kind, limit, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Analyze Multi-Hop Refactoring Blast Radius", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Compute the multi-hop transitive blast radius of modifying a symbol (class, interface, function, service, or database table), aggregating upstream callers, derived types, affected API endpoints, and downstream projects across multiple hops. Use before refactoring a shared symbol or contract to assess total downstream impact; for immediate 1-hop callers only use get_callers, and to select affected regression tests to run use get_affected_tests. Traverses transitive dependency and call edges in the indexed .codeexplorer/graph.db and returns YAML grouped into direct dependents, transitive dependents, impacted services/endpoints, and an overall risk classification.")]
    public async Task<CallToolResult> AnalyzeCodeImpactAsync(
        [Description("Required name of the class, interface, function, service, or database table being modified.")] string symbolName,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(symbolName))
        {
            return WrapError(MissingSymbolNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.AnalyzeCodeImpactAsync(symbolName, ws, cancellationToken), cancellationToken);
    }
}
