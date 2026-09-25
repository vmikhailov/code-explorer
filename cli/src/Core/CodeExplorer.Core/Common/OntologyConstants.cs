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
        public const string ExternalService = "ExternalService";
        public const string GitSettings = "GitSettings";
        public const string ApiInUse = "ApiInUse";
        public const string Counter = "Counter";
    }

    public static class Layers
    {
        public const string Workspace = "";
        public const string Physical = "Layer 1: Physical Topology";
        public const string ProjectBoundary = "Layer 2: Project Boundary";
        public const string Syntactic = "Layer 3: Syntactic Structure";
        public const string Semantic = "Layer 4: Semantic Structure";
    }

    public static class Relationships
    {
        public const string Contains = "CONTAINS";
        public const string DependsOn = "DEPENDS_ON";
        public const string Calls = "CALLS";
        public const string UsesType = "USES_TYPE";
        public const string Implements = "IMPLEMENTS";
        public const string InheritsFrom = "INHERITS_FROM";
        public const string PotentialType = "POTENTIAL_TYPE";
        public const string ImplementedBy = "IMPLEMENTED_BY";
        public const string UsesDb = "USES_DB";
        public const string TransformsTo = "TRANSFORMS_TO";
        public const string PublishesTo = "PUBLISHES_TO";
        public const string Triggers = "TRIGGERS";
        public const string Exposes = "EXPOSES";
        public const string UsesGit = "USES_GIT";
        public const string UsesApi = "USES_API";
        public const string UsesCloud = "USES_CLOUD";
        public const string TransitivelyCalls = "TRANSITIVELY_CALLS";
        public const string AttributedTo = "ATTRIBUTED_TO";
        public const string Defines = "DEFINES";
        public const string Declares = "DECLARES";

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
    }

    public static class LibraryTypes
    {
        public const string Framework = "framework";
        public const string RelationalDb = "db:relational";
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
        public const string Test = "Test";
    }
}

