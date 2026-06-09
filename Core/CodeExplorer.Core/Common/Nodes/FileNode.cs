using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents a source code file containing parsable content.", "`(Files|ProjectFolder)-[:CONTAINS]->(File)`", "`(File)-[:CONTAINS]->(Class)`", "`(File)-[:CONTAINS]->(Interface)`", "`(File)-[:CONTAINS]->(Function)`")]
public record FileNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    string FullPath,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.File;
}
