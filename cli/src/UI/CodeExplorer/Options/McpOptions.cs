using CommandLine;

namespace CodeExplorer.Options;

[Verb("mcp", HelpText = "Starts the Model Context Protocol (MCP) server for the nearest workspace (stdio by default).")]
public class McpOptions
{
    [Option("db-path", Required = false, HelpText = "Explicit SQLite database path (defaults to nearest .codeexplorer/graph.db).")]
    public string? DbPath { get; set; }

    [Option("root", Required = false, HelpText = "Explicit workspace root directory.")]
    public string? Root { get; set; }

    [Option("port", Default = 0, HelpText = "HTTP port to run as an SSE network service (0 for stdio).")]
    public int Port { get; set; } = 0;

    [Option('q', "quiet", Required = false, HelpText = "Suppress console logging output.")]
    public bool Quiet { get; set; }
}
