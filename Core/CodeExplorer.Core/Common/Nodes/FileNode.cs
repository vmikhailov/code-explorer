using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record FileNode(
    string Id,
    string Name,
    string Path,
    string FullPath,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.File;
}
