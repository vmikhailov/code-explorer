using CommandLine;

namespace CodeExplorer.Options;

[Verb("ingest", HelpText = "Recursively parses a directory and loads structural nodes into Memgraph.")]
class IngestOptions
{
    [Option("dir", Required = true, HelpText = "The codebase directory to scan and parse.")]
    public string Dir { get; set; } = "";

    [Option("bolt-url", Default = "bolt://localhost:7687", HelpText = "The Bolt connection URL to Memgraph.")]
    public string BoltUrl { get; set; } = "";

    [Option("username", Default = "", HelpText = "The database username.")]
    public string Username { get; set; } = "";

    [Option("password", Default = "", HelpText = "The database password.")]
    public string Password { get; set; } = "";

    [Option("clear", HelpText = "Whether to surgically clear only this workspace's previous data before ingestion.")]
    public bool Clear { get; set; }

    [Option("clear-all", HelpText = "Whether to perform a global database clear of all workspaces before ingestion.")]
    public bool ClearAll { get; set; }
}
