using CommandLine;

namespace CodeExplorer.Options;

[Verb("impact", HelpText = "Analyze multi-hop refactoring blast radius for a symbol (class, interface, function, service, or database table).")]
public class ImpactOptions
{
    [Value(0, MetaName = "symbol", Required = true, HelpText = "Target symbol name (Class, Interface, Function, Service, or Table).")]
    public string Symbol { get; set; } = string.Empty;

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
