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
public class McpGraphHandler(
    CodeExplorerRepository repository,
    IHttpContextAccessor httpContextAccessor,
    ILogger<McpGraphHandler>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;
    private string? GetCurrentWorkspacePath()
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
                return workspacePath;
            }
        }
        return repository.DefaultWorkspacePath ?? Common.WorkspaceLocator.Find()?.RootDirectory;
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

    // [UsedImplicitly]
    // [McpServerTool(Name = "initialize")]
    // [Description("Initialize the workspace")]
    // public async Task<CallToolResult> InitializeAsync(
    //     [Description("Workspace path")] string? path = null)
    // {
    //     return new CallToolResult
    //     {
    //         Content = new List<ContentBlock>
    //         {
    //             new TextContentBlock
    //             {
    //                 Text = "Initializing workspace..."
    //             }
    //         }
    //     };
    // }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Returns a lightweight, high-level overview of the workspace architecture, grouping projects into semantic layers (Core/Domain, Services/Backend, UI/Presentation, Infrastructure/Data, Tests) with project counts, databases, external services, and summary statistics. Ideal starting point for understanding repository structure without heavy payloads.")]
    public async Task<CallToolResult> GetArchitectureOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetArchitectureOverviewAsync(GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Returns the high-level infrastructure map of the workspace, including workspace folders, projects, their internal folders, and associated databases. Use this at the start of a task to understand the component boundaries.")]
    public async Task<CallToolResult> GetArchitectureMapAsync(
        [Description("Optional filter for a specific project name (e.g., 'AuthService'). If omitted, returns the top-level workspace structure.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetArchitectureMapAsync(projectName, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Retrieves the complete dependency graph between projects. Returns both direct project-to-project dependencies and transitive dependencies mediated through internal or external Packages.")]
    public async Task<CallToolResult> GetProjectDependenciesAsync(
        [Description("Optional name of a specific project to isolate its incoming and outgoing dependencies.")] string? projectFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetProjectDependenciesAsync(projectFilter, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Extracts the internal structural outline of a specific file without reading its entire source code text. Returns the names, types, and start/end lines of Classes, Interfaces, Functions, Variables, and Queries defined inside.")]
    public async Task<CallToolResult> GetFileOutlineAsync(
        [Description("The full or relative path to the file (e.g., 'src/Services/Auth/User.cs').")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return WrapError("Missing 'filePath' argument.");
        }
        return await ExecuteAsync(() => repository.GetFileOutlineAsync(filePath, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Searches the semantic graph for code symbols (Classes, Interfaces, Functions, or Variables) matching a partial or full name. Returns their type, full path, and declaration coordinates.")]
    public async Task<CallToolResult> FindSymbolAsync(
        [Description("The name or part of the name of the symbol to find (e.g., 'OrderProcessor').")] string name,
        [Description("Optional explicit filter by symbol type ('Class', 'Interface', 'Function', 'Variable').")] string? symbolType = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return WrapError("Missing 'name' argument.");
        }
        return await ExecuteAsync(() => repository.FindSymbolAsync(name, symbolType, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Traces and builds a sequential execution path (call graph) between a starting function and a target function. Essential for debugging business logic flows and tracing processing pipelines.")]
    public async Task<CallToolResult> GetCallChainAsync(
        [Description("The full name or symbol of the originating function (e.g., 'SubmitOrder').")] string startFunction,
        [Description("The full name or symbol of the destination function (e.g., 'SaveToDb').")] string endFunction,
        [Description("Maximum call stack depth to traverse in the graph (1-10).")] int maxDepth = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(startFunction) || string.IsNullOrEmpty(endFunction))
        {
            return WrapError("Missing 'startFunction' or 'endFunction' argument.");
        }
        return await ExecuteAsync(() => repository.GetCallChainAsync(startFunction, endFunction, maxDepth, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Resolves Polymorphism and Dependency Injection blind spots. When a function calls an abstract Interface method, this tool queries the graph to find all concrete Classes implementing that interface and points to their real physical function implementations.")]
    public async Task<CallToolResult> ResolveCallTargetAsync(
        [Description("The name of the interface being checked (e.g., 'IPaymentGateway').")] string interfaceName,
        [Description("The specific interface method name being called (e.g., 'ProcessPayment').")] string methodName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interfaceName) || string.IsNullOrEmpty(methodName))
        {
            return WrapError("Missing 'interfaceName' or 'methodName' argument.");
        }
        return await ExecuteAsync(() => repository.ResolveCallTargetAsync(interfaceName, methodName, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Performs a deterministic blast-radius analysis before refactoring. Given a Class, Interface, or Function, it tracks all incoming structural links ('CALLS', 'USES_TYPE') to identify every file and component that will be broken or affected by changing this symbol.")]
    public async Task<CallToolResult> AnalyzeCodeImpactAsync(
        [Description("The full name of the class, interface, or function to analyze.")] string symbolName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(symbolName))
        {
            return WrapError("Missing 'symbolName' argument.");
        }
        return await ExecuteAsync(() => repository.AnalyzeCodeImpactAsync(symbolName, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Bridges the gap between code and data. Tracks the blast radius of database changes by finding every raw SQL text or ORM Query (Query), the source File it resides in, and the Functions that invoke it based on a target physical Database Table name.")]
    public async Task<CallToolResult> InspectDataLineageAsync(
        [Description("The exact name of the database table to inspect (e.g., 'orders' or 'users').")] string tableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tableName))
        {
            return WrapError("Missing 'tableName' argument.");
        }
        return await ExecuteAsync(() => repository.InspectDataLineageAsync(tableName, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Finds all architectural entry points inside a project. It scans structural routing folders (Controllers, Endpoints, EventHandlers) to return top-level functions that trigger system execution flows.")]
    public async Task<CallToolResult> GetProjectEntryPointsAsync(
        [Description("The name of the target project (e.g., 'Gateway.Api').")] string projectName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError("Missing 'projectName' argument.");
        }
        return await ExecuteAsync(() => repository.GetProjectEntryPointsAsync(projectName, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Scans the project to find code health anomalies. Identifies 'Dead Code' (Functions or Classes with 0 incoming CALLS/USES_TYPE edges) and 'God Objects' (Classes containing a disproportionately large number of members).")]
    public async Task<CallToolResult> FindRefactoringOpportunitiesAsync(
        [Description("The project to audit for dead code and architectural bloat.")] string projectName,
        [Description("Filter by a specific health metric anomaly type ('dead_code', 'god_objects', 'all').")] string metricType = "all",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError("Missing 'projectName' argument.");
        }
        return await ExecuteAsync(() => repository.FindRefactoringOpportunitiesAsync(projectName, metricType, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Advanced escape-hatch tool. Allows the agent to write and execute custom Cypher read-queries (MATCH only) directly against the graph database when predefined tools are insufficient for complex analytical insights. Mutating queries (CREATE, DELETE, SET) are strictly blocked.")]
    public async Task<CallToolResult> ExecuteCustomReadCypherAsync(
        [Description("A valid read-only Cypher query targeted at the CodeExplorer taxonomy schema.")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            return WrapError("Missing 'query' argument.");
        }
        return await ExecuteAsync(() => repository.ExecuteCustomReadCypherAsync(query, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Retrieves the full structural taxonomy database schema mapping all active node types and their incoming/outgoing relationships.")]
    public async Task<CallToolResult> GetTaxonomyAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetTaxonomyAsync(GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Fetches the actual source code snippets for the given list of URN/node contexts. Receives a serialized JSON string containing one or more nodes with 'file_path', 'start_line', and 'end_line' specified.")]
    public async Task<CallToolResult> FetchCodeSnippets(
        [Description("JSON string representing the RAG node(s) to fetch snippets for.")] string nodesJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(nodesJson))
        {
            return WrapError("Missing 'nodesJson' argument.");
        }
        return await ExecuteAsync(() => repository.FetchCodeSnippetsAsync(nodesJson, GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Get documentation/schema details for a specific ontological Node Kind.")]
    public CallToolResult GetNodeDefinition(
        [Description("The kind of node to inspect (e.g. 'Workspace', 'Project', 'File', etc.)")] string kind)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return WrapError("Missing 'kind' argument.");
        }
        return Execute(() => repository.GetNodeDefinition(kind));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Lists all saved project-specific Cypher queries and their parameter schemas from '.codeexplorer/queries/'. Use this at the start of a task to discover existing custom architectural queries and audit rules for this workspace.")]
    public async Task<CallToolResult> ListProjectQueriesAsync(
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.ListProjectQueriesAsync(workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Saves a reusable, project-specific Cypher query with metadata into '.codeexplorer/queries/{name}.cypher' and '{name}.json'. Automatically compiles and validates Cypher syntax and read-only safety before saving. Use this when you identify a unique architectural pattern or recurring check for this codebase.")]
    public async Task<CallToolResult> SaveProjectQueryAsync(
        [Description("Unique identifier for the query (alphanumeric, underscores, hyphens, e.g. 'find_kafka_consumers').")] string name,
        [Description("Detailed description of what the query does and when the agent should use it.")] string description,
        [Description("The Cypher query text (may use parameters like $param, $workspaceId, etc.).")] string cypher,
        [Description("Optional JSON array defining parameters: [{\"name\":\"topicFilter\",\"type\":\"string\",\"required\":false,\"description\":\"...\"}]")] string? parametersJson = null,
        [Description("Optional description of returned fields (e.g. 'consumerClass, methodName, topicName').")] string? returns = null,
        [Description("Optional comma-separated tags (e.g. 'kafka,messaging,ingress').")] string? tags = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return WrapError("Missing 'name' argument.");
        if (string.IsNullOrWhiteSpace(description))
            return WrapError("Missing 'description' argument.");
        if (string.IsNullOrWhiteSpace(cypher))
            return WrapError("Missing 'cypher' argument.");

        return await ExecuteAsync(() => repository.SaveProjectQueryAsync(
            name, description, cypher, parametersJson, returns, tags, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Executes a previously saved project-specific Cypher query from '.codeexplorer/queries/' by name with optional parameters. Fast, optimized, and tailored to this repository.")]
    public async Task<CallToolResult> ExecuteProjectQueryAsync(
        [Description("The name of the saved query to execute (e.g. 'find_kafka_consumers').")] string name,
        [Description("Optional JSON object containing parameter key-value pairs (e.g. '{\"topicFilter\":\"orders\"}').")] string? parametersJson = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return WrapError("Missing 'name' argument.");

        return await ExecuteAsync(() => repository.ExecuteProjectQueryAsync(
            name, parametersJson, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }
}
