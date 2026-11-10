using CommandLine;

namespace CodeExplorer.Options;

[Verb("status", HelpText = "Show status and summary of the nearest workspace.")]
public class StatusOptions
{
    [Value(0, MetaName = "dir", Required = false, HelpText = "Starting directory to look for workspace (defaults to current directory).")]
    public string? PositionalDir { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Starting directory to look for workspace (defaults to current directory).")]
    public string? DirOption { get; set; }

    public string? Dir
    {
        get => !string.IsNullOrWhiteSpace(DirOption) ? DirOption : PositionalDir;
        set => DirOption = value;
    }
}

[Verb("info", HelpText = "Alias for 'status': show summary of the nearest workspace.")]
public class InfoOptions : StatusOptions { }
