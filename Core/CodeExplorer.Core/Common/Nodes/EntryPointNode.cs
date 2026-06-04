using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

public record EntryPointNode(
    string Id,
    string Name,
    string Protocol,
    string RouteOrTopic,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.EntryPoint;
}
