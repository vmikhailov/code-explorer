using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record FileNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Path,
    string FullPath,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.File;
}
