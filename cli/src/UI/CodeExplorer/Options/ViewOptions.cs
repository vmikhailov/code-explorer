using CommandLine;

namespace CodeExplorer.Options;

[Verb("view", HelpText = "View architectural projections (C1 system context, C2 service flow, C3 component, domain map, tiered).")]
public class ViewOptions
{
    [Value(0, MetaName = "target", Required = false, Default = "architecture", HelpText = "Target view to display ('architecture', 'layers', 'domain', 'context').")]
    public string Target { get; set; } = "architecture";

    [Option('l', "level", Required = false, Default = "c1", HelpText = "Architecture level ('c1'/'system', 'c2'/'service', 'c3'/'component', 'domain'/'context', 'tiers').")]
    public string Level { get; set; } = "c1";

    [Option('s', "scope", Required = false, HelpText = "Optional scope filter (e.g. project name).")]
    public string? Scope { get; set; }

    [Option("include-libs", Required = false, Default = true, HelpText = "Include library projects.")]
    public bool IncludeLibraries { get; set; } = true;

    [Option('f', "format", Required = false, Default = "markdown", HelpText = "Output format: 'markdown', 'toon', 'mermaid', 'json'.")]
    public string Format { get; set; } = "markdown";

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
