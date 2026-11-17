using CommandLine;

namespace CodeExplorer.Options;

[Verb("context", HelpText = "Compile a one-shot task context pack (anchors, side-effects, canonical table owners, legacy debt traps, and surgical read plan).")]
public class ContextOptions
{
    [Value(0, MetaName = "query", Required = false, HelpText = "Task symptom, feature description, route, or space-separated domain keywords.")]
    public string Query { get; set; } = string.Empty;

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }

    [Option('m', "max-anchors", Required = false, Default = 8, HelpText = "Maximum diversified anchor symbols (3 to 15).")]
    public int MaxAnchors { get; set; } = 8;

    [Option("no-gates", Required = false, Default = false, HelpText = "Omit semantic gate guardrails and legacy trap checks.")]
    public bool NoGates { get; set; }

    [Option("check-gates", Required = false, Default = false, HelpText = "Check semantic gates via the warm daemon (if --url / CE_SERVER_URL is set) or local workspace.")]
    public bool CheckGates { get; set; }

    [Option("url", Required = false, HelpText = "Optional warm CodeExplorer server URL (e.g. http://127.0.0.1:19580, or set CE_SERVER_URL).")]
    public string? ServerUrl { get; set; }
}
