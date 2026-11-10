using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ExternalService,
    idScheme: "{workspaceId}:es:{protocol}:{host}",
    purpose: "Represents a physical/logical external host dependency.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "cloud",
    order: 12,
    pluralLabel: "External Services"
)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.CallsEndpoint)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.CalledBy)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.TargetedBy)]
public record ExternalServiceNode(
    string Id,
    string Name,
    string Protocol,
    string DomainOrService,
    string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line")]
    public int? StartLine { get; set; }

    [JsonPropertyName("start_col"), OntologyProperty("The starting column number of the call site.", isSystem: true)]
    public int? StartCol { get; set; }

    [JsonPropertyName("caller_symbol_id")]
    public string? CallerSymbolId { get; set; }

    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("config_key")]
    public string? ConfigKey { get; set; }

    [JsonPropertyName("graphql_field")]
    public string? GraphqlField { get; set; }

    [JsonPropertyName("operation_type")]
    public string? OperationType { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("is_external")]
    public bool? IsExternal { get; set; }

    [JsonPropertyName("is_internal")]
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
