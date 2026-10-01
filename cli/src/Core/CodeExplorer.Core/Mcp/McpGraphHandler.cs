using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
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
    [Description("Primary architecture tool for inspecting workspace structure. Supports multi-level architectural views: 'c1' / 'system' (cross-service, external APIs, databases, brokers), 'c2' / 'service' (inter-project dependencies & service flows), 'c3' / 'component' (internal packages, controllers, namespaces), 'domain' / 'context' (bounded contexts synthesized from projects and domains), and 'tiers' (3-tier layered breakdown). Supports output formats: 'markdown', 'toon', 'mermaid', 'json'.")]
    public async Task<CallToolResult> GetArchitectureViewAsync(
        [Description("View level: 'c1' (system context), 'c2' (service flow), 'c3' (component), 'domain' (bounded contexts), or 'tiers' (layered architecture). Default: 'c1'.")] string level = "c1",
        [Description("Optional scope (e.g. project or service name) to isolate in the view.")] string? scope = null,
        [Description("Whether to include shared libraries and utility projects. Default: true.")] bool includeLibraries = true,
        [Description("Output format: 'markdown', 'toon', 'mermaid', or 'json'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetArchitectureViewAsync(level, scope, includeLibraries, format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Retrieves the AI-distilled Bounded Contexts Map (DDD Context Map) containing business domains, ubiquitous language entities, emitted and handled events, layer breakdown, CQRS roles, and cross-context interactions (service calls, shared databases, shared message queues).")]
    public async Task<CallToolResult> GetBoundedContextsAsync(
        [Description("Output format: 'markdown', 'mermaid', 'json', or 'toon'. Default: 'markdown'.")] string format = "markdown",
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetBoundedContextsAsync(format, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Returns a lightweight, high-level overview of the workspace architecture, grouping projects into semantic layers (Core/Domain, Services/Backend, UI/Presentation, Infrastructure/Data, Tests) with project counts, databases, external services, and summary statistics. Ideal starting point for understanding repository structure without heavy payloads.")]
    public async Task<CallToolResult> GetArchitectureOverviewAsync(
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetArchitectureOverviewAsync(GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Returns the high-level infrastructure map of the workspace, including workspace folders, projects, their internal folders, and associated databases. Use this at the start of a task to understand the component boundaries.")]
    public async Task<CallToolResult> GetArchitectureMapAsync(
        [Description("Optional filter for a specific project name (e.g., 'AuthService'). If omitted, returns the top-level workspace structure.")] string? projectName = null,
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetArchitectureMapAsync(projectName, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Retrieves the complete dependency graph between projects. Supports 'markdown', 'mermaid' diagrams, and 'json' outputs, with filtering by runtime vs build/package dependencies.")]
    public async Task<CallToolResult> GetProjectDependenciesAsync(
        [Description("Optional name of a specific project to isolate its incoming and outgoing dependencies.")] string? projectFilter = null,
        [Description("Output format: 'markdown', 'mermaid', 'json', 'yaml', or 'toon' (default: 'markdown').")] string format = "markdown",
        [Description("Maximum results to return (default: 50).")] int limit = 50,
        [Description("Filter dependency types: 'all', 'runtime' (direct project-to-project & service calls), or 'build' (compile/package dependencies). Default: 'all'.")] string type = "all",
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetProjectDependenciesAsync(projectFilter, format, limit, type, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Lists ingress and egress communication contracts for a specific service (HTTP endpoints, gRPC calls, message publishers, and event subscribers).")]
    public async Task<CallToolResult> GetServiceContractsAsync(
        [Description("The name of the service or project (e.g., 'AuthService' or 'OrderApi').")] string serviceName,
        [Description("Direction of contracts: 'ingress' (incoming APIs/subscribers), 'egress' (outgoing HTTP calls/publishers), or 'all'. Default: 'all'.")] string direction = "all",
        [Description("Output format: 'markdown', 'toon', 'mermaid', or 'json'. Default: 'markdown'.")] string format = "markdown",
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
    [Description("Traces end-to-end distributed execution flow across microservices starting from an ingress controller or message topic (e.g. Gateway -> Orders -> RabbitMQ -> Payments -> DB).")]
    public async Task<CallToolResult> TraceCrossServiceFlowAsync(
        [Description("Starting service or project name (e.g. 'Gateway' or 'OrderService').")] string startService,
        [Description("Optional specific entry point (endpoint URL or method name) to narrow the trace.")] string? entryPoint = null,
        [Description("Maximum service traversal depth (1-10, default: 3).")] int maxDepth = 3,
        [Description("Output format: 'markdown', 'toon', 'mermaid', or 'json'. Default: 'markdown'.")] string format = "markdown",
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
    [Description("Extracts the internal structural outline of a specific file without reading its entire source code text. Returns results as a structured Markdown outline or JSON.")]
    public async Task<CallToolResult> GetFileOutlineAsync(
        [Description("The full or relative path to the file (e.g., 'src/Services/Auth/User.cs').")] string filePath,
        [Description("Output format: 'markdown', 'json', 'yaml', or 'toon' (default: 'markdown').")] string format = "markdown",
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
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
    [Description("Searches the semantic graph for code symbols (Classes, Interfaces, Functions, or Variables) matching a partial or full name. Returns results as a compact Markdown table or JSON.")]
    public async Task<CallToolResult> FindSymbolAsync(
        [Description("The name or part of the name of the symbol to find (e.g., 'OrderProcessor').")] string name,
        [Description("Optional explicit filter by symbol type ('Class', 'Interface', 'Function', 'Variable').")] string? symbolType = null,
        [Description("Output format: 'markdown', 'json', 'yaml', or 'toon' (default: 'markdown').")] string format = "markdown",
        [Description("Maximum results to return (default: 50).")] int limit = 50,
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
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
    [Description("Traces and builds a sequential execution path (call graph) between a starting function and a target function. Supports 'markdown', 'mermaid', and 'json' outputs.")]
    public async Task<CallToolResult> GetCallChainAsync(
        [Description("The full name or symbol of the originating function (e.g., 'SubmitOrder').")] string startFunction,
        [Description("The full name or symbol of the destination function (e.g., 'SaveToDb').")] string endFunction,
        [Description("Maximum call stack depth to traverse in the graph (1-10, default: 5).")] int maxDepth = 5,
        [Description("Output format: 'markdown', 'mermaid', 'json', 'yaml', or 'toon' (default: 'markdown').")] string format = "markdown",
        [Description("Optional workspace root path. If omitted, uses current workspace context.")] string? workspacePath = null,
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
    [Description("Resolves Polymorphism and Dependency Injection blind spots. When a function calls an abstract Interface method, this tool queries the graph to find all concrete Classes implementing that interface and points to their real physical function implementations.")]
    public async Task<CallToolResult> ResolveCallTargetAsync(
        [Description("The name of the interface being checked (e.g., 'IPaymentGateway').")] string interfaceName,
        [Description("The specific interface method name being called (e.g., 'ProcessPayment').")] string methodName,
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
    [Description("Performs a deterministic blast-radius analysis before refactoring. Given a Class, Interface, or Function, it tracks all incoming structural links ('CALLS', 'USES_TYPE') to identify every file and component that will be broken or affected by changing this symbol.")]
    public async Task<CallToolResult> AnalyzeCodeImpactAsync(
        [Description("The full name of the class, interface, or function to analyze.")] string symbolName,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(symbolName))
        {
            return WrapError("Missing 'symbolName' argument.");
        }
        return await ExecuteAsync(() => repository.AnalyzeCodeImpactAsync(symbolName, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Finds exact unit and integration test methods affected by changed source files, git diff, or symbols via reverse static call graph traversal. Enables Test Impact Analysis (TIA) and outputs ready-to-run test runner commands.")]
    public async Task<CallToolResult> GetAffectedTestsAsync(
        [Description("The relative or absolute path of the changed file (e.g. 'src/Services/OrderService.cs').")] string? filePath = null,
        [Description("List of changed file paths.")] string[]? filePaths = null,
        [Description("Raw git unified diff content to inspect modified lines and hunks.")] string? gitDiff = null,
        [Description("The full name of the modified function, class, or symbol.")] string? symbolName = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetAffectedTestsAsync(filePath, symbolName, filePaths, gitDiff, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Analyzes static test reachability coverage across the workspace: returns covered and uncovered classes and methods, coverage percentages, and covering test counts without running test suites.")]
    public async Task<CallToolResult> GetTestCoverageAsync(
        [Description("Filter by project name (e.g., 'OrderService').")] string? projectName = null,
        [Description("Filter by directory or file path prefix.")] string? pathPrefix = null,
        [Description("Filter status: 'all', 'covered', or 'uncovered'.")] string? status = null,
        [Description("Maximum number of covered/uncovered items to return.")] int? limit = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetTestCoverageAsync(projectName, pathPrefix, status, limit, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Bridges the gap between code and data. Tracks the blast radius of database changes by finding every raw SQL text or ORM Query (Query), the source File it resides in, and the Functions that invoke it based on a target physical Database Table name.")]
    public async Task<CallToolResult> InspectDataLineageAsync(
        [Description("The exact name of the database table to inspect (e.g., 'orders' or 'users').")] string tableName,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tableName))
        {
            return WrapError("Missing 'tableName' argument.");
        }
        return await ExecuteAsync(() => repository.InspectDataLineageAsync(tableName, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Finds all architectural entry points inside a project. It scans structural routing folders (Controllers, Endpoints, EventHandlers) to return top-level functions that trigger system execution flows.")]
    public async Task<CallToolResult> GetProjectEntryPointsAsync(
        [Description("The name of the target project (e.g., 'Gateway.Api').")] string projectName,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError("Missing 'projectName' argument.");
        }
        return await ExecuteAsync(() => repository.GetProjectEntryPointsAsync(projectName, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Scans the project to find code health anomalies. Identifies 'Dead Code' (Functions or Classes with 0 incoming CALLS/USES_TYPE edges) and 'God Objects' (Classes containing a disproportionately large number of members).")]
    public async Task<CallToolResult> FindRefactoringOpportunitiesAsync(
        [Description("The project to audit for dead code and architectural bloat.")] string projectName,
        [Description("Filter by a specific health metric anomaly type ('dead_code', 'god_objects', 'all').")] string metricType = "all",
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError("Missing 'projectName' argument.");
        }
        return await ExecuteAsync(() => repository.FindRefactoringOpportunitiesAsync(projectName, metricType, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Advanced escape-hatch tool. Allows the agent to write and execute custom Cypher read-queries (MATCH only) directly against the graph database when predefined tools are insufficient for complex analytical insights. Mutating queries (CREATE, DELETE, SET) are strictly blocked.")]
    public async Task<CallToolResult> ExecuteCustomReadCypherAsync(
        [Description("A valid read-only Cypher query targeted at the CodeExplorer taxonomy schema.")] string query,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            return WrapError("Missing 'query' argument.");
        }
        return await ExecuteAsync(() => repository.ExecuteCustomReadCypherAsync(query, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Retrieves the full structural taxonomy database schema mapping all active node types and their incoming/outgoing relationships.")]
    public async Task<CallToolResult> GetTaxonomyAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetTaxonomyAsync(GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Fetches the actual source code snippets for the given list of URN/node contexts. Receives a serialized JSON string containing one or more nodes with 'file_path', 'start_line', and 'end_line' specified.")]
    public async Task<CallToolResult> FetchCodeSnippets(
        [Description("JSON string representing the RAG node(s) to fetch snippets for.")] string nodesJson,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(nodesJson))
        {
            return WrapError("Missing 'nodesJson' argument.");
        }
        return await ExecuteAsync(() => repository.FetchCodeSnippetsAsync(nodesJson, GetCurrentWorkspacePath(workspacePath), cancellationToken));
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

    [UsedImplicitly]
    [McpServerTool]
    [Description("Exports visual architecture and system topology diagrams directly from the knowledge graph in Mermaid or C4 syntax (flowcharts, container diagrams, data lineage, or CQRS pipeline diagrams).")]
    public async Task<CallToolResult> ExportArchitectureDiagramAsync(
        [Description("Diagram syntax format: 'mermaid' (default) or 'c4'.")] string format = "mermaid",
        [Description("Diagram type: 'architecture' (default system topology), 'domain' (domain microservices and bounded contexts map), 'lineage' (data model/entities to tables), or 'cqrs' (events/commands/saga message flow).")] string type = "architecture",
        [Description("Optional project name to scope the diagram to a specific subsystem.")] string? project = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.ExportArchitectureDiagramAsync(
            format, type, project, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Initializes a new '.codeexplorer' workspace in the target directory if not already created.")]
    public async Task<CallToolResult> InitWorkspaceAsync(
        [Description("Optional display name for the workspace (defaults to target directory name).")] string? name = null,
        [Description("Optional workspace root directory path. Defaults to current directory.")] string? workspacePath = null,
        [Description("If true, reinitializes an existing workspace (default: false).")] bool force = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.InitWorkspaceAsync(
            name, workspacePath ?? GetCurrentWorkspacePath(), force, cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Scans and indexes source files, projects, ASTs, and dependencies into the embedded graph database. Call this after editing code or adding new files to refresh the knowledge graph.")]
    public async Task<CallToolResult> ScanWorkspaceAsync(
        [Description("Optional relative or absolute subfolder path to scan (defaults to entire workspace).")] string? path = null,
        [Description("If true, clears previous index data for the specified path before scanning (default: false).")] bool clear = false,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.ScanWorkspaceAsync(
            path, clear, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Returns the workspace health status, SQLite database size, indexed projects by language, total node/relationship counts, and custom query counts.")]
    public async Task<CallToolResult> GetWorkspaceStatusAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetWorkspaceStatusAsync(
            workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Clears indexed data from the workspace database for a specific subpath or the entire workspace.")]
    public async Task<CallToolResult> ClearWorkspaceIndexAsync(
        [Description("Optional subpath to selectively clear. If omitted or set to workspace root, clears all graph data.")] string? path = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.ClearWorkspaceIndexAsync(
            path, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Direct batch ingestion of custom/external nodes and relationships into the graph database.")]
    public async Task<CallToolResult> IngestGraphDataAsync(
        [Description("JSON array of node objects: [{\"id\":\"...\",\"kind\":\"...\",\"properties\":{...}}]")] string nodesJson,
        [Description("Optional JSON array of relationship objects: [{\"type\":\"...\",\"from_id\":\"...\",\"to_id\":\"...\",\"properties\":{...}}]")] string? relationshipsJson = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodesJson))
            return WrapError("Missing 'nodesJson' argument.");

        return await ExecuteAsync(() => repository.IngestGraphDataAsync(
            nodesJson, relationshipsJson, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Lists, adds, or removes architectural domain definitions in .codeexplorer/domains.json.")]
    public Task<CallToolResult> ManageDomainAsync(
        [Description("Action to perform: 'list', 'add', or 'remove'.")] string action,
        [Description("Domain name (required for 'add' and 'remove').")] string? name = null,
        [Description("Human-readable display name (e.g. 'Billing & Payments').")] string? displayName = null,
        [Description("Domain description.")] string? description = null,
        [Description("Domain icon (e.g. emoji or icon identifier).")] string? icon = null,
        [Description("Hex color code (e.g. '#3b82f6').")] string? color = null,
        [Description("Target domain to reassign existing services to when deleting a domain.")] string? reassignTo = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var wsRoot = workspacePath ?? GetCurrentWorkspacePath();
        if (string.IsNullOrWhiteSpace(wsRoot)) return Task.FromResult(WrapError("No active workspace found."));

        switch (action.ToLowerInvariant())
        {
            case "list":
                var config = DomainManagementService.LoadConfig(wsRoot);
                return Task.FromResult(WrapResult(System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })));

            case "add":
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(WrapError("Domain 'name' is required for 'add' action."));
                var domain = DomainManagementService.AddOrUpdateDomain(wsRoot, new DomainDefinitionDto
                {
                    Name = name.Trim(),
                    DisplayName = displayName,
                    Description = description,
                    Icon = icon,
                    Color = color
                });
                return Task.FromResult(WrapResult($"Domain '{domain.Name}' added/updated in {DomainManagementService.GetConfigFilePath(wsRoot)}."));

            case "remove":
            case "delete":
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(WrapError("Domain 'name' is required for 'remove' action."));
                var removed = DomainManagementService.RemoveDomain(wsRoot, name.Trim(), reassignTo);
                return Task.FromResult(removed
                    ? WrapResult($"Domain '{name}' removed from {DomainManagementService.GetConfigFilePath(wsRoot)}.")
                    : WrapError($"Domain '{name}' not found."));

            default:
                return Task.FromResult(WrapError($"Unknown action '{action}'. Valid actions are 'list', 'add', 'remove'."));
        }
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Assigns or overrides the architectural domain and/or bounded context for a specific service or project.")]
    public async Task<CallToolResult> AssignServiceDomainAsync(
        [Description("The service or project name to override.")] string serviceName,
        [Description("The target domain name (required unless removeOverride is true).")] string? domainName = null,
        [Description("Optional bounded context name.")] string? boundedContext = null,
        [Description("If true, removes any existing override for this service instead of adding one.")] bool removeOverride = false,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var wsRoot = workspacePath ?? GetCurrentWorkspacePath();
        if (string.IsNullOrWhiteSpace(wsRoot)) return WrapError("No active workspace found.");

        if (removeOverride)
        {
            var removed = DomainManagementService.RemoveServiceOverride(wsRoot, serviceName);
            return removed
                ? WrapResult($"Override removed for service '{serviceName}'.")
                : WrapError($"No override found for service '{serviceName}'.");
        }

        if (string.IsNullOrWhiteSpace(domainName))
        {
            return WrapError("'domainName' is required when assigning a domain.");
        }

        var client = await repository.ResolveClientAsync(wsRoot);
        DomainManagementService.AssignServiceDomain(wsRoot, serviceName, domainName, boundedContext, client, cancellationToken);
        return WrapResult($"Assigned service '{serviceName}' to domain '{domainName}'" +
            (string.IsNullOrWhiteSpace(boundedContext) ? "" : $" (context: '{boundedContext}')") + ".");
    }
}
