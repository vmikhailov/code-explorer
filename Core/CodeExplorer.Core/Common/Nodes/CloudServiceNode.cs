using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

public record CloudServiceNode(
    string Id,
    string Name,
    string Type,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.CloudService;
}
