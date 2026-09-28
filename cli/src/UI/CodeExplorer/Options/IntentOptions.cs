using CommandLine;

namespace CodeExplorer.Options;

[Verb("intent", HelpText = "Enrich graph with architectural intents using native LLM distillation.")]
public class IntentOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "Path to workspace or directory to enrich (defaults to current directory).")]
    public string? Path { get; set; }

    [Option("limit", Required = false, HelpText = "Maximum number of candidate files to analyze in this pass.")]
    public int? Limit { get; set; }

    [Option("clear", Default = false, HelpText = "Clear all cached intent records for this workspace.")]
    public bool Clear { get; set; }

    [Option("reset-errors", Default = false, HelpText = "Reset error counter for files that failed distillation previously.")]
    public bool ResetErrors { get; set; }

    [Option("stop", Default = false, HelpText = "Stop any active intent distillation process running for this workspace.")]
    public bool Stop { get; set; }

    [Option("status", Default = false, HelpText = "Check status of the intent distillation process for this workspace.")]
    public bool Status { get; set; }

    [Option("force", Default = false, HelpText = "Force stop any existing intent process before starting a new run.")]
    public bool Force { get; set; }
}
