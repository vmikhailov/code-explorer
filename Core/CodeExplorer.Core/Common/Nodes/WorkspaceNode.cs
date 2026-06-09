using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Workspace,
    idScheme: "{workspaceId}",
    purpose: "Represents the absolute root of the workspace directory hierarchy.",
    layer: OntologyConstants.Layers.Workspace
)]
[OntologyEdge<FilesStructureNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProjectsStructureNode>(OntologyConstants.Relationships.Contains)]
public record WorkspaceNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Workspace;
}
