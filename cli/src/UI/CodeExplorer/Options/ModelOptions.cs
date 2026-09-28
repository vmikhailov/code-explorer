using CommandLine;

namespace CodeExplorer.Options;

[Verb("model", HelpText = "Manage local LLM models for CodeExplorer intent distillation.")]
public class ModelOptions
{
    [Value(0, MetaName = "action", Required = false, HelpText = "Action to perform: 'status' (default) or 'download'.")]
    public string? Action { get; set; } = "status";

    [Option("force", Default = false, HelpText = "Force re-download even if model file already exists.")]
    public bool Force { get; set; }
}
