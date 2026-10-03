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
    [Description("Blast-radius impact analysis: all callers and dependents affected by modifying a symbol.")]
    public async Task<CallToolResult> AnalyzeCodeImpactAsync(
        [Description("Class, interface, or function name.")] string symbolName,
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
    [Description("Test Impact Analysis (TIA): finds tests affected by changed files, git diff, or symbols.")]
    public async Task<CallToolResult> GetAffectedTestsAsync(
        [Description("Changed file path.")] string? filePath = null,
        [Description("List of changed file paths.")] string[]? filePaths = null,
        [Description("Git unified diff content.")] string? gitDiff = null,
        [Description("Modified symbol name.")] string? symbolName = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetAffectedTestsAsync(filePath, symbolName, filePaths, gitDiff, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Static test reachability coverage: covered/uncovered classes and methods.")]
    public async Task<CallToolResult> GetTestCoverageAsync(
        [Description("Project name filter.")] string? projectName = null,
        [Description("Directory or file prefix filter.")] string? pathPrefix = null,
        [Description("Filter: 'all', 'covered', or 'uncovered'.")] string? status = null,
        [Description("Max items to return.")] int? limit = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetTestCoverageAsync(projectName, pathPrefix, status, limit, GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Data lineage: trace SQL/ORM queries, source files, and functions accessing a database table.")]
    public async Task<CallToolResult> InspectDataLineageAsync(
        [Description("Database table name.")] string tableName,
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
    [Description("Find entry points (controllers, handlers, endpoints) inside a project.")]
    public async Task<CallToolResult> GetProjectEntryPointsAsync(
        [Description("Project name.")] string projectName,
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
    [Description("Find code health anomalies: dead code (0 incoming edges) and god objects.")]
    public async Task<CallToolResult> FindRefactoringOpportunitiesAsync(
        [Description("Target project name.")] string projectName,
        [Description("Anomaly type: 'dead_code', 'god_objects', 'all'. Default: 'all'.")] string metricType = "all",
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
    [Description("Execute custom read-only Cypher query (MATCH only) against the code graph.")]
    public async Task<CallToolResult> ExecuteCustomReadCypherAsync(
        [Description("Read-only Cypher query.")] string query,
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
    [Description("Retrieve active graph schema (node kinds, relationship types, properties).")]
    public async Task<CallToolResult> GetTaxonomyAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetTaxonomyAsync(GetCurrentWorkspacePath(workspacePath), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Fetch source code snippets for given graph nodes.")]
    public async Task<CallToolResult> FetchCodeSnippets(
        [Description("JSON string of node objects with file_path, start_line, end_line.")] string nodesJson,
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
    [Description("Get schema and documentation for a specific ontological Node Kind.")]
    public CallToolResult GetNodeDefinition(
        [Description("Node kind name (e.g. 'Workspace', 'Project', 'File', 'Endpoint').")] string kind)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return WrapError("Missing 'kind' argument.");
        }
        return Execute(() => repository.GetNodeDefinition(kind));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("List saved project-specific Cypher queries in .codeexplorer/queries/.")]
    public async Task<CallToolResult> ListProjectQueriesAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.ListProjectQueriesAsync(workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Save a reusable Cypher query with metadata into .codeexplorer/queries/.")]
    public async Task<CallToolResult> SaveProjectQueryAsync(
        [Description("Query identifier (e.g. 'find_kafka_consumers').")] string name,
        [Description("Query purpose description.")] string description,
        [Description("Cypher query text with parameters ($param, etc.).")] string cypher,
        [Description("Optional parameter schema JSON.")] string? parametersJson = null,
        [Description("Optional description of returned fields.")] string? returns = null,
        [Description("Optional comma-separated tags.")] string? tags = null,
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
    [Description("Execute a saved project-specific Cypher query by name with parameters.")]
    public async Task<CallToolResult> ExecuteProjectQueryAsync(
        [Description("Saved query name.")] string name,
        [Description("Optional JSON object of parameter key-values.")] string? parametersJson = null,
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
    [Description("Export Mermaid or C4 system architecture diagrams from the knowledge graph.")]
    public async Task<CallToolResult> ExportArchitectureDiagramAsync(
        [Description("Format: 'mermaid' or 'c4'. Default: 'mermaid'.")] string format = "mermaid",
        [Description("Type: 'architecture', 'domain', 'lineage', 'cqrs'. Default: 'architecture'.")] string type = "architecture",
        [Description("Optional project filter.")] string? project = null,
        [Description("Include shared libraries. Default: false.")] bool includeLibraries = false,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.ExportArchitectureDiagramAsync(
            format, type, project, includeLibraries, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Initialize a new '.codeexplorer' workspace in the target directory.")]
    public async Task<CallToolResult> InitWorkspaceAsync(
        [Description("Optional workspace display name.")] string? name = null,
        [Description("Optional workspace root directory.")] string? workspacePath = null,
        [Description("Reinitialize existing workspace if true.")] bool force = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.InitWorkspaceAsync(
            name, workspacePath ?? GetCurrentWorkspacePath(), force, cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Scan and index source files, projects, ASTs, and dependencies into graph DB.")]
    public async Task<CallToolResult> ScanWorkspaceAsync(
        [Description("Optional subfolder path to scan.")] string? path = null,
        [Description("Clear index before scanning if true.")] bool clear = false,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.ScanWorkspaceAsync(
            path, clear, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Get workspace health, DB size, indexed project languages, node and edge counts.")]
    public async Task<CallToolResult> GetWorkspaceStatusAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.GetWorkspaceStatusAsync(
            workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Clear indexed data from the workspace database.")]
    public async Task<CallToolResult> ClearWorkspaceIndexAsync(
        [Description("Optional subpath to clear, or omit for entire workspace.")] string? path = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => repository.ClearWorkspaceIndexAsync(
            path, workspacePath ?? GetCurrentWorkspacePath(), cancellationToken));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Batch ingest custom nodes and relationships into the graph database.")]
    public async Task<CallToolResult> IngestGraphDataAsync(
        [Description("JSON array of node objects.")] string nodesJson,
        [Description("Optional JSON array of relationship objects.")] string? relationshipsJson = null,
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
    [Description("List, add, or remove architectural domain definitions in .codeexplorer/domains.json.")]
    public Task<CallToolResult> ManageDomainAsync(
        [Description("Action: 'list', 'add', or 'remove'.")] string action,
        [Description("Domain name (required for 'add' and 'remove').")] string? name = null,
        [Description("Display name.")] string? displayName = null,
        [Description("Domain description.")] string? description = null,
        [Description("Domain icon or emoji.")] string? icon = null,
        [Description("Hex color code.")] string? color = null,
        [Description("Reassign target domain when deleting.")] string? reassignTo = null,
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
    [Description("Assign or override the architectural domain and bounded context for a service.")]
    public async Task<CallToolResult> AssignServiceDomainAsync(
        [Description("Service or project name.")] string serviceName,
        [Description("Target domain name.")] string? domainName = null,
        [Description("Optional bounded context name.")] string? boundedContext = null,
        [Description("Remove existing override if true.")] bool removeOverride = false,
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
