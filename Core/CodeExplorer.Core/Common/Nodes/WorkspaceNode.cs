using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Workspace,
    idScheme: "{workspaceId}",
    purpose: "Represents the absolute root of the workspace directory hierarchy.",
    layer: OntologyConstants.Layers.Workspace
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<GitSettingsNode>(OntologyConstants.Relationships.Contains)]
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
