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
    private const string PropLineEnd = "line_end";
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
    private const string PropIsParallel = "is_parallel";
    private const string PropArgFlow = "arg_flow";
    private const string PropHops = "hops";
    private const string PropSinkKind = "sink_kind";
    private const string PropFilePath = "file_path";
    private const string PropOperation = "operation";
    private const string PropEvidenceKind = "evidence_kind";
    private const string PropDependencyType = "dependency_type";
    private const string PropCategory = "category";
    private const string PropIsSemantic = "is_semantic";
    private const string PropIsCanonical = "is_canonical";
    private const string PropSemanticLifted = "semantic_lifted";
    private const string PropIsExternal = "is_external";
    private const string PropIsInternal = "is_internal";
    private const string PropIsOrm = "is_orm";
    private const string PropFromConfig = "from_config";
    private const string PropScope = "scope";
    private const string PropBrokerType = "broker_type";
    private const string PropMessagingDirection = "messaging_direction";
    private const string PropViaLibrary = "via_library";
    private const string PropCallChain = "call_chain";
    private const string PropConfigKey = "config_key";
    private const string PropUrl = "url";
    private const string PropServiceName = "service_name";
    private const string PropServiceKey = "service_key";
    private const string PropResourceRole = "resource_role";
    private const string PropAsset = "asset";
    private const string PropTargetFile = "target_file";
    private const string PropVia = "via";
    private const string PropProvider = "provider";
    private const string PropSchema = "schema";
    private const string PropEngine = "engine";
    private const string PropDbType = "db_type";
    private const string PropIsImplicitSchema = "is_implicit_schema";
    private const string PropProtocols = "protocols";
    private const string BoolTrueStr = "true";
    private const string BoolFalseStr = "false";

    private Dictionary<string, object>? _properties;

    public string From { get; init; }
    public string To { get; init; }
    public RelationshipKind Kind { get; init; }

    [JsonIgnore]
    public int Line { get; set; }

    [JsonIgnore]
    public int LineEnd { get; set; }

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
    public string? FilePath { get; set; }

    [JsonIgnore]
    public string? Operation { get; set; }

    [JsonIgnore]
    public string? EvidenceKind { get; set; }

    [JsonIgnore]
    public string? DependencyType { get; set; }

    [JsonIgnore]
    public string? Category { get; set; }

    [JsonIgnore]
    public bool? IsSemantic { get; set; }

    [JsonIgnore]
    public bool IsCanonical { get; set; }

    [JsonIgnore]
    public bool IsSemanticLifted { get; set; }

    [JsonIgnore]
    public bool IsExternal { get; set; }

    [JsonIgnore]
    public bool? IsInternal { get; set; }

    [JsonIgnore]
    public bool IsOrm { get; set; }

    [JsonIgnore]
    public bool FromConfig { get; set; }

    [JsonIgnore]
    public string? Scope { get; set; }

    [JsonIgnore]
    public string? BrokerType { get; set; }

    [JsonIgnore]
    public string? MessagingDirection { get; set; }

    [JsonIgnore]
    public string? ViaLibrary { get; set; }

    [JsonIgnore]
    public string? CallChain { get; set; }

    [JsonIgnore]
    public string? ConfigKey { get; set; }

    [JsonIgnore]
    public string? Url { get; set; }

    [JsonIgnore]
    public string? ServiceName { get; set; }

    [JsonIgnore]
    public string? ServiceKey { get; set; }

    [JsonIgnore]
    public string? ResourceRole { get; set; }

    [JsonIgnore]
    public string? Asset { get; set; }

    [JsonIgnore]
    public string? TargetFile { get; set; }

    [JsonIgnore]
    public string? Via { get; set; }

    [JsonIgnore]
    public string? Provider { get; set; }

    [JsonIgnore]
    public string? Schema { get; set; }

    [JsonIgnore]
    public string? Engine { get; set; }

    [JsonIgnore]
    public string? DbType { get; set; }

    [JsonIgnore]
    public bool IsImplicitSchema { get; set; }

    [JsonIgnore]
    public string? Protocols { get; set; }

    [JsonIgnore]
    public Dictionary<string, string>? CustomProperties { get; set; }

    [JsonIgnore]
    public bool HasTelemetry =>
        Line > 0 ||
        LineEnd > 0 ||
        CallCount > 0 ||
        CallContext.Flags != CallExecutionFlags.None ||
        CallContext.LoopDepth > 0 ||
        !string.IsNullOrEmpty(ArgFlow) ||
        EvidenceCount > 0 ||
        Hops > 0 ||
        (EvidenceIds is { Count: > 0 }) ||
        !string.IsNullOrEmpty(FilePath) ||
        !string.IsNullOrEmpty(Operation) ||
        !string.IsNullOrEmpty(EvidenceKind) ||
        !string.IsNullOrEmpty(DependencyType) ||
        !string.IsNullOrEmpty(Category) ||
        IsSemantic.HasValue ||
        IsCanonical ||
        IsSemanticLifted ||
        IsExternal ||
        IsInternal.HasValue ||
        IsOrm ||
        FromConfig ||
        !string.IsNullOrEmpty(Scope) ||
        !string.IsNullOrEmpty(BrokerType) ||
        !string.IsNullOrEmpty(MessagingDirection) ||
        !string.IsNullOrEmpty(ViaLibrary) ||
        !string.IsNullOrEmpty(CallChain) ||
        !string.IsNullOrEmpty(ConfigKey) ||
        !string.IsNullOrEmpty(Url) ||
        !string.IsNullOrEmpty(ServiceName) ||
        !string.IsNullOrEmpty(ServiceKey) ||
        !string.IsNullOrEmpty(ResourceRole) ||
        !string.IsNullOrEmpty(Asset) ||
        !string.IsNullOrEmpty(TargetFile) ||
        !string.IsNullOrEmpty(Via) ||
        !string.IsNullOrEmpty(Provider) ||
        !string.IsNullOrEmpty(Schema) ||
        !string.IsNullOrEmpty(Engine) ||
        !string.IsNullOrEmpty(DbType) ||
        IsImplicitSchema ||
        !string.IsNullOrEmpty(Protocols) ||
        (CustomProperties is { Count: > 0 }) ||
        (_properties is { Count: > 0 });

    [JsonIgnore]
    public bool HasMaterializedProperties => _properties != null;

    public Dictionary<string, object> Properties
    {
        get
        {
            if (_properties != null)
            {
                foreach (var (k, v) in MaterializeProperties())
                {
                    _properties.TryAdd(k, v);
                }
                return _properties;
            }
            return _properties = MaterializeProperties();
        }
        init
        {
            _properties = value;
            if (value != null)
            {
                HydrateTypedProperties(value);
            }
        }
    }

    [JsonIgnore]
    public RelationshipKind RelationshipKind => Kind;

    [JsonConstructor]
    public Relationship(string from, string to, RelationshipKind kind, Dictionary<string, object>? properties)
    {
        From = from;
        To = to;
        Kind = kind;
        _properties = properties is { Count: > 0 } ? properties : null;
        if (_properties != null)
        {
            HydrateTypedProperties(_properties);
        }
    }

    public Relationship(string from, string to, RelationshipKind kind)
    {
        From = from;
        To = to;
        Kind = kind;
    }

    public Relationship(string from, string to, string kind, Dictionary<string, object>? properties)
        : this(from, to, EnumExtensions.ParseRelationshipKind(kind), properties)
    {
    }

    public Relationship(string from, string to, string kind)
        : this(from, to, EnumExtensions.ParseRelationshipKind(kind))
    {
    }

    private void HydrateTypedProperties(IReadOnlyDictionary<string, object> props)
    {
        if (props.TryGetValue(PropLine, out var ln) && int.TryParse(ln?.ToString(), out var pLine)) Line = pLine;
        if (props.TryGetValue(PropLineEnd, out var lne) && int.TryParse(lne?.ToString(), out var pLineEnd)) LineEnd = pLineEnd;
        if (props.TryGetValue(PropColumn, out var col) && int.TryParse(col?.ToString(), out var pCol)) Column = pCol;
        if (props.TryGetValue(PropCallCount, out var cc) && int.TryParse(cc?.ToString(), out var pCc)) CallCount = pCc;
        if (props.TryGetValue(PropHops, out var hp) && int.TryParse(hp?.ToString(), out var pHops)) Hops = pHops;
        if (props.TryGetValue(PropSinkKind, out var sk) && sk != null) TargetSinkClassification = sk.ToString();
        if (props.TryGetValue(PropFilePath, out var fp) && fp != null) FilePath = fp.ToString();
        if (props.TryGetValue(PropOperation, out var op) && op != null) Operation = op.ToString();
        if (props.TryGetValue(PropEvidenceKind, out var ek) && ek != null) EvidenceKind = ek.ToString();
        if (props.TryGetValue(PropDependencyType, out var dt) && dt != null) DependencyType = dt.ToString();
        if (props.TryGetValue(PropCategory, out var cat) && cat != null) Category = cat.ToString();
        if (props.TryGetValue(PropIsSemantic, out var sem) && bool.TryParse(sem?.ToString(), out var pSem)) IsSemantic = pSem;
        if (props.TryGetValue(PropIsCanonical, out var can) && bool.TryParse(can?.ToString(), out var pCan)) IsCanonical = pCan;
        if (props.TryGetValue(PropSemanticLifted, out var sl) && bool.TryParse(sl?.ToString(), out var pSl)) IsSemanticLifted = pSl;
        if (props.TryGetValue(PropIsExternal, out var ext) && bool.TryParse(ext?.ToString(), out var pExt)) IsExternal = pExt;
        if (props.TryGetValue(PropIsInternal, out var intr) && bool.TryParse(intr?.ToString(), out var pInt)) IsInternal = pInt;
        if (props.TryGetValue(PropIsOrm, out var orm) && bool.TryParse(orm?.ToString(), out var pOrm)) IsOrm = pOrm;
        if (props.TryGetValue(PropFromConfig, out var fc) && bool.TryParse(fc?.ToString(), out var pFc)) FromConfig = pFc;
        if (props.TryGetValue(PropScope, out var sc) && sc != null) Scope = sc.ToString();
        if (props.TryGetValue(PropBrokerType, out var bt) && bt != null) BrokerType = bt.ToString();
        if (props.TryGetValue(PropMessagingDirection, out var md) && md != null) MessagingDirection = md.ToString();
        if (props.TryGetValue(PropViaLibrary, out var vl) && vl != null) ViaLibrary = vl.ToString();
        if (props.TryGetValue(PropCallChain, out var cch) && cch != null) CallChain = cch.ToString();
        if (props.TryGetValue(PropConfigKey, out var ck) && ck != null) ConfigKey = ck.ToString();
        if (props.TryGetValue(PropUrl, out var url) && url != null) Url = url.ToString();
        if (props.TryGetValue(PropServiceName, out var sn) && sn != null) ServiceName = sn.ToString();
        if (props.TryGetValue(PropServiceKey, out var skey) && skey != null) ServiceKey = skey.ToString();
        if (props.TryGetValue(PropResourceRole, out var rr) && rr != null) ResourceRole = rr.ToString();
        if (props.TryGetValue(PropAsset, out var ast) && ast != null) Asset = ast.ToString();
        if (props.TryGetValue(PropTargetFile, out var tf) && tf != null) TargetFile = tf.ToString();
        if (props.TryGetValue(PropVia, out var via) && via != null) Via = via.ToString();
        if (props.TryGetValue(PropProvider, out var prv) && prv != null) Provider = prv.ToString();
        if (props.TryGetValue(PropSchema, out var sch) && sch != null) Schema = sch.ToString();
        if (props.TryGetValue(PropEngine, out var eng) && eng != null) Engine = eng.ToString();
        if (props.TryGetValue(PropDbType, out var dbt) && dbt != null) DbType = dbt.ToString();
        if (props.TryGetValue(PropIsImplicitSchema, out var imp) && bool.TryParse(imp?.ToString(), out var pImp)) IsImplicitSchema = pImp;
        if (props.TryGetValue(PropProtocols, out var prt) && prt != null) Protocols = prt.ToString();
        HydrateCallContext(props);
    }

    private void HydrateCallContext(IReadOnlyDictionary<string, object> props)
    {
        var flags = CallContext.Flags;
        var loopDepth = CallContext.LoopDepth;
        if (props.TryGetValue(PropInLoop, out var il) && string.Equals(il?.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase)) flags |= CallExecutionFlags.InLoop;
        if (props.TryGetValue(PropLoopDepth, out var ld) && byte.TryParse(ld?.ToString(), out var pLd))
        {
            loopDepth = pLd;
            if (pLd >= 2) flags |= CallExecutionFlags.InNestedLoop;
        }
        if (props.TryGetValue(PropIsConditional, out var ic) && string.Equals(ic?.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase)) flags |= CallExecutionFlags.IsConditional;
        if (props.TryGetValue(PropInTry, out var it) && string.Equals(it?.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase)) flags |= CallExecutionFlags.InTryBlock;
        if (props.TryGetValue(PropInCatch, out var icatch) && string.Equals(icatch?.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase)) flags |= CallExecutionFlags.InCatchBlock;
        if (props.TryGetValue(PropInFinally, out var ifin) && string.Equals(ifin?.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase)) flags |= CallExecutionFlags.InFinallyBlock;
        if (props.TryGetValue(PropInLock, out var ilock) && string.Equals(ilock?.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase)) flags |= CallExecutionFlags.InLockBlock;
        if (props.TryGetValue(PropIsAwaited, out var iaw) && string.Equals(iaw?.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase)) flags |= CallExecutionFlags.IsAwaited;
        if (props.TryGetValue(PropIsParallel, out var ipar) && string.Equals(ipar?.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase)) flags |= CallExecutionFlags.IsParallelBatch;
        if (flags != CallExecutionFlags.None || loopDepth > 0)
        {
            CallContext = new CallSiteExecutionContext(flags, loopDepth, CallContext.ConditionalDepth);
        }
        if (props.TryGetValue(PropArgFlow, out var af) && af != null) ArgFlow = af.ToString();
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
        var rel = new Relationship(from, to, kind);
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
        var dbRel = new Relationship(rel.From, rel.To, rel.Kind);
        if (rel is OntologyRelationship typed)
        {
            dbRel.Line = typed.Line;
            dbRel.LineEnd = typed.LineEnd;
            dbRel.Column = typed.Column;
            dbRel.FilePath = typed.FilePath;
            dbRel.Operation = typed.Operation;
            dbRel.EvidenceKind = typed.EvidenceKind;
            dbRel.DependencyType = typed.DependencyType;
            dbRel.Category = typed.Category;
            dbRel.IsSemantic = typed.IsSemantic;
            dbRel.IsCanonical = typed.IsCanonical;
            dbRel.IsSemanticLifted = typed.IsSemanticLifted;
            dbRel.IsExternal = typed.IsExternal;
            dbRel.IsInternal = typed.IsInternal;
            dbRel.IsOrm = typed.IsOrm;
            dbRel.Scope = typed.Scope;
            dbRel.BrokerType = typed.BrokerType;
            dbRel.MessagingDirection = typed.MessagingDirection;
            dbRel.ViaLibrary = typed.ViaLibrary;
            dbRel.CallChain = typed.CallChain;
            dbRel.ConfigKey = typed.ConfigKey;
            dbRel.Url = typed.Url;
            dbRel.ServiceName = typed.ServiceName;
            dbRel.ServiceKey = typed.ServiceKey;
            dbRel.ResourceRole = typed.ResourceRole;
            dbRel.Asset = typed.Asset;
            dbRel.TargetFile = typed.TargetFile;
            dbRel.Via = typed.Via;
            dbRel.Provider = typed.Provider;
            dbRel.Schema = typed.Schema;
            dbRel.IsImplicitSchema = typed.IsImplicitSchema;
            dbRel.Protocols = typed.Protocols;
        }
        if (rel.CustomProperties is { Count: > 0 })
        {
            dbRel.CustomProperties = new Dictionary<string, string>(rel.CustomProperties, StringComparer.Ordinal);
        }
        return dbRel;
    }

    private Dictionary<string, object> MaterializeProperties()
    {
        var dict = new Dictionary<string, object>();
        if (Line > 0)
        {
            dict[PropLine] = Line.ToString();
            if (Column > 0 || CallCount > 0 || AdditionalCallSites is { Count: > 0 })
            {
                var col = Math.Max(1, Column);
                dict[PropColumn] = col.ToString();
                if (AdditionalCallSites is { Count: > 0 })
                {
                    var sites = new List<string>(1 + AdditionalCallSites.Count) { $"{Line}:{col}" };
                    foreach (var (l, c) in AdditionalCallSites)
                    {
                        sites.Add($"{l}:{c}");
                    }
                    dict[PropCallSites] = JsonSerializer.Serialize(sites);
                    dict[PropCallCount] = Math.Max(sites.Count, CallCount).ToString();
                }
                else
                {
                    dict[PropCallSites] = $"[\"{Line}:{col}\"]";
                    dict[PropCallCount] = Math.Max(1, CallCount).ToString();
                }
            }
        }
        if (CallContext.Flags != CallExecutionFlags.None || CallContext.LoopDepth > 0 || !string.IsNullOrEmpty(ArgFlow))
        {
            var flags = CallContext.Flags;
            if (flags.HasFlag(CallExecutionFlags.InLoop)) dict[PropInLoop] = true;
            if (CallContext.LoopDepth > 0) dict[PropLoopDepth] = (int)CallContext.LoopDepth;
            if (flags.HasFlag(CallExecutionFlags.IsConditional)) dict[PropIsConditional] = true;
            if (flags.HasFlag(CallExecutionFlags.InTryBlock)) dict[PropInTry] = true;
            if (flags.HasFlag(CallExecutionFlags.InCatchBlock)) dict[PropInCatch] = true;
            if (flags.HasFlag(CallExecutionFlags.InFinallyBlock)) dict[PropInFinally] = true;
            if (flags.HasFlag(CallExecutionFlags.InLockBlock)) dict[PropInLock] = true;
            if (flags.HasFlag(CallExecutionFlags.IsAwaited)) dict[PropIsAwaited] = true;
            if (CallContext.IsParallel) dict[PropIsParallel] = true;
            if (!string.IsNullOrEmpty(ArgFlow)) dict[PropArgFlow] = ArgFlow;
        }
        if (LineEnd > 0) dict[PropLineEnd] = LineEnd.ToString();
        if (!string.IsNullOrEmpty(FilePath)) dict[PropFilePath] = FilePath;
        if (!string.IsNullOrEmpty(Operation)) dict[PropOperation] = Operation;
        if (!string.IsNullOrEmpty(EvidenceKind)) dict[PropEvidenceKind] = EvidenceKind;
        if (!string.IsNullOrEmpty(DependencyType)) dict[PropDependencyType] = DependencyType;
        if (!string.IsNullOrEmpty(Category)) dict[PropCategory] = Category;
        if (IsSemantic.HasValue) dict[PropIsSemantic] = IsSemantic.Value ? BoolTrueStr : BoolFalseStr;
        if (IsCanonical) dict[PropIsCanonical] = BoolTrueStr;
        if (IsSemanticLifted) dict[PropSemanticLifted] = BoolTrueStr;
        if (IsExternal) dict[PropIsExternal] = BoolTrueStr;
        if (IsInternal.HasValue) dict[PropIsInternal] = IsInternal.Value ? BoolTrueStr : BoolFalseStr;
        if (IsOrm) dict[PropIsOrm] = BoolTrueStr;
        if (FromConfig) dict[PropFromConfig] = BoolTrueStr;
        if (!string.IsNullOrEmpty(Scope)) dict[PropScope] = Scope;
        if (!string.IsNullOrEmpty(BrokerType)) dict[PropBrokerType] = BrokerType;
        if (!string.IsNullOrEmpty(MessagingDirection)) dict[PropMessagingDirection] = MessagingDirection;
        if (!string.IsNullOrEmpty(ViaLibrary)) dict[PropViaLibrary] = ViaLibrary;
        if (!string.IsNullOrEmpty(CallChain)) dict[PropCallChain] = CallChain;
        if (!string.IsNullOrEmpty(ConfigKey)) dict[PropConfigKey] = ConfigKey;
        if (!string.IsNullOrEmpty(Url)) dict[PropUrl] = Url;
        if (!string.IsNullOrEmpty(ServiceName)) dict[PropServiceName] = ServiceName;
        if (!string.IsNullOrEmpty(ServiceKey)) dict[PropServiceKey] = ServiceKey;
        if (!string.IsNullOrEmpty(ResourceRole)) dict[PropResourceRole] = ResourceRole;
        if (!string.IsNullOrEmpty(Asset)) dict[PropAsset] = Asset;
        if (!string.IsNullOrEmpty(TargetFile)) dict[PropTargetFile] = TargetFile;
        if (!string.IsNullOrEmpty(Via)) dict[PropVia] = Via;
        if (!string.IsNullOrEmpty(Provider)) dict[PropProvider] = Provider;
        if (!string.IsNullOrEmpty(Schema)) dict[PropSchema] = Schema;
        if (!string.IsNullOrEmpty(Engine)) dict[PropEngine] = Engine;
        if (!string.IsNullOrEmpty(DbType)) dict[PropDbType] = DbType;
        if (IsImplicitSchema) dict[PropIsImplicitSchema] = BoolTrueStr;
        if (!string.IsNullOrEmpty(Protocols)) dict[PropProtocols] = Protocols;

        if (EvidenceIds is { Count: > 0 })
        {
            dict[OntologyConstants.PropertyNames.EvidenceIds] = JsonSerializer.Serialize(EvidenceIds);
            dict[OntologyConstants.PropertyNames.EvidenceCount] = Math.Max(EvidenceIds.Count, EvidenceCount);
            if (EvidenceTruncated)
            {
                dict[OntologyConstants.PropertyNames.EvidenceTruncated] = BoolTrueStr;
            }
        }

        if (Hops > 0)
        {
            dict[PropHops] = Hops;
        }
        if (!string.IsNullOrEmpty(TargetSinkClassification))
        {
            dict[PropSinkKind] = TargetSinkClassification;
        }

        if (CustomProperties is { Count: > 0 })
        {
            foreach (var (k, v) in CustomProperties)
            {
                dict.TryAdd(k, v);
            }
        }

        return dict;
    }

    public void WriteJsonProperties(Utf8JsonWriter writer)
    {
        if (_properties != null)
        {
            foreach (var (k, v) in MaterializeProperties())
            {
                _properties.TryAdd(k, v);
            }
            JsonSerializer.Serialize(writer, _properties);
            return;
        }

        writer.WriteStartObject();
        if (Line > 0)
        {
            var lineStr = Line.ToString();
            writer.WriteString(PropLine, lineStr);
            if (Column > 0 || CallCount > 0 || AdditionalCallSites is { Count: > 0 })
            {
                var col = Math.Max(1, Column);
                var colStr = col.ToString();
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
            }
        }
        if (CallContext.Flags != CallExecutionFlags.None || CallContext.LoopDepth > 0 || !string.IsNullOrEmpty(ArgFlow))
        {
            var flags = CallContext.Flags;
            if (flags.HasFlag(CallExecutionFlags.InLoop)) writer.WriteBoolean(PropInLoop, true);
            if (CallContext.LoopDepth > 0) writer.WriteNumber(PropLoopDepth, (int)CallContext.LoopDepth);
            if (flags.HasFlag(CallExecutionFlags.IsConditional)) writer.WriteBoolean(PropIsConditional, true);
            if (flags.HasFlag(CallExecutionFlags.InTryBlock)) writer.WriteBoolean(PropInTry, true);
            if (flags.HasFlag(CallExecutionFlags.InCatchBlock)) writer.WriteBoolean(PropInCatch, true);
            if (flags.HasFlag(CallExecutionFlags.InFinallyBlock)) writer.WriteBoolean(PropInFinally, true);
            if (flags.HasFlag(CallExecutionFlags.InLockBlock)) writer.WriteBoolean(PropInLock, true);
            if (flags.HasFlag(CallExecutionFlags.IsAwaited)) writer.WriteBoolean(PropIsAwaited, true);
            if (CallContext.IsParallel) writer.WriteBoolean(PropIsParallel, true);
            if (!string.IsNullOrEmpty(ArgFlow)) writer.WriteString(PropArgFlow, ArgFlow);
        }
        if (LineEnd > 0) writer.WriteString(PropLineEnd, LineEnd.ToString());
        if (!string.IsNullOrEmpty(FilePath)) writer.WriteString(PropFilePath, FilePath);
        if (!string.IsNullOrEmpty(Operation)) writer.WriteString(PropOperation, Operation);
        if (!string.IsNullOrEmpty(EvidenceKind)) writer.WriteString(PropEvidenceKind, EvidenceKind);
        if (!string.IsNullOrEmpty(DependencyType)) writer.WriteString(PropDependencyType, DependencyType);
        if (!string.IsNullOrEmpty(Category)) writer.WriteString(PropCategory, Category);
        if (IsSemantic.HasValue) writer.WriteString(PropIsSemantic, IsSemantic.Value ? BoolTrueStr : BoolFalseStr);
        if (IsCanonical) writer.WriteString(PropIsCanonical, BoolTrueStr);
        if (IsSemanticLifted) writer.WriteString(PropSemanticLifted, BoolTrueStr);
        if (IsExternal) writer.WriteString(PropIsExternal, BoolTrueStr);
        if (IsInternal.HasValue) writer.WriteString(PropIsInternal, IsInternal.Value ? BoolTrueStr : BoolFalseStr);
        if (IsOrm) writer.WriteString(PropIsOrm, BoolTrueStr);
        if (FromConfig) writer.WriteString(PropFromConfig, BoolTrueStr);
        if (!string.IsNullOrEmpty(Scope)) writer.WriteString(PropScope, Scope);
        if (!string.IsNullOrEmpty(BrokerType)) writer.WriteString(PropBrokerType, BrokerType);
        if (!string.IsNullOrEmpty(MessagingDirection)) writer.WriteString(PropMessagingDirection, MessagingDirection);
        if (!string.IsNullOrEmpty(ViaLibrary)) writer.WriteString(PropViaLibrary, ViaLibrary);
        if (!string.IsNullOrEmpty(CallChain)) writer.WriteString(PropCallChain, CallChain);
        if (!string.IsNullOrEmpty(ConfigKey)) writer.WriteString(PropConfigKey, ConfigKey);
        if (!string.IsNullOrEmpty(Url)) writer.WriteString(PropUrl, Url);
        if (!string.IsNullOrEmpty(ServiceName)) writer.WriteString(PropServiceName, ServiceName);
        if (!string.IsNullOrEmpty(ServiceKey)) writer.WriteString(PropServiceKey, ServiceKey);
        if (!string.IsNullOrEmpty(ResourceRole)) writer.WriteString(PropResourceRole, ResourceRole);
        if (!string.IsNullOrEmpty(Asset)) writer.WriteString(PropAsset, Asset);
        if (!string.IsNullOrEmpty(TargetFile)) writer.WriteString(PropTargetFile, TargetFile);
        if (!string.IsNullOrEmpty(Via)) writer.WriteString(PropVia, Via);
        if (!string.IsNullOrEmpty(Provider)) writer.WriteString(PropProvider, Provider);
        if (!string.IsNullOrEmpty(Schema)) writer.WriteString(PropSchema, Schema);
        if (!string.IsNullOrEmpty(Engine)) writer.WriteString(PropEngine, Engine);
        if (!string.IsNullOrEmpty(DbType)) writer.WriteString(PropDbType, DbType);
        if (IsImplicitSchema) writer.WriteString(PropIsImplicitSchema, BoolTrueStr);
        if (!string.IsNullOrEmpty(Protocols)) writer.WriteString(PropProtocols, Protocols);

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

        if (CustomProperties is { Count: > 0 })
        {
            foreach (var (k, v) in CustomProperties)
            {
                writer.WriteString(k, v);
            }
        }
        writer.WriteEndObject();
    }

    public string SerializeJsonProperties(ArrayBufferWriter<byte> bufferWriter, Utf8JsonWriter jsonWriter)
    {
        const string emptyPropsJson = "{}";
        if (!HasTelemetry) return emptyPropsJson;

        bufferWriter.Clear();
        jsonWriter.Reset(bufferWriter);
        WriteJsonProperties(jsonWriter);
        jsonWriter.Flush();
        return System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
    }
}
