using CodeExplorer.Core.Common;

namespace CodeExplorer.Common;

public static class HelpDisplay
{
    public static void ShowWelcomeAndHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("CodeExplorer (ce) - High-performance graph-based code intelligence & MCP server");
        Console.ResetColor();
        Console.WriteLine($"Version: {AppVersionProvider.GetAppVersion()}\n");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("USAGE:");
        Console.ResetColor();
        Console.WriteLine("  ce <command> [options]\n");

        // Workspace detection
        var ws = WorkspaceLocator.Find();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("CURRENT WORKSPACE:");
        Console.ResetColor();
        if (ws != null)
        {
            var dbExists = File.Exists(ws.DbPath);
            var dbSize = dbExists ? new FileInfo(ws.DbPath).Length / (1024.0 * 1024.0) : 0.0;
            var customQueriesCount = Directory.Exists(ws.QueriesDirectory)
                ? Directory.GetFiles(ws.QueriesDirectory, "*.cypher").Length
                : 0;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  [OK] ");
            Console.ResetColor();
            Console.WriteLine($"Found at '{ws.RootDirectory}'");
            Console.WriteLine($"       Database: {ws.DbPath} ({(dbExists ? $"{dbSize:F1} MB" : "not indexed yet")})");
            Console.WriteLine($"       Queries:  {ws.QueriesDirectory} ({customQueriesCount} custom query files)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("  [!]  ");
            Console.ResetColor();
            Console.WriteLine("No .codeexplorer workspace detected in current directory or any parent.");
            Console.WriteLine("       Run 'ce init [name]' to initialize a workspace here.");
        }
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("QUICK START WORKFLOW:");
        Console.ResetColor();
        Console.WriteLine("  1. ce init [name]       Initialize a .codeexplorer workspace in current directory");
        Console.WriteLine("  2. ce scan [path]       Index code topology, AST, dependencies and semantic graph");
        Console.WriteLine("  3. ce status            View workspace health, indexed projects, and statistics");
        Console.WriteLine("  4. ce queries           List all available built-in and custom Cypher queries");
        Console.WriteLine("  5. ce query \"<cypher>\"  Execute Cypher query directly against the code graph");
        Console.WriteLine("  6. ce mcp               Start MCP server (stdio) for Cursor, Claude Desktop, etc.\n");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("AVAILABLE COMMANDS:");
        Console.ResetColor();
        Console.WriteLine("  search <query>          Search identifiers, routes, tables, and topics using fast hybrid vector/trigram ranking");
        Console.WriteLine("  callers <target>        Find all callers, implementors, subclasses, publishers & subscribers (with path:line)");
        Console.WriteLine("  impact <symbol>         Compute architectural blast radius & inbound dependents of a symbol (with path:line)");
        Console.WriteLine("  trace <start> [end]     Trace call chains and distributed cross-service execution flows");
        Console.WriteLine("  query \"<cypher>\"        Run a read-only OpenCypher query against the knowledge graph (use --no-truncate)");
        Console.WriteLine("  queries                 List all available built-in and workspace custom queries");
        Console.WriteLine("  view [ontology|domain]  View architecture projections (C1, C2, C3, domain map, ontology layers & schema)");
        Console.WriteLine("  status (alias: info)    Show workspace summary, projects, node kinds, and statistics");
        Console.WriteLine("  dependencies (or deps)  Inspect project dependencies with runtime vs build filtering");
        Console.WriteLine("  contracts               List ingress/egress APIs, gRPC, and messaging contracts");
        Console.WriteLine("  gate                    Execute Semantic Invariant Gates (SIG) architectural rules");
        Console.WriteLine("  init                    Initialize a new .codeexplorer workspace");
        Console.WriteLine("  scan (alias: index)     Scan and index a directory into the nearest workspace");
        Console.WriteLine("  clear                   Clear indexed data from the workspace database");
        Console.WriteLine("  mcp                     Run Model Context Protocol server (stdio default, or --port)");
        Console.WriteLine("  configure               Auto-configure MCP servers and agent rules for AI tools");
        Console.WriteLine("  serve                   Run real-time WebSocket and HTTP API server (ws:// on --port)");
        Console.WriteLine("  ingest                  Direct batch ingestion of code nodes and relationships");
        Console.WriteLine("  intent                  Enrich graph with architectural intents using native LLM distillation");
        Console.WriteLine("  model                   Manage local LLM models (status, download)");
        Console.WriteLine("  test                    Test Impact Analysis (TIA): find exact test methods affected by changes");
        Console.WriteLine("  coverage                Analyze static test reachability coverage");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("CYPHER GRAPH SCHEMA CHEAT SHEET (for 'ce query \"<cypher>\" --no-truncate'):");
        Console.ResetColor();
        Console.WriteLine("  Node Labels:      Project (Service, Library, Worker), ExternalService, Type (n.kind = 'class'|'interface'), Function, Table, Topic, Endpoint");
        Console.WriteLine("  Node Properties:  n.name, n.path (relative file path), n.start_line, n.project_name, n.symbol, n.is_test (stored directly on nodes)");
        Console.WriteLine("  Relationships:    (caller:Function)-[:CALLS]->(callee:Function)");
        Console.WriteLine("                    (fn:Function)-[:SERVICE_CALL]->(target)                       -- cross-service HTTP/RPC call");
        Console.WriteLine("                    (cls:Type)-[:IMPLEMENTS]->(iface:Type)                        -- direct interface realization");
        Console.WriteLine("                    (cls:Type)-[:IMPLEMENTS|INHERITS_FROM*1..5]->(base:Type)      -- transitive interface/base-class inheritance");
        Console.WriteLine("                    (entity:Type)-[:PERSISTED_IN]->(tbl:Table)                    -- ORM entity to DB table");
        Console.WriteLine("                    (fn:Function)-[:PUBLISHES_TO|SUBSCRIBES_TO]->(top:Topic)      -- async messaging");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("EXAMPLES:");
        Console.ResetColor();
        Console.WriteLine("  ce search \"gates\"");
        Console.WriteLine("  ce callers IComponentLibraryParser");
        Console.WriteLine("  ce callers CheckGateAsync");
        Console.WriteLine("  ce impact GateExecutionEngine");
        Console.WriteLine("  ce view ontology");
        Console.WriteLine("  ce status");
        Console.WriteLine("  ce query \"MATCH (cls:Type)-[:IMPLEMENTS|INHERITS_FROM*1..5]->(i:Type {name: 'ISemanticExtension'}) WHERE cls.kind = 'class' RETURN cls.project_name, count(DISTINCT cls)\" --no-truncate\n");

        Console.WriteLine("For options and arguments on any specific command, run:");
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  ce <command> --help\n");
        Console.ResetColor();
    }
}
