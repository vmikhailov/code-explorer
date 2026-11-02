using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public record Node
{
    public string Id { get; init; }
    public string Kind { get; init; }
    public Dictionary<string, object> Properties { get; init; }

    [JsonIgnore]
    public NodeKind NodeKind => EnumExtensions.ParseNodeKind(Kind);

    [JsonConstructor]
    public Node(string id, string kind, Dictionary<string, object> properties)
    {
        Id = id;
        Kind = kind;
        Properties = properties;
    }

    public Node(string id, NodeKind kind, Dictionary<string, object> properties)
        : this(id, kind.ToCypherLabel(), properties)
    {
    }

    public static Node FromNode(IOntologyNode node) => NodeFactory.Create(node);
}
