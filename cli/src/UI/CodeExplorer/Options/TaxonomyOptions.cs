using CommandLine;

namespace CodeExplorer.Options;

[Verb("taxonomy", HelpText = "Inspect active graph schema (actual node labels, properties, and relationships in the database).")]
public class TaxonomyOptions
{
    [Option('d', "dir", Required = false, HelpText = "Starting directory to look for workspace (defaults to current directory).")]
    public string? Dir { get; set; }
}

[Verb("schema", Hidden = true, HelpText = "Alias for 'taxonomy': inspect active graph schema.")]
public class SchemaOptions : TaxonomyOptions { }
