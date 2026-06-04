using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

public record GitSettingsNode(
    string Id,
    string Name,
    string Branch,
    string OriginUrl,
    string UserName,
    string UserEmail,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.GitSettings;
}
