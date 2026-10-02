using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeExplorer.Cypher.Common;

[JsonConverter(typeof(NodeKindJsonConverter))]
public enum NodeKind
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
    Type = 15,
    Function = 16,
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
    Ingress = 30
}

public enum RelationshipKind
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
    Configures = 14
}

public enum ProjectRole
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

public enum DatabaseType
{
    Unspecified = 0,
    Relational = 1,
    Document = 2,
    KeyValue = 3,
    Search = 4,
    Cache = 5,
    Vector = 6,
    Graph = 7
}

public enum NetworkProtocol
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

public enum ArchitectureViewType
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
    public static string ToCypherLabel(this NodeKind kind) => kind.ToString();

    public static NodeKind ParseNodeKind(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return NodeKind.Unspecified;
        if (Enum.TryParse<NodeKind>(label, ignoreCase: true, out var kind))
            return kind;
        
        return label.ToLowerInvariant() switch
        {
            "frontendapp" => NodeKind.App,
            "cli" => NodeKind.CliTool,
            "db" => NodeKind.Database,
            "queue" or "broker" => NodeKind.Topic,
            "external" => NodeKind.ExternalService,
            "class" or "interface" => NodeKind.Type,
            "method" => NodeKind.Function,
            "field" or "property" => NodeKind.Member,
            "bounded_context" or "context" => NodeKind.BoundedContext,
            _ => NodeKind.Unspecified
        };
    }

    public static string ToCypherType(this RelationshipKind kind) => kind switch
    {
        RelationshipKind.Calls => "CALLS",
        RelationshipKind.DependsOn => "DEPENDS_ON",
        RelationshipKind.ServiceCall => "SERVICE_CALL",
        RelationshipKind.UsesDb => "USES_DB",
        RelationshipKind.Produces => "PRODUCES",
        RelationshipKind.Consumes => "CONSUMES",
        RelationshipKind.CallsEndpoint => "CALLS_ENDPOINT",
        RelationshipKind.Contains => "CONTAINS",
        RelationshipKind.Implements => "IMPLEMENTS",
        RelationshipKind.WritesTo => "WRITES_TO",
        RelationshipKind.ReadsFrom => "READS_FROM",
        RelationshipKind.Exposes => "EXPOSES",
        RelationshipKind.AttributedTo => "ATTRIBUTED_TO",
        RelationshipKind.Configures => "CONFIGURES",
        _ => kind.ToString().ToUpperInvariant()
    };

    public static RelationshipKind ParseRelationshipKind(string? relType)
    {
        if (string.IsNullOrWhiteSpace(relType)) return RelationshipKind.Unspecified;
        return Enum.TryParse<RelationshipKind>(relType.Replace("_", ""), ignoreCase: true, out var kind)
            ? kind
            : RelationshipKind.Unspecified;
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
        if (Enum.TryParse<DatabaseType>(dbType, ignoreCase: true, out var t))
            return t;
        return dbType.ToLowerInvariant() switch
        {
            "rdbms" or "sql" => DatabaseType.Relational,
            "nosql" or "mongodb" => DatabaseType.Document,
            "kv" => DatabaseType.KeyValue,
            "elasticsearch" => DatabaseType.Search,
            "redis" or "memcached" => DatabaseType.Cache,
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
