using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record QueueNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Type,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.Queue;
}
