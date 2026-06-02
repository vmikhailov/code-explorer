using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record WorkspaceFolderNode(
    string Id,
    string Name,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.WorkspaceFolder;
}
