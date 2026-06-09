using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.FilesStructure,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:files_structure",
    purpose: "Represents an intermediate node grouping all source code files and folders of a project.",
    layer: OntologyConstants.Layers.Physical
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.Contains)]
public record FilesStructureNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.FilesStructure;
}
