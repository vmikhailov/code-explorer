namespace CodeExplorer.Core.Semantics;

/// <summary>
/// Compact in-memory summary of a declared function parameter captured during Layer 3 AST traversal
/// and enriched during Layer 5 interprocedural coloring.
/// </summary>
public sealed class SemanticParamSignal
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public string? DataType { get; init; }
    public required int Line { get; init; }
    public ParameterNature Nature { get; set; }
    public string? SinkCallee { get; set; }
}

/// <summary>
/// Compact in-memory summary of a call-site argument (or options-object property) captured during Layer 3 AST slicing
/// before AST syntax trees leave memory.
/// </summary>
public sealed class SemanticArgumentSlice
{
    public required string Callee { get; init; }
    public required string MethodName { get; init; }
    public required int CallLine { get; init; }
    public required int CallColumn { get; init; }
    public required int ArgIndex { get; init; }
    public string? PropertyKey { get; init; }
    public required string SlotKey { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required string RawExpression { get; init; }
    public string? ResolvedValue { get; set; }
    public string? CompositeTemplate { get; init; }
    public ParameterNature Nature { get; set; }
    public ArgumentProvenance Provenance { get; init; }
    public ExpressionRole Role { get; init; }
    public int? ReferencedParamIndex { get; init; }
    public string? OriginFile { get; init; }
    public int? OriginLine { get; init; }
    public string? OriginSymbol { get; init; }
}
