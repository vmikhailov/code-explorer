namespace CodeExplorer.Cypher.Common;

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
    Workspace = 27
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
    public static string ToCypherLabel(this NodeKind kind) => kind switch
    {
        NodeKind.Service => "Service",
        NodeKind.App => "App",
        NodeKind.Worker => "Worker",
        NodeKind.CliTool => "CliTool",
        NodeKind.Library => "Library",
        NodeKind.SharedLibrary => "SharedLibrary",
        NodeKind.Database => "Database",
        NodeKind.Topic => "Topic",
        NodeKind.ExternalService => "ExternalService",
        NodeKind.Project => "Project",
        NodeKind.Endpoint => "Endpoint",
        NodeKind.EntryPoint => "EntryPoint",
        NodeKind.Table => "Table",
        NodeKind.Query => "Query",
        NodeKind.Type => "Type",
        NodeKind.Function => "Function",
        NodeKind.Member => "Member",
        NodeKind.File => "File",
        NodeKind.Folder => "Folder",
        NodeKind.Package => "Package",
        NodeKind.DataSet => "DataSet",
        NodeKind.CloudService => "CloudService",
        NodeKind.ApiInUse => "ApiInUse",
        NodeKind.TestSuite => "TestSuite",
        NodeKind.Test => "Test",
        NodeKind.Procedure => "Procedure",
        NodeKind.Workspace => "Workspace",
        _ => kind.ToString()
    };

    public static NodeKind ParseNodeKind(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return NodeKind.Unspecified;
        if (Enum.TryParse<NodeKind>(label, ignoreCase: true, out var kind))
            return kind;
        return label.ToLowerInvariant() switch
        {
            "service" => NodeKind.Service,
            "app" or "frontendapp" or "ingress" => NodeKind.App,
            "worker" => NodeKind.Worker,
            "clitool" or "cli" => NodeKind.CliTool,
            "library" => NodeKind.Library,
            "sharedlibrary" => NodeKind.SharedLibrary,
            "database" or "db" => NodeKind.Database,
            "topic" or "queue" or "broker" => NodeKind.Topic,
            "externalservice" or "external" => NodeKind.ExternalService,
            "endpoint" => NodeKind.Endpoint,
            "entrypoint" => NodeKind.EntryPoint,
            "table" => NodeKind.Table,
            "query" => NodeKind.Query,
            "type" or "class" or "interface" => NodeKind.Type,
            "function" or "method" => NodeKind.Function,
            "member" or "field" or "property" => NodeKind.Member,
            "file" => NodeKind.File,
            "folder" => NodeKind.Folder,
            "package" => NodeKind.Package,
            "dataset" => NodeKind.DataSet,
            "cloudservice" => NodeKind.CloudService,
            "apiinuse" => NodeKind.ApiInUse,
            "testsuite" => NodeKind.TestSuite,
            "test" => NodeKind.Test,
            "procedure" => NodeKind.Procedure,
            "workspace" => NodeKind.Workspace,
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
        var normalized = relType.Replace("_", "").ToLowerInvariant();
        return normalized switch
        {
            "calls" => RelationshipKind.Calls,
            "dependson" => RelationshipKind.DependsOn,
            "servicecall" => RelationshipKind.ServiceCall,
            "usesdb" => RelationshipKind.UsesDb,
            "produces" => RelationshipKind.Produces,
            "consumes" => RelationshipKind.Consumes,
            "callsendpoint" => RelationshipKind.CallsEndpoint,
            "contains" => RelationshipKind.Contains,
            "implements" => RelationshipKind.Implements,
            "writesto" => RelationshipKind.WritesTo,
            "readsfrom" => RelationshipKind.ReadsFrom,
            "exposes" => RelationshipKind.Exposes,
            "attributedto" => RelationshipKind.AttributedTo,
            "configures" => RelationshipKind.Configures,
            _ => Enum.TryParse<RelationshipKind>(relType, ignoreCase: true, out var kind) ? kind : RelationshipKind.Unspecified
        };
    }

    public static ProjectRole ParseProjectRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return ProjectRole.Unspecified;
        return role.ToLowerInvariant() switch
        {
            "service" => ProjectRole.Service,
            "app" or "frontendapp" or "ingress" => ProjectRole.App,
            "worker" => ProjectRole.Worker,
            "clitool" or "cli" => ProjectRole.CliTool,
            "sharedlibrary" or "library" => ProjectRole.SharedLibrary,
            "test" => ProjectRole.Test,
            "general" => ProjectRole.General,
            _ => Enum.TryParse<ProjectRole>(role, ignoreCase: true, out var r) ? r : ProjectRole.Unspecified
        };
    }

    public static DatabaseType ParseDatabaseType(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType)) return DatabaseType.Unspecified;
        return dbType.ToLowerInvariant() switch
        {
            "relational" or "rdbms" or "sql" => DatabaseType.Relational,
            "document" or "nosql" or "mongodb" => DatabaseType.Document,
            "keyvalue" or "kv" => DatabaseType.KeyValue,
            "search" or "elasticsearch" => DatabaseType.Search,
            "cache" or "redis" or "memcached" => DatabaseType.Cache,
            "vector" => DatabaseType.Vector,
            "graph" => DatabaseType.Graph,
            _ => Enum.TryParse<DatabaseType>(dbType, ignoreCase: true, out var t) ? t : DatabaseType.Unspecified
        };
    }

    public static NetworkProtocol ParseNetworkProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol)) return NetworkProtocol.Unspecified;
        return protocol.ToLowerInvariant() switch
        {
            "http" => NetworkProtocol.Http,
            "https" => NetworkProtocol.Https,
            "grpc" => NetworkProtocol.Grpc,
            "ws" => NetworkProtocol.Ws,
            "wss" => NetworkProtocol.Wss,
            "amqp" => NetworkProtocol.Amqp,
            "kafka" => NetworkProtocol.Kafka,
            "soap" => NetworkProtocol.Soap,
            _ => Enum.TryParse<NetworkProtocol>(protocol, ignoreCase: true, out var p) ? p : NetworkProtocol.Unspecified
        };
    }

    public static ArchitectureViewType ParseArchitectureViewType(string? viewType)
    {
        if (string.IsNullOrWhiteSpace(viewType)) return ArchitectureViewType.Unspecified;
        var normalized = viewType.Replace("-", "").Replace("_", "").ToLowerInvariant();
        return normalized switch
        {
            "systemcontext" or "c1" => ArchitectureViewType.SystemContext,
            "domainarchitecture" or "domain" or "macro" => ArchitectureViewType.DomainArchitecture,
            "serviceflow" or "c2" => ArchitectureViewType.ServiceFlow,
            "boundedcontexts" or "context" => ArchitectureViewType.BoundedContexts,
            "tiered" or "layer" => ArchitectureViewType.Tiered,
            "nodegrid" or "grid" => ArchitectureViewType.NodeGrid,
            _ => Enum.TryParse<ArchitectureViewType>(viewType, ignoreCase: true, out var vt) ? vt : ArchitectureViewType.Unspecified
        };
    }
}
