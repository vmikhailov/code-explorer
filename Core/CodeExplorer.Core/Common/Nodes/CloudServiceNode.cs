using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents a cloud provider service used by the project (e.g. AWS S3, Stripe, Firebase).", "`(CloudServices)-[:USES_CLOUD]->(CloudService)`", "`(File|Class|Function)-[:USES_CLOUD]->(CloudService)`")]
public record CloudServiceNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The package type or entity type.")] string Type,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.CloudService;
}
