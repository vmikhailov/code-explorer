using System.Text.Json.Serialization;
using CodeExplorer.Common;

namespace CodeExplorer.Core.Common.Nodes;

public abstract record CompositeNode(
    [property: JsonIgnore, OntologyProperty("A unique identifier for the node.")] string Id,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyNode
{
    [JsonIgnore]
    public abstract string Kind { get; }

    [JsonIgnore]
    public abstract string Path { get; init; }

    [JsonIgnore]
    public List<IOntologyNode> Children { get; } = [];

    [JsonIgnore]
    public List<Reference> References { get; } = [];
}
