using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ProjectsStructure,
    idScheme: "{workspaceId}:ps",
    purpose: "Represents an intermediate node grouping all logical projects in the workspace.",
    layer: OntologyConstants.Layers.ProjectBoundary,
    isSystemNode: true
)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Contains)]
public record ProjectsStructureNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonIgnore]
    public override NodeKind Kind => NodeKind.ProjectsStructure;
}
