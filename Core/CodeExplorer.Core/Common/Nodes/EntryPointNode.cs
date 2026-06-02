using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record EntryPointNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Protocol,
    string RouteOrTopic,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.EntryPoint;
}
