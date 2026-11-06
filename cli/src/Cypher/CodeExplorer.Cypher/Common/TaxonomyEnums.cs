using System.Text.Json.Serialization;

namespace CodeExplorer.Cypher.Common;

[JsonConverter(typeof(MemberKindJsonConverter))]
public enum MemberKind : byte
{
    Unspecified = 0,
    Field = 1,
    Property = 2,
    Parameter = 3,
    Variable = 4,
    Constant = 5,
    Event = 6,
    EnumMember = 7,
    Indexer = 8,
    Method = 9,
    Constructor = 10
}

[JsonConverter(typeof(TypeKindJsonConverter))]
public enum TypeKind : byte
{
    Unspecified = 0,
    Class = 1,
    Interface = 2,
    Struct = 3,
    Record = 4,
    Enum = 5,
    Delegate = 6,
    Type = 7,
    Module = 8,
    Trait = 9,
    Union = 10,
    Object = 11,
    Annotation = 12,
    Protocol = 13,
    Component = 14,
    Namespace = 15
}

[JsonConverter(typeof(BrokerTypeJsonConverter))]
public enum BrokerType : byte
{
    Unspecified = 0,
    RabbitMq = 1,
    Kafka = 2,
    Sqs = 3,
    Sns = 4,
    ServiceBus = 5,
    AzureServiceBus = 6,
    Nats = 7,
    Redis = 8,
    InMemory = 9,
    EventGrid = 10,
    PubSub = 11,
    GcpPubSub = 12,
    MediatR = 13,
    MassTransit = 14,
    EventBus = 15,
    Channel = 16,
    ActiveMq = 17,
    Pulsar = 18,
    ZeroMq = 19,
    Mqtt = 20,
    Event = 21,
    Amqp = 22,
    Generic = 23,
    Spring = 24
}

[JsonConverter(typeof(EntryPointTypeJsonConverter))]
public enum EntryPointType : byte
{
    Unspecified = 0,
    Main = 1,
    MainMethod = 2,
    TopLevel = 3,
    ScriptMain = 4,
    BundleEntry = 5,
    WorkerEntry = 6,
    CliEntry = 7,
    ServerlessHandler = 8,
    WebHost = 9,
    TestRunner = 10,
    HostedService = 11,
    PackageExport = 12,
    LifecycleHook = 13,
    BuildHook = 14,
    FolderOpenTask = 15,
    Grpc = 16,
    QueueListener = 17,
    Cron = 18,
    Activity = 19,
    Provider = 20,
    Channel = 21,
    Receiver = 22,
    Service = 23,
    Cli = 24,
    Worker = 25,
    Http = 26,
    WebSocket = 27,
    GraphQl = 28,
    ScheduledJob = 29,
    UiMount = 30,
    App = 31,
    React = 32,
    Vue = 33,
    Angular = 34,
    Svelte = 35,
    Vite = 36,
    NextJs = 37,
    Nuxt = 38,
    Remix = 39,
    Astro = 40,
    Electron = 41,
    Subprocess = 42,
    Test = 43,
    Benchmark = 44,
    Migration = 45
}

[JsonConverter(typeof(DomainTypeJsonConverter))]
public enum DomainType : byte
{
    Unspecified = 0,
    Core = 1,
    Supporting = 2,
    Generic = 3,
    Shared = 4,
    External = 5
}

[JsonConverter(typeof(CloudResourceTypeJsonConverter))]
public enum CloudResourceType : byte
{
    Unspecified = 0,
    CloudService = 1,
    S3Bucket = 2,
    BlobStorage = 3,
    GcsBucket = 4,
    DynamoDb = 5,
    CosmosDb = 6,
    Firestore = 7,
    Lambda = 8,
    AzureFunction = 9,
    CloudFunction = 10,
    SecretsManager = 11,
    SecretManager = 12,
    KeyVault = 13,
    ParameterStore = 14,
    Cognito = 15,
    Auth0 = 16,
    Firebase = 17,
    Stripe = 18,
    Twilio = 19,
    SendGrid = 20,
    Ses = 21,
    Sns = 22,
    Sqs = 23,
    EventBridge = 24,
    StepFunctions = 25,
    CloudFront = 26,
    ApiGateway = 27,
    Kms = 28,
    KmsKeySet = 29,
    Iam = 30
}

