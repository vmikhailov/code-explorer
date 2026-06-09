using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer1_Physical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Folder,
    idScheme: "{workspaceId}:folder:{relativeDirectoryPath}",
    purpose: "Represents a directory within the indexed workspace.",
    layer: OntologyConstants.Layers.Physical
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<GitSettingsNode>(OntologyConstants.Relationships.Contains)]
public record FolderNode(
    string Id,
    [property: OntologyProperty("The name of the folder.")] string Name,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Folder;

    [JsonIgnore]
    [OntologyProperty("The path to the folder.")]
    public override string Path { get; init; } = Path;
}
