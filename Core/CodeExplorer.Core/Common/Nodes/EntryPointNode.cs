using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents an exposed API route, message listener, or application entry point.", "`(EntryPoints)-[:EXPOSES]->(EntryPoint)`", "`(EntryPoint)-[:IMPLEMENTED_BY]->(Function)`")]
public record EntryPointNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The communication protocol (e.g. 'http', 'ws', 'event').")] string Protocol,
    [property: OntologyProperty("The routing path or message topic.")] string RouteOrTopic,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.EntryPoint;
}
