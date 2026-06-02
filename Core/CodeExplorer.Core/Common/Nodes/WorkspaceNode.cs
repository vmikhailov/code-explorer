using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record WorkspaceNode(
    [property: JsonIgnore] string Id,
    string Name,
    string Path,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public string Kind => OntologyConstants.NodeLabels.Workspace;
}
