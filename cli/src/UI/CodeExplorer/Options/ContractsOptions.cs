using CommandLine;

namespace CodeExplorer.Options;

[Verb("contracts", HelpText = "List ingress and egress service communication contracts (HTTP endpoints, gRPC calls, message publishers/subscribers).")]
public class ContractsOptions
{
    [Option('s', "service", Required = true, HelpText = "Name of the service or project.")]
    public string Service { get; set; } = string.Empty;

    [Option("direction", Required = false, Default = "all", HelpText = "Contract direction: 'ingress', 'egress', or 'all'.")]
    public string Direction { get; set; } = "all";

    [Option('f', "format", Required = false, Default = "markdown", HelpText = "Output format: 'markdown', 'toon', 'mermaid', 'json'.")]
    public string Format { get; set; } = "markdown";

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
