using CommandLine;

namespace CodeExplorer.Options;

[Verb("query", HelpText = "Runs a safe read-only Cypher query against the SQLite graph database.")]
class QueryOptions
{
    [Option('q', "query", Required = true, HelpText = "The Cypher query string to execute.")]
    public string Query { get; set; } = "";

    [Option("db-path", Default = ".codeexplorer/graph.db", HelpText = "The SQLite database path (or ':memory:').")]
    public string DbPath { get; set; } = ".codeexplorer/graph.db";

    [Option("bolt-url", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string BoltUrl { get; set; } = "";

    [Option("username", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string Username { get; set; } = "";

    [Option("password", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string Password { get; set; } = "";
}
