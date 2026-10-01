using CommandLine;

namespace CodeExplorer.Options;

[Verb("coverage", HelpText = "Analyze static test reachability coverage: list covered and uncovered classes & methods.")]
public class CoverageOptions
{
    [Option('p', "project", Required = false, HelpText = "Filter analysis to a specific project name.")]
    public string? Project { get; set; }

    [Option("path", Required = false, HelpText = "Filter analysis to files matching path prefix.")]
    public string? PathPrefix { get; set; }

    [Option("uncovered-only", Default = false, HelpText = "Display only uncovered classes and methods.")]
    public bool UncoveredOnly { get; set; }

    [Option("covered-only", Default = false, HelpText = "Display only covered classes and methods.")]
    public bool CoveredOnly { get; set; }

    [Option("threshold", Required = false, HelpText = "Minimum acceptable method coverage percentage for CI gating.")]
    public double? Threshold { get; set; }

    [Option('f', "format", Required = false, Default = "markdown", HelpText = "Output format: 'markdown', 'json'.")]
    public string Format { get; set; } = "markdown";

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
