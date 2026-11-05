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
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonPropertyName("is_implicit_schema"), OntologyProperty("Indicates whether the dataset node uses an implicit default schema.")]
    public bool? IsImplicitSchema { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.DataSet;
}
