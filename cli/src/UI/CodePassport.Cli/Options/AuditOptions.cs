using CommandLine;

namespace CodePassport.Cli.Options;

[Verb("audit", HelpText = "Audit a workspace or node_modules directory for Tier 3 privileged packages and Toxic Combinations.")]
public sealed class AuditOptions
{
    [Value(0, MetaName = "path", Default = ".", HelpText = "Workspace or package root directory to audit.")]
    public string Path { get; set; } = ".";

    [Option("json", HelpText = "Emit audit findings as formatted JSON.")]
    public bool Json { get; set; }
}
