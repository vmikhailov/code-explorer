using CommandLine;

namespace CodeExplorer.Options;

[Verb("search", HelpText = "Search code identifiers, routes, tables, and topics using fast hybrid vector & trigram similarity.")]
public class SearchOptions
{
    [Value(0, MetaName = "query", Required = true, HelpText = "Search query (symbol name, route, or colloquial keyword).")]
    public string Query { get; set; } = string.Empty;

    [Option('k', "kind", Required = false, HelpText = "Filter by node kind (Type, Function, Member, Endpoint, Table, Topic, Project).")]
    public string? Kind { get; set; }

    [Option('l', "limit", Required = false, Default = 15, HelpText = "Maximum number of search results to return (default: 15).")]
    public int Limit { get; set; } = 15;

    [Option('f', "format", Required = false, Default = "markdown", HelpText = "Output format: 'markdown', 'toon', 'json', 'table'.")]
    public string Format { get; set; } = "markdown";

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
