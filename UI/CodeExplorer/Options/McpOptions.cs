using CommandLine;

namespace CodeExplorer;

[Verb("mcp", HelpText = "Starts the stdio Model Context Protocol (MCP) server daemon.")]
class McpOptions
{
    [Option("bolt-url", Default = "bolt://localhost:7687", HelpText = "The Bolt connection URL to Memgraph.")]
    public string BoltUrl { get; set; } = "";

    [Option("username", Default = "", HelpText = "The database username.")]
    public string Username { get; set; } = "";

    [Option("password", Default = "", HelpText = "The database password.")]
    public string Password { get; set; } = "";

    [Option("port", Default = 0, HelpText = "The HTTP port to run the Model Context Protocol (MCP) server as an SSE network service (0 for stdio).")]
    public int Port { get; set; } = 0;
}
