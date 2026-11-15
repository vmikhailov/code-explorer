using CommandLine;

namespace CodeExplorer.Options;

[Verb("dependencies", HelpText = "Inspect project dependencies with runtime vs build filtering.")]
public class DependenciesOptions
{
    [Value(0, MetaName = "project", Required = false, HelpText = "Optional project name filter.")]
    public string? PositionalProject { get; set; }

    [Option('p', "project", Required = false, HelpText = "Optional project name filter (alternative to positional argument).")]
    public string? Project { get; set; }

    public string? EffectiveProject => !string.IsNullOrWhiteSpace(Project) ? Project : PositionalProject;

    [Option('t', "type", Required = false, Default = "all", HelpText = "Dependency filter type: 'all', 'runtime', 'build'.")]
    public string Type { get; set; } = "all";

    [Option("limit", Required = false, Default = 50, HelpText = "Maximum dependencies to display.")]
    public int Limit { get; set; } = 50;

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}

[Verb("deps", HelpText = "Alias for 'dependencies'. Inspect project dependencies.")]
public class DepsOptions : DependenciesOptions
{
}
