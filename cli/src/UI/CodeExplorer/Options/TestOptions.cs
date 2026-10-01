using CommandLine;

namespace CodeExplorer.Options;

[Verb("test", HelpText = "Test Impact Analysis (TIA): find exact test methods affected by changed files or git diff.")]
public class TestOptions
{
    [Value(0, MetaName = "subcommand", Required = false, Default = "impact", HelpText = "Test subcommand ('impact' or 'coverage').")]
    public string Subcommand { get; set; } = "impact";

    [Option("files", Required = false, HelpText = "Comma-separated list of changed file paths.")]
    public string? Files { get; set; }

    [Option("git", Required = false, HelpText = "Inspect working git diff against HEAD.")]
    public bool Git { get; set; }

    [Option("git-base", Required = false, HelpText = "Base branch or commit for git diff (e.g. 'origin/main', 'HEAD~1').")]
    public string? GitBase { get; set; }

    [Option("diff", Required = false, HelpText = "Raw git unified diff content.")]
    public string? Diff { get; set; }

    [Option("symbols", Required = false, HelpText = "Comma-separated list of modified symbol names.")]
    public string? Symbols { get; set; }

    [Option("max-depth", Required = false, Default = 15, HelpText = "Maximum call traversal depth (default: 15).")]
    public int MaxDepth { get; set; } = 15;

    [Option('f', "format", Required = false, Default = "markdown", HelpText = "Output format: 'markdown', 'list', 'commands', 'json'.")]
    public string Format { get; set; } = "markdown";

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }

    // Flags for coverage subcommand
    [Option('p', "project", Required = false, HelpText = "Filter by project name (when subcommand is 'coverage').")]
    public string? Project { get; set; }

    [Option("uncovered-only", Default = false, HelpText = "Display only uncovered items.")]
    public bool UncoveredOnly { get; set; }

    [Option("covered-only", Default = false, HelpText = "Display only covered items.")]
    public bool CoveredOnly { get; set; }

    [Option("threshold", Required = false, HelpText = "Minimum required coverage percentage (fails with exit code 1 if below).")]
    public double? Threshold { get; set; }
}
