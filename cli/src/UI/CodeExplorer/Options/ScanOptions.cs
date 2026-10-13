using CommandLine;

namespace CodeExplorer.Options;

[Verb("scan", HelpText = "Scan and index a directory into the nearest workspace.")]
public class ScanOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "Path to scan and index (defaults to current directory).")]
    public string? Path { get; set; }

    [Option("clear", Default = false, HelpText = "Clear previous data for this path before scanning.")]
    public bool Clear { get; set; }

    [Option("clean", Default = false, Hidden = true, HelpText = "Alias for --clear.")]
    public bool Clean { get; set; }

    public bool ShouldClear => Clear || Clean;

    [Option("intent", Default = false, HelpText = "Run LLM architectural intent distillation (cached intents are applied automatically by default).")]
    public bool Intent { get; set; }

    [Option("watch", Default = false, HelpText = "Watch directory for file changes and reindex incrementally in background.")]
    public bool Watch { get; set; }

    [Option("init", Default = false, HelpText = "Automatically initialize workspace (.codeexplorer) if not found.")]
    public bool Init { get; set; }

    [Option("auto-init", Default = false, Hidden = true, HelpText = "Alias for --init.")]
    public bool AutoInit { get; set; }

    public bool ShouldAutoInit => Init || AutoInit;
}

[Verb("index", HelpText = "Alias for 'scan': index a directory into the nearest workspace.")]
public class IndexOptions : ScanOptions;
