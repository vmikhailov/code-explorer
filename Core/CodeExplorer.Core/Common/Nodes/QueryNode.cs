using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

public record QueryNode(
    string Id,
    string Name,
    string QueryText,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Query;
}
