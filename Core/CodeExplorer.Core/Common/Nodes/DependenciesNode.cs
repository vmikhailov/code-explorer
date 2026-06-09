using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents an intermediate node grouping external packages / third-party dependencies of a project.", "`(Project)-[:CONTAINS]->(Dependencies)`", "`(Dependencies)-[:DEPENDS_ON]->(Package)`")]
public record DependenciesNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Dependencies;
}
