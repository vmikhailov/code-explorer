using CommandLine;

namespace CodeExplorer.Options;

[Verb("trace", HelpText = "Trace cross-service execution flow from an entry point or service.")]
public class TraceOptions
{
    [Value(0, MetaName = "subcommand", Required = false, Default = "flow", HelpText = "Trace subcommand ('flow').")]
    public string Subcommand { get; set; } = "flow";

    [Option("from", Required = false, HelpText = "Starting service or project name.")]
    public string? From { get; set; }

    [Option('s', "service", Required = false, HelpText = "Starting service (alias for --from).")]
    public string? Service { get; set; }

    public string StartService => !string.IsNullOrWhiteSpace(From) ? From : (Service ?? string.Empty);

    [Option('e', "entry", Required = false, HelpText = "Optional entry point or endpoint URL.")]
    public string? Entry { get; set; }

    [Option("max-depth", Required = false, Default = 3, HelpText = "Maximum service traversal depth (default: 3).")]
    public int MaxDepth { get; set; } = 3;

    [Option('f', "format", Required = false, Default = "markdown", HelpText = "Output format: 'markdown', 'toon', 'mermaid', 'json'.")]
    public string Format { get; set; } = "markdown";

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
