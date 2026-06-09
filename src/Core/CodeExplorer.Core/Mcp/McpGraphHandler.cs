using System.ComponentModel;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CodeExplorer.Core.Mcp;

[McpServerToolType]
public class McpGraphHandler(
    CodeExplorerRepository repository,
    IHttpContextAccessor httpContextAccessor)
{
    private string? GetCurrentWorkspacePath()
    {
        var httpContext = httpContextAccessor.HttpContext;
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
        return null;
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

    private static async Task<CallToolResult> ExecuteAsync(Func<Task<string>> action)
    {
        try
        {
            var result = await action();
            return WrapResult(result);
        }
        catch (Exception ex)
        {
            return WrapError(ex);
        }
    }

    private static CallToolResult Execute(Func<string> action)
    {
        try
        {
            var result = action();
            return WrapResult(result);
        }
        catch (Exception ex)
        {
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
    [Description("Returns the high-level infrastructure map of the workspace, including workspace folders, projects, their internal folders, and associated databases. Use this at the start of a task to understand the component boundaries.")]
    public async Task<CallToolResult> GetArchitectureMapAsync(
        [Description("Optional filter for a specific project name (e.g., 'AuthService'). If omitted, returns the top-level workspace structure.")] string? projectName = null)
    {
        return await ExecuteAsync(() => repository.GetArchitectureMapAsync(projectName, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Retrieves the complete dependency graph between projects. Returns both direct project-to-project dependencies and transitive dependencies mediated through internal or external Packages.")]
    public async Task<CallToolResult> GetProjectDependenciesAsync(
        [Description("Optional name of a specific project to isolate its incoming and outgoing dependencies.")] string? projectFilter = null)
    {
        return await ExecuteAsync(() => repository.GetProjectDependenciesAsync(projectFilter, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Extracts the internal structural outline of a specific file without reading its entire source code text. Returns the names, types, and start/end lines of Classes, Interfaces, Functions, Variables, and Queries defined inside.")]
    public async Task<CallToolResult> GetFileOutlineAsync(
        [Description("The full or relative path to the file (e.g., 'src/Services/Auth/User.cs').")] string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return WrapError("Missing 'filePath' argument.");
        }
        return await ExecuteAsync(() => repository.GetFileOutlineAsync(filePath, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Searches the semantic graph for code symbols (Classes, Interfaces, Functions, or Variables) matching a partial or full name. Returns their type, full path, and declaration coordinates.")]
    public async Task<CallToolResult> FindSymbolAsync(
        [Description("The name or part of the name of the symbol to find (e.g., 'OrderProcessor').")] string name,
        [Description("Optional explicit filter by symbol type ('Class', 'Interface', 'Function', 'Variable').")] string? symbolType = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            return WrapError("Missing 'name' argument.");
        }
        return await ExecuteAsync(() => repository.FindSymbolAsync(name, symbolType, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Traces and builds a sequential execution path (call graph) between a starting function and a target function. Essential for debugging business logic flows and tracing processing pipelines.")]
    public async Task<CallToolResult> GetCallChainAsync(
        [Description("The full name or symbol of the originating function (e.g., 'SubmitOrder').")] string startFunction,
        [Description("The full name or symbol of the destination function (e.g., 'SaveToDb').")] string endFunction,
        [Description("Maximum call stack depth to traverse in the graph (1-10).")] int maxDepth = 5)
    {
        if (string.IsNullOrEmpty(startFunction) || string.IsNullOrEmpty(endFunction))
        {
            return WrapError("Missing 'startFunction' or 'endFunction' argument.");
        }
        return await ExecuteAsync(() => repository.GetCallChainAsync(startFunction, endFunction, maxDepth, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Resolves Polymorphism and Dependency Injection blind spots. When a function calls an abstract Interface method, this tool queries the graph to find all concrete Classes implementing that interface and points to their real physical function implementations.")]
    public async Task<CallToolResult> ResolveCallTargetAsync(
        [Description("The name of the interface being checked (e.g., 'IPaymentGateway').")] string interfaceName,
        [Description("The specific interface method name being called (e.g., 'ProcessPayment').")] string methodName)
    {
        if (string.IsNullOrEmpty(interfaceName) || string.IsNullOrEmpty(methodName))
        {
            return WrapError("Missing 'interfaceName' or 'methodName' argument.");
        }
        return await ExecuteAsync(() => repository.ResolveCallTargetAsync(interfaceName, methodName, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Performs a deterministic blast-radius analysis before refactoring. Given a Class, Interface, or Function, it tracks all incoming structural links ('CALLS', 'USES_TYPE') to identify every file and component that will be broken or affected by changing this symbol.")]
    public async Task<CallToolResult> AnalyzeCodeImpactAsync(
        [Description("The full name of the class, interface, or function to analyze.")] string symbolName)
    {
        if (string.IsNullOrEmpty(symbolName))
        {
            return WrapError("Missing 'symbolName' argument.");
        }
        return await ExecuteAsync(() => repository.AnalyzeCodeImpactAsync(symbolName, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Bridges the gap between code and data. Tracks the blast radius of database changes by finding every raw SQL text or ORM Query (Query), the source File it resides in, and the Functions that invoke it based on a target physical Database Table name.")]
    public async Task<CallToolResult> InspectDataLineageAsync(
        [Description("The exact name of the database table to inspect (e.g., 'orders' or 'users').")] string tableName)
    {
        if (string.IsNullOrEmpty(tableName))
        {
            return WrapError("Missing 'tableName' argument.");
        }
        return await ExecuteAsync(() => repository.InspectDataLineageAsync(tableName, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Finds all architectural entry points inside a project. It scans structural routing folders (Controllers, Endpoints, EventHandlers) to return top-level functions that trigger system execution flows.")]
    public async Task<CallToolResult> GetProjectEntryPointsAsync(
        [Description("The name of the target project (e.g., 'Gateway.Api').")] string projectName)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError("Missing 'projectName' argument.");
        }
        return await ExecuteAsync(() => repository.GetProjectEntryPointsAsync(projectName, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Scans the project to find code health anomalies. Identifies 'Dead Code' (Functions or Classes with 0 incoming CALLS/USES_TYPE edges) and 'God Objects' (Classes containing a disproportionately large number of members).")]
    public async Task<CallToolResult> FindRefactoringOpportunitiesAsync(
        [Description("The project to audit for dead code and architectural bloat.")] string projectName,
        [Description("Filter by a specific health metric anomaly type ('dead_code', 'god_objects', 'all').")] string metricType = "all")
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError("Missing 'projectName' argument.");
        }
        return await ExecuteAsync(() => repository.FindRefactoringOpportunitiesAsync(projectName, metricType, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Advanced escape-hatch tool. Allows the agent to write and execute custom Cypher read-queries (MATCH only) directly against Memgraph when predefined tools are insufficient for complex analytical insights. Mutating queries (CREATE, DELETE, SET) are strictly blocked.")]
    public async Task<CallToolResult> ExecuteCustomReadCypherAsync(
        [Description("A valid read-only Cypher query targeted at the CodeExplorer taxonomy schema.")] string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return WrapError("Missing 'query' argument.");
        }
        return await ExecuteAsync(() => repository.ExecuteCustomReadCypherAsync(query, GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Retrieves the full structural taxonomy database schema mapping all active node types and their incoming/outgoing relationships.")]
    public async Task<CallToolResult> GetTaxonomyAsync()
    {
        return await ExecuteAsync(() => repository.GetTaxonomyAsync(GetCurrentWorkspacePath()));
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Fetches the actual source code snippets for the given list of URN/node contexts. Receives a serialized JSON string containing one or more nodes with 'file_path', 'start_line', and 'end_line' specified.")]
    public async Task<CallToolResult> FetchCodeSnippets(
        [Description("JSON string representing the RAG node(s) to fetch snippets for.")] string nodesJson)
    {
        if (string.IsNullOrEmpty(nodesJson))
        {
            return WrapError("Missing 'nodesJson' argument.");
        }
        return await ExecuteAsync(() => repository.FetchCodeSnippetsAsync(nodesJson, GetCurrentWorkspacePath()));
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
}
