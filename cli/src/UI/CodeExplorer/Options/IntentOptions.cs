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
}
