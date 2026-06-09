using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents a subdirectory inside a Project, containing files and other project folders.", "`(Files|ProjectFolder)-[:CONTAINS]->(ProjectFolder)`", "`(ProjectFolder)-[:CONTAINS]->(File)`")]
public record ProjectFolderNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ProjectFolder;
}
