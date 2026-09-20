using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ProjectsStructure,
    idScheme: "{workspaceId}:projects_structure",
    purpose: "Represents an intermediate node grouping all logical projects in the workspace.",
    layer: OntologyConstants.Layers.ProjectBoundary
)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.Contains)]
public record ProjectsStructureNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ProjectsStructure;
}
