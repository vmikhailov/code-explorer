using System.Text.Json.Serialization;
using CodeExplorer.Common;

namespace CodeExplorer.Core.Common.Nodes;

public abstract record CompositeNode(
    [property: JsonIgnore] string Id,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public abstract string Kind { get; }

    [JsonIgnore]
    public List<IOntologyNode> Children { get; } = [];

    [JsonIgnore]
    public List<Reference> References { get; } = [];
}
