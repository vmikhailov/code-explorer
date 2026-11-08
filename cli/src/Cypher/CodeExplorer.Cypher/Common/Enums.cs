using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeExplorer.Cypher.Common;

[JsonConverter(typeof(NodeKindJsonConverter))]
public enum NodeKind : byte
{
    Unspecified = 0,
    Service = 1,
    App = 2,
    Worker = 3,
    CliTool = 4,
    Library = 5,
    SharedLibrary = 6,
    Database = 7,
    Topic = 8,
    ExternalService = 9,
    Project = 10,
    Endpoint = 11,
    EntryPoint = 12,
    Table = 13,
    Query = 14,
    Function = 15,
    Type = 16,
    Member = 17,
    File = 18,
    Folder = 19,
    Package = 20,
    DataSet = 21,
    CloudService = 22,
    ApiInUse = 23,
    TestSuite = 24,
    Test = 25,
    Procedure = 26,
    Workspace = 27,
    Domain = 28,
    BoundedContext = 29,
    Ingress = 30,
    PeerMesh = 31,
    ScanIssue = 32,
    FilesStructure = 33,
    ProjectsStructure = 34,
    SyntaxStructure = 35,
    ProjectSyntax = 36,
    SemanticStructure = 37,
    ProjectSemantic = 38,
    LogicalStructure = 39,
    CallLiteral = 40,
    CatchClause = 41,
    Counter = 42,
    GitSettings = 43
}

[JsonConverter(typeof(RelationshipKindJsonConverter))]
public enum RelationshipKind : byte
{
    Unspecified = 0,
    Calls = 1,
    DependsOn = 2,
    ServiceCall = 3,
    UsesDb = 4,
    Produces = 5,
    Consumes = 6,
    CallsEndpoint = 7,
    Contains = 8,
    Implements = 9,
    WritesTo = 10,
    ReadsFrom = 11,
    Exposes = 12,
    AttributedTo = 13,
    Configures = 14,
    CoordinatesWith = 15,
    PublishesTo = 16,
    SubscribedBy = 17,
    ImplementedBy = 18,
    ExposedBy = 19,
    HasMember = 20,
    HasMethod = 21,
    HasVariable = 22,
    HasEntrypoint = 23,
    DeclaresType = 24,
    UsesType = 25,
    OfType = 26,
    QueriedBy = 27,
    PersistedIn = 28,
    TransformsTo = 29,
    LocatedIn = 30,
    UsesCloud = 31,
    UsesGit = 32,
    Triggers = 33,
    Tests = 34,
    UsesApi = 35,
    TransitivelyCalls = 36,
    Defines = 37,
    Declares = 38,
    PassesLiteral = 39,
    Instantiates = 40,
    IntegratesWith = 41,
    DeclaredIn = 42,
    QueriesDb = 43,
    SubscribesTo = 44,
    AccessedBy = 45,
    CalledBy = 46,
    PublishedBy = 47,
    BelongsTo = 48,
    Deploys = 49,
    DeployedBy = 50,
    BelongsToDomain = 51,
    ExposesDomain = 52,
    SubdomainOf = 53,
    Handles = 54,
    InheritsFrom = 55,
    PotentialType = 56,
    ExposesEndpoint = 57,
    Inherits = 58,
    AnnotatedWith = 59,
    Uses = 60,
    Publishes = 61,
    Subscribes = 62,
    Library = 63,
    AccessesTable = 64,
    Related = 65
}

/// <summary>
/// High-performance, zero-allocation pre-compiled FrozenDictionary lookup maps
/// for bidirectional mapping between string labels/types and strongly-typed 1-byte enums.
/// </summary>
public static class OntologyEnumMaps
{
    public static readonly FrozenDictionary<string, NodeKind> NodeKindByName;
    public static readonly FrozenDictionary<NodeKind, string> NameByNodeKind;
    public static readonly FrozenDictionary<string, RelationshipKind> RelKindByName;
    public static readonly FrozenDictionary<RelationshipKind, string> NameByRelKind;

