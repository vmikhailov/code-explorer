namespace CodeExplorer.Core.Common;

public static class OntologyConstants
{
    public static class NodeLabels
    {
        public const string Workspace = "Workspace";
        public const string Folder = "Folder";
        public const string Project = "Project";
        public const string File = "File";
        public const string Type = "Type";
        public const string Function = "Function";
        public const string Member = "Member";
        public const string Package = "Package";
                public const string FilesStructure = "FilesStructure";
        public const string ProjectsStructure = "ProjectsStructure";
        public const string SyntaxStructure = "SyntaxStructure";
        public const string ProjectSyntax = "ProjectSyntax";
        public const string SemanticStructure = "SemanticStructure";
        public const string ProjectSemantic = "ProjectSemantic";
        public const string Service = "Service";
        public const string App = "App";
        public const string Library = "Library";
        public const string Worker = "Worker";
        public const string CliTool = "CliTool";
        public const string Database = "Database";
        public const string DataSet = "DataSet";
        public const string Table = "Table";
        public const string Procedure = "Procedure";
        public const string Query = "Query";
        public const string Topic = "Topic";
        public const string EntryPoint = "EntryPoint";
        public const string Endpoint = "Endpoint";
        public const string CloudService = "CloudService";
        public const string PeerMesh = "PeerMesh";
        public const string ExternalService = "ExternalService";
        public const string GitSettings = "GitSettings";
        public const string ApiInUse = "ApiInUse";
        public const string Counter = "Counter";
        public const string Domain = "Domain";
        public const string BoundedContext = "BoundedContext";
        public const string LogicalStructure = "LogicalStructure";
        public const string TestSuite = "TestSuite";
        public const string CallLiteral = "CallLiteral";
        public const string CatchClause = "CatchClause";
        public const string ScanIssue = "ScanIssue";
        public const string Parameter = "Parameter";
        public const string CallArgument = "CallArgument";
    }

    public static class IdPrefixes
    {
        public const string Workspace = "ws";
        public const string FilesStructure = "fs";
        public const string ProjectsStructure = "ps";
        public const string SyntaxStructure = "ss";
        public const string SemanticStructure = "sem";
        public const string LogicalStructure = "ls";
        public const string File = "f";
        public const string Folder = "dir";
        public const string Git = "git";
        public const string Project = "p";
        public const string Package = "pkg";
        public const string Symbol = "sym";
        public const string Service = "s";
        public const string App = "app";
        public const string Worker = "w";
        public const string CliTool = "cli";
        public const string Library = "lib";
        public const string Database = "db";
        public const string Table = "tbl";
        public const string Topic = "top";
        public const string Endpoint = "ep";
        public const string ExternalService = "es";
        public const string DataSet = "ds";
        public const string Procedure = "proc";
        public const string Query = "q";
        public const string EntryPoint = "entry";
        public const string CloudService = "cloud";
        public const string PeerMesh = "mesh";
        public const string ApiInUse = "api";
        public const string Domain = "dom";
        public const string BoundedContext = "bc";
        public const string TestSuite = "suite";
        public const string CallLiteral = "lit";
        public const string CatchClause = "catch";
        public const string ScanIssue = "issue";
        public const string Parameter = "param";
        public const string CallArgument = "arg";
    }

    public static class Layers
    {
        public const string Workspace = "";
        public const string Physical = "Layer 1: File Structure";
        public const string ProjectBoundary = "Layer 2: Project Structure";
        public const string Syntactic = "Layer 3: Syntax Structure";
        public const string Semantic = "Layer 4: Semantic Structure";
        public const string Logical = "Layer 5: Logical Structure";
    }

    public static class Relationships
    {
        public const string Contains = "CONTAINS";
        public const string DependsOn = "DEPENDS_ON";
        public const string Tests = "TESTS";
        public const string Calls = "CALLS";
        public const string UsesType = "USES_TYPE";
        public const string Implements = "IMPLEMENTS";
        public const string InheritsFrom = "INHERITS_FROM";
        public const string PotentialType = "POTENTIAL_TYPE";
        public const string ImplementedBy = "IMPLEMENTED_BY";
        public const string Produces = "PRODUCES";
        public const string UsesDb = "USES_DB";
        public const string TransformsTo = "TRANSFORMS_TO";
        public const string PublishesTo = "PUBLISHES_TO";
        public const string Triggers = "TRIGGERS";
        public const string Exposes = "EXPOSES";
        public const string UsesGit = "USES_GIT";
        public const string UsesApi = "USES_API";
        public const string UsesCloud = "USES_CLOUD";
        public const string CoordinatesWith = "COORDINATES_WITH";
        public const string TransitivelyCalls = "TRANSITIVELY_CALLS";
        public const string AttributedTo = "ATTRIBUTED_TO";
        public const string Defines = "DEFINES";
        public const string Declares = "DECLARES";
        public const string PassesLiteral = "PASSES_LITERAL";
        public const string Instantiates = "INSTANTIATES";

