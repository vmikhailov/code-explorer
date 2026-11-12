using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Cloud,
    idScheme: "{workspaceId}:cloud:{provider}",
    purpose: "Represents a cloud platform, managed infrastructure provider, or SaaS vendor hosting external services, databases, brokers, or workloads (e.g. AWS, GCP, Azure, Cloudflare, Stripe, Supabase).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "cloud",
    order: 13,
    pluralLabel: "Cloud Platforms & Vendors"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record CloudNode(
    string Id,
    [property: OntologyProperty("Cloud platform or vendor display name (e.g. AWS, GCP, Azure, Cloudflare, Stripe).")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("provider"), OntologyProperty("Normalized cloud or vendor identifier (aws, gcp, azure, cloudflare, stripe, auth0, supabase, firebase).")]
    public string? Provider { get; set; }

    [JsonPropertyName("region"), OntologyProperty("Configured cloud region when declared (e.g. us-east-1, europe-west1).")]
    public string? Region { get; set; }

    [JsonPropertyName("category"), OntologyProperty("Vendor category (hyperscaler, saas, edge, managed_data).")]
    public string? Category { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Cloud;
}
