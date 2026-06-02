using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record CloudServiceNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Type,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.CloudService;
}
