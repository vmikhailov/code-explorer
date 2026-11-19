using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CodeExplorer.Core.Mcp;

public partial class McpGraphHandler
{
    private const string MissingTableNameError = "Missing 'tableName' argument.";
    private const string MissingProjectNameError = "Missing 'projectName' argument.";
    private const string MissingProjectNameForAnomaliesError = "Missing 'projectName' argument (required when 'anomalies' is set).";
    private const string MissingCypherOrSavedQueryError = "Provide 'query', 'savedQuery', or set 'listSaved=true'.";
    private const string MissingNodesError = "Missing 'nodes' argument.";
    private const string MissingKindError = "Missing 'kind' argument.";
    private const string MissingDescriptionError = "Missing 'description' argument.";
    private const string MissingCypherError = "Missing 'cypher' argument.";
    private const string NoActiveWorkspaceError = "No active workspace found.";
    private const string DomainNameRequiredForAddError = "Domain 'name' is required for 'add' action.";
    private const string DomainNameRequiredForRemoveError = "Domain 'name' is required for 'remove' action.";
    private const string DomainNameRequiredWhenAssigningError = "'domainName' is required when assigning a domain.";
    private const string MissingTaskQueryError = "Missing 'query' argument.";

    [UsedImplicitly]
    [McpServerTool(Title = "Analyze Affected Tests and Static Test Coverage", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Select unit and integration tests impacted by code edits (via filePath, filePaths, gitDiff, or symbolName) or audit static call-graph test reachability coverage across a project or path prefix (via coverageStatus, projectName, or pathPrefix). Use change selectors (filePath, filePaths, gitDiff, symbolName) to pick targeted regression tests after editing code, or set coverageStatus ('all', 'covered', 'uncovered') without change selectors to find untested production symbols; for non-test downstream dependents use analyze_code_impact. Evaluates static call-graph reachability in .codeexplorer/graph.db without running test processes (unioning change selectors in impact mode, or combining projectName, pathPrefix, and coverageStatus as AND filters capped at limit in coverage mode) and returns YAML test lists or coverage summaries.")]
    public async Task<CallToolResult> GetAffectedTestsAsync(
        [Description("Optional single modified file path (workspace-relative or absolute, e.g. 'src/Orders/OrderService.cs'). Unioned with filePaths, gitDiff, and symbolName in impact mode.")] string? filePath = null,
        [Description("Optional array of modified file paths (workspace-relative or absolute) to evaluate in a single batch.")] string[]? filePaths = null,
        [Description("Optional raw git unified diff text (e.g. output of 'git diff') from which modified files and line ranges are parsed automatically.")] string? gitDiff = null,
        [Description("Optional modified function, class, or interface name to trace upstream to reaching test methods.")] string? symbolName = null,
        [Description("Optional case-insensitive project name filter to scope static test reachability coverage when no change selectors are passed.")] string? projectName = null,
        [Description("Optional workspace-relative directory or file path prefix filter (e.g. 'src/Core/') for static test coverage mode.")] string? pathPrefix = null,
        [AllowedValues("all", "covered", "uncovered")]
        [Description("Optional static coverage mode filter: 'all', 'covered' (reached by at least one test), or 'uncovered' (0 reaching tests). Omit for test impact mode unless projectName or pathPrefix is set.")] string? coverageStatus = null,
        [Range(1, 500)]
        [Description("Optional maximum number of symbol coverage entries to return in coverage mode (1 to 500; omit for default cap).")] int? limit = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var hasImpactSelector = !string.IsNullOrWhiteSpace(filePath)
            || (filePaths is { Length: > 0 })
            || !string.IsNullOrWhiteSpace(gitDiff)
            || !string.IsNullOrWhiteSpace(symbolName);
        var hasCoverageSelector = !string.IsNullOrWhiteSpace(coverageStatus)
            || !string.IsNullOrWhiteSpace(projectName)
            || !string.IsNullOrWhiteSpace(pathPrefix);

        return await ExecuteForWorkspaceAsync(workspacePath, ws =>
            !hasImpactSelector && hasCoverageSelector
                ? repository.GetTestCoverageAsync(projectName, pathPrefix, coverageStatus, limit, ws, cancellationToken)
                : repository.GetAffectedTestsAsync(filePath, symbolName, filePaths, gitDiff, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> GetTestCoverageAsync(
        string? projectName = null,
        string? pathPrefix = null,
        string? status = null,
        int? limit = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetTestCoverageAsync(projectName, pathPrefix, status, limit, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Inspect Database Table Data Lineage", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Trace end-to-end data lineage for a database table, identifying all ORM entities, SQL queries, repository functions, source files, and services that read, write, or migrate tableName. Use when investigating schema changes, table access patterns, or read/write ownership; for general code symbol callers use get_callers, and for visual lineage diagrams use get_architecture_map with view='lineage' and format='mermaid'. Queries READS_FROM, WRITES_TO, and IMPLEMENTED_BY edges in the indexed .codeexplorer/graph.db (matching tableName case-insensitively) and returns YAML detailing access operations (SELECT, INSERT, UPDATE, DELETE, DDL), calling functions, services, and file coordinates.")]
    public async Task<CallToolResult> InspectDataLineageAsync(
        [Description("Required database table or collection name to trace (case-insensitive exact or partial match, e.g. 'orders' or 'users').")] string tableName,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tableName))
        {
            return WrapError(MissingTableNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.InspectDataLineageAsync(tableName, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> GetProjectEntryPointsAsync(
        string projectName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError(MissingProjectNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetProjectEntryPointsAsync(projectName, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> FindRefactoringOpportunitiesAsync(
        string projectName,
        string metricType = "all",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError(MissingProjectNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.FindRefactoringOpportunitiesAsync(projectName, metricType, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Execute Read-Only or Saved OpenCypher Query", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Execute an ad-hoc read-only OpenCypher query (query), run a reusable workspace query from .codeexplorer/queries/ with parameter bindings (savedQuery and parameters), or list available saved queries (listSaved=true) against the workspace SQLite knowledge graph. Use when built-in graph tools do not cover a custom multi-hop pattern or when running workspace-specific domain queries; call get_taxonomy first to inspect populated node labels, relationship types, and property keys. Requires an indexed .codeexplorer/graph.db (unless listSaved=true), strictly rejects mutating Cypher clauses (CREATE, MERGE, DELETE, SET, REMOVE), enforces a query timeout and <10 KB inline response budget, and returns tabular YAML rows or the saved query catalog (returning a validation error if neither query, savedQuery, nor listSaved=true is supplied).")]
    public async Task<CallToolResult> ExecuteCustomReadCypherAsync(
        [Description("Optional ad-hoc read-only OpenCypher query string (must use MATCH/RETURN only; include a LIMIT clause to keep output under 10 KB, e.g. 'MATCH (p:Project) RETURN p.name, p.language LIMIT 25').")] string? query = null,
        [Description("Optional identifier of a saved query in .codeexplorer/queries/ to execute instead of an ad-hoc query (e.g. 'find_kafka_consumers').")] string? savedQuery = null,
        [Description("Optional YAML key-value mapping of parameter values to bind into $param placeholders when executing savedQuery (e.g. 'serviceName: OrderService').")] string? parameters = null,
        [Description("When true, lists all reusable OpenCypher queries saved in .codeexplorer/queries/ along with their parameter schemas instead of executing a query. Default: false.")] bool listSaved = false,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (!listSaved && string.IsNullOrWhiteSpace(savedQuery) && string.IsNullOrWhiteSpace(query))
        {
            return WrapError(MissingCypherOrSavedQueryError);
        }

        return await ExecuteForWorkspaceAsync(workspacePath, ws =>
        {
            if (listSaved)
            {
                return repository.ListProjectQueriesAsync(ws, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(savedQuery))
            {
                return repository.ExecuteProjectQueryAsync(savedQuery, parameters, ws, cancellationToken);
            }

            return repository.ExecuteCustomReadCypherAsync(query!, ws, cancellationToken);
        }, cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get Graph Ontology Schema and Workspace Status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Inspect the workspace knowledge graph ontology and index health: omit kind and statusOnly to list all populated node labels, relationship types, and property keys in .codeexplorer/graph.db; pass kind to retrieve the formal static specification for one ontology node label; or set statusOnly=true to check workspace initialization status, database size, projects, and node counts. Use statusOnly=true to verify whether a repository is indexed before calling scan_workspace, and use the default schema output or kind lookup before writing queries for execute_custom_read_cypher; for an architectural inventory of services, endpoints, and databases use get_architecture_map. Operates strictly in read-only mode (resolving kind in memory without an indexed database, and returning status='not_initialized' or a standby notice when unindexed).")]
    public async Task<CallToolResult> GetTaxonomyAsync(
        [Description("Optional ontology node kind name (case-insensitive, e.g. 'Workspace', 'Project', 'Service', 'File', 'Type', 'Function', 'Member', 'Endpoint', 'Table', 'Topic', 'Database') to inspect its formal property and edge specification.")] string? kind = null,
        [Description("When true (and kind is omitted), reports workspace initialization health, .codeexplorer/graph.db size, indexed projects, and node counts instead of full relationship triplets. Default: false.")] bool statusOnly = false,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(kind))
        {
            return Execute(() => repository.GetNodeDefinition(kind));
        }

        return await ExecuteForWorkspaceAsync(workspacePath, ws =>
            statusOnly
                ? repository.GetWorkspaceStatusAsync(ws, cancellationToken)
                : repository.GetTaxonomyAsync(ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Fetch Source Code Snippets by Line Range", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Read exact source code line ranges from workspace files on disk for a batch of graph nodes or file coordinates passed as a YAML list. Use when inspecting implementation bodies of symbols discovered via find_symbol, get_callers, or execute_custom_read_cypher; to view a file's structural symbol list without reading source text use get_file_outline. Resolves each item's file_path relative to workspacePath, clamps start_line..end_line to valid file bounds (recording per-item errors if a file is missing without failing the batch), and returns YAML snippets in input order.")]
    public async Task<CallToolResult> FetchCodeSnippets(
        [Description("Required YAML list of snippet request objects, each specifying 'file_path' (workspace-relative or absolute path), 'start_line' (1-based inclusive start line), and 'end_line' (1-based inclusive end line).")] string nodes,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(nodes))
        {
            return WrapError(MissingNodesError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.FetchCodeSnippetsAsync(nodes, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public CallToolResult GetNodeDefinition(string kind)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return WrapError(MissingKindError);
        }
        return Execute(() => repository.GetNodeDefinition(kind));
    }

    [UsedImplicitly]
    public async Task<CallToolResult> ListProjectQueriesAsync(
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ListProjectQueriesAsync(ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> SaveProjectQueryAsync(
        string name,
        string description,
        string cypher,
        string? parameters = null,
        string? returns = null,
        string? tags = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return WrapError(MissingNameError);
        if (string.IsNullOrWhiteSpace(description))
            return WrapError(MissingDescriptionError);
        if (string.IsNullOrWhiteSpace(cypher))
            return WrapError(MissingCypherError);

        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.SaveProjectQueryAsync(
            name, description, cypher, parameters, returns, tags, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> ExecuteProjectQueryAsync(
        string name,
        string? parameters = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return WrapError(MissingNameError);

        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ExecuteProjectQueryAsync(
            name, parameters, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> ExportArchitectureDiagramAsync(
        string format = "mermaid",
        string type = "architecture",
        string? project = null,
        bool includeLibraries = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ExportArchitectureDiagramAsync(
            format, type, project, includeLibraries, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> InitWorkspaceAsync(
        string? name = null,
        string? workspacePath = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.InitWorkspaceAsync(
            name, ws, force, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Initialize and Index Workspace into Code Graph", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Initialize (if needed) and index workspace source files, project manifests, AST symbols, call graphs, database schemas, and service contracts synchronously into the local .codeexplorer/graph.db SQLite cache. Use when onboarding an unindexed repository or after modifying source files to refresh graph state before running queries or check_semantic_gates; to check whether an index already exists without scanning use get_taxonomy with statusOnly=true. Automatically creates .codeexplorer/graph.db on first run, performs fast incremental file-hash indexing by default (scoping to path when specified), or purges and rebuilds the derived SQLite cache from scratch when clear=true without modifying any repository source files, returning YAML with indexed node and relationship counts.")]
    public async Task<CallToolResult> ScanWorkspaceAsync(
        [Description("Optional workspace-relative subfolder path to scope the scan to a specific directory tree. Omit to scan the entire workspace.")] string? path = null,
        [Description("Whether to purge all existing indexed graph data before rebuilding the index from scratch. Default: false (incremental delta scan).")] bool clear = false,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ScanWorkspaceAsync(
            path, clear, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> GetWorkspaceStatusAsync(
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetWorkspaceStatusAsync(
            ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> ClearWorkspaceIndexAsync(
        string? path = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ClearWorkspaceIndexAsync(
            path, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> IngestGraphDataAsync(
        string nodes,
        string? relationships = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodes))
            return WrapError(MissingNodesError);

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

        if (string.IsNullOrWhiteSpace(wsRoot)) return WrapError(NoActiveWorkspaceError);

        switch (action.ToLowerInvariant())
        {
            case "list":
                var config = DomainManagementService.LoadConfig(wsRoot);
                return WrapResult(ToonYamlSerializer.SerializeObject(config));

            case "add":
                if (string.IsNullOrWhiteSpace(name)) return WrapError(DomainNameRequiredForAddError);
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
                if (string.IsNullOrWhiteSpace(name)) return WrapError(DomainNameRequiredForRemoveError);
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

        if (string.IsNullOrWhiteSpace(wsRoot)) return WrapError(NoActiveWorkspaceError);

        if (removeOverride)
        {
            var removed = DomainManagementService.RemoveServiceOverride(wsRoot, serviceName);
            return removed
                ? WrapResult($"Override removed for service '{serviceName}'.")
                : WrapError($"No override found for service '{serviceName}'.");
        }

        if (string.IsNullOrWhiteSpace(domainName))
        {
            return WrapError(DomainNameRequiredWhenAssigningError);
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
    [McpServerTool(Title = "Evaluate or List Semantic Invariant Gates and Code Anomalies", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Evaluate Semantic Invariant Gates (architectural and platform rules) against the ratchet baseline, list configured gate definitions (listOnly=true), or detect structural code-health anomalies ('dead_code', 'god_objects', or 'all' via anomalies and projectName). Use default evaluation mode after code edits and scan_workspace to enforce zero architectural regressions, set listOnly=true to browse gate IDs and FOIM coordinates without running queries, or pass anomalies with projectName to find unreferenced symbols and oversized classes; for symbol blast radius use analyze_code_impact. Operates strictly in read-only mode against .codeexplorer/graph.db (combining category, platform, scope, dimension, and fileFilter as AND filters, grouping by 'file'/'symbol'/'callee' when groupBy is set, and capping output at limit items), returning YAML pass/fail results, gate catalogs, or anomaly lists.")]
    public async Task<CallToolResult> CheckSemanticGatesAsync(
        [Description("Optional single Gate ID to evaluate (e.g. 'SIG-P-B-001' or 'SIG-M-R-001'). Omit to evaluate all matching gates.")] string? gateId = null,
        [AllowedValues("Architectural", "Platform")]
        [Description("Optional rule category filter: 'Architectural' or 'Platform'. Omit to include both categories.")] string? category = null,
        [Description("Optional target platform filter (e.g. 'agnostic', 'dotnet', 'typescript', 'python', 'go', 'java', 'terraform').")] string? platform = null,
        [AllowedValues("Method", "Class", "Module", "Service", "System")]
        [Description("Optional FOIM scope filter: 'Method', 'Class', 'Module', 'Service', or 'System'.")] string? scope = null,
        [AllowedValues("Contract", "Rules", "Resources", "Boundaries")]
        [Description("Optional FOIM dimension filter: 'Contract', 'Rules', 'Resources', or 'Boundaries'.")] string? dimension = null,
        [Description("Whether to include baseline-tolerated technical debt violations in the output alongside new regressions. Default: false.")] bool includeTolerated = false,
        [Range(1, 500)]
        [Description("Maximum number of violation entries or aggregation groups to return per gate (1 to 500, default: 50).")] int limit = 50,
        [Description("Optional case-insensitive file path substring filter to scope violation reporting to matching files.")] string? fileFilter = null,
        [AllowedValues("file", "symbol", "callee")]
        [Description("Optional aggregation dimension for grouping violations: 'file', 'symbol', or 'callee'. Omit for flat violation records.")] string? groupBy = null,
        [Description("When true, returns the catalog of configured gate definitions, FOIM coordinates, and baseline metadata without evaluating gate queries. Default: false.")] bool listOnly = false,
        [AllowedValues("all", "dead_code", "god_objects", "empty_catches")]
        [Description("Optional structural anomaly detection mode: 'dead_code' (unreferenced production classes/functions), 'god_objects' (oversized production classes and functions/components/hooks), 'empty_catches' (empty production catch blocks), or 'all' (requires projectName).")] string? anomalies = null,
        [Description("Target project name required when anomalies is specified (case-insensitive match).")] string? projectName = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(anomalies))
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return WrapError(MissingProjectNameForAnomaliesError);
            }

            return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.FindRefactoringOpportunitiesAsync(projectName, anomalies, ws, cancellationToken), cancellationToken);
        }

        if (listOnly)
        {
            return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ListSemanticGatesAsync(category, platform, ws, cancellationToken, scope, dimension), cancellationToken);
        }

        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.CheckSemanticGatesAsync(gateId, category, platform, scope, dimension, includeTolerated, limit, fileFilter, groupBy, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    public async Task<CallToolResult> ListSemanticGatesAsync(
        string? category = null,
        string? platform = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ListSemanticGatesAsync(category, platform, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Compile One-Shot Task Subgraph Context Pack", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Compile a complete, token-compact (<4 KB) architectural context pack for a coding, bugfix, or refactoring task in a single call. Use when starting a feature, bugfix, or refactoring task to get the full architectural subgraph upfront before calling find_symbol or fetch_code_snippets. Given a natural-language task symptom, route, or domain keywords, resolves diversified anchor services/controllers/methods, expands 1-hop outbound side-effects (caches, queues, external calls) and inbound callers/endpoints, computes canonical owner services for every touched database table, checks module import cycle safety, flags ratcheted legacy debt traps in touched files, and returns a surgical file read plan.")]
    public async Task<CallToolResult> GetTaskContextAsync(
        [Description("Required task symptom, feature description, route, or space-separated domain keywords (e.g. 'revoke api key usage limit' or 'DELETE /oauth/register/:clientId').")] string query,
        [Range(3, 15)]
        [Description("Maximum number of diversified anchor symbols to include across matched concepts (3 to 15; default: 8).")] int maxAnchors = 8,
        [Description("When true, includes active Semantic Invariant Gates and flags any ratcheted legacy debt traps inside the matched subgraph files. Default: true.")] bool includeGates = true,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return WrapError(MissingTaskQueryError);
        }

        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetTaskContextAsync(query, maxAnchors, includeGates, ws, cancellationToken), cancellationToken);
    }
}
