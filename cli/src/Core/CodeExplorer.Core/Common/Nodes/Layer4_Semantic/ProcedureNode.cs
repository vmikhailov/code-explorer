using CodeExplorer.Cypher.Common;
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
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("engine")]
    public string? Engine { get; set; }

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Procedure;
}
