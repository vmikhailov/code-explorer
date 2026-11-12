using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Endpoint,
    idScheme: "{workspaceId}:ep:{httpMethod}:{routeTemplate}",
    purpose: "Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "radio-tower",
    order: 10,
    pluralLabel: "API & Message Endpoints (REST, gRPC, WS, MSG)"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Triggers)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.ExposedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.ExposedBy)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.SubscribesTo)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.TargetedBy)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Calls)]
[OntologyEdge<CallLiteralNode>(OntologyConstants.Relationships.PassesLiteral)]
[OntologyEdge<CatchClauseNode>(OntologyConstants.Relationships.Declares)]
[OntologyEdge<QueryNode>(OntologyConstants.Relationships.AttributedTo)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.AttributedTo)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.AttributedTo)]
[OntologyEdge<CloudServiceNode>(OntologyConstants.Relationships.AttributedTo)]
public record EndpointNode(
    string Id,
    [property: OntologyProperty("The endpoint name (e.g. GET /api/orders, MSG:SubmitOrder).")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("http_method"), OntologyProperty("The HTTP Verb or contract verb (GET, POST, PUT, DELETE, ALL, MSG, WS, RPC).")] string HttpMethod,
    [property: JsonPropertyName("route_template"), OntologyProperty("The declared route template or message contract name.")] string RouteTemplate,
    [property: JsonPropertyName("protocol"), OntologyProperty("The contract protocol (REST, gRPC, GraphQL, WS, WCF, MSG, RPC).")] string Protocol = "REST",
    [property: JsonPropertyName("is_anonymous"), OntologyProperty("Whether endpoint allows unauthenticated anonymous access.")] bool IsAnonymous = false,
    [property: JsonPropertyName("required_roles"), OntologyProperty("Comma-separated required roles or permissions.")] string? RequiredRoles = null,
    [property: JsonPropertyName("policies"), OntologyProperty("Authorization policies guarding the endpoint.")] string? Policies = null,
    [property: JsonPropertyName("request_type"), OntologyProperty("The request payload type (e.g. StartRideRequest, CreateOrderDto).")] string? RequestType = null,
    [property: JsonPropertyName("response_type"), OntologyProperty("The response payload type (e.g. RideDto, OrderResponse).")] string? ResponseType = null,
    [property: JsonPropertyName("operation_type"), OntologyProperty("The operation type for GraphQL (Query, Mutation, Subscription) or gRPC (Unary, Streaming).")] OperationType? OperationType = null,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public EndpointNode(
        string Id,
        string Name,
        string Path,
        string HttpMethod,
        string RouteTemplate,
        string Protocol,
        bool IsAnonymous,
        string? RequiredRoles,
        string? Policies,
        string? RequestType,
        string? ResponseType,
        string? OperationType,
        Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, HttpMethod, RouteTemplate, Protocol, IsAnonymous, RequiredRoles, Policies, RequestType, ResponseType, TaxonomyEnumExtendedExtensions.ParseNullableOperationType(OperationType), CustomProperties)
    {
    }

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file declaring the endpoint.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line"), OntologyProperty("1-indexed starting line number of the endpoint declaration.")]
    public int? StartLine { get; set; }

    [JsonPropertyName("end_line"), OntologyProperty("The ending line number of the endpoint declaration.", isSystem: true)]
    public int? EndLine { get; set; }

    [JsonPropertyName("line"), OntologyProperty("1-indexed source line number of the route or contract mapping.")]
    public int? Line { get; set; }

    [JsonPropertyName("line_end"), OntologyProperty("The ending line number of the endpoint declaration.", isSystem: true)]
    public int? LineEnd { get; set; }

    [JsonPropertyName("operation"), OntologyProperty("Operation or handler action name associated with this endpoint.")]
    public string? Operation { get; set; }

    [JsonPropertyName("evidence_kind"), OntologyProperty("Extraction provenance kind (endpoint, grpc, graphql, ws, consumer).")]
    public string? EvidenceKind { get; set; }

    [JsonPropertyName("graphql_field"), OntologyProperty("Exposed GraphQL root field name when protocol is GraphQL.")]
    public string? GraphqlField { get; set; }

    [JsonPropertyName("graphql_sdl"), OntologyProperty("Indicates whether the endpoint was extracted from a GraphQL SDL schema.")]
    public bool? IsGraphqlSdl { get; set; }

    [JsonPropertyName("status_code"), OntologyProperty("Declared HTTP response status codes (e.g. 200, 201, 404).")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("summary"), OntologyProperty("OpenAPI / Swagger or docstring endpoint summary.")]
    public string? Summary { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Endpoint;
}
