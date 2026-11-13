using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.CloudResource,
    idScheme: "{workspaceId}:p:{relativeProjectDir}:cr:{resourceAddress}",
    purpose: "Represents a provisioned or referenced cloud infrastructure resource (e.g. RDS instance, SQS queue, S3 bucket, VPC, IAM role, KMS key, Kubernetes workload) hosted in a Cloud platform.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "cloud-upload",
    order: 14,
    pluralLabel: "Cloud Resources"
)]
[OntologyEdge<CloudNode>(OntologyConstants.Relationships.HostedIn)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<CloudResourceNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<MemberNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.Deploys)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.Deploys)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.Deploys)]
[OntologyEdge<WorkloadNode>(OntologyConstants.Relationships.Deploys)]
public record CloudResourceNode(
    string Id,
    [property: OntologyProperty("The canonical resource address or name (e.g. aws_s3_bucket.assets).")] string Name,
    [property: JsonPropertyName("resource_type"), OntologyProperty("The provider resource type identifier (e.g. aws_s3_bucket, aws_db_instance, google_sql_database_instance).")] string ResourceType,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("provider"), OntologyProperty("Cloud or infrastructure provider (e.g. aws, gcp, azure, kubernetes, digitalocean, cloudflare).")]
    public string? Provider { get; init; }

    [JsonPropertyName("resource_role"), OntologyProperty("Operational role of the cloud resource (database, messaging, storage, compute, network, security, secrets, cdn, dns, container, monitoring).")]
    public string? ResourceRole { get; init; }

    [JsonPropertyName("mode"), OntologyProperty("Declaration mode of the resource (managed or data).")]
    public string? Mode { get; init; }

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file declaring the cloud resource.")]
    public string? FilePath { get; init; }

    [JsonPropertyName("line"), OntologyProperty("1-indexed starting line number of the resource declaration.")]
    public int? Line { get; init; }

    [JsonPropertyName("line_end"), OntologyProperty("1-indexed ending line number of the resource declaration.", isSystem: true)]
    public int? LineEnd { get; init; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.CloudResource;
}
