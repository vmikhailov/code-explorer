using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

public record QueueNode(
    string Id,
    string Name,
    string Type,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Queue;
}
