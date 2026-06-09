using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents an external API library or client service used by the project (e.g. NestJS, Axios, HttpClient).", "`(ApisInUse)-[:USES_API]->(ApiInUse)`", "`(File|Class|Function)-[:USES_API]->(ApiInUse)`")]
public record ApiInUseNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ApiInUse;
}
