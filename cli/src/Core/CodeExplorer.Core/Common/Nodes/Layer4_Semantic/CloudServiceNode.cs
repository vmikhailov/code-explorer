using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.CloudService,
    idScheme: "{workspaceId}:p:{relativeProjectDir}:cloud:{serviceName}",
    purpose: "Represents a cloud provider service used by the project (e.g. AWS S3, Stripe, Firebase).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "cloud-upload",
    order: 13,
    pluralLabel: "Cloud Resources"
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
