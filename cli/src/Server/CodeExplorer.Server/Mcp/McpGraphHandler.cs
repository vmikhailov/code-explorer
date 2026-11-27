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

#pragma warning disable MCP9005 // Keep MCP roots/list compatibility for clients that still report workspace roots
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
#pragma warning restore MCP9005

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
    private const string ViewOverview = "overview";
    private const string ViewLineage = "lineage";
    private const string ViewCqrs = "cqrs";
    private const string FormatYaml = "yaml";
    private const string FormatMermaid = "mermaid";
    private const string FormatC4 = "c4";
    private const string DiagramArchitecture = "architecture";
    private const string DirectionAll = "all";
    private const string DirectionEntryPoints = "entry_points";
    private const string DirectionFlow = "flow";
    private const string MatchModeHybrid = "hybrid";
    private const string SymLabelClass = "Class";
    private const string SymLabelInterface = "Interface";
    private const string SymLabelType = "Type";
    private const string SymLabelVariable = "Variable";
    private const string SymLabelMember = "Member";
    private const string SymLabelEndpoint = "Endpoint";
    private const string SymLabelTable = "Table";
    private const string SymLabelTopic = "Topic";
    private const string SymLabelProject = "Project";

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
    public async Task<CallToolResult> GetArchitectureViewAsync(
        string level = "c1",
        string? scope = null,
        bool includeLibraries = false,
        string? workspacePath = null,
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
    [McpServerTool(Title = "Get Workspace Architecture Map and Diagrams", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Inspect workspace architecture as structured YAML or renderable Mermaid/C4 diagram syntax across viewpoints: 'overview' (flat <1,000-token inventory of projects, languages, databases, endpoints, external services, and saved queries), 'c1' (system context), 'c2' (inter-service communication), 'c3' or 'project-map' (internal modules and circular dependency cycles of one project), 'tiers' (Clean Architecture layers), 'lineage' (database table read/write graph), or 'cqrs' (command/event pipelines). Use first when orienting on a repository, checking module cycles, or exporting architecture diagrams; for filtered runtime vs build package edges use get_project_dependencies, and for detailed routes/topics or multi-hop distributed flows of one service use get_service_contracts. Reads the indexed .codeexplorer/graph.db in read-only mode (scoping to projectName when provided and adding shared libraries when includeLibraries=true) and returns YAML or diagram markup (or a standby notice if unindexed).")]
    public async Task<CallToolResult> GetArchitectureMapAsync(
        [Description("Optional case-insensitive project or service filter to scope the inventory, topology view, or exported diagram (for 'c3'/'project-map', defaults to the largest project when omitted).")] string? projectName = null,
        [AllowedValues("overview", "c1", "c2", "c3", "project-map", "tiers", "lineage", "cqrs")]
        [Description("Architecture viewpoint to project: 'overview' (flat workspace inventory), 'c1' (system context), 'c2' (service communication), 'c3' or 'project-map' (internal modules and cycles), 'tiers' (layers), 'lineage' (DB reads/writes), or 'cqrs' (commands/events). Default: 'overview'.")] string view = "overview",
        [AllowedValues("yaml", "mermaid", "c4")]
        [Description("Output representation: 'yaml' (structured YAML for agent analysis), 'mermaid' (Mermaid flowchart/C4 syntax), or 'c4' (PlantUML/C4 syntax). Default: 'yaml'.")] string format = "yaml",
        [Description("Whether to include shared class libraries alongside deployable services in topology views and exported diagrams. Default: false.")] bool includeLibraries = false,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var normView = string.IsNullOrWhiteSpace(view) ? ViewOverview : view.Trim().ToLowerInvariant();
        var normFormat = string.IsNullOrWhiteSpace(format) ? FormatYaml : format.Trim().ToLowerInvariant();

        return await ExecuteForWorkspaceAsync(workspacePath, ws =>
        {
            if (normFormat is FormatMermaid or FormatC4)
            {
                var diagramView = normView is ViewLineage or ViewCqrs ? normView : DiagramArchitecture;
                return repository.ExportArchitectureDiagramAsync(normFormat, diagramView, projectName, includeLibraries, ws, cancellationToken);
            }

            if (normView is ViewLineage or ViewCqrs)
            {
                return repository.ExportArchitectureDiagramAsync(FormatMermaid, normView, projectName, includeLibraries, ws, cancellationToken);
            }

            return normView == ViewOverview
                ? repository.GetArchitectureMapAsync(projectName, ws, cancellationToken)
                : repository.GetArchitectureViewAsync(normView, projectName, includeLibraries, ws, cancellationToken);
        }, cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get Project and Package Dependencies", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List directed project-to-project references and external package dependencies (NuGet, npm, PyPI, Go, Maven, Cargo, and Terraform modules) from the workspace graph. Use when auditing package versions or project-level coupling; for symbol-level refactoring blast radius use analyze_code_impact, and for workspace topology or module cycle projections use get_architecture_map. Requires an indexed .codeexplorer/graph.db; combines projectFilter (matching source or target project name case-insensitively) with type ('runtime' vs 'build' vs 'all') and truncates output at limit edges, returning YAML rows with source project, target project/package, version, and dependency scope (or an empty list if none match).")]
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
    [McpServerTool(Title = "Get Service Contracts, Entry Points, and Cross-Service Flows", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Inspect communication boundaries and distributed propagation paths for a named service or project across HTTP/REST routes, gRPC services, GraphQL resolvers, CLI commands, background workers, and message broker topics. Use 'all', 'ingress', or 'egress' for static service contracts, 'entry_points' for internal handler symbols with file and line coordinates, or 'flow' to trace multi-hop cross-service HTTP/gRPC/broker propagation up to maxDepth hops; for intra-process function-to-function call paths between two methods use get_call_chain. Queries the indexed .codeexplorer/graph.db where serviceName matches case-insensitively (anchoring 'flow' traversal to entryPoint when provided and clamping maxDepth to 1..10), returning YAML contracts, handler coordinates, or distributed hop chains (or an empty result if none exist).")]
    public async Task<CallToolResult> GetServiceContractsAsync(
        [Description("Required service or project name (case-insensitive exact or substring match, e.g. 'OrderService' or 'WebApi').")] string serviceName,
        [AllowedValues("all", "ingress", "egress", "entry_points", "flow")]
        [Description("Boundary inspection mode: 'all' (ingress and egress contracts), 'ingress' (exposed endpoints and subscribed topics), 'egress' (outbound HTTP/gRPC calls and published topics), 'entry_points' (handler symbols and file coordinates), or 'flow' (multi-hop cross-service propagation trace). Default: 'all'.")] string direction = "all",
        [Description("Optional URL route substring, topic name, or handler method name within serviceName to anchor traversal when direction='flow' (e.g. '/api/orders' or 'CreateOrder').")] string? entryPoint = null,
        [Range(1, 10)]
        [Description("Maximum cross-service hop depth to traverse when direction='flow' (integer 1 to 10, default: 3).")] int maxDepth = 3,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return WrapError(MissingServiceNameError);
        }

        var normDirection = string.IsNullOrWhiteSpace(direction) ? DirectionAll : direction.Trim().ToLowerInvariant();
        return await ExecuteForWorkspaceAsync(workspacePath, ws => normDirection switch
        {
            DirectionEntryPoints => repository.GetProjectEntryPointsAsync(serviceName, ws, cancellationToken),
            DirectionFlow => repository.TraceCrossServiceFlowAsync(serviceName, entryPoint, maxDepth, ws, cancellationToken),
            _ => repository.GetServiceContractsAsync(serviceName, normDirection, ws, cancellationToken)
        }, cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> TraceCrossServiceFlowAsync(
        string startService,
        string? entryPoint = null,
        int maxDepth = 3,
        string? workspacePath = null,
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
    [McpServerTool(Title = "Find Code Symbols and Ontology Identifiers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Search the indexed workspace graph for code declarations and architectural entities ('Class', 'Interface', 'Type', 'Function', 'Variable'/'Member', 'Endpoint', 'Table', 'Topic', or 'Project') using deterministic substring matching ('substring') or hybrid trigram and semantic vector similarity ('hybrid'). Use 'substring' when locating symbols by known or partial identifier name, and 'hybrid' for fuzzy or keyword discovery across routes, tables, topics, and code; to list all symbols inside a known file or folder path use get_file_outline, and to read source implementation lines use fetch_code_snippets. Queries .codeexplorer/graph.db case-insensitively (automatically routing 'Endpoint'/'Table'/'Topic'/'Project' filters or matchMode='hybrid' through ranked lexical-vector search and capping output at limit rows), returning YAML matches with kind, qualified name, file_path, and line coordinates (or an empty list if none match).")]
    public async Task<CallToolResult> FindSymbolAsync(
        [Description("Required symbol name, substring, URL route fragment, table name, or domain keyword (e.g. 'OrderController', 'ValidateToken', or '/api/orders').")] string name,
        [AllowedValues("Class", "Interface", "Type", "Function", "Variable", "Member", "Endpoint", "Table", "Topic", "Project")]
        [Description("Optional entity filter: 'Class', 'Interface', 'Type', 'Function', 'Variable', 'Member', 'Endpoint', 'Table', 'Topic', or 'Project' ('Variable' and 'Member' are aliases). Omit to search all kinds.")] string? symbolType = null,
        [AllowedValues("substring", "hybrid")]
        [Description("Search strategy: 'substring' (exact and substring AST symbol match) or 'hybrid' (fuzzy trigram + semantic vector similarity across code, routes, tables, and topics). Default: 'substring'.")] string matchMode = "substring",
        [Range(1, 500)]
        [Description("Maximum number of matching symbols to return (1 to 500, default: 50; clamped to 200 in 'hybrid' mode).")] int limit = 50,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return WrapError(MissingNameError);
        }

        var useHybrid = string.Equals(matchMode, MatchModeHybrid, StringComparison.OrdinalIgnoreCase)
            || symbolType is SymLabelEndpoint or SymLabelTable or SymLabelTopic or SymLabelProject;

        return await ExecuteForWorkspaceAsync(workspacePath, ws =>
        {
            if (useHybrid)
            {
                var mappedFilter = symbolType switch
                {
                    SymLabelClass or SymLabelInterface => SymLabelType,
                    SymLabelVariable => SymLabelMember,
                    _ => symbolType
                };
                return repository.SearchIdentifiersAsync(name, mappedFilter, Math.Clamp(limit, 1, 200), ws, cancellationToken);
            }

            return repository.FindSymbolAsync(name, symbolType, limit, ws, cancellationToken);
        }, cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Trace Function-to-Function Call Chain", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Find directed function-to-function call paths connecting a specific startFunction to a specific endFunction within the indexed code graph up to maxDepth hops. Use only when both the starting caller and destination callee are known; to list all direct 1-hop callers or concrete interface implementations use get_callers, for multi-hop transitive blast radius of one symbol use analyze_code_impact, and for cross-service HTTP/messaging flows use get_service_contracts with direction='flow'. Evaluates bounded shortest-path traversal (clamped to 1..10 hops) in .codeexplorer/graph.db without modifying state and returns all discovered call chains in YAML with ordered function names, file paths, and line numbers (or an empty result if no path connects them within maxDepth).")]
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
    public async Task<CallToolResult> ResolveCallTargetAsync(
        string interfaceName,
        string methodName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interfaceName) || string.IsNullOrEmpty(methodName))
        {
            return WrapError(MissingInterfaceOrMethodNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ResolveCallTargetAsync(interfaceName, methodName, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Find Inbound Callers or Interface Implementations", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all direct 1-hop inbound callers, references, service consumers, or table/topic accessors targeting a named symbol or resource, or resolve concrete class implementations when methodName is specified for an interface or abstract base type target. Use without methodName to inspect immediate 1-hop consumers of target, or pass methodName to resolve polymorphic DI implementations of target.methodName; for multi-hop transitive blast radius use analyze_code_impact, and for point-to-point paths between two functions use get_call_chain. Queries CALLS, ACCESSED_BY, DEPENDS_ON, IMPLEMENTS, and INHERITS_FROM edges in .codeexplorer/graph.db (excluding test callers when includeTests=false) and returns YAML records with symbol, kind, file_path, and line coordinates (or an empty list if none match).")]
    public async Task<CallToolResult> GetCallersAsync(
        [Description("Required target name (function, class/interface, service, project, database table, or messaging topic; acts as the interface/abstract type name when methodName is provided).")] string target,
        [Description("Whether to include callers located in unit/integration test projects when listing inbound callers. Default: false (production callers only).")] bool includeTests = false,
        [Description("Optional interface or abstract method name declared on target (e.g. 'ProcessPaymentAsync'); when specified, resolves concrete implementing classes and DI targets instead of inbound callers.")] string? methodName = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(target))
        {
            return WrapError(MissingTargetError);
        }

        return await ExecuteForWorkspaceAsync(workspacePath, ws =>
            !string.IsNullOrWhiteSpace(methodName)
                ? repository.ResolveCallTargetAsync(target, methodName, ws, cancellationToken, includeTests)
                : repository.GetCallersAsync(target, includeTests, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> SearchIdentifiersAsync(
        string query,
        string? kind = null,
        int limit = 15,
        string? workspacePath = null,
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