    static OntologyEnumMaps()
    {
        var nodeByName = new Dictionary<string, NodeKind>(StringComparer.OrdinalIgnoreCase);
        var nameByNode = new Dictionary<NodeKind, string>();

        foreach (var val in Enum.GetValues<NodeKind>())
        {
            var name = val.ToString();
            nodeByName[name] = val;
            if (val != NodeKind.Unspecified && !nameByNode.ContainsKey(val))
            {
                nameByNode[val] = name;
            }
        }

        // Aliases
        nodeByName["frontendapp"] = NodeKind.App;
        nodeByName["databasemigration"] = NodeKind.Worker;
        nodeByName["database_migration"] = NodeKind.Worker;
        nodeByName["functionapp"] = NodeKind.Service;
        nodeByName["function_app"] = NodeKind.Service;
        nodeByName["cli"] = NodeKind.CliTool;
        nodeByName["db"] = NodeKind.Database;
        nodeByName["queue"] = NodeKind.Topic;
        nodeByName["broker"] = NodeKind.Topic;
        nodeByName["external"] = NodeKind.ExternalService;
        nodeByName["class"] = NodeKind.Type;
        nodeByName["interface"] = NodeKind.Type;
        nodeByName["struct"] = NodeKind.Type;
        nodeByName["record"] = NodeKind.Type;
        nodeByName["enum"] = NodeKind.Type;
        nodeByName["method"] = NodeKind.Function;
        nodeByName["field"] = NodeKind.Member;
        nodeByName["property"] = NodeKind.Member;
        nodeByName["context"] = NodeKind.BoundedContext;
        nodeByName["bounded_context"] = NodeKind.BoundedContext;
        nodeByName["scan_issue"] = NodeKind.ScanIssue;
        nodeByName["parse_issue"] = NodeKind.ScanIssue;
        nodeByName["parseissue"] = NodeKind.ScanIssue;

        NodeKindByName = nodeByName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        NameByNodeKind = nameByNode.ToFrozenDictionary();

        var relByName = new Dictionary<string, RelationshipKind>(StringComparer.OrdinalIgnoreCase);
        var nameByRel = new Dictionary<RelationshipKind, string>();

        foreach (var val in Enum.GetValues<RelationshipKind>())
        {
            var rawName = val.ToString();
            var cypherName = ToUpperUnderscore(rawName);
            relByName[rawName] = val;
            relByName[cypherName] = val;
            relByName[rawName.ToLowerInvariant()] = val;
            relByName[cypherName.ToLowerInvariant()] = val;
            relByName[cypherName.Replace("_", "")] = val;

            if (val != RelationshipKind.Unspecified && !nameByRel.ContainsKey(val))
            {
                nameByRel[val] = cypherName;
            }
        }

        // Relationship aliases
        relByName["ASYNC_CALL"] = RelationshipKind.Calls;
        relByName["asynccall"] = RelationshipKind.Calls;
        relByName["USES_DATABASE"] = RelationshipKind.UsesDb;
        relByName["usesdatabase"] = RelationshipKind.UsesDb;
        relByName["CALLS_SERVICE"] = RelationshipKind.ServiceCall;
        relByName["callsservice"] = RelationshipKind.ServiceCall;
        relByName["HAS_PROPERTY"] = RelationshipKind.HasMember;
        relByName["hasproperty"] = RelationshipKind.HasMember;
        relByName["HAS_FIELD"] = RelationshipKind.HasMember;
        relByName["hasfield"] = RelationshipKind.HasMember;
        relByName["EXTENDS"] = RelationshipKind.InheritsFrom;
        relByName["extends"] = RelationshipKind.InheritsFrom;

        RelKindByName = relByName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        NameByRelKind = nameByRel.ToFrozenDictionary();
    }

