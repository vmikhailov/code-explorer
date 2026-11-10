using CommandLine;

namespace CodeExplorer.Options;

[Verb("taxonomy", HelpText = "Inspect active graph schema (actual node labels, properties, and relationships in the database).")]
public class TaxonomyOptions
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

[Verb("schema", Hidden = true, HelpText = "Alias for 'taxonomy': inspect active graph schema.")]
public class SchemaOptions : TaxonomyOptions { }
