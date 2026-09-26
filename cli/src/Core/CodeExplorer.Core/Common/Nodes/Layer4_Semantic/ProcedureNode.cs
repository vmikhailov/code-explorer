using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Procedure,
    idScheme: "{workspaceId}:proc:{procedureName}",
    purpose: "Represents a stored procedure in a database.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "database",
    order: 23,
    pluralLabel: "Stored Procedures"
)]
[OntologyEdge<QueryNode>(OntologyConstants.Relationships.Contains)]
public record ProcedureNode(
    string Id,
    string Name,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Procedure;
}
