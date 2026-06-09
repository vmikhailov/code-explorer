using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.DataSet,
    idScheme: "{workspaceId}:dataset:{datasetName}",
    purpose: "Represents a collection of data tables or datasets."
)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.Contains)]
public record DataSetNode(
    string Id,
    string Name,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.DataSet;
}
