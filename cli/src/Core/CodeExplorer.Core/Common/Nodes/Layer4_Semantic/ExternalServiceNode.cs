using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ExternalService,
    idScheme: "{workspaceId}:es:{protocol}:{host}",
    purpose: "Represents an external API, managed cloud resource, or remote service dependency outside the workspace (REST, gRPC, GraphQL, S3, Stripe, Auth0).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "globe",
    order: 12,
    pluralLabel: "External Services & Resources"
)]
[OntologyEdge<CloudNode>(OntologyConstants.Relationships.HostedIn)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.CallsEndpoint)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.CalledBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.TransitivelyCalls)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.TargetedBy)]
public record ExternalServiceNode(
    string Id,
    [property: OntologyProperty("Logical host or external client dependency name.")] string Name,
    [property: JsonPropertyName("protocol"), OntologyProperty("Outbound transport protocol (HTTP, HTTPS, gRPC, GraphQL, WS, WCF).")] string Protocol,
    [property: JsonPropertyName("domain_or_service"), OntologyProperty("Target domain hostname or logical service identifier.")] string DomainOrService,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file issuing the outbound call.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line"), OntologyProperty("1-indexed line number of the outbound client call.")]
    public int? StartLine { get; set; }

    [JsonPropertyName("start_col"), OntologyProperty("The starting column number of the call site.", isSystem: true)]
    public int? StartCol { get; set; }

    [JsonPropertyName("caller_symbol_id"), OntologyProperty("Symbol ID of the calling method or function.", isSystem: true)]
    public string? CallerSymbolId { get; set; }

    [JsonPropertyName("base_url"), OntologyProperty("Configured base URL or service address.")]
    public string? BaseUrl { get; set; }

    [JsonPropertyName("url"), OntologyProperty("Resolved outbound request URL or path template.")]
    public string? Url { get; set; }

    [JsonPropertyName("config_key"), OntologyProperty("Configuration setting or environment variable supplying the host URL.")]
    public string? ConfigKey { get; set; }

    [JsonPropertyName("graphql_field"), OntologyProperty("Queried GraphQL field name on the remote service.")]
    public string? GraphqlField { get; set; }

    [JsonPropertyName("operation_type"), OntologyProperty("Remote operation kind (Query, Mutation, Unary, Streaming).")]
    public string? OperationType { get; set; }

    [JsonPropertyName("method"), OntologyProperty("Outbound HTTP verb or RPC method name.")]
    public string? Method { get; set; }

    [JsonPropertyName("provider"), OntologyProperty("Cloud platform or SaaS vendor hosting this external service (e.g. AWS, GCP, Azure, Stripe, Auth0).")]
    public string? Provider { get; set; }

    [JsonPropertyName("resource_role"), OntologyProperty("Operational role when representing a managed cloud resource (storage, secrets, auth, compute, cdn, ai).")]
    public string? ResourceRole { get; set; }

    [JsonPropertyName("is_external"), OntologyProperty("True when the target resolves outside workspace services.")]
    public bool? IsExternal { get; set; }

    [JsonPropertyName("is_internal"), OntologyProperty("True when matched to an internal workspace service.", isSystem: true)]
    public bool? IsInternal { get; set; }

    [JsonPropertyName("is_relative_path"), OntologyProperty("Indicates whether the target URL is a relative path.", isSystem: true)]
    public bool? IsRelativePath { get; set; }

    [JsonPropertyName("inferred_from_path"), OntologyProperty("Indicates whether the service was inferred from an HTTP path.", isSystem: true)]
    public bool? InferredFromPath { get; set; }

    [JsonPropertyName("inferred_from_identifier"), OntologyProperty("Indicates whether the service was inferred from an identifier.", isSystem: true)]
    public bool? InferredFromIdentifier { get; set; }

    [JsonIgnore]
    public CallSiteExecutionContext CallContext { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.ExternalService;
}
