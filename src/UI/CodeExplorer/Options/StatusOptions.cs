using CommandLine;

namespace CodeExplorer.Options;

[Verb("status", HelpText = "Show status and summary of the nearest workspace.")]
public class StatusOptions
{
    [Option('d', "dir", Required = false, HelpText = "Starting directory to look for workspace (defaults to current directory).")]
    public string? Dir { get; set; }

    [Option("json", Default = false, HelpText = "Output status in JSON format.")]
    public bool Json { get; set; }
}

[Verb("info", HelpText = "Alias for 'status': show summary of the nearest workspace.")]
public class InfoOptions : StatusOptions { }