        // Transitive architectural macro-relationships (C4 Level)
        public const string IntegratesWith = "INTEGRATES_WITH";
        public const string ServiceCall = "SERVICE_CALL";
        public const string WritesTo = "WRITES_TO";
        public const string ReadsFrom = "READS_FROM";

        // New 5-layers ontology relationships
        public const string DeclaredIn = "DECLARED_IN";
        public const string DeclaresType = "DECLARES_TYPE";
        public const string HasMethod = "HAS_METHOD";
        public const string HasMember = "HAS_MEMBER";
        public const string HasVariable = "HAS_VARIABLE";
        public const string ExposesEndpoint = "EXPOSES_ENDPOINT";
        public const string CallsEndpoint = "CALLS_ENDPOINT";
        public const string QueriesDb = "QUERIES_DB";
        public const string SubscribesTo = "SUBSCRIBES_TO";
        public const string OfType = "OF_TYPE";

        public const string AccessedBy = "ACCESSED_BY";
        public const string CalledBy = "CALLED_BY";
        public const string QueriedBy = "QUERIED_BY";
        public const string PublishedBy = "PUBLISHED_BY";
        public const string SubscribedBy = "SUBSCRIBED_BY";
        public const string ExposedBy = "EXPOSED_BY";
        public const string BelongsTo = "BELONGS_TO";
        public const string LocatedIn = "LOCATED_IN";
        public const string PersistedIn = "PERSISTED_IN";
        public const string Configures = "CONFIGURES";
        public const string Deploys = "DEPLOYS";
        public const string DeployedBy = "DEPLOYED_BY";
        public const string BelongsToDomain = "BELONGS_TO_DOMAIN";
        public const string ExposesDomain = "EXPOSES_DOMAIN";
        public const string SubdomainOf = "SUBDOMAIN_OF";
        public const string Handles = "HANDLES";
        public const string HasEntrypoint = "HAS_ENTRYPOINT";
        public const string HasParameter = "HAS_PARAMETER";
        public const string PassesArgument = "PASSES_ARGUMENT";
        public const string FlowsTo = "FLOWS_TO";
        public const string TargetedBy = "TARGETED_BY";
    }

    public static class EvidenceKinds
    {
        public const string Endpoint = "endpoint";
        public const string EntryPoint = "entrypoint";
        public const string HttpCall = "http_call";
        public const string GrpcCall = "grpc_call";
        public const string WcfCall = "wcf_call";
        public const string Publish = "publish";
        public const string Subscribe = "subscribe";
        public const string Query = "query";
        public const string Procedure = "procedure";
        public const string Orm = "orm";
        public const string Table = "table";
        public const string Config = "config";
        public const string Migration = "migration";
        public const string LibraryCall = "library_call";
        public const string TestCall = "test_call";
        public const string TypeReference = "type_reference";
    }

    public static class EvidenceConfidence
    {
        public const string Recorded = "recorded";
        public const string Inferred = "inferred";
    }

    public static class LibraryTypes
    {
        public const string Framework = "framework";
        public const string RelationalDb = "db:relational";
        public const string AnalyticsDb = "db:analytics";
        public const string DocumentDb = "db:document";
        public const string GraphDb = "db:graph";
        public const string KeyValueDb = "db:kv";
        public const string TimeSeriesDb = "db:timeseries";
        public const string Cloud = "cloud";
        public const string Queue = "queue";
        public const string Http = "http";
    }

    public static class ProjectRoles
    {
        public const string Service = "Service";
        public const string SharedLibrary = "SharedLibrary";
        public const string FrontendApp = "FrontendApp";
        public const string Worker = "Worker";
        public const string DatabaseMigration = "DatabaseMigration";
        public const string CliTool = "CliTool";
        public const string ScriptProject = "ScriptProject";
        public const string Test = "Test";
    }

