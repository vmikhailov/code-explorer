using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record PackageNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Version,
    string Type,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.Package;
}
