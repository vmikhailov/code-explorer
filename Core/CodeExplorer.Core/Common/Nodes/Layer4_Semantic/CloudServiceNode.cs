using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.CloudService,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:cloudservice:{serviceName}",
    purpose: "Represents a cloud provider service used by the project (e.g. AWS S3, Stripe, Firebase)."
)]
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
