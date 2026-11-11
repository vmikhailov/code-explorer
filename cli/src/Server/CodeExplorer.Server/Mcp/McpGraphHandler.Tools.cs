using System.ComponentModel;
using JetBrains.Annotations;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CodeExplorer.Core.Mcp;

public partial class McpGraphHandler
{
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetAffectedTestsAsync(filePath, symbolName, filePaths, gitDiff, ws, cancellationToken), cancellationToken);
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetTestCoverageAsync(projectName, pathPrefix, status, limit, ws, cancellationToken), cancellationToken);
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.InspectDataLineageAsync(tableName, ws, cancellationToken), cancellationToken);
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetProjectEntryPointsAsync(projectName, ws, cancellationToken), cancellationToken);
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.FindRefactoringOpportunitiesAsync(projectName, metricType, ws, cancellationToken), cancellationToken);
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ExecuteCustomReadCypherAsync(query, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Retrieve active graph schema (node kinds, relationship types, properties).")]
    public async Task<CallToolResult> GetTaxonomyAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetTaxonomyAsync(ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Fetch source code snippets for given graph nodes.")]
    public async Task<CallToolResult> FetchCodeSnippets(
        [Description("YAML list of node objects with file_path, start_line, end_line.")] string nodes,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(nodes))
        {
            return WrapError("Missing 'nodes' argument.");
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.FetchCodeSnippetsAsync(nodes, ws, cancellationToken), cancellationToken);
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ListProjectQueriesAsync(ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Save a reusable Cypher query with metadata into .codeexplorer/queries/.")]
    public async Task<CallToolResult> SaveProjectQueryAsync(
        [Description("Query identifier (e.g. 'find_kafka_consumers').")] string name,
        [Description("Query purpose description.")] string description,
        [Description("Cypher query text with parameters ($param, etc.).")] string cypher,
        [Description("Optional parameter schema YAML list.")] string? parameters = null,
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

        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.SaveProjectQueryAsync(
            name, description, cypher, parameters, returns, tags, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Execute a saved project-specific Cypher query by name with parameters.")]
    public async Task<CallToolResult> ExecuteProjectQueryAsync(
        [Description("Saved query name.")] string name,
        [Description("Optional YAML mapping of parameter key-values.")] string? parameters = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return WrapError("Missing 'name' argument.");

        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ExecuteProjectQueryAsync(
            name, parameters, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Export Mermaid or C4 system architecture diagrams from the knowledge graph.")]
    public async Task<CallToolResult> ExportArchitectureDiagramAsync(
        [Description("Format: 'mermaid' or 'c4'. Default: 'mermaid'.")] string format = "mermaid",
        [Description("Type: 'architecture', 'lineage', 'cqrs'. Default: 'architecture'.")] string type = "architecture",
        [Description("Optional project filter.")] string? project = null,
        [Description("Include shared libraries. Default: false.")] bool includeLibraries = false,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ExportArchitectureDiagramAsync(
            format, type, project, includeLibraries, ws, cancellationToken), cancellationToken);
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.InitWorkspaceAsync(
            name, ws, force, cancellationToken), cancellationToken);
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
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ScanWorkspaceAsync(
            path, clear, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Get workspace health, DB size, indexed project languages, node and edge counts.")]
    public async Task<CallToolResult> GetWorkspaceStatusAsync(
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetWorkspaceStatusAsync(
            ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Clear indexed data from the workspace database.")]
    public async Task<CallToolResult> ClearWorkspaceIndexAsync(
        [Description("Optional subpath to clear, or omit for entire workspace.")] string? path = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ClearWorkspaceIndexAsync(
            path, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Batch ingest custom nodes and relationships into the graph database.")]
    public async Task<CallToolResult> IngestGraphDataAsync(
        [Description("YAML list of node objects.")] string nodes,
        [Description("Optional YAML list of relationship objects.")] string? relationships = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodes))
            return WrapError("Missing 'nodes' argument.");

        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.IngestGraphDataAsync(
            nodes, relationships, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> ManageDomainAsync(
        string action,
        string? name = null,
        string? displayName = null,
        string? description = null,
        string? icon = null,
        string? color = null,
        string? reassignTo = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        string? wsRoot;
        try
        {
            wsRoot = await ResolveWorkspacePathAsync(workspacePath, cancellationToken);
        }
        catch (Exception ex)
        {
            return WrapError(ex);
        }

        if (string.IsNullOrWhiteSpace(wsRoot)) return WrapError("No active workspace found.");

        switch (action.ToLowerInvariant())
        {
            case "list":
                var config = DomainManagementService.LoadConfig(wsRoot);
                return WrapResult(ToonYamlSerializer.SerializeObject(config));

            case "add":
                if (string.IsNullOrWhiteSpace(name)) return WrapError("Domain 'name' is required for 'add' action.");
                var domain = DomainManagementService.AddOrUpdateDomain(wsRoot, new DomainDefinitionDto
                {
                    Name = name.Trim(),
                    DisplayName = displayName,
                    Description = description,
                    Icon = icon,
                    Color = color
                });
                return WrapResult($"Domain '{domain.Name}' added/updated in {DomainManagementService.GetConfigFilePath(wsRoot)}.");

            case "remove":
            case "delete":
                if (string.IsNullOrWhiteSpace(name)) return WrapError("Domain 'name' is required for 'remove' action.");
                var removed = DomainManagementService.RemoveDomain(wsRoot, name.Trim(), reassignTo);
                return removed
                    ? WrapResult($"Domain '{name}' removed from {DomainManagementService.GetConfigFilePath(wsRoot)}.")
                    : WrapError($"Domain '{name}' not found.");

            default:
                return WrapError($"Unknown action '{action}'. Valid actions are 'list', 'add', 'remove'.");
        }
    }

    [UsedImplicitly]
    public async Task<CallToolResult> AssignServiceDomainAsync(
        string serviceName,
        string? domainName = null,
        string? boundedContext = null,
        bool removeOverride = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        string? wsRoot;
        try
        {
            wsRoot = await ResolveWorkspacePathAsync(workspacePath, cancellationToken);
        }
        catch (Exception ex)
        {
            return WrapError(ex);
        }

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

        try
        {
            var client = await repository.ResolveClientAsync(wsRoot);
            DomainManagementService.AssignServiceDomain(wsRoot, serviceName, domainName, boundedContext, client, cancellationToken);
            return WrapResult($"Assigned service '{serviceName}' to domain '{domainName}'" +
                (string.IsNullOrWhiteSpace(boundedContext) ? "" : $" (context: '{boundedContext}')") + ".");
        }
        catch (Exception ex)
        {
            return WrapError(ex);
        }
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("Evaluate Semantic Invariant Gates (SIG) across the workspace or against specific changed files. Returns zero-match status, new vs tolerated violations, and remediation guidance.")]
    public async Task<CallToolResult> CheckSemanticGatesAsync(
        [Description("Optional specific Gate ID to run (e.g. 'SIG-P-B-001').")] string? gateId = null,
        [Description("Optional category filter: 'Architectural' or 'Platform'.")] string? category = null,
        [Description("Optional platform filter: e.g. 'agnostic', 'dotnet', 'typescript', 'python', 'go', 'java'.")] string? platform = null,
        [Description("Optional scope filter: 'Method', 'Class', 'Module', 'Service', 'System'.")] string? scope = null,
        [Description("Optional dimension filter: 'Contract', 'Rules', 'Resources', 'Boundaries'.")] string? dimension = null,
        [Description("Include tolerated baseline debt in results. Default: false.")] bool includeTolerated = false,
        [Description("Maximum number of violations or groups to return. Default: 50.")] int limit = 50,
        [Description("Filter violations by file path substring.")] string? fileFilter = null,
        [Description("Group violations summary by: 'file', 'symbol', or 'callee'.")] string? groupBy = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.CheckSemanticGatesAsync(gateId, category, platform, scope, dimension, includeTolerated, limit, fileFilter, groupBy, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool]
    [Description("List all available Semantic Invariant Gates (both built-in and workspace-specific) with FOIM coordinates and debt status.")]
    public async Task<CallToolResult> ListSemanticGatesAsync(
        [Description("Optional category filter: 'Architectural' or 'Platform'.")] string? category = null,
        [Description("Optional platform filter: e.g. 'agnostic', 'dotnet', 'typescript', 'python', 'go', 'java'.")] string? platform = null,
        [Description("Optional workspace root path.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ListSemanticGatesAsync(category, platform, ws, cancellationToken), cancellationToken);
    }
}
