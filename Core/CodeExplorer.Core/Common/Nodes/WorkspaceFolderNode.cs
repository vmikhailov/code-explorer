using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents a subdirectory inside a Workspace, housing projects or other folders outside projects. Cannot contain files directly (files outside projects are ignored).", "`(Workspace|WorkspaceFolder)-[:CONTAINS]->(WorkspaceFolder)`", "`(WorkspaceFolder)-[:CONTAINS]->(Project)`")]
public record WorkspaceFolderNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.WorkspaceFolder;
}
