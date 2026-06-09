using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Endpoint,
    idScheme: "{workspaceId}:endpoint:{httpMethod}:{routeTemplate}",
    purpose: "Represents an exposed HTTP API endpoint route."
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Triggers)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.ExposedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.ExposedBy)]
public record EndpointNode(
    string Id,
    [property: OntologyProperty("The HTTP endpoint name (e.g. GET /api/orders).")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("http_method"), OntologyProperty("The HTTP Verb (GET, POST, PUT, DELETE).")] string HttpMethod,
    [property: JsonPropertyName("route_template"), OntologyProperty("The declared route template.")] string RouteTemplate,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Endpoint;
}
