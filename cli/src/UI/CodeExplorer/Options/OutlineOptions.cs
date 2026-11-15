using CommandLine;

namespace CodeExplorer.Options;

[Verb("outline", HelpText = "Extract the structural AST outline of a file or directory without reading raw source text.")]
public class OutlineOptions
{
    [Value(0, MetaName = "path", Required = false, Default = ".", HelpText = "File or directory path to outline (workspace-relative, absolute, or '.' for workspace root).")]
    public string? PositionalPath { get; set; } = ".";

    [Option('p', "path", Required = false, HelpText = "File or directory path to outline (alternative to positional argument).")]
    public string? PathOption { get; set; }

    public string EffectivePath => !string.IsNullOrWhiteSpace(PathOption)
        ? PathOption
        : (!string.IsNullOrWhiteSpace(PositionalPath) ? PositionalPath : ".");

    [Option('d', "dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }
}
