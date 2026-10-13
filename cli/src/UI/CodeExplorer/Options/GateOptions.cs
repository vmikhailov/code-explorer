using CommandLine;

namespace CodeExplorer.Options;

[Verb("gate", HelpText = "Manage and evaluate Semantic Invariant Gates (SIG) for architectural governance and technical debt ratchets.")]
public class GateOptions : ITargetOption
{
    [Value(0, MetaName = "action", Required = false, Default = "check", HelpText = "Action: 'check' (default), 'list', 'init', 'ratchet', 'violations' (or 'show').")]
    public string Action { get; set; } = "check";

    [Option('g', "gate", Required = false, HelpText = "Optional specific Gate ID to evaluate (e.g. 'SIG-P-B-001').")]
    public string? GateId { get; set; }

    [Option('c', "category", Required = false, HelpText = "Category filter ('Architectural', 'Platform').")]
    public string? Category { get; set; }

    [Option('p', "platform", Required = false, HelpText = "Platform filter (e.g. 'agnostic', 'dotnet', 'typescript', 'python', 'go', 'java').")]
    public string? Platform { get; set; }

    [Option('s', "scope", Required = false, HelpText = "Scope filter ('Method', 'Class', 'Module', 'Service', 'System').")]
    public string? Scope { get; set; }

    [Option('d', "dimension", Required = false, HelpText = "Dimension filter ('Boundaries', 'Rules', 'Contract', 'Resources').")]
    public string? Dimension { get; set; }

    [Option('f', "format", Required = false, Default = "table", HelpText = "Output format: 'table', 'markdown', 'json'.")]
    public string Format { get; set; } = "table";

    [Option('u', "update", Required = false, Default = false, HelpText = "When used with 'ratchet', updates baselines to lock in technical debt improvements.")]
    public bool Update { get; set; } = false;

    [Option('l', "limit", Required = false, Default = 50, HelpText = "Maximum number of violations to display (default: 50, use 0 for unlimited).")]
    public int Limit { get; set; } = 50;

    [Option('o', "offset", Required = false, Default = 0, HelpText = "Offset for pagination of violations.")]
    public int Offset { get; set; } = 0;

    [Option("file", Required = false, HelpText = "Filter violations by file path substring.")]
    public string? FileFilter { get; set; }

    [Option("symbol", Required = false, HelpText = "Filter violations by caller symbol substring.")]
    public string? SymbolFilter { get; set; }

    [Option("target", Required = false, HelpText = "Filter violations by target/callee substring.")]
    public string? TargetFilter { get; set; }

    [Option("all", Required = false, Default = false, HelpText = "Include both tolerated baseline debt and new violations.")]
    public bool All { get; set; } = false;

    [Option("new", Required = false, Default = false, HelpText = "Show only new un-baselined violations.")]
    public bool OnlyNew { get; set; } = false;

    [Option("tolerated", Required = false, Default = false, HelpText = "Show only tolerated baseline violations.")]
    public bool OnlyTolerated { get; set; } = false;

    [Option("group-by", Required = false, HelpText = "Group and aggregate violations summary by: 'file', 'symbol', or 'callee' ('target').")]
    public string? GroupBy { get; set; }

    [Option("dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }

    [Option("init", Required = false, Default = false, HelpText = "Automatically initialize workspace (.codeexplorer) if not found.")]
    public bool Init { get; set; }

    [Option("auto-init", Required = false, Default = false, Hidden = true, HelpText = "Alias for --init.")]
    public bool AutoInit { get; set; }

    public bool ShouldAutoInit => Init || AutoInit;

    [Option("scan", Required = false, Default = false, HelpText = "Automatically scan and index workspace before evaluating gates.")]
    public bool Scan { get; set; }

    [Option("auto-scan", Required = false, Default = false, Hidden = true, HelpText = "Alias for --scan.")]
    public bool AutoScan { get; set; }

    public bool ShouldAutoScan => Scan || AutoScan;

    [Option("clear", Required = false, Default = false, HelpText = "When used with --scan, perform a clean rescan before evaluating gates.")]
    public bool Clear { get; set; }

    string ITargetOption.Target => Action;
}
