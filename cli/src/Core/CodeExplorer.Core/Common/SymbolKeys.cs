using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Compact 9-byte struct key (1-byte <see cref="NodeKind"/> enum + 8-byte string reference)
/// for zero-allocation hashing and dictionary indexing of symbols across analysis layers.
/// </summary>
public readonly record struct SymbolKey(NodeKind Kind, string Name)
{
    public SymbolKey(string kind, string name)
        : this(EnumExtensions.ParseNodeKind(kind), name)
    {
    }

    public override string ToString() => $"{Kind}:{Name}";
}

/// <summary>
/// Compact 17-byte struct key for zero-allocation hashing and deduplication of graph relationships.
/// </summary>
public readonly record struct RelationshipKey(string From, string To, RelationshipKind Kind)
{
    public RelationshipKey(string from, string to, string kind)
        : this(from, to, EnumExtensions.ParseRelationshipKind(kind))
    {
    }

    public override string ToString() => $"{From} -[{Kind}]-> {To}";
}
