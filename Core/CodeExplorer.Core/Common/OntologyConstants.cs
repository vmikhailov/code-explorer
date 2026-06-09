namespace CodeExplorer.Core.Common;

public static class OntologyConstants
{
    public static class NodeLabels
    {
        public const string Workspace = "Workspace";
        public const string WorkspaceFolder = "WorkspaceFolder";
        public const string ProjectFolder = "ProjectFolder";
        public const string Project = "Project";
        public const string File = "File";
        public const string Class = "Class";
        public const string Interface = "Interface";
        public const string Function = "Function";
        public const string Variable = "Variable";
        public const string Package = "Package";
        public const string Dependencies = "Dependencies";
        public const string EntryPoints = "EntryPoints";
        public const string Files = "Files";
        public const string DataBases = "DataBases";
        public const string ApisInUse = "ApisInUse";
        public const string CloudServices = "CloudServices";
        public const string DB = "DB";
        public const string DataSet = "DataSet";
        public const string Table = "Table";
        public const string Procedure = "Procedure";
        public const string Query = "Query";
        public const string Queue = "Queue";
        public const string EntryPoint = "EntryPoint";
        public const string CloudService = "CloudService";
        public const string ExternalService = "ExternalService";
        public const string GitSettings = "GitSettings";
        public const string ApiInUse = "ApiInUse";
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
    }
}
