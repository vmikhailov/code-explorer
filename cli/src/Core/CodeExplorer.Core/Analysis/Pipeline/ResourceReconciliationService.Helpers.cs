using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Analysis;

public partial class ResourceReconciliationService
{
    public static HashSet<string> KnownDbEnginesAndNames => ResourceNamingHelper.KnownDbEnginesAndNames;
    public static HashSet<string> AbstractEngineNames => ResourceNamingHelper.AbstractEngineNames;
    public static HashSet<string> SystemCatalogTables => ResourceNamingHelper.SystemCatalogTables;
    public static HashSet<string> KnownTableValuedFunctions => ResourceNamingHelper.KnownTableValuedFunctions;

    public static bool IsAbstractEngineOnlyDatabase(string? name, string? schema, string? dbType, string? id) =>
        ResourceNamingHelper.IsAbstractEngineOnlyDatabase(name, schema, dbType, id);

    public static bool IsValidTableIdentifier(string? name) =>
        ResourceNamingHelper.IsValidTableIdentifier(name);

    public static string CleanProjectNameToDomain(string projectName) =>
        ResourceNamingHelper.CleanProjectNameToDomain(projectName);
}
