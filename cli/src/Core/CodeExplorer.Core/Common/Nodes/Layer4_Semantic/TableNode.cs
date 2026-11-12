using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Table,
    idScheme: "{workspaceId}:tbl:{tableName}",
    purpose: "Represents a physical database table.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "table",
    order: 22,
    pluralLabel: "Tables & Views"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<QueryNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.TargetedBy)]
public record TableNode(
    string Id,
    [property: OntologyProperty("Physical database table or view name.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("engine"), OntologyProperty("Database engine hosting this table (e.g. postgresql, sqlserver, sqlite).")]
    public string? Engine { get; set; }

    [JsonPropertyName("schema"), OntologyProperty("Database schema qualifying this table (e.g. dbo, public, core).")]
    public string? Schema { get; set; }

    [JsonPropertyName("entity"), OntologyProperty("Mapped ORM entity or domain model type name, if applicable.")]
    public string? EntityName { get; set; }

    [JsonPropertyName("is_implicit_schema"), OntologyProperty("Indicates whether the table node uses an implicit default schema.", isSystem: true)]
    public bool? IsImplicitSchema { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Table;
}
