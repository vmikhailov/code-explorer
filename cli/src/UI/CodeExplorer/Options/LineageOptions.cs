using CommandLine;

namespace CodeExplorer.Options;

[Verb("lineage", HelpText = "Inspect end-to-end database table data lineage (ORM entities, SQL queries, readers, writers, and file coordinates).")]
public class LineageOptions
{
    [Value(0, MetaName = "table", Required = false, HelpText = "Database table or collection name to trace (e.g. 'orders' or 'users').")]
    public string? PositionalTable { get; set; }

    [Option('t', "table", Required = false, HelpText = "Database table or collection name (alternative to positional argument).")]
    public string? TableOption { get; set; }

    public string? EffectiveTable => !string.IsNullOrWhiteSpace(TableOption) ? TableOption : PositionalTable;

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
