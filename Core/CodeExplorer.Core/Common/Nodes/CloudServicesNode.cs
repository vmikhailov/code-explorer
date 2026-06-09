using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents an intermediate node grouping all cloud services used by a project.", "`(Project)-[:CONTAINS]->(CloudServices)`", "`(CloudServices)-[:USES_CLOUD]->(CloudService)`")]
public record CloudServicesNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.CloudServices;
}
