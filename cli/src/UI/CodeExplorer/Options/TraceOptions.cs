using CommandLine;

namespace CodeExplorer.Options;

[Verb("trace", HelpText = "Trace function-to-function call chains ('ce trace <startFn> <endFn>') or cross-service execution flows ('ce trace flow --from <service>').")]
public class TraceOptions
{
    [Value(0, MetaName = "start", Required = false, HelpText = "Originating caller function name (or 'flow' for cross-service flow mode).")]
    public string? First { get; set; }

    [Value(1, MetaName = "end", Required = false, HelpText = "Destination callee function name (when tracing function-to-function call chains).")]
    public string? Second { get; set; }

    [Option("start", Required = false, HelpText = "Originating caller function name (alternative to positional argument).")]
    public string? StartFunctionOption { get; set; }

    [Option("to", Required = false, HelpText = "Destination callee function name (alternative to second positional argument).")]
    public string? EndFunctionOption { get; set; }

    [Option("from", Required = false, HelpText = "Starting service or project name (for cross-service flow mode).")]
    public string? From { get; set; }

    [Option('s', "service", Required = false, HelpText = "Starting service (alias for --from in cross-service flow mode).")]
    public string? Service { get; set; }

    public string StartService => !string.IsNullOrWhiteSpace(From) ? From : (Service ?? string.Empty);

    [Option('e', "entry", Required = false, HelpText = "Optional entry point or endpoint URL (for cross-service flow mode).")]
    public string? Entry { get; set; }

    [Option("max-depth", Required = false, HelpText = "Maximum hop depth (default: 5 for call chains, 3 for cross-service flows).")]
    public int? MaxDepth { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}

[Verb("call-chain", Hidden = true, HelpText = "Alias for 'trace <startFn> <endFn>': trace function-to-function call chains.")]
public class CallChainOptions : TraceOptions
{
}
