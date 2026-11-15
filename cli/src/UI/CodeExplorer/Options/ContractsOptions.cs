using CommandLine;

namespace CodeExplorer.Options;

[Verb("contracts", HelpText = "Inspect service communication contracts, entry points, and cross-service flows (all, ingress, egress, entry_points, flow).")]
public class ContractsOptions
{
    [Value(0, MetaName = "service", Required = false, HelpText = "Name of the service or project.")]
    public string? PositionalService { get; set; }

    [Option('s', "service", Required = false, HelpText = "Name of the service or project (alternative to positional argument).")]
    public string? Service { get; set; }

    public string EffectiveService => !string.IsNullOrWhiteSpace(Service)
        ? Service
        : (PositionalService ?? string.Empty);

    [Option("direction", Required = false, Default = "all", HelpText = "Boundary mode: 'all' (default), 'ingress', 'egress', 'entry_points', or 'flow'.")]
    public string Direction { get; set; } = "all";

    [Option('e', "entry", Required = false, HelpText = "Optional URL route substring, topic, or handler method to anchor traversal when direction='flow'.")]
    public string? EntryPoint { get; set; }

    [Option("max-depth", Required = false, Default = 3, HelpText = "Maximum cross-service hop depth when direction='flow' (1 to 10, default: 3).")]
    public int MaxDepth { get; set; } = 3;

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
