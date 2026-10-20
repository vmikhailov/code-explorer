using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CodeExplorer.Core.Common;
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
    ILogger<McpGraphHandler>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;
    private string? GetCurrentWorkspacePath(string? explicitWorkspace = null)
    {
        string? ws = null;
        if (!string.IsNullOrWhiteSpace(explicitWorkspace))
        {
            ws = explicitWorkspace;
        }
        else
        {
            var httpContext = httpContextAccessor?.HttpContext;
            if (httpContext != null)
            {
                var workspacePath = httpContext.Request.Query["ws"].ToString();
                if (string.IsNullOrEmpty(workspacePath))
                {
                    workspacePath = httpContext.Request.Query["workspacePath"].ToString();
                }
                if (!string.IsNullOrEmpty(workspacePath))
                {
                    ws = workspacePath;
                }
            }
            if (string.IsNullOrEmpty(ws))
            {
                ws = repository.DefaultWorkspacePath ?? Common.WorkspaceLocator.FindWithFallbacks()?.RootDirectory;
            }
        }

        if (!string.IsNullOrEmpty(ws))
        {
            WorkspaceConventions.LoadFromWorkspace(ws);
        }
        return ws;
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
        if (name.EndsWith("Async", StringComparison.Ordinal))
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
    [Description("Architecture view of workspace structure ('c1', 'c2', 'c3', 'domain', 'tiers'). Formats: markdown, toon, mermaid, json.")]
    public async Task<CallToolResult> GetArchitectureViewAsync(
        [Description("View level: 'c1', 'c2', 'c3', 'domain', or 'tiers'. Default: 'c1'.")] string level = "c1",
        [Description("Optional project/service name filter.")] string? scope = null,
        [Description("Include shared libraries. Default: false.")] bool includeLibraries = false,
        [Description("Format: 'markdown', 'toon', 'mermaid', 'json'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetArchitectureViewAsync(level, scope, includeLibraries, format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("DDD Context Map: domains, entities, events, CQRS roles, and cross-context interactions.")]
    public async Task<CallToolResult> GetBoundedContextsAsync(
        [Description("Format: 'markdown', 'mermaid', 'json', 'toon'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetBoundedContextsAsync(format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("High-level architecture overview grouped by semantic layers (Domain, Services, UI, Infra, Tests).")]
    public async Task<CallToolResult> GetArchitectureOverviewAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetArchitectureOverviewAsync(GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Infrastructure map: workspace folders, projects, internal folders, and databases.")]
    public async Task<CallToolResult> GetArchitectureMapAsync(
        [Description("Optional project filter.")] string? projectName = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetArchitectureMapAsync(projectName, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Project dependency graph with runtime vs build filtering.")]
    public async Task<CallToolResult> GetProjectDependenciesAsync(
        [Description("Optional project name filter.")] string? projectFilter = null,
        [Description("Format: 'markdown', 'mermaid', 'json', 'yaml', 'toon'. Default: 'markdown'.")] string format = "markdown",
        [Description("Max results (default: 50).")] int limit = 50,
        [Description("Type: 'all', 'runtime', or 'build'. Default: 'all'.")] string type = "all",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetProjectDependenciesAsync(projectFilter, format, limit, type, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Ingress and egress contracts for a service (endpoints, RPC, publishers, subscribers).")]
    public async Task<CallToolResult> GetServiceContractsAsync(
        [Description("Service or project name.")] string serviceName,
        [Description("Direction: 'ingress', 'egress', or 'all'. Default: 'all'.")] string direction = "all",
        [Description("Format: 'markdown', 'toon', 'mermaid', 'json'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return WrapError("Missing 'serviceName' argument.");
        }
        return await ExecuteAsync(() => repository.GetServiceContractsAsync(serviceName, direction, format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Trace distributed flow across microservices starting from an endpoint or topic.")]
    public async Task<CallToolResult> TraceCrossServiceFlowAsync(
        [Description("Starting service name.")] string startService,
        [Description("Optional entry point URL or method name.")] string? entryPoint = null,
        [Description("Max traversal depth (1-10, default: 3).")] int maxDepth = 3,
        [Description("Format: 'markdown', 'toon', 'mermaid', 'json'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(startService))
        {
            return WrapError("Missing 'startService' argument.");
        }
        return await ExecuteAsync(() => repository.TraceCrossServiceFlowAsync(startService, entryPoint, maxDepth, format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Extract file structural outline without reading source text.")]
    public async Task<CallToolResult> GetFileOutlineAsync(
        [Description("File path relative to workspace or absolute.")] string filePath,
        [Description("Format: 'markdown', 'json', 'yaml', 'toon'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return WrapError("Missing 'filePath' argument.");
        }
        return await ExecuteAsync(() => repository.GetFileOutlineAsync(filePath, format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Search graph for code symbols (Class, Interface, Function, Variable).")]
    public async Task<CallToolResult> FindSymbolAsync(
        [Description("Symbol name or partial name.")] string name,
        [Description("Optional type: 'Class', 'Interface', 'Function', 'Variable'.")] string? symbolType = null,
        [Description("Format: 'markdown', 'json', 'yaml', 'toon'. Default: 'markdown'.")] string format = "markdown",
        [Description("Max results (default: 50).")] int limit = 50,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return WrapError("Missing 'name' argument.");
        }
        return await ExecuteAsync(() => repository.FindSymbolAsync(name, symbolType, format, limit, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Trace sequential call graph between start and end functions.")]
    public async Task<CallToolResult> GetCallChainAsync(
        [Description("Originating function name.")] string startFunction,
        [Description("Destination function name.")] string endFunction,
        [Description("Max depth (1-10, default: 5).")] int maxDepth = 5,
        [Description("Format: 'markdown', 'mermaid', 'json', 'yaml', 'toon'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(startFunction) || string.IsNullOrEmpty(endFunction))
        {
            return WrapError("Missing 'startFunction' or 'endFunction' argument.");
        }
        return await ExecuteAsync(() => repository.GetCallChainAsync(startFunction, endFunction, maxDepth, format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
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
        return await ExecuteAsync(() => repository.ResolveCallTargetAsync(interfaceName, methodName, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Find all callers, inbound references, service consumers, or table/topic accessors for a target (functions, microservices, database tables, messaging topics). Prioritize this tool over grep for discovering callers.")]
    public async Task<CallToolResult> GetCallersAsync(
        [Description("Target name (Function, Service, Project, Table, Topic, or Type).")] string target,
        [Description("Format: 'markdown', 'toon', 'json', 'table'. Default: 'markdown'.")] string format = "markdown",
        [Description("Include test callers (default: false).")] bool includeTests = false,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(target))
        {
            return WrapError("Missing 'target' argument.");
        }
        return await ExecuteAsync(() => repository.GetCallersAsync(target, format, includeTests, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Fast hybrid vector and trigram search across code identifiers, routes, tables, and topics. Finds exact or fuzzy matches with sub-millisecond ranking.")]
    public async Task<CallToolResult> SearchIdentifiersAsync(
        [Description("Search query (symbol name, route, or colloquial keyword).")] string query,
        [Description("Optional filter by node kind ('Type', 'Function', 'Member', 'Endpoint', 'Table', 'Topic', 'Project').")] string? kind = null,
        [Description("Maximum results to return (default: 15).")] int limit = 15,
        [Description("Format: 'markdown', 'json'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            return WrapError("Missing 'query' argument.");
        }
        return await ExecuteAsync(() => repository.SearchIdentifiersAsync(query, kind, limit, format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Blast-radius impact analysis: all callers, dependents, and references affected by modifying a symbol (Class, Interface, Function, Service, Table). Prioritize over grep.")]
    public async Task<CallToolResult> AnalyzeCodeImpactAsync(
        [Description("Class, interface, function, service, or table name.")] string symbolName,
        [Description("Format: 'markdown', 'toon', 'json', 'table'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(symbolName))
        {
            return WrapError("Missing 'symbolName' argument.");
        }
        return await ExecuteAsync(() => repository.AnalyzeCodeImpactAsync(symbolName, format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }
}
