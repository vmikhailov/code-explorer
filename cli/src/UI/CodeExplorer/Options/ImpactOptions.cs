using CommandLine;

namespace CodeExplorer.Options;

[Verb("impact", Hidden = true, HelpText = "Analyze blast-radius impact: find all dependents, callers, and references affected by modifying a symbol.")]
public class ImpactOptions
{
    [Value(0, MetaName = "symbol", Required = true, HelpText = "Target symbol name (Class, Interface, Function, Service, or Table).")]
    public string Symbol { get; set; } = string.Empty;

    [Option('f', "format", Required = false, Default = "markdown", HelpText = "Output format: 'markdown', 'toon', 'json', 'table'.")]
    public string Format { get; set; } = "markdown";

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
