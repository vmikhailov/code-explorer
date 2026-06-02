using CommandLine;

namespace CodeExplorer;

[Verb("query", HelpText = "Runs a safe read-only Cypher query against Memgraph.")]
class QueryOptions
{
    [Option('q', "query", Required = true, HelpText = "The Cypher query string to execute.")]
    public string Query { get; set; } = "";

    [Option("bolt-url", Default = "bolt://localhost:7687", HelpText = "The Bolt connection URL to Memgraph.")]
    public string BoltUrl { get; set; } = "";

    [Option("username", Default = "", HelpText = "The database username.")]
    public string Username { get; set; } = "";

    [Option("password", Default = "", HelpText = "The database password.")]
    public string Password { get; set; } = "";
}
