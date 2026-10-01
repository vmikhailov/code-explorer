using CommandLine;

namespace CodeExplorer.Options;

[Verb("intent", HelpText = "Enrich graph with architectural intents using native LLM distillation.")]
public class IntentOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "Path to workspace or directory to enrich (defaults to current directory).")]
    public string? Path { get; set; }

    [Option('s', "service", Required = false, HelpText = "Filter analysis to specific service or comma-separated list of services (e.g. -s order-service,payment-service).")]
    public string? Service { get; set; }

    [Option('p', "project", Required = false, HelpText = "Alias for --service.")]
    public string? Project { get; set; }

    [Option("domains-only", Default = false, HelpText = "Only synthesize and materialize macro-domains from project signatures without running per-file LLM distillation.")]
    public bool DomainsOnly { get; set; }

    [Option("deep", Default = false, HelpText = "Enable multi-turn agentic graph exploration loop (ReAct) for local LLM.")]
    public bool Deep { get; set; }

    [Option("limit", Required = false, HelpText = "Maximum number of candidate files to analyze in this pass.")]
    public int? Limit { get; set; }

    [Option('r', "reanalyze", Default = false, HelpText = "Force re-analysis of selected files or services, bypassing the content-hash cache.")]
    public bool Reanalyze { get; set; }

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

    [Option("endpoint", Required = false, HelpText = "Custom OpenAI-compatible API endpoint URL (e.g. http://localhost:11434/v1).")]
    public string? Endpoint { get; set; }

    [Option("model", Required = false, HelpText = "Model name for custom API endpoint (e.g. qwen2.5-coder:7b).")]
    public string? Model { get; set; }

    [Option("api-key", Required = false, HelpText = "Optional API key for custom API endpoint.")]
    public string? ApiKey { get; set; }
}
