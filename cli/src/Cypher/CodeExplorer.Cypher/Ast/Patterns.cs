using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Cypher.Ast;

public readonly record struct RangeLength(int? Min, int? Max);

public record NodePattern(
    string? Variable,
    List<string> Labels,
    Dictionary<string, Expression>? Properties
)
{
    public IReadOnlyList<NodeKind> NodeKinds =>
        Labels.Select(EnumExtensions.ParseNodeKind).ToList();
}

public record RelationshipPattern(
    string? Variable,
    List<string> Types,
    RangeLength? Range,
    Direction Direction,
    Dictionary<string, Expression>? Properties
)
{
    public IReadOnlyList<RelationshipKind> RelationshipKinds =>
        Types.Select(EnumExtensions.ParseRelationshipKind).ToList();
}

public record PathElement(RelationshipPattern Relationship, NodePattern Target);

public record PathPattern(
    NodePattern Head,
    List<PathElement> Chain,
    string? PathVariable = null,
    bool IsShortestPath = false,
    bool IsAllShortestPaths = false
);
