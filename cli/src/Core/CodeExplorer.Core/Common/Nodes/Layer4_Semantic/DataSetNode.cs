using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.DataSet,
    idScheme: "{workspaceId}:ds:{datasetName}",
    purpose: "Represents a collection of data tables or datasets.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "database",
    order: 21,
    pluralLabel: "DataSets & Schemas"
)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.Contains)]
public record DataSetNode(
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

    [JsonPropertyName("is_implicit_schema"), OntologyProperty("Indicates whether the dataset node uses an implicit default schema.", isSystem: true)]
    public bool? IsImplicitSchema { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.DataSet;
}
