using CommandLine;

namespace CodeExplorer.Options;

[Verb("map", HelpText = "Inspect workspace architecture map or export diagrams across viewpoints (overview, clusters, c1, c2, c3, project-map, tiers, lineage, cqrs).")]
public class MapOptions
{
    [Value(0, MetaName = "project", Required = false, HelpText = "Optional project, service, or cluster filter.")]
    public string? PositionalProject { get; set; }

    [Option('p', "project", Required = false, HelpText = "Optional project, service, or cluster filter.")]
    public string? ProjectOption { get; set; }

    public string? EffectiveProject => !string.IsNullOrWhiteSpace(ProjectOption) ? ProjectOption : PositionalProject;

    [Option('v', "view", Required = false, Default = "overview", HelpText = "Architecture viewpoint: 'overview' (default), 'clusters', 'c1', 'c2', 'c3', 'project-map', 'tiers', 'lineage', 'cqrs'.")]
    public string View { get; set; } = "overview";

    [Option('f', "format", Required = false, Default = "yaml", HelpText = "Output format: 'yaml' (default), 'mermaid', 'c4'.")]
    public string Format { get; set; } = "yaml";

    [Option("include-libs", Required = false, Default = false, HelpText = "Include shared library projects alongside deployable services.")]
    public bool IncludeLibraries { get; set; }

    [Option('o', "output", Required = false, HelpText = "Optional output file path (prints to stdout when omitted).")]
    public string? Output { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}

[Verb("view", Hidden = true, HelpText = "Alias for 'map': inspect workspace architecture map and viewpoints.")]
public class ViewOptions : MapOptions
{
}
