using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Procedure,
    idScheme: "{workspaceId}:procedure:{procedureName}",
    purpose: "Represents a stored procedure in a database."
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
