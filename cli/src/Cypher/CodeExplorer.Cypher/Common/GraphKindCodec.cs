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

    public static readonly string WorkloadRoleKindIdsSql = NodeKindsInSql(
        NodeKind.Service, NodeKind.App, NodeKind.Worker, NodeKind.CliTool);

    public static readonly string ModuleOnlyRoleKindIdsSql = NodeKindsInSql(
        NodeKind.Library, NodeKind.SharedLibrary, NodeKind.Test, NodeKind.TestSuite);

    private static readonly NodeKind[] WorkloadConcreteKinds =
    [
        NodeKind.Service, NodeKind.App, NodeKind.Worker, NodeKind.CliTool
    ];

    private static readonly NodeKind[] ModuleConcreteKinds =
    [
        NodeKind.Service, NodeKind.App, NodeKind.Worker, NodeKind.CliTool,
        NodeKind.Library, NodeKind.SharedLibrary, NodeKind.TestSuite, NodeKind.Test
    ];

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

    public static bool IsAbstractNodeKind(NodeKind kind) =>
        kind is NodeKind.Module or NodeKind.Workload;

    public static ReadOnlySpan<NodeKind> ExpandConcreteNodeKinds(NodeKind kind) => kind switch
    {
        NodeKind.Workload => WorkloadConcreteKinds,
        NodeKind.Module => ModuleConcreteKinds,
        _ => [kind]
    };

    public static string BuildSingleLabelFilterSql(string kindColumnSql, string label)
    {
        var parsed = EnumExtensions.ParseNodeKind(label);
        if (parsed == NodeKind.Unspecified)
        {
            throw new ArgumentException($"Unknown NodeKind label: '{label}'. Use a valid NodeKind from the ontology.", nameof(label));
        }

        var expanded = ExpandConcreteNodeKinds(parsed);
        return expanded.Length == 1
            ? $"{kindColumnSql} = {(int)expanded[0]}"
            : $"{kindColumnSql} IN ({NodeKindsInSql(expanded)})";
    }

    public static string BuildLabelsFilterSql(
        string kindColumnSql,
        IReadOnlyList<NodeKind> nodeKinds,
        IReadOnlyList<string> labels)
    {
        if (nodeKinds.Count == 1)
        {
            var singleKind = nodeKinds[0];
            if (singleKind == NodeKind.Unspecified)
            {
                return BuildSingleLabelFilterSql(kindColumnSql, labels[0]);
            }

            var expanded = ExpandConcreteNodeKinds(singleKind);
            return expanded.Length == 1
                ? $"{kindColumnSql} = {(int)expanded[0]}"
                : $"{kindColumnSql} IN ({NodeKindsInSql(expanded)})";
        }

        List<int> ids = new(nodeKinds.Count * 2);
        for (var i = 0; i < nodeKinds.Count; i++)
        {
            var k = nodeKinds[i];
            if (k == NodeKind.Unspecified)
            {
                var parsed = EnumExtensions.ParseNodeKind(labels[i]);
                if (parsed == NodeKind.Unspecified)
                {
                    throw new ArgumentException($"Unknown NodeKind label: '{labels[i]}'. Use a valid NodeKind from the ontology.", nameof(labels));
                }
                k = parsed;
            }

            foreach (var concrete in ExpandConcreteNodeKinds(k))
            {
                var id = (int)concrete;
                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids.Count == 1
            ? $"{kindColumnSql} = {ids[0]}"
            : $"{kindColumnSql} IN ({string.Join(", ", ids)})";
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