[JsonConverter(typeof(OperationTypeJsonConverter))]
public enum OperationType : byte
{
    Unspecified = 0,
    Query = 1,
    Mutation = 2,
    Subscription = 3,
    Unary = 4,
    ClientStreaming = 5,
    ServerStreaming = 6,
    DuplexStreaming = 7,
    Command = 8,
    Event = 9,
    Read = 10,
    Write = 11,
    ReadWrite = 12,
    Orchestration = 13,
    Validation = 14,
    Transformation = 15,
    Configuration = 16,
    Rest = 17,
    Rpc = 18,
    WebSocket = 19,
    Compute = 20,
    Auth = 21,
    Integration = 22,
    Notification = 23,
    Scheduling = 24,
    Lifecycle = 25,
    Testing = 26,
    Unknown = 27,
    BidirectionalStreaming = 28,
    Grain = 29,
    Wcf = 30
}

[JsonConverter(typeof(ScanIssueCategoryJsonConverter))]
public enum ScanIssueCategory : byte
{
    Unspecified = 0,
    SyntaxError = 1,
    ParseException = 2,
    VisitorException = 3,
    ManifestError = 4,
    IoError = 5,
    ReferenceError = 6,
    SemanticError = 7,
    ConfigurationError = 8
}

[JsonConverter(typeof(ProjectLanguageJsonConverter))]
public enum ProjectLanguage : byte
{
    Unspecified = 0,
    CSharp = 1,
    TypeScript = 2,
    JavaScript = 3,
    Python = 4,
    Go = 5,
    Rust = 6,
    Java = 7,
    Kotlin = 8,
    Sql = 9,
    ColdFusion = 10,
    Script = 11,
    C = 12,
    Cpp = 13,
    Ruby = 14,
    Php = 15,
    Swift = 16,
    Dart = 17,
    Scala = 18,
    Elixir = 19,
    GraphQl = 20,
    Protobuf = 21,
    Html = 22,
    Css = 23,
    Json = 24,
    Yaml = 25,
    Toml = 26,
    Xml = 27,
    Markdown = 28,
    Dockerfile = 29,
    Terraform = 30,
    Batch = 31,
    Shell = 32,
    PowerShell = 33,
    Android = 34,
    MultiLanguage = 35,
    Unknown = 36
}

[JsonConverter(typeof(ProjectArchetypeJsonConverter))]
public enum ProjectArchetype : byte
{
    Unspecified = 0,
    Unclassified = 1,
    PureUtility = 2,
    ClientUi = 3,
    CliTool = 4,
    Framework = 5,
    BackendService = 6,
    DatabaseDriver = 7,
    NativeAddon = 8,
    Service = 9,
    SharedLibrary = 10,
    FrontendApp = 11,
    Worker = 12,
    DatabaseMigration = 13,
    ScriptProject = 14,
    Test = 15,
    App = 16,
    Library = 17
}

[JsonConverter(typeof(SyntacticSymbolKindJsonConverter))]
public enum SyntacticSymbolKind : byte
{
    Unspecified = 0,
    File = 1,
    Type = 2,
    Class = 3,
    Interface = 4,
    Struct = 5,
    Record = 6,
    Enum = 7,
    Delegate = 8,
    Trait = 9,
    Union = 10,
    Object = 11,
    Module = 12,
    Namespace = 13,
    Package = 14,
    Function = 15,
    Method = 16,
    Constructor = 17,
    Member = 18,
    Property = 19,
    Field = 20,
    Parameter = 21,
    Variable = 22,
    Constant = 23,
    Event = 24,
    EnumMember = 25,
    Endpoint = 26,
    EntryPoint = 27,
    Table = 28,
    Query = 29,
    Procedure = 30,
    Database = 31,
    Topic = 32,
    ExternalService = 33,
    CloudService = 34,
    CallLiteral = 35,
    CatchClause = 36,
    Import = 37,
    Export = 38,
    Attribute = 39,
    Component = 40,
    Hook = 41,
    Unknown = 42
}

