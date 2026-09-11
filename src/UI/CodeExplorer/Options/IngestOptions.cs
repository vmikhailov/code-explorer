using CommandLine;

namespace CodeExplorer.Options;

[Verb("ingest", HelpText = "Recursively parses a directory and loads structural nodes into the SQLite graph database.")]
class IngestOptions
{
    [Option("dir", Required = true, HelpText = "The codebase directory to scan and parse.")]
    public string Dir { get; set; } = "";

    [Option("db-path", Default = ".codeexplorer/graph.db", HelpText = "The SQLite database path (or ':memory:').")]
    public string DbPath { get; set; } = ".codeexplorer/graph.db";

    [Option("bolt-url", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string BoltUrl { get; set; } = "";

    [Option("username", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string Username { get; set; } = "";

    [Option("password", Hidden = true, HelpText = "Legacy option, ignored.")]
    public string Password { get; set; } = "";

    [Option("clear", HelpText = "Whether to surgically clear only this workspace's previous data before ingestion.")]
    public bool Clear { get; set; }

    [Option("clear-all", HelpText = "Whether to perform a global database clear of all workspaces before ingestion.")]
    public bool ClearAll { get; set; }
}
