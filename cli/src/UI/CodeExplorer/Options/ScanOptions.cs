using CommandLine;

namespace CodeExplorer.Options;

[Verb("scan", HelpText = "Scan and index a directory into the nearest workspace.")]
public class ScanOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "Path to scan and index (defaults to current directory).")]
    public string? Path { get; set; }

    [Option("clear", Default = false, HelpText = "Clear previous data for this path before scanning.")]
    public bool Clear { get; set; }
}

[Verb("index", HelpText = "Alias for 'scan': index a directory into the nearest workspace.")]
public class IndexOptions : ScanOptions;
