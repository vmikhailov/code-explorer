using CommandLine;

namespace CodeExplorer.Options;

[Verb("init", HelpText = "Initialize a .codeexplorer workspace in the current or specified directory.")]
public class InitOptions
{
    [Value(0, MetaName = "name", Required = false, HelpText = "Workspace name (defaults to current folder name).")]
    public string? Name { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Target directory to initialize (defaults to current working directory).")]
    public string? Dir { get; set; }

    [Option('f', "force", Default = false, HelpText = "Reinitialize workspace and overwrite existing database if already present.")]
    public bool Force { get; set; }
}