    private static string ToUpperUnderscore(string camelOrPascal)
    {
        if (string.IsNullOrEmpty(camelOrPascal)) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < camelOrPascal.Length; i++)
        {
            var c = camelOrPascal[i];
            if (char.IsUpper(c) && i > 0 && !char.IsUpper(camelOrPascal[i - 1]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }
}

public enum ProjectRole : byte
{
    Unspecified = 0,
    Service = 1,
    App = 2,
    Worker = 3,
    CliTool = 4,
    SharedLibrary = 5,
    Test = 6,
    General = 7
}

[JsonConverter(typeof(DatabaseTypeJsonConverter))]
public enum DatabaseType : byte
{
    Unspecified = 0,
    Relational = 1,
    Document = 2,
    KeyValue = 3,
    Search = 4,
    Cache = 5,
    Vector = 6,
    Graph = 7,
    Analytics = 8,
    TimeSeries = 9,
    Columnar = 10,
    MultiModel = 11,
    Unknown = 12
}

public enum NetworkProtocol : byte
{
    Unspecified = 0,
    Http = 1,
    Https = 2,
    Grpc = 3,
    Ws = 4,
    Wss = 5,
    Amqp = 6,
    Kafka = 7,
    Soap = 8
}

public enum ArchitectureViewType : byte
{
    Unspecified = 0,
    SystemContext = 1,
    DomainArchitecture = 2,
    ServiceFlow = 3,
    BoundedContexts = 4,
    Tiered = 5,
    NodeGrid = 6
}

public static class EnumExtensions
{
    public static string ToCypherLabel(this NodeKind kind) =>
        OntologyEnumMaps.NameByNodeKind.GetValueOrDefault(kind, kind.ToString());

    public static NodeKind ParseNodeKind(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return NodeKind.Unspecified;
        return OntologyEnumMaps.NodeKindByName.GetValueOrDefault(label.Trim(), NodeKind.Unspecified);
    }

    public static string ToCypherType(this RelationshipKind kind) =>
        OntologyEnumMaps.NameByRelKind.GetValueOrDefault(kind, kind.ToString().ToUpperInvariant());

    public static RelationshipKind ParseRelationshipKind(string? relType)
    {
        if (string.IsNullOrWhiteSpace(relType)) return RelationshipKind.Unspecified;
        var trimmed = relType.Trim();
        if (OntologyEnumMaps.RelKindByName.TryGetValue(trimmed, out var kind))
            return kind;
        var normalized = trimmed.Replace("_", "");
        return OntologyEnumMaps.RelKindByName.GetValueOrDefault(normalized, RelationshipKind.Unspecified);
    }

    public static ProjectRole ParseProjectRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return ProjectRole.Unspecified;
        if (Enum.TryParse<ProjectRole>(role, ignoreCase: true, out var r))
            return r;
        return role.ToLowerInvariant() switch
        {
            "frontendapp" or "ingress" => ProjectRole.App,
            "cli" => ProjectRole.CliTool,
            "library" => ProjectRole.SharedLibrary,
            _ => ProjectRole.Unspecified
        };
    }

    public static DatabaseType ParseDatabaseType(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType)) return DatabaseType.Unspecified;
        var trimmed = dbType.Trim().Replace("_", "").Replace("-", "");
        if (Enum.TryParse<DatabaseType>(trimmed, ignoreCase: true, out var t))
            return t;
        return trimmed.ToLowerInvariant() switch
        {
            "rdbms" or "sql" or "postgres" or "postgresql" or "sqlserver" or "mssql" or "mysql" or "sqlite" => DatabaseType.Relational,
            "nosql" or "mongodb" => DatabaseType.Document,
            "kv" => DatabaseType.KeyValue,
            "elasticsearch" => DatabaseType.Search,
            "redis" or "memcached" => DatabaseType.Cache,
            "neo4j" => DatabaseType.Graph,
            _ => DatabaseType.Unspecified
        };
    }

    public static NetworkProtocol ParseNetworkProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol)) return NetworkProtocol.Unspecified;
        return Enum.TryParse<NetworkProtocol>(protocol, ignoreCase: true, out var p)
            ? p
            : NetworkProtocol.Unspecified;
    }

    public static ArchitectureViewType ParseArchitectureViewType(string? viewType)
    {
        if (string.IsNullOrWhiteSpace(viewType)) return ArchitectureViewType.Unspecified;
        var normalized = viewType.Replace("-", "").Replace("_", "");
        if (Enum.TryParse<ArchitectureViewType>(normalized, ignoreCase: true, out var vt))
            return vt;
        return normalized.ToLowerInvariant() switch
        {
            "c1" => ArchitectureViewType.SystemContext,
            "domain" or "macro" => ArchitectureViewType.DomainArchitecture,
            "c2" => ArchitectureViewType.ServiceFlow,
            "context" => ArchitectureViewType.BoundedContexts,
            "layer" => ArchitectureViewType.Tiered,
            "grid" => ArchitectureViewType.NodeGrid,
            _ => ArchitectureViewType.Unspecified
        };
    }
}

public class NodeKindJsonConverter : JsonConverter<NodeKind>
{
    public override NodeKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return EnumExtensions.ParseNodeKind(reader.GetString());
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            return (NodeKind)reader.GetInt32();
        }
        return NodeKind.Unspecified;
    }

    public override void Write(Utf8JsonWriter writer, NodeKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToCypherLabel());
    }
}
