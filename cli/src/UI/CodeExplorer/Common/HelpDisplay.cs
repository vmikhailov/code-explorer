namespace CodeExplorer.Common;

public static class HelpDisplay
{
    private const string BannerTitle = "CodeExplorer (ce) - High-performance graph-based code intelligence & MCP server";
    private const string UsageHeader = "USAGE:";
    private const string UsageBody = "  ce <command> [options]\n";
    private const string CurrentWorkspaceHeader = "CURRENT WORKSPACE:";
    private const string CypherQueryGlob = "*.cypher";
    private const string OkPrefix = "  [OK] ";
    private const string WarnPrefix = "  [!]  ";
    private const string NoWorkspaceLine1 = "No .codeexplorer workspace detected in current directory or any parent.";
    private const string NoWorkspaceLine2 = "       Run 'ce scan' or 'ce init [name]' to initialize a workspace here.";
    private const string QuickStartHeader = "QUICK START WORKFLOW:";
    private const string QuickStartBody = """
  1. ce scan [path]       Initialize (if needed) and index AST, dependencies, and semantic graph
  2. ce map               Inspect workspace architecture map, projects, endpoints, and databases
  3. ce taxonomy          Inspect graph ontology schema (or --kind <Kind>, --status)
  4. ce symbol <name>     Locate code declarations and ontology entities (or 'ce search <query>')
  5. ce gate check        Evaluate Semantic Invariant Gates (SIG) against ratchet baselines
  6. ce mcp               Start MCP server (stdio) for Cursor, Claude Code, Antigravity, etc.
""";
    private const string AvailableCommandsHeader = "AVAILABLE COMMANDS:";
    private const string AvailableCommandsBody = """
  scan [path]             Initialize (if needed) and index workspace into the code graph (alias: index)
  taxonomy [--kind|--status] Inspect graph ontology schema, node kind spec, or workspace health
  map [project]           Inspect architecture map (overview, c1..c3, tiers) or export Mermaid/C4 diagrams
  dependencies [project]  Inspect project and package dependencies with runtime vs build filtering (alias: deps)
  contracts <service>     Inspect service contracts, entry_points, and cross-service flows
  outline [path]          Extract structural AST outline of a file or directory
  snippets <file:s-e>     Fetch exact source code line ranges by file:start-end or --nodes YAML
  symbol <name>           Find code declarations and ontology entities (substring default, or --mode hybrid)
  search <query>          Fuzzy hybrid trigram & vector identifier search (or --mode substring)
  callers <target>        Find inbound callers or resolve interface/DI implementations (-m <method>)
  trace <start> <end>     Trace function-to-function call chains (or 'trace flow --from <service>')
  impact <symbol>         Analyze multi-hop refactoring blast radius for a symbol
  lineage <table>         Trace database table data lineage (ORM entities, SQL queries, readers & writers)
  test [impact|coverage]  Select affected tests for code changes or audit static test coverage
  gate [check|list|...]   Evaluate Semantic Invariant Gates (SIG), baselines, and code anomalies
  query "<cypher>"        Execute read-only or saved OpenCypher queries (alias: queries for --list)
  status (alias: info)    Show workspace summary, projects, node kinds, and statistics
  doctor [--issues|--gaps|--report] Diagnose index health, ScanIssue parse errors, and unsupported stack gaps
  detector [list|validate|schema] Manage declarative L4 YAML detectors (.codeexplorer/detectors/*.yaml)
  init / clear / export   Workspace lifecycle and standalone diagram export helpers
  mcp / serve / configure Start MCP server, WebSocket API server, or configure AI coding tools
""";
    private const string ExamplesHeader = "EXAMPLES:";
    private const string ExamplesBody = """
  ce map
  ce outline cli/src
  ce symbol WorkspaceIndexer --type Class
  ce callers IComponentLibraryParser -m TryParse
  ce trace HandleAsync ExecuteCypherAsync
  ce query "MATCH (p:Project) RETURN p.name, p.language" --no-truncate
""";
    private const string HelpFooterPrompt = "For options and arguments on any specific command, run:";
    private const string HelpFooterCommand = "  ce <command> --help\n";

    public static void ShowWelcomeAndHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(BannerTitle);
        Console.ResetColor();
        Console.WriteLine($"Version: {AppVersionProvider.GetAppVersion()}\n");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(UsageHeader);
        Console.ResetColor();
        Console.WriteLine(UsageBody);

        var ws = WorkspaceLocator.Find();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(CurrentWorkspaceHeader);
        Console.ResetColor();
        if (ws != null)
        {
            var dbExists = File.Exists(ws.DbPath);
            var dbSize = dbExists ? new FileInfo(ws.DbPath).Length / (1024.0 * 1024.0) : 0.0;
            var customQueriesCount = Directory.Exists(ws.QueriesDirectory)
                ? Directory.GetFiles(ws.QueriesDirectory, CypherQueryGlob).Length
                : 0;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(OkPrefix);
            Console.ResetColor();
            Console.WriteLine($"Found at '{ws.RootDirectory}'");
            Console.WriteLine($"       Database: {ws.DbPath} ({(dbExists ? $"{dbSize:F1} MB" : "not indexed yet")})");
            Console.WriteLine($"       Queries:  {ws.QueriesDirectory} ({customQueriesCount} custom query files)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write(WarnPrefix);
            Console.ResetColor();
            Console.WriteLine(NoWorkspaceLine1);
            Console.WriteLine(NoWorkspaceLine2);
        }
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(QuickStartHeader);
        Console.ResetColor();
        Console.WriteLine(QuickStartBody);
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(AvailableCommandsHeader);
        Console.ResetColor();
        Console.WriteLine(AvailableCommandsBody);
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(ExamplesHeader);
        Console.ResetColor();
        Console.WriteLine(ExamplesBody);
        Console.WriteLine();

        Console.WriteLine(HelpFooterPrompt);
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(HelpFooterCommand);
        Console.ResetColor();
    }
}
