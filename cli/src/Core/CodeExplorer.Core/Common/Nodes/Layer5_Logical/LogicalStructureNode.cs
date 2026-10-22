using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer5_Logical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.LogicalStructure,
    idScheme: "{workspaceId}:ls",
    purpose: "Represents an intermediate root node grouping all high-level business domains, bounded contexts, and logical architectures in the workspace.",
    layer: OntologyConstants.Layers.Logical,
    isSystemNode: true
)]
[OntologyEdge<DomainNode>(OntologyConstants.Relationships.Contains)]
public record LogicalStructureNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the workspace root.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.LogicalStructure;
}
