using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

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
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.TransitivelyCalls)]
public record CloudServiceNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The cloud service resource type (e.g. S3Bucket, DynamoDb, CloudService).")] CloudResourceType ResourceType,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public CloudServiceNode(string Id, string Name, string ResourceType, string Path, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, TaxonomyEnumExtensions.ParseCloudResourceType(ResourceType), Path, CustomProperties)
    {
    }

    [JsonPropertyName("provider"), OntologyProperty("Cloud or SaaS vendor (aws, gcp, azure, cloudflare, stripe, firebase, supabase).")]
    public string? Provider { get; set; }

    [JsonPropertyName("resource_role"), OntologyProperty("Operational role of the cloud resource (storage, secrets, auth, compute, cdn, ai).")]
    public string? ResourceRole { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.CloudService;
}
