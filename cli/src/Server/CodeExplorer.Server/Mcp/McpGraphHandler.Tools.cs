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
    private const string MissingNodesError = "Missing 'nodes' argument.";
    private const string MissingKindError = "Missing 'kind' argument.";
    private const string MissingDescriptionError = "Missing 'description' argument.";
    private const string MissingCypherError = "Missing 'cypher' argument.";
    private const string NoActiveWorkspaceError = "No active workspace found.";
    private const string DomainNameRequiredForAddError = "Domain 'name' is required for 'add' action.";
    private const string DomainNameRequiredForRemoveError = "Domain 'name' is required for 'remove' action.";
    private const string DomainNameRequiredWhenAssigningError = "'domainName' is required when assigning a domain.";

    [UsedImplicitly]
    [McpServerTool(Title = "Find Tests Affected by Code Changes", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Identify unit and integration test methods impacted by modified files, file lists, unified git diffs, or symbol names via static call-graph and dependency reachability (Test Impact Analysis). Use to select targeted regression tests before or after editing code; to audit overall covered vs uncovered production symbols across a project use get_test_coverage, and to inspect non-test production dependents use analyze_code_impact. Requires an indexed .codeexplorer/graph.db and at least one input selector (filePath, filePaths, gitDiff, or symbolName, which are unioned together when combined), returning YAML lists of affected test methods, test classes, and test projects (or an empty list if no tests reach the modified code).")]
    public async Task<CallToolResult> GetAffectedTestsAsync(
        [Description("Optional single modified file path (workspace-relative or absolute, e.g. 'src/Orders/OrderService.cs'). Unioned with filePaths, gitDiff, and symbolName when combined.")] string? filePath = null,
        [Description("Optional array of modified file paths (workspace-relative or absolute) to evaluate in a single batch.")] string[]? filePaths = null,
        [Description("Optional raw git unified diff text (e.g. output of 'git diff') from which modified files and line ranges are parsed automatically.")] string? gitDiff = null,
        [Description("Optional modified function, class, or interface name to trace upstream to reaching test methods.")] string? symbolName = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetAffectedTestsAsync(filePath, symbolName, filePaths, gitDiff, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get Static Test Reachability Coverage", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Compute static test reachability coverage across production classes and functions, reporting which symbols are transitively reached by at least one test method ('covered', 'uncovered', or 'all'). Use when auditing test gaps across a project or directory prefix; to find which specific tests exercise a modified file or symbol use get_affected_tests. Evaluates static call-graph paths from test nodes in the indexed .codeexplorer/graph.db without executing test runners; projectName, pathPrefix, and status combine as conjunctive AND filters before limit caps the returned rows, yielding YAML with aggregate coverage percentages and per-symbol reachability records.")]
    public async Task<CallToolResult> GetTestCoverageAsync(
        [Description("Optional case-insensitive project name filter to scope static coverage analysis to matching projects.")] string? projectName = null,
        [Description("Optional workspace-relative directory or file path prefix filter (e.g. 'src/Core/'). Combined with projectName and status.")] string? pathPrefix = null,
        [AllowedValues("all", "covered", "uncovered")]
        [Description("Reachability status filter: 'all', 'covered' (reached by at least one test), or 'uncovered' (0 reaching tests). Default: 'all'.")] string? status = null,
        [Range(1, 500)]
        [Description("Optional maximum number of symbol coverage entries to return (1 to 500; omit for default cap).")] int? limit = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetTestCoverageAsync(projectName, pathPrefix, status, limit, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Inspect Database Table Data Lineage", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Trace end-to-end data lineage for a database table, identifying all ORM entities, SQL queries, repository functions, source files, and services that read, write, or migrate tableName. Use when investigating schema changes, table access patterns, or read/write ownership; for general code symbol callers use get_callers, and for visual lineage diagrams use export_architecture_diagram with type='lineage'. Queries READS_FROM, WRITES_TO, and MAPS_TO_TABLE edges in the indexed .codeexplorer/graph.db (matching tableName case-insensitively) and returns YAML detailing access operations (SELECT, INSERT, UPDATE, DELETE, DDL), calling functions, services, and file coordinates.")]
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
    [McpServerTool(Title = "Get Project Entry Points and Handlers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all execution entry points declared inside a specific project, including HTTP controllers, minimal API handlers, gRPC service methods, GraphQL resolvers, CLI commands, background workers, and message consumers. Use to discover where execution enters a given projectName; to inspect both inbound and outbound service contracts (including egress HTTP/gRPC calls and published topics) use get_service_contracts. Queries the indexed .codeexplorer/graph.db (matching projectName case-insensitively) and returns YAML records with entry point kind, route or command signature, handler symbol, file_path, and line numbers (or an empty list if the project defines no entry points).")]
    public async Task<CallToolResult> GetProjectEntryPointsAsync(
        [Description("Required target project or service name (case-insensitive exact or substring match, e.g. 'CodeExplorer.Server' or 'WebApi').")] string projectName,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError(MissingProjectNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetProjectEntryPointsAsync(projectName, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Find Dead Code and God Object Anomalies", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Detect structural code-health anomalies within a project: unreferenced dead code (0 inbound calls/references excluding entry points and tests) and oversized god objects (high method count, line count, or fan-out coupling). Use when prioritizing cleanup or class decomposition inside projectName; to evaluate formal architectural and platform rules across the workspace use check_semantic_gates. Analyzes symbol degree and complexity metrics in the indexed .codeexplorer/graph.db (scoped to projectName and filtered by metricType) and returns YAML grouped into dead_code and god_objects sections with complexity metrics, file_path, and line ranges.")]
    public async Task<CallToolResult> FindRefactoringOpportunitiesAsync(
        [Description("Required target project name to analyze for structural anomalies (case-insensitive match).")] string projectName,
        [AllowedValues("all", "dead_code", "god_objects")]
        [Description("Anomaly category to detect: 'dead_code' (unreferenced classes and functions), 'god_objects' (oversized/high-coupling classes), or 'all'. Default: 'all'.")] string metricType = "all",
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return WrapError(MissingProjectNameError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.FindRefactoringOpportunitiesAsync(projectName, metricType, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Execute Read-Only OpenCypher Graph Query", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Execute an ad-hoc read-only OpenCypher query (MATCH, OPTIONAL MATCH, WHERE, WITH, RETURN, ORDER BY, LIMIT) directly against the workspace SQLite knowledge graph. Use when built-in tools do not cover a custom multi-hop graph pattern; to run a pre-saved domain query by name use execute_project_query, and to inspect valid node labels and relationship types first call get_taxonomy. Requires an indexed .codeexplorer/graph.db, rejects mutating clauses (CREATE, MERGE, DELETE, SET, REMOVE), enforces a query timeout and <10 KB inline response budget, and returns tabular YAML with column headers and row arrays.")]
    public async Task<CallToolResult> ExecuteCustomReadCypherAsync(
        [Description("Required read-only OpenCypher query string (must use MATCH/RETURN only; include a LIMIT clause to keep output under 10 KB, e.g. 'MATCH (p:Project) RETURN p.name, p.language LIMIT 25').")] string query,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            return WrapError(MissingQueryError);
        }
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ExecuteCustomReadCypherAsync(query, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get Active Graph Ontology and Schema", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Retrieve the active ontology schema of the indexed workspace knowledge graph, listing all populated node labels, relationship types, property keys, and entity counts across architectural layers. Use before writing custom Cypher queries for execute_custom_read_cypher or save_project_query; to inspect detailed documentation and property definitions for a single node label use get_node_definition, and for a high-level project/service inventory use get_architecture_map. Reads populated schema metadata from .codeexplorer/graph.db (reflecting only node and edge kinds present in the current workspace index) and returns a compact YAML schema summary.")]
    public async Task<CallToolResult> GetTaxonomyAsync(
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetTaxonomyAsync(ws, cancellationToken), cancellationToken);
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
    [McpServerTool(Title = "Get Ontology Node Kind Schema Definition", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Retrieve the formal ontology specification for a single graph node kind (such as 'Workspace', 'Project', 'Service', 'File', 'Type', 'Function', 'Member', 'Endpoint', 'Table', or 'Topic'), including its layer, property schema, and inbound/outbound relationship types. Use when you need the exact property names and edge semantics of a specific node label; to list all active node kinds and edge types populated in a workspace use get_taxonomy. Matches kind case-insensitively against static ontology metadata in memory (requiring no indexed workspace database) and returns YAML documentation or a list of valid node kinds if kind is unrecognized.")]
    public CallToolResult GetNodeDefinition(
        [Description("Required ontology node kind name (case-insensitive, e.g. 'Workspace', 'Project', 'Service', 'File', 'Type', 'Function', 'Member', 'Endpoint', 'Table', 'Topic', 'Database').")] string kind)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return WrapError(MissingKindError);
        }
        return Execute(() => repository.GetNodeDefinition(kind));
    }

    [UsedImplicitly]
    [McpServerTool(Title = "List Saved Workspace OpenCypher Queries", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all reusable project-specific OpenCypher queries saved in the workspace's .codeexplorer/queries/ directory along with their descriptions, required parameters, return fields, and tags. Use to discover existing domain queries before writing a new query with execute_custom_read_cypher; to run one of the listed queries use execute_project_query, and to create or update a saved query use save_project_query. Reads .codeexplorer/queries/*.yaml files from disk and returns a YAML catalog of saved query definitions (or an empty list if no saved queries exist).")]
    public async Task<CallToolResult> ListProjectQueriesAsync(
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ListProjectQueriesAsync(ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Save Reusable Workspace OpenCypher Query", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Create or update (overwrite) a reusable, parameterized OpenCypher query and its metadata (description, parameter schema, return fields, and tags) in .codeexplorer/queries/<name>.yaml within the workspace. Use when codifying a recurring architectural check or domain lookup for future reuse; to run a one-off query without saving it to disk use execute_custom_read_cypher, and to execute an already saved query use execute_project_query. Auto-creates the .codeexplorer/queries/ directory if missing, idempotently writes or overwrites <name>.yaml on disk (without modifying source code or .codeexplorer/graph.db), and returns YAML confirming the saved file path.")]
    public async Task<CallToolResult> SaveProjectQueryAsync(
        [Description("Required snake_case identifier for the query file (e.g. 'find_kafka_consumers', written to .codeexplorer/queries/find_kafka_consumers.yaml; existing files with the same name are overwritten).")] string name,
        [Description("Required human-readable explanation of what the query inspects and when to run it.")] string description,
        [Description("Required read-only OpenCypher query template (may reference $param placeholders defined in parameters).")] string cypher,
        [Description("Optional YAML list defining query parameters (each item with 'name', 'type', 'description', and optional 'default').")] string? parameters = null,
        [Description("Optional description of the columns or domain entities returned by the query.")] string? returns = null,
        [Description("Optional comma-separated categorization tags (e.g. 'messaging,kafka,architecture').")] string? tags = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
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
    [McpServerTool(Title = "Execute Saved Workspace OpenCypher Query", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Execute a named OpenCypher query previously saved in .codeexplorer/queries/<name>.yaml, substituting optional YAML key-value parameters ($param) against the workspace graph. Use when running an established workspace query discovered via list_project_queries or get_architecture_map; for ad-hoc Cypher queries not stored in .codeexplorer/queries/ use execute_custom_read_cypher. Loads <name>.yaml from disk, binds parameters safely against .codeexplorer/graph.db strictly in read-only mode (rejecting any mutating clauses), and returns tabular YAML columns and rows (or an error listing available queries if <name> is not found).")]
    public async Task<CallToolResult> ExecuteProjectQueryAsync(
        [Description("Required identifier of the saved query in .codeexplorer/queries/ (without the .yaml extension, e.g. 'find_kafka_consumers').")] string name,
        [Description("Optional YAML key-value mapping of parameter values to bind into $param placeholders (e.g. 'serviceName: OrderService').")] string? parameters = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return WrapError(MissingNameError);

        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ExecuteProjectQueryAsync(
            name, parameters, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Export Mermaid or C4 Architecture Diagram", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Generate renderable Mermaid or C4 architecture diagram source code from the indexed workspace graph for system topology ('architecture'), database access ('lineage'), or command/event flows ('cqrs'). Use when producing visual diagram markup for documentation or markdown artifacts; for structured YAML topology data consumed programmatically by an agent use get_architecture_view or get_architecture_map. Projects the requested viewpoint from .codeexplorer/graph.db in read-only mode (scoping to project when provided and adding shared libraries when includeLibraries=true) and returns the diagram source text directly in the response without writing any files to disk.")]
    public async Task<CallToolResult> ExportArchitectureDiagramAsync(
        [AllowedValues("mermaid", "c4")]
        [Description("Diagram output syntax: 'mermaid' (Mermaid flowchart/C4 syntax) or 'c4' (PlantUML/C4 model syntax). Default: 'mermaid'.")] string format = "mermaid",
        [AllowedValues("architecture", "lineage", "cqrs")]
        [Description("Diagram viewpoint type: 'architecture' (services, projects, and dependencies), 'lineage' (database table reads/writes), or 'cqrs' (commands, queries, handlers, and events). Default: 'architecture'.")] string type = "architecture",
        [Description("Optional project or service name filter to scope the diagram to a specific subgraph.")] string? project = null,
        [Description("Whether to include shared class libraries in the generated diagram. Default: false.")] bool includeLibraries = false,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ExportArchitectureDiagramAsync(
            format, type, project, includeLibraries, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Initialize CodeExplorer Graph Workspace", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Initialize a '.codeexplorer' configuration directory and empty SQLite graph database (.codeexplorer/graph.db) in the target workspace root. Use when onboarding an unindexed repository before running scan_workspace; if a workspace is already initialized, use get_workspace_status to inspect its state or scan_workspace to index source files. Creates .codeexplorer/config.json and .codeexplorer/graph.db on disk without touching repository source files (preserving existing graph data when force=false, or recreating the database when force=true) and returns YAML with the initialized workspace path and status.")]
    public async Task<CallToolResult> InitWorkspaceAsync(
        [Description("Optional human-readable display name for the workspace (defaults to the root directory name).")] string? name = null,
        [Description("Optional absolute path to the target workspace root directory to initialize. Omit to use the active client root.")] string? workspacePath = null,
        [Description("Whether to overwrite and reinitialize an existing '.codeexplorer' configuration and database. Default: false.")] bool force = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.InitWorkspaceAsync(
            name, ws, force, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Scan and Index Workspace into Code Graph", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Parse and index workspace source files, project manifests, AST symbols, call graphs, database schemas, and service contracts synchronously into the local .codeexplorer/graph.db SQLite database. Use after initializing a workspace or modifying source files to refresh graph state before running queries or check_semantic_gates; to check if an index already exists and is up to date without re-indexing use get_workspace_status, and to wipe the index without scanning use clear_workspace_index. Performs incremental file-hash indexing by default (scoping to path relative to workspacePath when specified, or purging and rebuilding from scratch when clear=true) without modifying any repository source files, returning YAML with scan duration and indexed node/edge counts.")]
    public async Task<CallToolResult> ScanWorkspaceAsync(
        [Description("Optional workspace-relative subfolder path to scope the scan to a specific directory tree. Omit to scan the entire workspace.")] string? path = null,
        [Description("Whether to purge all existing indexed graph data before scanning from scratch. Default: false (incremental delta scan).")] bool clear = false,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ScanWorkspaceAsync(
            path, clear, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Get Workspace Index Status and Health", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Report the health and indexing statistics of the active workspace, including .codeexplorer/graph.db file size, schema version, detected programming languages, and total node and relationship counts. Use to verify whether a workspace is initialized and indexed before running graph queries or scan_workspace; for architectural content (projects, services, endpoints, and databases) use get_architecture_map, and for node/edge type breakdowns use get_taxonomy. Reads metadata from .codeexplorer/graph.db in read-only mode and returns a compact YAML status summary (or a standby notice if no workspace is bound).")]
    public async Task<CallToolResult> GetWorkspaceStatusAsync(
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.GetWorkspaceStatusAsync(
            ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Clear Workspace Graph Index", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Delete indexed nodes, relationships, and file hashes from the workspace .codeexplorer/graph.db database for the entire repository or a specific subfolder path. Use when resetting a corrupted or stale graph index without immediately rescanning; to clear and immediately re-index in one step use scan_workspace with clear=true. Irreversibly removes indexed graph rows from .codeexplorer/graph.db (leaving the graph empty until scan_workspace is run, while never deleting or modifying workspace source files or saved queries in .codeexplorer/queries/) and returns YAML deletion counts.")]
    public async Task<CallToolResult> ClearWorkspaceIndexAsync(
        [Description("Optional workspace-relative subfolder path whose indexed nodes and edges should be removed. Omit to clear the entire workspace index.")] string? path = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ClearWorkspaceIndexAsync(
            path, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "Batch Ingest Custom Graph Nodes and Edges", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Upsert custom nodes and directed relationships supplied as YAML lists directly into the workspace .codeexplorer/graph.db knowledge graph. Use when enriching the code graph with external metadata, custom architectural annotations, or synthetic entities not derived from source files; to index standard source code and manifests use scan_workspace, and to save reusable Cypher queries use save_project_query. Executes inside a single SQLite transaction, idempotently merging/overwriting nodes by 'id' and relationships by '(from_id, to_id, type)' (rolling back on malformed YAML), and returns YAML counts of upserted nodes and edges.")]
    public async Task<CallToolResult> IngestGraphDataAsync(
        [Description("Required YAML list of node objects to upsert, each with 'id' (unique string), 'kind' (ontology node label), and 'properties' (key-value map).")] string nodes,
        [Description("Optional YAML list of relationship objects to upsert, each with 'type' (edge label), 'from_id' (source node ID), 'to_id' (target node ID), and optional 'properties'.")] string? relationships = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
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
    [McpServerTool(Title = "Evaluate Semantic Invariant Gates", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Evaluate Semantic Invariant Gates (architectural and platform rules) against the indexed workspace graph, comparing detected violations against the ratchet baseline to distinguish new regressions from tolerated technical debt. Use to verify architectural compliance and zero-failed-gate invariants after code changes; to browse available gate definitions and FOIM coordinates without evaluating violations use list_semantic_gates, and to find ad-hoc dead code or god objects use find_refactoring_opportunities. Executes gate Cypher rules against .codeexplorer/graph.db in read-only mode (where gateId targets a single gate, category/platform/scope/dimension combine as AND filters, fileFilter narrows violations to matching file paths, and groupBy aggregates results by 'file', 'symbol', or 'callee'), returning YAML with pass/fail status, violation counts, and remediation guidance capped at limit items.")]
    public async Task<CallToolResult> CheckSemanticGatesAsync(
        [Description("Optional single Gate ID to evaluate (e.g. 'SIG-P-B-001' or 'SIG-M-R-001'). Omit to evaluate all matching gates.")] string? gateId = null,
        [AllowedValues("Architectural", "Platform")]
        [Description("Optional rule category filter: 'Architectural' or 'Platform'. Omit to run both categories.")] string? category = null,
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
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.CheckSemanticGatesAsync(gateId, category, platform, scope, dimension, includeTolerated, limit, fileFilter, groupBy, ws, cancellationToken), cancellationToken);
    }

    [UsedImplicitly]
    [McpServerTool(Title = "List Semantic Invariant Gate Definitions", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all configured Semantic Invariant Gates (built-in and workspace-custom rules) filtered by category ('Architectural' or 'Platform') and target platform, including Gate IDs, FOIM coordinates (Scope x Dimension), severity, and baseline debt counts. Use to inspect which architectural rules exist before running or filtering a specific gate; to actually evaluate gates and detect code violations use check_semantic_gates. Reads gate catalog metadata and .codeexplorer/gates-baseline.yaml summaries in read-only mode (combining category and platform as AND filters when both are specified) and returns YAML listing each gate's ID, title, scope, dimension, and tolerated baseline count.")]
    public async Task<CallToolResult> ListSemanticGatesAsync(
        [AllowedValues("Architectural", "Platform")]
        [Description("Optional rule category filter: 'Architectural' or 'Platform'. Omit to list all categories.")] string? category = null,
        [Description("Optional target platform filter (e.g. 'agnostic', 'dotnet', 'typescript', 'python', 'go', 'java', 'terraform').")] string? platform = null,
        [Description("Optional absolute path to the target workspace root directory. Omit to use the bound workspace, active client root, or sticky session workspace.")] string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForWorkspaceAsync(workspacePath, ws => repository.ListSemanticGatesAsync(category, platform, ws, cancellationToken), cancellationToken);
    }
}
