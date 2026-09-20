using CommandLine;

namespace CodeExplorer.Options;

[Verb("clear", HelpText = "Clear indexed data from the nearest workspace database.")]
public class ClearOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "Specific sub-path to clear from the graph. If omitted, clears entire database.")]
    public string? Path { get; set; }

    [Option('y', "yes", Default = false, HelpText = "Skip confirmation prompt when clearing.")]
    public bool Yes { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Starting directory to look for workspace (defaults to current directory).")]
    public string? Dir { get; set; }
}
