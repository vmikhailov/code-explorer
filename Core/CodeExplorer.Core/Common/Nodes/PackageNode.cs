using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents an external dependency package or workspace package referenced or produced by projects.", "`(Dependencies)-[:DEPENDS_ON]->(Package)` (for external dependencies)", "`(Project)-[:DEPENDS_ON]->(Package)` (for produced packages)", "`(Package)-[:IMPLEMENTED_BY]->(Project)`")]
public record PackageNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The package version.")] string Version,
    [property: OntologyProperty("The package type or entity type.")] string Type,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Package;
}
