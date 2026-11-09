using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Common;

public record Reference
{
    private readonly string _targetName = string.Empty;
    private readonly string? _receiverName;
    private readonly string? _memberName;

    public string ScopeSymbolId { get; init; } = string.Empty;

    public string TargetName
    {
        get => _targetName;
        init
        {
            _targetName = value ?? string.Empty;
            SplitQualifiedTarget(_targetName, out _receiverName, out _memberName);
        }
    }

    public string? ReceiverName
    {
        get => _receiverName;
        init => _receiverName = string.IsNullOrEmpty(value) ? null : value;
    }

    public string MemberName
    {
        get => _memberName ?? _targetName;
        init => _memberName = value;
    }

    [JsonIgnore]
    public bool HasExplicitReceiver => !string.IsNullOrEmpty(_receiverName);

    public RelationshipKind Kind { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public CallSiteExecutionContext Context { get; init; }
    public string? ArgFlow { get; init; }

    [JsonIgnore]
    public RelationshipKind RelationshipKind => Kind;

    [JsonConstructor]
    public Reference(
        string scopeSymbolId,
        string targetName,
        RelationshipKind kind,
        int line = 0,
        int column = 0,
        CallSiteExecutionContext context = default,
        string? argFlow = null,
        string? receiverName = null,
        string? memberName = null)
    {
        ScopeSymbolId = scopeSymbolId;
        TargetName = targetName;
        if (receiverName != null || memberName != null)
        {
            _receiverName = string.IsNullOrEmpty(receiverName) ? null : receiverName;
            _memberName = memberName ?? _targetName;
        }
        Kind = kind;
        Line = line;
        Column = column;
        Context = context;
        ArgFlow = argFlow;
    }

    public Reference(
        string scopeSymbolId,
        string targetName,
        string kind,
        int line = 0,
        int column = 0,
        CallSiteExecutionContext context = default,
        string? argFlow = null,
        string? receiverName = null,
        string? memberName = null)
        : this(scopeSymbolId, targetName, EnumExtensions.ParseRelationshipKind(kind), line, column, context, argFlow, receiverName, memberName)
    {
    }

    private static void SplitQualifiedTarget(string target, out string? receiverName, out string memberName)
    {
        if (string.IsNullOrEmpty(target))
        {
            receiverName = null;
            memberName = string.Empty;
            return;
        }

        var dotIdx = target.LastIndexOf('.');
        if (dotIdx > 0 && dotIdx < target.Length - 1)
        {
            receiverName = target[..dotIdx];
            memberName = target[(dotIdx + 1)..];
        }
        else if (dotIdx == 0 && target.Length > 1)
        {
            receiverName = null;
            memberName = target[1..];
        }
        else
        {
            receiverName = null;
            memberName = target;
        }
    }
}
