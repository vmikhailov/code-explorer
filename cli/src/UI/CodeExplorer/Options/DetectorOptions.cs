using CommandLine;

namespace CodeExplorer.Options;

[Verb("detector", aliases: ["detectors"], HelpText = "List, validate, and inspect schema for declarative Layer-4 framework/library detectors (.codeexplorer/detectors/*.yaml).")]
public class DetectorOptions : ITargetOption
{
    [Value(0, MetaName = "action", Required = false, Default = "list", HelpText = "Action: 'list' (default), 'validate', or 'schema'.")]
    public string Action { get; set; } = "list";

    [Option("custom", Required = false, Default = false, HelpText = "When used with 'list', show only custom workspace and global YAML detectors.")]
    public bool CustomOnly { get; set; }

    [Option('l', "language", Required = false, HelpText = "Filter listed detectors by language (e.g. java, csharp, go, python, typescript).")]
    public string? Language { get; set; }

    [Option("strict", Required = false, Default = false, HelpText = "When used with 'validate', treat warnings (such as shadowing imperative AST rules) as errors.")]
    public bool Strict { get; set; }

    [Option("template", Required = false, Default = false, HelpText = "When used with 'schema', output a ready-to-edit starter YAML detector template instead of JSON Schema.")]
    public bool Template { get; set; }

    [Option('f', "format", Required = false, Default = "table", HelpText = "Output format for list/validate: 'table' (or 'text'), 'json', 'yaml'.")]
    public string Format { get; set; } = "table";

    [Option('o', "output", Required = false, HelpText = "Write output to the specified file path instead of stdout.")]
    public string? OutputPath { get; set; }

    [Option("dir", Required = false, HelpText = "Path to workspace directory (defaults to current directory).")]
    public string? Dir { get; set; }

    string ITargetOption.Target => Action;
}
