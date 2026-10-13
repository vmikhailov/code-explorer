namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Authoritative architectural category of an inter-component dependency or relationship.
/// </summary>
public enum DependencyCategory
{
    UnidentifiedDependency = 0,
    Library = 1,
    ServiceCall = 2,
    Database = 3,
    Messaging = 4,
    Configuration = 5
}

/// <summary>
/// Canonical string mapping and parsing utilities for <see cref="DependencyCategory"/>.
/// </summary>
public static class DependencyCategoryExtensions
{
    public const string UnidentifiedDependency = "unidentified_dependency";
    public const string Library = "library";
    public const string ServiceCall = "service_call";
    public const string Database = "database";
    public const string Messaging = "messaging";
    public const string Configuration = "configuration";

    public static string ToCategoryString(this DependencyCategory category) => category switch
    {
        DependencyCategory.Library => Library,
        DependencyCategory.ServiceCall => ServiceCall,
        DependencyCategory.Database => Database,
        DependencyCategory.Messaging => Messaging,
        DependencyCategory.Configuration => Configuration,
        _ => UnidentifiedDependency
    };

    public static DependencyCategory ParseCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DependencyCategory.UnidentifiedDependency;
        return value.ToLowerInvariant() switch
        {
            Library => DependencyCategory.Library,
            ServiceCall => DependencyCategory.ServiceCall,
            Database => DependencyCategory.Database,
            Messaging => DependencyCategory.Messaging,
            Configuration => DependencyCategory.Configuration,
            _ => DependencyCategory.UnidentifiedDependency
        };
    }
}
