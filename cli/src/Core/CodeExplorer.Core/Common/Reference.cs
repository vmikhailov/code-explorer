using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Common;

public record Reference
{
    public string ScopeSymbolId { get; init; }
    public string TargetName { get; init; }
    public string Kind { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public CallSiteExecutionContext Context { get; init; }
    public string? ArgFlow { get; init; }

    [JsonIgnore]
    public RelationshipKind RelationshipKind => EnumExtensions.ParseRelationshipKind(Kind);

    [JsonConstructor]
    public Reference(
        string scopeSymbolId,
        string targetName,
        string kind,
        int line = 0,
        int column = 0,
        CallSiteExecutionContext context = default,
        string? argFlow = null)
    {
        ScopeSymbolId = scopeSymbolId;
        TargetName = targetName;
        Kind = kind;
        Line = line;
        Column = column;
        Context = context;
        ArgFlow = argFlow;
    }

    public Reference(
        string scopeSymbolId,
        string targetName,
        RelationshipKind kind,
        int line = 0,
        int column = 0,
        CallSiteExecutionContext context = default,
        string? argFlow = null)
        : this(scopeSymbolId, targetName, kind.ToCypherType(), line, column, context, argFlow)
    {
    }
}
