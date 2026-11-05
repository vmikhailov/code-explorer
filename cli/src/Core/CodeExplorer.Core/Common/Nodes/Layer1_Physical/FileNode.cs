using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer1_Physical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.File,
    idScheme: "{workspaceId}:f:{relativeFilePath}",
    purpose: "Represents a source code file containing parsable content.",
    layer: OntologyConstants.Layers.Physical,
    icon: "file-code",
    order: 1,
    pluralLabel: "Files"
)]
[OntologyEdge<ScanIssueNode>(OntologyConstants.Relationships.Contains)]
public record FileNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    string FullPath,
    bool IsTest,
    ProjectEntityKind? ProjectKind = null,
    string? ProjectName = null,
    int? LineCount = null,
    long? SizeBytes = null,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    public FileNode(string id, string name, string path, string fullPath, Dictionary<string, string>? extensions = null)
        : this(id, name, path, fullPath, false, null, null, null, null, extensions)
    {
    }

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the file is part of a test suite or test project.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public ProjectEntityKind? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonPropertyName("line_count"), OntologyProperty("Total lines of code in this file.")]
    public int? LineCount { get; set; } = LineCount;

    [JsonPropertyName("size_bytes"), OntologyProperty("Size of the file in bytes.")]
    public long? SizeBytes { get; set; } = SizeBytes;

    [JsonPropertyName("artifact_role"), OntologyProperty("The physical artifact role of the file (source, dist, minified, declaration, script).")]
    public string? ArtifactRole { get; set; }

    [JsonPropertyName("has_parse_issues"), OntologyProperty("Indicates whether syntax diagnostics were recorded while parsing this file.")]
    public bool? HasParseIssues { get; set; }

    [JsonPropertyName("parse_issue_count"), OntologyProperty("Number of syntax diagnostics recorded for this file.")]
    public int? ParseIssueCount { get; set; }

    [JsonPropertyName("has_entity_or_table"), OntologyProperty("Indicates whether the file declares ORM entities, tables, or DbContext mappings.")]
    public bool? HasEntityOrTable { get; set; }

    [JsonPropertyName("dbcontext_base_conn"), OntologyProperty("Connection string name passed to DbContext base constructor.")]
    public string? DbContextBaseConn { get; set; }

    [JsonPropertyName("dbcontext_classes"), OntologyProperty("Comma-separated DbContext class names declared in this file.")]
    public string? DbContextClasses { get; set; }

    [JsonPropertyName("declared_schema_raw"), OntologyProperty("Database schema name explicitly declared in this file.")]
    public string? DeclaredSchemaRaw { get; set; }

    [JsonPropertyName("verified_db_signals"), OntologyProperty("Comma-separated database driver signals detected in this file.")]
    public string? VerifiedDbSignals { get; set; }

    [JsonPropertyName("rust_topology_summary"), OntologyProperty("Pre-computed Rust cloud/peer topology tokens.")]
    public string? RustTopologySummary { get; set; }

    [JsonPropertyName("rust_topology_body_summary"), OntologyProperty("Pre-computed Rust body-level cloud/peer topology tokens.")]
    public string? RustTopologyBodySummary { get; set; }

    [JsonIgnore]
    public string? ContentHash { get; set; }

    [JsonIgnore]
    public DateTime? LastModifiedUtc { get; set; }

    [JsonIgnore]
    public string? PreloadedContent { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.File;
}
