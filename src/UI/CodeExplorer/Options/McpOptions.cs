using CommandLine;

namespace CodeExplorer.Options;

[Verb("mcp", HelpText = "Starts the Model Context Protocol (MCP) server daemon with embedded SQLite.")]
class McpOptions
{
    [Option("db-path", Default = ".codeexplorer/graph.db", HelpText = "The SQLite database path (or ':memory:').")]
    public string DbPath { get; set; } = ".codeexplorer/graph.db";

    [Option("bolt-url", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string BoltUrl { get; set; } = "";

    [Option("username", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string Username { get; set; } = "";

    [Option("password", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string Password { get; set; } = "";

    [Option("port", Default = 0, HelpText = "The HTTP port to run the Model Context Protocol (MCP) server as an SSE network service (0 for stdio).")]
    public int Port { get; set; } = 0;
}