public enum BranchKind : byte
{
    Unspecified = 0,
    Break = 1,
    Continue = 2,
    Goto = 3,
    Return = 4,
    Yield = 5,
    Throw = 6
}

public enum SyntaxDiagnosticKind : byte
{
    Unspecified = 0,
    SyntaxError = 1,
    MissingToken = 2,
    Warning = 3
}

[JsonConverter(typeof(AppTypeJsonConverter))]
public enum AppType : byte
{
    Unspecified = 0,
    Spa = 1,
    Ssr = 2,
    Static = 3,
    Mobile = 4,
    Desktop = 5,
    Cli = 6,
    Web = 7,
    Hybrid = 8
}

[JsonConverter(typeof(ExternalServiceCategoryJsonConverter))]
public enum ExternalServiceCategory : byte
{
    Unspecified = 0,
    Payment = 1,
    Auth = 2,
    Identity = 3,
    Messaging = 4,
    Email = 5,
    Sms = 6,
    Storage = 7,
    Analytics = 8,
    Monitoring = 9,
    Ai = 10,
    Search = 11,
    Crm = 12,
    Social = 13,
    Cloud = 14,
    HttpApi = 15,
    GrpcService = 16,
    Webhook = 17,
    External = 18,
    General = 19
}

[JsonConverter(typeof(ProjectMapModuleTypeJsonConverter))]
public enum ProjectMapModuleType : byte
{
    Unspecified = 0,
    Module = 1,
    SubModule = 2,
    File = 3,
    Class = 4,
    EntryPoint = 5,
    Endpoint = 6,
    SubProject = 7,
    Resource = 8,
    Database = 9
}

[JsonConverter(typeof(BuiltInQueryCategoryJsonConverter))]
public enum BuiltInQueryCategory : byte
{
    Unspecified = 0,
    Architecture = 1,
    Refactoring = 2,
    DataLineage = 3,
    Security = 4,
    Testing = 5,
    General = 6,
    Symbols = 7,
    Taxonomy = 8
}

[JsonConverter(typeof(QueryParameterTypeJsonConverter))]
public enum QueryParameterType : byte
{
    Unspecified = 0,
    String = 1,
    Int = 2,
    Integer = 3,
    Number = 4,
    Float = 5,
    Double = 6,
    Bool = 7,
    Boolean = 8,
    Array = 9,
    Object = 10
}

[JsonConverter(typeof(BoundedContextInteractionTypeJsonConverter))]
public enum BoundedContextInteractionType : byte
{
    Unspecified = 0,
    Call = 1,
    Event = 2,
    Database = 3,
    Shared = 4,
    Http = 5,
    Grpc = 6,
    Message = 7,
    Messaging = 8,
    ServiceCall = 9
}

[JsonConverter(typeof(GraphEdgeCategoryJsonConverter))]
public enum GraphEdgeCategory : byte
{
    Unspecified = 0,
    Architecture = 1,
    Syntactic = 2,
    Semantic = 3,
    DataFlow = 4,
    ControlFlow = 5,
    Dependency = 6,
    Messaging = 7,
    Database = 8,
    Call = 9,
    Containment = 10,
    Inheritance = 11,
    Testing = 12,
    Logical = 13,
    ServiceCall = 14,
    Library = 15,
    Resource = 16,
    Test = 17,
    External = 18,
    Configuration = 19,
    UnidentifiedDependency = 20,
    Event = 21,
    Shared = 22,
    Http = 23,
    Grpc = 24,
    Message = 25
}

[JsonConverter(typeof(EvidenceKindJsonConverter))]
public enum EvidenceKind : byte
{
    Unspecified = 0,
    Endpoint = 1,
    EntryPoint = 2,
    HttpCall = 3,
    GrpcCall = 4,
    WcfCall = 5,
    Publish = 6,
    Subscribe = 7,
    Query = 8,
    Procedure = 9,
    Orm = 10,
    Table = 11,
    Config = 12,
    Migration = 13,
    LibraryCall = 14,
    TestCall = 15,
    TypeReference = 16
}
