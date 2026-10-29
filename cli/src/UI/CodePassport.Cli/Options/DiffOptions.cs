using CommandLine;

namespace CodePassport.Cli.Options;

[Verb("diff", HelpText = "Compute behavioral capability drift between two package versions (Delta = Cap(to) & ~Cap(from)).")]
public sealed class DiffOptions
{
    [Value(0, MetaName = "fromPath", Required = true, HelpText = "Baseline package directory or PackagePassport.json (v_N-1).")]
    public string FromPath { get; set; } = string.Empty;

    [Value(1, MetaName = "toPath", Required = true, HelpText = "Target package directory or PackagePassport.json (v_N).")]
    public string ToPath { get; set; } = string.Empty;

    [Option("json", HelpText = "Emit the PassportDiffResult as formatted JSON.")]
    public bool Json { get; set; }

    [Option("fail-on-drift", Default = false, HelpText = "Return a non-zero exit code (2) when CriticalDrift is detected.")]
    public bool FailOnDrift { get; set; }
}
