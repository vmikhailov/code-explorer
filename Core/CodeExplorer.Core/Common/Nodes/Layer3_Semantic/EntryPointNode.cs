using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer2_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.EntryPoint,
    idScheme: "{workspaceId}:entrypoint:{type}:{name}",
    purpose: "Represents non-HTTP execution triggers (e.g. gRPC services, CLI command definitions, Cron schedules, queue subscribers)."
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Triggers)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.ExposedBy)]
public record EntryPointNode(
    string Id,
    [property: OntologyProperty("The name of the entry point.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("entry_type"), OntologyProperty("The specifier type (grpc, cli, cron, queue-listener).")] string EntryType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.EntryPoint;
}
