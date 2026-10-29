using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ApiInUse,
    idScheme: "{workspaceId}:p:{relativeProjectDir}:api:{apiName}",
    purpose: "Represents an external API library or client service used by the project (e.g. NestJS, Axios, HttpClient).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "plug",
    order: 31,
    pluralLabel: "APIs in Use"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.AccessedBy)]
public record ApiInUseNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null,
    [property: JsonPropertyName("capability"), OntologyProperty("The canonical capability identifier (e.g. fs:read, net:http-client, proc:exec).")] string? Capability = null,
    [property: JsonPropertyName("dimension"), OntologyProperty("The capability dimension (e.g. Filesystem, Network, Process, DynamicCode, Environment, Native, Crypto, Concurrency).")] string? Dimension = null,
    [property: JsonPropertyName("semantic_role"), OntologyProperty("The semantic security role of the API call (Sink, SensitiveSource, Transform, MonkeyPatch, Benign).")] string? SemanticRole = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ApiInUse;
}
