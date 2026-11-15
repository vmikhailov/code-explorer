using CommandLine;

namespace CodeExplorer.Options;

[Verb("snippets", HelpText = "Fetch exact source code line ranges for one or more file:start-end specs or a YAML/JSON nodes list.")]
public class SnippetsOptions
{
    [Value(0, MetaName = "ranges", Required = false, HelpText = "File line-range specs (e.g. 'src/OrderService.cs:10-35' or 'src/Program.cs:1-20').")]
    public IEnumerable<string>? Ranges { get; set; }

    [Option('n', "nodes", Required = false, HelpText = "YAML or JSON list of snippet objects with 'file_path', 'start_line', and 'end_line'.")]
    public string? Nodes { get; set; }

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}

[Verb("snippet", Hidden = true, HelpText = "Alias for 'snippets': fetch source code line ranges.")]
public class SnippetOptions : SnippetsOptions
{
}
