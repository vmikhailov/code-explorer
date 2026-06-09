using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer2_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Table,
    idScheme: "{workspaceId}:table:{tableName}",
    purpose: "Represents a physical database table."
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.QueriedBy)]
[OntologyEdge<QueryNode>(OntologyConstants.Relationships.QueriedBy)]
public record TableNode(
    string Id,
    string Name,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Table;
}
