using CommandLine;

namespace CodeExplorer.Options;

[Verb("dependencies", HelpText = "Inspect project dependencies with runtime vs build filtering.")]
public class DependenciesOptions
{
    [Option('p', "project", Required = false, HelpText = "Optional project name filter.")]
    public string? Project { get; set; }

    [Option('t', "type", Required = false, Default = "all", HelpText = "Dependency filter type: 'all', 'runtime', 'build'.")]
    public string Type { get; set; } = "all";

    [Option('f', "format", Required = false, Default = "markdown", HelpText = "Output format: 'markdown', 'toon', 'mermaid', 'json'.")]
    public string Format { get; set; } = "markdown";

    [Option("limit", Required = false, Default = 50, HelpText = "Maximum dependencies to display.")]
    public int Limit { get; set; } = 50;

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}

[Verb("deps", HelpText = "Alias for 'dependencies'. Inspect project dependencies.")]
public class DepsOptions : DependenciesOptions
{
}
