using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

public record DataSetNode(
    string Id,
    string Name,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.DataSet;
}
