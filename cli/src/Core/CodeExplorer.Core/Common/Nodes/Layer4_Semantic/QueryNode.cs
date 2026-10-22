using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Query,
    idScheme: "{workspaceId}:q:{queryHash}",
    purpose: "Represents a SQL query.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "search",
    order: 30,
    pluralLabel: "SQL Queries"
)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.ReadsFrom)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.WritesTo)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.AccessedBy)]
public record QueryNode(
    string Id,
    string Name,
    string QueryText,
    string Path,
    int StartLine = 0,
    int EndLine = 0,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Query;
}
