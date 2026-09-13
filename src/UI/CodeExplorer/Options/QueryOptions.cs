using CommandLine;

namespace CodeExplorer.Options;

[Verb("query", HelpText = "Runs a safe read-only Cypher query against the SQLite graph database.")]
public class QueryOptions
{
    [Value(0, MetaName = "cypher", Required = false, HelpText = "The Cypher query string to execute.")]
    public string? PositionalQuery { get; set; }

    [Option('q', "query", Required = false, HelpText = "The Cypher query string to execute (alternative to positional argument).")]
    public string? QueryOption { get; set; }

    public string Query => !string.IsNullOrWhiteSpace(PositionalQuery)
        ? PositionalQuery
        : (QueryOption ?? string.Empty);

    [Option('f', "file", Required = false, HelpText = "Path to a .cypher file to execute.")]
    public string? FilePath { get; set; }

    [Option("db-path", Required = false, HelpText = "Explicit SQLite database path (defaults to nearest .codeexplorer/graph.db).")]
    public string? DbPath { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Starting directory to look for workspace (defaults to current directory).")]
    public string? Dir { get; set; }

    [Option("format", Default = "table", HelpText = "Output format: 'table' or 'json'.")]
    public string Format { get; set; } = "table";
}
