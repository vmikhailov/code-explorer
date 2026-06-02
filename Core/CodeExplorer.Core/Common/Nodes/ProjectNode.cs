using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record ProjectNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Path,
    string ProjectType,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.Project;
}
