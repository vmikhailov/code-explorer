using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record TableNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Path,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.Table;
}
