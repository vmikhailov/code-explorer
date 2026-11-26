using CommandLine;

namespace CodeExplorer.Options;

[Verb("domains", HelpText = "Partition multi-repo or monorepo workspaces into architectural domains using hierarchy coarsening, hub separation, WCC, and Leiden community detection.")]
public class DomainsOptions
{
    [Option('d', "dir", Required = false, HelpText = "Workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }

    [Option("resolution", Required = false, Default = 1.0, HelpText = "Leiden resolution parameter gamma (higher yields smaller, tighter domains; default 1.0).")]
    public double Resolution { get; set; } = 1.0;

    [Option("hub-threshold", Required = false, Default = 0.15, HelpText = "Inbound unit ratio threshold (0.0-1.0) to extract Shared Platform / Commons hubs before clustering (default 0.15).")]
    public double HubThreshold { get; set; } = 0.15;

    [Option('f', "format", Required = false, Default = "text", HelpText = "Output format: text, json, yaml, markdown.")]
    public string Format { get; set; } = "text";

    [Option('o', "output", Required = false, HelpText = "Optional file path to write the partition report.")]
    public string? OutputPath { get; set; }
}