    public static class PropertyNames
    {
        public const string Id = "id";
        public const string Name = "name";
        public const string Kind = "kind";
        public const string SubKind = "sub_kind";
        public const string DefaultSubKind = "class";
        public const string Path = "path";
        public const string FilePath = "file_path";
        public const string SrcPath = "src_path";
        public const string TgtPath = "tgt_path";
        public const string Source = "source";
        public const string Target = "target";
        public const string Weight = "weight";
        public const string Count = "count";
        public const string Label = "label";
        public const string IsTest = "is_test";
        public const string FnId = "fn_id";
        public const string TypeName = "type_name";
        public const string ImplName = "impl_name";
        public const string IfaceName = "iface_name";
        public const string Route = "route";
        public const string Method = "method";
        public const string Tier = "tier";
        public const string TierLabel = "tier_label";
        public const string TierReason = "tier_reason";
        public const string CycleGroup = "cycle_group";
        public const string IsCircular = "is_circular";
        public const string ResourceKind = "resource_kind";
        public const string ProjectKind = "project_kind";
        public const string ProjectName = "project_name";
        public const string StageOrder = "stage_order";
        public const string Line = "line";
        public const string LineEnd = "line_end";
        public const string Column = "column";
        public const string Operation = "operation";
        public const string EvidenceKind = "evidence_kind";
        public const string EvidenceIds = "evidence_ids";
        public const string EvidenceCount = "evidence_count";
        public const string EvidenceTruncated = "evidence_truncated";
        public const string MessagingDirection = "messaging_direction";
        public const string BrokerType = "broker_type";
        public const string TypeKind = "type_kind";
        public const string MemberKind = "member_kind";
        public const string DataType = "data_type";
        public const string GraphqlField = "graphql_field";
        public const string GraphqlSchema = "graphql_schema";
        public const string Value = "value";
        public const string Callee = "callee";
        public const string ArgIndex = "arg_index";
        public const string Role = "role";
        public const string IsDecorator = "is_decorator";
        public const string ExceptionType = "exception_type";
        public const string IsEmpty = "is_empty";
        public const string HasComment = "has_comment";
        public const string StatementCount = "statement_count";
        public const string Extends = "extends";
        public const string Implements = "implements";
        public const string Attributes = "attributes";

        /// <summary>
        /// JSON object on a workload project mapping a dependency project directory (workspace-relative) to the
        /// workspace-relative source files of that dependency reachable from the workload's entry point.
        /// Dependencies absent from the map are treated as fully reachable.
        /// </summary>
        public const string SourceReachability = "source_reachability";
    }

    public static class ExtensionKeys
    {
        public const string PackageName = "package_name";
        public const string PackageVersion = "package_version";
        public const string Publisher = "publisher";
        public const string MainFile = "main_file";
        public const string ModuleFile = "module_file";
        public const string Description = "description";
        public const string HasBin = "has_bin";
        public const string HasContributes = "has_contributes";
        public const string HasVsCodeEngine = "has_vscode_engine";
        public const string KeywordsJson = "keywords_json";
        public const string ActivationEventsJson = "activation_events_json";
        public const string HasNativeNode = "has_native_node";
        public const string HasNativeAddon = "has_native_addon";
        public const string HasDotNetDll = "has_dotnet_dll";
        public const string IndexedAt = "indexed_at";
        public const string TrueValue = "true";
    }

    public static class ManifestFields
    {
        public const string Id = "id";
        public const string Name = "name";
        public const string Version = "version";
        public const string Publisher = "publisher";
        public const string Main = "main";
        public const string Module = "module";
        public const string Description = "description";
        public const string Bin = "bin";
        public const string Contributes = "contributes";
        public const string Engines = "engines";
        public const string VsCode = "vscode";
        public const string Keywords = "keywords";
        public const string ActivationEvents = "activationEvents";
        public const string Tasks = "tasks";
        public const string RunOptions = "runOptions";
        public const string RunOn = "runOn";
        public const string Label = "label";
        public const string Command = "command";
        public const string CompilerOptions = "compilerOptions";
        public const string Paths = "paths";
        public const string TasksJsonFileName = "tasks.json";
        public const string TasksJsonRelPath = ".vscode/tasks.json";
        public const string FolderOpenTaskType = "vscode-task-folder-open";
        public const string NuGetEcosystem = "nuget";
    }

    public static class Archetypes
    {
        public const string Unclassified = "Unclassified";
        public const string PureUtility = "PureUtility";
        public const string ClientUi = "ClientUi";
        public const string CliTool = "CliTool";
        public const string Framework = "Framework";
        public const string BackendService = "BackendService";
        public const string DatabaseDriver = "DatabaseDriver";
        public const string NativeAddon = "NativeAddon";
    }
}
