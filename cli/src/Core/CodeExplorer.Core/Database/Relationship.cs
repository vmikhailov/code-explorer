using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public record Relationship
{
    private const string PropLine = "line";
    private const string PropColumn = "column";
    private const string PropCallSites = "call_sites";
    private const string PropCallCount = "call_count";
    private const string PropInLoop = "in_loop";
    private const string PropLoopDepth = "loop_depth";
    private const string PropIsConditional = "is_conditional";
    private const string PropInTry = "in_try";
    private const string PropInCatch = "in_catch";
    private const string PropInFinally = "in_finally";
    private const string PropInLock = "in_lock";
    private const string PropIsAwaited = "is_awaited";
    private const string PropArgFlow = "arg_flow";
    private const string PropHops = "hops";
    private const string PropSinkKind = "sink_kind";
    private const string BoolTrueStr = "true";

    private Dictionary<string, object>? _properties;

    public string From { get; init; }
    public string To { get; init; }
    public string Kind { get; init; }

    [JsonIgnore]
    public int Line { get; set; }

    [JsonIgnore]
    public int Column { get; set; }

    [JsonIgnore]
    public int CallCount { get; set; }

    [JsonIgnore]
    public CallSiteExecutionContext CallContext { get; set; }

    [JsonIgnore]
    public string? ArgFlow { get; set; }

    [JsonIgnore]
    public List<(int Line, int Column)>? AdditionalCallSites { get; set; }

    [JsonIgnore]
    public int EvidenceCount { get; set; }

    [JsonIgnore]
    public bool EvidenceTruncated { get; set; }

    [JsonIgnore]
    public List<string>? EvidenceIds { get; set; }

    [JsonIgnore]
    public int Hops { get; set; }

    [JsonIgnore]
    public string? TargetSinkClassification { get; set; }

    [JsonIgnore]
    public bool HasTelemetry =>
        Line > 0 ||
        CallCount > 0 ||
        EvidenceCount > 0 ||
        Hops > 0 ||
        (EvidenceIds is { Count: > 0 }) ||
        (_properties is { Count: > 0 });

    [JsonIgnore]
    public bool HasMaterializedProperties => _properties != null;

    public Dictionary<string, object> Properties
    {
        get => _properties ??= MaterializeProperties();
        init => _properties = value;
    }

    [JsonIgnore]
    public RelationshipKind RelationshipKind => EnumExtensions.ParseRelationshipKind(Kind);

    [JsonConstructor]
    public Relationship(string from, string to, string kind, Dictionary<string, object>? properties)
    {
        From = from;
        To = to;
        Kind = kind;
        _properties = properties is { Count: > 0 } ? properties : null;
    }

    public Relationship(string from, string to, string kind)
    {
        From = from;
        To = to;
        Kind = kind;
    }

    public Relationship(string from, string to, RelationshipKind kind, Dictionary<string, object>? properties)
        : this(from, to, kind.ToCypherType(), properties)
    {
    }

    public Relationship(string from, string to, RelationshipKind kind)
        : this(from, to, kind.ToCypherType())
    {
    }

    public static Relationship CreateWithCallSite(
        string from,
        string to,
        RelationshipKind kind,
        int line,
        int column,
        CallSiteExecutionContext context = default,
        string? argFlow = null)
    {
        var rel = new Relationship(from, to, kind.ToCypherType());
        if (line > 0)
        {
            rel.Line = line;
            rel.Column = Math.Max(1, column);
            rel.CallCount = 1;
            rel.CallContext = context;
            rel.ArgFlow = string.IsNullOrEmpty(argFlow) ? null : argFlow;
        }
        return rel;
    }

    public static Relationship FromRelationship(IOntologyRelationship rel)
    {
        if (rel.Extensions is not { Count: > 0 })
        {
            return new Relationship(rel.From, rel.To, rel.Kind);
        }

        var properties = new Dictionary<string, object>(rel.Extensions.Count);
        foreach (var kvp in rel.Extensions)
        {
            properties[kvp.Key] = kvp.Value;
        }

        return new Relationship(rel.From, rel.To, rel.Kind, properties);
    }

    private Dictionary<string, object> MaterializeProperties()
    {
        var dict = new Dictionary<string, object>();
        if (Line > 0)
        {
            var col = Math.Max(1, Column);
            dict["line"] = Line.ToString();
            dict["column"] = col.ToString();
            if (AdditionalCallSites is { Count: > 0 })
            {
                var sites = new List<string>(1 + AdditionalCallSites.Count) { $"{Line}:{col}" };
                foreach (var (l, c) in AdditionalCallSites)
                {
                    sites.Add($"{l}:{c}");
                }
                dict["call_sites"] = JsonSerializer.Serialize(sites);
                dict["call_count"] = Math.Max(sites.Count, CallCount).ToString();
            }
            else
            {
                dict["call_sites"] = $"[\"{Line}:{col}\"]";
                dict["call_count"] = Math.Max(1, CallCount).ToString();
            }

            var flags = CallContext.Flags;
            if (flags.HasFlag(CallExecutionFlags.InLoop)) dict["in_loop"] = "true";
            if (CallContext.LoopDepth > 0) dict["loop_depth"] = CallContext.LoopDepth.ToString();
            if (flags.HasFlag(CallExecutionFlags.IsConditional)) dict["is_conditional"] = "true";
            if (flags.HasFlag(CallExecutionFlags.InTryBlock)) dict["in_try"] = "true";
            if (flags.HasFlag(CallExecutionFlags.InCatchBlock)) dict["in_catch"] = "true";
            if (flags.HasFlag(CallExecutionFlags.InFinallyBlock)) dict["in_finally"] = "true";
            if (flags.HasFlag(CallExecutionFlags.InLockBlock)) dict["in_lock"] = "true";
            if (flags.HasFlag(CallExecutionFlags.IsAwaited)) dict["is_awaited"] = "true";
            if (!string.IsNullOrEmpty(ArgFlow)) dict["arg_flow"] = ArgFlow;
        }

        if (EvidenceIds is { Count: > 0 })
        {
            dict[OntologyConstants.PropertyNames.EvidenceIds] = JsonSerializer.Serialize(EvidenceIds);
            dict[OntologyConstants.PropertyNames.EvidenceCount] = Math.Max(EvidenceIds.Count, EvidenceCount);
            if (EvidenceTruncated)
            {
                dict[OntologyConstants.PropertyNames.EvidenceTruncated] = "true";
            }
        }

        if (Hops > 0)
        {
            dict["hops"] = Hops;
        }
        if (!string.IsNullOrEmpty(TargetSinkClassification))
        {
            dict["sink_kind"] = TargetSinkClassification;
        }

        return dict;
    }

    public void WriteJsonProperties(Utf8JsonWriter writer)
    {
        if (_properties != null)
        {
            JsonSerializer.Serialize(writer, _properties);
            return;
        }

        writer.WriteStartObject();
        if (Line > 0)
        {
            var col = Math.Max(1, Column);
            var lineStr = Line.ToString();
            var colStr = col.ToString();
            writer.WriteString(PropLine, lineStr);
            writer.WriteString(PropColumn, colStr);
            if (AdditionalCallSites is { Count: > 0 })
            {
                var sites = new List<string>(1 + AdditionalCallSites.Count) { $"{lineStr}:{colStr}" };
                foreach (var (l, c) in AdditionalCallSites)
                {
                    sites.Add($"{l}:{c}");
                }
                writer.WriteString(PropCallSites, JsonSerializer.Serialize(sites));
                writer.WriteString(PropCallCount, Math.Max(sites.Count, CallCount).ToString());
            }
            else
            {
                writer.WriteString(PropCallSites, $"[\"{lineStr}:{colStr}\"]");
                writer.WriteString(PropCallCount, Math.Max(1, CallCount).ToString());
            }

            var flags = CallContext.Flags;
            if (flags.HasFlag(CallExecutionFlags.InLoop)) writer.WriteString(PropInLoop, BoolTrueStr);
            if (CallContext.LoopDepth > 0) writer.WriteString(PropLoopDepth, CallContext.LoopDepth.ToString());
            if (flags.HasFlag(CallExecutionFlags.IsConditional)) writer.WriteString(PropIsConditional, BoolTrueStr);
            if (flags.HasFlag(CallExecutionFlags.InTryBlock)) writer.WriteString(PropInTry, BoolTrueStr);
            if (flags.HasFlag(CallExecutionFlags.InCatchBlock)) writer.WriteString(PropInCatch, BoolTrueStr);
            if (flags.HasFlag(CallExecutionFlags.InFinallyBlock)) writer.WriteString(PropInFinally, BoolTrueStr);
            if (flags.HasFlag(CallExecutionFlags.InLockBlock)) writer.WriteString(PropInLock, BoolTrueStr);
            if (flags.HasFlag(CallExecutionFlags.IsAwaited)) writer.WriteString(PropIsAwaited, BoolTrueStr);
            if (!string.IsNullOrEmpty(ArgFlow)) writer.WriteString(PropArgFlow, ArgFlow);
        }

        if (EvidenceIds is { Count: > 0 })
        {
            writer.WriteString(OntologyConstants.PropertyNames.EvidenceIds, JsonSerializer.Serialize(EvidenceIds));
            writer.WriteNumber(OntologyConstants.PropertyNames.EvidenceCount, Math.Max(EvidenceIds.Count, EvidenceCount));
            if (EvidenceTruncated)
            {
                writer.WriteString(OntologyConstants.PropertyNames.EvidenceTruncated, BoolTrueStr);
            }
        }

        if (Hops > 0)
        {
            writer.WriteNumber(PropHops, Hops);
        }
        if (!string.IsNullOrEmpty(TargetSinkClassification))
        {
            writer.WriteString(PropSinkKind, TargetSinkClassification);
        }
        writer.WriteEndObject();
    }
}
