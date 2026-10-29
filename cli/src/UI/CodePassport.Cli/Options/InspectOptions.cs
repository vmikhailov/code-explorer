using CommandLine;

namespace CodePassport.Cli.Options;

[Verb("inspect", HelpText = "Generate a Behavioral Passport (10D capability matrix, Tier 0-3, exports, and toxic combinations) for a package directory.")]
public sealed class InspectOptions
{
    [Value(0, MetaName = "path", Default = ".", HelpText = "Path to the package directory to inspect.")]
    public string Path { get; set; } = ".";

    [Option("json", HelpText = "Emit the PackagePassport as formatted JSON.")]
    public bool Json { get; set; }

    [Option('o', "out", HelpText = "Write the PackagePassport JSON to the specified file path.")]
    public string? OutputPath { get; set; }
}
