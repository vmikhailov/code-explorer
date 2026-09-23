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
        Console.WriteLine("  init                    Initialize a new .codeexplorer workspace");
        Console.WriteLine("  scan (alias: index)     Scan and index a directory into the nearest workspace");
        Console.WriteLine("  status (alias: info)    Show workspace summary, projects, node kinds, and statistics");
        Console.WriteLine("  clear                   Clear indexed data from the workspace database");
        Console.WriteLine("  queries                 List all available built-in and workspace custom queries");
        Console.WriteLine("  query                   Run a read-only Cypher query against the knowledge graph");
        Console.WriteLine("  export                  Export architecture and lineage diagrams (Mermaid, C4)");
        Console.WriteLine("  mcp                     Run Model Context Protocol server (stdio default, or --port)");
        Console.WriteLine("  serve                   Run real-time WebSocket and HTTP API server (ws:// on --port)");
        Console.WriteLine("  ingest                  Direct batch ingestion of code nodes and relationships");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("EXAMPLES:");
        Console.ResetColor();
        Console.WriteLine("  ce init MyProject");
        Console.WriteLine("  ce scan ./src");
        Console.WriteLine("  ce status");
        Console.WriteLine("  ce queries");
        Console.WriteLine("  ce query -n get_architecture_map_workspace");
        Console.WriteLine("  ce query --show get_architecture_map_workspace");
        Console.WriteLine("  ce query \"MATCH (p:Project) RETURN p.name, p.project_type\"");
        Console.WriteLine("  ce query --file my_query.cypher");
        Console.WriteLine("  ce mcp");
        Console.WriteLine("  ce clear ./src/old-module\n");

        Console.WriteLine("For options and arguments on any specific command, run:");
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  ce <command> --help\n");
        Console.ResetColor();
    }
}
