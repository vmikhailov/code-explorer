using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Relationships;

public interface IOntologyRelationship
{
    string From { get; }
    string To { get; }
    RelationshipKind Kind { get; }
    RelationshipKind RelationshipKind => Kind;
    Dictionary<string, string>? Extensions { get; }
}
