using CommandLine;

namespace CodeExplorer.Options;

[Verb("slice", HelpText = "Extract a task-focused repository/project subgraph around 1-3 seeds (repo, service, endpoint, or table) using Personalized PageRank (PPR).")]
public class SliceOptions
{
    [Value(0, MetaName = "seeds", Required = true, HelpText = "One or more seed names (repository, project, service, /endpoint/route, or table name).")]
    public IEnumerable<string> Seeds { get; set; } = [];

    [Option('d', "dir", Required = false, HelpText = "Workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }

    [Option('k', "max-repos", Required = false, Default = 10, HelpText = "Maximum repositories/root units to include in the slice (default 10).")]
    public int MaxRepos { get; set; } = 10;

    [Option("alpha", Required = false, Default = 0.85, HelpText = "Personalized PageRank damping factor (0.1 to 0.99, default 0.85).")]
    public double Alpha { get; set; } = 0.85;

    [Option("write-workspace", Required = false, HelpText = "Optional path to generate a VS Code multi-root .code-workspace file for the sliced units.")]
    public string? WriteWorkspacePath { get; set; }

    [Option('f', "format", Required = false, Default = "text", HelpText = "Output format: text, json, yaml.")]
    public string Format { get; set; } = "text";
}
