using CommandLine;

namespace CodeExplorer.Options;

[Verb("callers", HelpText = "Find all callers, inbound references, service consumers, or table/topic accessors for a target.")]
public class CallersOptions
{
    [Value(0, MetaName = "target", Required = true, HelpText = "Target name (Function, Service, Project, Table, Topic, or Type).")]
    public string Target { get; set; } = string.Empty;

    [Option('t', "tests", Required = false, Default = false, HelpText = "Include test callers (default: false).")]
    public bool IncludeTests { get; set; } = false;

    [Option('m', "method", Required = false, HelpText = "Optional interface or abstract method name declared on target; resolves concrete implementing classes and DI targets instead of inbound callers.")]
    public string? MethodName { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
