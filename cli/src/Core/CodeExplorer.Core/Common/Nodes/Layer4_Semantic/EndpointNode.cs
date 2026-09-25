using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Endpoint,
    idScheme: "{workspaceId}:endpoint:{httpMethod}:{routeTemplate}",
    purpose: "Represents an exposed HTTP API endpoint route.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "radio-tower",
    order: 6,
    pluralLabel: "API Endpoints (REST, gRPC, WS)"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Triggers)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.ExposedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.ExposedBy)]
public record EndpointNode(
    string Id,
    [property: OntologyProperty("The HTTP endpoint name (e.g. GET /api/orders).")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("http_method"), OntologyProperty("The HTTP Verb (GET, POST, PUT, DELETE, ALL).")] string HttpMethod,
    [property: JsonPropertyName("route_template"), OntologyProperty("The declared route template.")] string RouteTemplate,
    [property: JsonPropertyName("protocol"), OntologyProperty("The API protocol (REST, gRPC, GraphQL).")] string Protocol = "REST",
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
