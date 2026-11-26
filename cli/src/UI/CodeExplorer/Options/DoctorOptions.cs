using CommandLine;

namespace CodeExplorer.Options;

[Verb("doctor", HelpText = "Diagnose workspace index health, parsing issues (ScanIssue), and unsupported frameworks/parsers for self-improvement.")]
public class DoctorOptions
{
    [Option("issues", Required = false, Default = false, HelpText = "Show only index health and parsing/ScanIssue diagnostics.")]
    public bool IssuesOnly { get; set; }

    [Option("gaps", Required = false, Default = false, HelpText = "Show only unsupported extensions, XML dialects, unmatched libraries, and coverage anomalies.")]
    public bool GapsOnly { get; set; }

    [Option("report", Required = false, Default = false, HelpText = "Generate a privacy-safe, anonymized report (Markdown by default) ready to share or attach to a GitHub Issue.")]
    public bool Report { get; set; }

    [Option("anonymize", Required = false, Default = false, HelpText = "Redact file paths and project names from output.")]
    public bool Anonymize { get; set; }

    [Option('f', "format", Required = false, HelpText = "Output format: 'text' (default), 'markdown', 'yaml', or 'json'.")]
    public string? Format { get; set; }

    [Option('o', "output", Required = false, HelpText = "Optional file path to write the diagnostic report.")]
    public string? OutputPath { get; set; }

    [Option('l', "limit", Required = false, Default = 25, HelpText = "Maximum number of items per section (default: 25).")]
    public int Limit { get; set; } = 25;

    [Option("timeout", Required = false, Default = 60, HelpText = "Database query timeout in seconds (default: 60; 0 to disable timeout).")]
    public int TimeoutSeconds { get; set; } = 60;

    [Option("dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
