using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.SemanticStructure,
    idScheme: "{workspaceId}:semantic_structure",
    purpose: "Represents an intermediate node grouping all runtime entry points, databases, endpoints, cloud services, and APIs used in the entire workspace.",
    layer: OntologyConstants.Layers.Semantic
)]
[OntologyEdge<ProjectSemanticNode>(OntologyConstants.Relationships.Contains)]
public record SemanticStructureNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.SemanticStructure;
}
