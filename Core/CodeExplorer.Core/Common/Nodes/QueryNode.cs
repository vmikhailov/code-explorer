using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record QueryNode(
    [property: JsonIgnore] string Id,
    string Name,
    string QueryText,
    string Path,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.Query;
}
