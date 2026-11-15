using CommandLine;

namespace CodeExplorer.Options;

[Verb("search", HelpText = "Search code symbols and ontology identifiers (substring AST match or hybrid vector & trigram similarity).")]
public class SearchOptions
{
    [Value(0, MetaName = "query", Required = true, HelpText = "Symbol name, substring, URL route fragment, table name, or keyword.")]
    public string Query { get; set; } = string.Empty;

    [Option('k', "kind", Required = false, HelpText = "Entity filter: Class, Interface, Type, Function, Variable, Member, Endpoint, Table, Topic, Project.")]
    public string? Kind { get; set; }

    [Option('t', "type", Required = false, HelpText = "Alias for --kind (entity filter matching MCP symbolType).")]
    public string? SymbolType { get; set; }

    public string? EffectiveSymbolType => !string.IsNullOrWhiteSpace(SymbolType) ? SymbolType : Kind;

    [Option('m', "mode", Required = false, HelpText = "Search strategy: 'substring' (deterministic AST match) or 'hybrid' (fuzzy trigram + vector similarity).")]
    public string? Mode { get; set; }

    public virtual string DefaultMode => "hybrid";

    public virtual int DefaultLimit => 15;

    [Option('l', "limit", Required = false, HelpText = "Maximum number of results to return.")]
    public int? Limit { get; set; }

    public int EffectiveLimit => Limit ?? DefaultLimit;

    public string EffectiveMode => !string.IsNullOrWhiteSpace(Mode) ? Mode : DefaultMode;

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}

[Verb("symbol", HelpText = "Find code declarations and ontology entities by deterministic substring or hybrid search.")]
public class SymbolOptions : SearchOptions
{
    public override string DefaultMode => "substring";
    public override int DefaultLimit => 50;
}

[Verb("find", Hidden = true, HelpText = "Alias for 'symbol': find code declarations and ontology entities.")]
public class FindSymbolAliasOptions : SymbolOptions
{
}
