using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record ProjectNode(
    string Id,
    string Name,
    string Path,
    string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Project;
}
