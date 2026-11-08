namespace CodeExplorer.Cypher.Common;

/// <summary>
/// Strict bi-directional integer codec for graph node kinds (<see cref="NodeKind"/>)
/// and relationship kinds (<see cref="RelationshipKind"/>) in SQLite.
/// </summary>
public static class GraphKindCodec
{
    private const int UnspecifiedKindId = 0;

    public static readonly string ProjectRoleKindIdsSql = NodeKindsInSql(
        NodeKind.Service, NodeKind.App, NodeKind.Library, NodeKind.SharedLibrary,
        NodeKind.Worker, NodeKind.CliTool, NodeKind.Test, NodeKind.TestSuite);

    public static readonly string ProjectAndWorkloadKindsSql = NodeKindsInSql(
        NodeKind.Project, NodeKind.Service, NodeKind.App, NodeKind.Worker,
        NodeKind.Library, NodeKind.CliTool);

    public static readonly string SymbolKindsSql = NodeKindsInSql(
        NodeKind.Type, NodeKind.Function, NodeKind.Endpoint);

    public static readonly string SinkKindsSql = NodeKindsInSql(
        NodeKind.ExternalService, NodeKind.Database, NodeKind.Query, NodeKind.CloudService);

    public static readonly string DatabaseKindsSql = NodeKindsInSql(NodeKind.Database);

    public static readonly string DatabaseUsageRelsSql = RelKindsInSql(
        RelationshipKind.UsesDb, RelationshipKind.ReadsFrom, RelationshipKind.WritesTo);

    public static readonly string ServiceCallRelsSql = RelKindsInSql(RelationshipKind.ServiceCall);

    public static readonly string TypeAndSubtypeKindsSql = NodeKindsInSql(NodeKind.Type);

    public static readonly string ArchitecturalNodeKindsSql = NodeKindsInSql(
        NodeKind.Endpoint, NodeKind.Table, NodeKind.Topic, NodeKind.Type);

    public static readonly string FileAndSymbolKindsSql = NodeKindsInSql(
        NodeKind.File, NodeKind.Folder, NodeKind.Type,
        NodeKind.Function, NodeKind.Endpoint, NodeKind.EntryPoint);

    public static readonly string ChangedSymbolKindsSql = NodeKindsInSql(
        NodeKind.Function, NodeKind.Type, NodeKind.Member);

    public static readonly string TestContainerTypeKindsSql = NodeKindsInSql(NodeKind.Type);

    public static readonly string TestFunctionKindsSql = NodeKindsInSql(NodeKind.Function);

    public static int EncodeNodeKind(NodeKind kind) => (int)kind;

    public static int EncodeNodeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return UnspecifiedKindId;
        var parsed = EnumExtensions.ParseNodeKind(kind);
        if (parsed == NodeKind.Unspecified)
        {
            throw new ArgumentException($"Unknown NodeKind label: '{kind}'. Use a valid NodeKind from the ontology.", nameof(kind));
        }
        return (int)parsed;
    }

    public static string DecodeNodeKind(int id)
    {
        if (id <= UnspecifiedKindId) return string.Empty;
        var kind = (NodeKind)id;
        return OntologyEnumMaps.NameByNodeKind.TryGetValue(kind, out var name) ? name : string.Empty;
    }

    public static string DecodeNodeKind(long id) => DecodeNodeKind((int)id);

    public static int EncodeRelKind(RelationshipKind kind) => (int)kind;

    public static int EncodeRelKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return UnspecifiedKindId;
        var parsed = EnumExtensions.ParseRelationshipKind(kind);
        if (parsed == RelationshipKind.Unspecified)
        {
            throw new ArgumentException($"Unknown RelationshipKind label: '{kind}'. Use a valid RelationshipKind from the ontology.", nameof(kind));
        }
        return (int)parsed;
    }

    public static string DecodeRelKind(int id)
    {
        if (id <= UnspecifiedKindId) return string.Empty;
        var kind = (RelationshipKind)id;
        return OntologyEnumMaps.NameByRelKind.TryGetValue(kind, out var name) ? name : string.Empty;
    }

    public static string DecodeRelKind(long id) => DecodeRelKind((int)id);

    public static string NodeKindsInSql(params ReadOnlySpan<NodeKind> kinds)
    {
        if (kinds.Length == 0) return "0";
        var ids = new int[kinds.Length];
        for (var i = 0; i < kinds.Length; i++)
        {
            ids[i] = (int)kinds[i];
        }
        return string.Join(", ", ids);
    }

    public static string NodeKindsInSql(params ReadOnlySpan<string> kinds)
    {
        if (kinds.Length == 0) return "0";
        var ids = new int[kinds.Length];
        for (var i = 0; i < kinds.Length; i++)
        {
            ids[i] = EncodeNodeKind(kinds[i]);
        }
        return string.Join(", ", ids);
    }

    public static string RelKindsInSql(params ReadOnlySpan<RelationshipKind> kinds)
    {
        if (kinds.Length == 0) return "0";
        var ids = new int[kinds.Length];
        for (var i = 0; i < kinds.Length; i++)
        {
            ids[i] = (int)kinds[i];
        }
        return string.Join(", ", ids);
    }

    public static string RelKindsInSql(params ReadOnlySpan<string> kinds)
    {
        if (kinds.Length == 0) return "0";
        var ids = new int[kinds.Length];
        for (var i = 0; i < kinds.Length; i++)
        {
            ids[i] = EncodeRelKind(kinds[i]);
        }
        return string.Join(", ", ids);
    }
}
