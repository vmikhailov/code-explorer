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
public record TableNode(
    string Id,
    string Name,
    string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("engine")]
    public string? Engine { get; set; }

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("entity")]
    public string? EntityName { get; set; }

    [JsonPropertyName("is_implicit_schema"), OntologyProperty("Indicates whether the table node uses an implicit default schema.", isSystem: true)]
    public bool? IsImplicitSchema { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Table;
}
