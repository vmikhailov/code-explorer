using CommandLine;

namespace CodeExplorer.Options;

[Verb("taxonomy", HelpText = "Inspect active graph ontology schema, a specific node kind definition (--kind), or workspace health (--status).")]
public class TaxonomyOptions
{
    [Value(0, MetaName = "dir", Required = false, HelpText = "Starting directory to look for workspace (defaults to current directory).")]
    public string? PositionalDir { get; set; }

    [Option('k', "kind", Required = false, HelpText = "Optional ontology node kind name (e.g. 'Project', 'Service', 'Function', 'Endpoint', 'Table') to inspect its formal specification.")]
    public string? Kind { get; set; }

    [Option("status", Required = false, Default = false, HelpText = "Report workspace initialization health, database size, projects, and node counts instead of full relationship triplets.")]
    public bool StatusOnly { get; set; }

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
