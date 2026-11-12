using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.DbSchema,
    idScheme: "{workspaceId}:db:{dbType}:{dbName}:schema:{schemaName}",
    purpose: "Represents a logical or physical schema namespace within a database catalog owning tables, views, and stored procedures (e.g. public, dbo, billing, main).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "symbol-namespace",
    order: 21,
    pluralLabel: "Database Schemas"
)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProcedureNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record DbSchemaNode(
    string Id,
    [property: OntologyProperty("Database schema name (e.g. public, dbo, billing, main).")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("engine"), OntologyProperty("Database engine owning this schema (e.g. PostgreSQL, SqlServer, SQLite).")]
    public string? Engine { get; set; }

    [JsonPropertyName("database_id"), OntologyProperty("URN of the parent Database catalog containing this schema.")]
    public string? DatabaseId { get; set; }

    [JsonPropertyName("is_default"), OntologyProperty("True when synthesized for the engine's default schema (public, dbo, main).")]
    public bool? IsDefault { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.DbSchema;
}
