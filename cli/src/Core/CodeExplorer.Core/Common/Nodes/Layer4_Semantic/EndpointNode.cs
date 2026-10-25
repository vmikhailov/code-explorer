using System.Text.Json.Serialization;
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
    [property: JsonPropertyName("operation_type"), OntologyProperty("The operation type for GraphQL (Query, Mutation, Subscription) or gRPC (Unary, Streaming).")] string? OperationType = null,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Endpoint;
}
