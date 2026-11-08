using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public record Node
{
    private Dictionary<string, object>? _properties;

    public string Id { get; init; }
    public NodeKind Kind { get; init; }

    [JsonIgnore]
    public IOntologyNode? OntologyNode { get; init; }

    [JsonIgnore]
    public bool HasMaterializedProperties => _properties != null;

    public Dictionary<string, object> Properties
    {
        get => _properties ??= OntologyNode != null
            ? NodeFactory.MaterializeProperties(OntologyNode)
            : new Dictionary<string, object>(StringComparer.Ordinal);
        init => _properties = value;
    }

    [JsonIgnore]
    public NodeKind NodeKind => OntologyNode?.Kind ?? Kind;

    [JsonConstructor]
    public Node(string id, NodeKind kind, Dictionary<string, object> properties)
    {
        Id = id;
        Kind = kind;
        _properties = properties;
    }

    public Node(string id, string kind, Dictionary<string, object> properties)
        : this(id, EnumExtensions.ParseNodeKind(kind), properties)
    {
    }

    public Node(IOntologyNode ontologyNode)
    {
        ArgumentNullException.ThrowIfNull(ontologyNode);
        Id = ontologyNode.Id;
        Kind = ontologyNode.Kind;
        OntologyNode = ontologyNode;
    }

    public string GetName() =>
        OntologyNode != null
            ? OntologyNode.GetName()
            : (_properties != null && _properties.TryGetValue(OntologyConstants.PropertyNames.Name, out var raw) && raw is string s ? s : string.Empty);

    public static Node FromNode(IOntologyNode node) => NodeFactory.Create(node);
}
