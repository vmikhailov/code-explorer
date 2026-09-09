namespace CodeExplorer.Cypher.Ast;

public readonly record struct RangeLength(int? Min, int? Max);

public record NodePattern(
    string? Variable,
    List<string> Labels,
    Dictionary<string, Expression>? Properties
);

public record RelationshipPattern(
    string? Variable,
    List<string> Types,
    RangeLength? Range,
    Direction Direction,
    Dictionary<string, Expression>? Properties
);

public record PathElement(RelationshipPattern Relationship, NodePattern Target);

public record PathPattern(
    NodePattern Head,
    List<PathElement> Chain,
    string? PathVariable = null
);
