using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer1_Physical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.FilesStructure,
    idScheme: "{workspaceId}:files_structure",
    purpose: "Represents an intermediate node grouping the physical folder and file tree of the entire workspace.",
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
