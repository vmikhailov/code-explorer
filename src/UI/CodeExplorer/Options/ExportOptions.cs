using CommandLine;

namespace CodeExplorer.Options;

[Verb("export", HelpText = "Export architecture and system topology diagrams in Mermaid or C4 format.")]
public class ExportOptions
{
    [Option('f', "format", Required = false, Default = "mermaid", HelpText = "Diagram format ('mermaid' or 'c4').")]
    public string Format { get; set; } = "mermaid";

    [Option('t', "type", Required = false, Default = "architecture", HelpText = "Diagram type ('architecture', 'lineage', 'cqrs').")]
    public string Type { get; set; } = "architecture";

    [Option('p', "project", Required = false, HelpText = "Optional project name filter.")]
    public string? Project { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output file path (prints to stdout if omitted).")]
    public string? Output { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
