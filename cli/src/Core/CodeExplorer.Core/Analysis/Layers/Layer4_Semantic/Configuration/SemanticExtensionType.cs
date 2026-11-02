namespace CodeExplorer.Core.Parser;

/// <summary>
/// Strongly-typed classification of semantic extensions, libraries, frameworks, and resource adapters.
/// Replaces legacy stringly-typed "db:relational", "framework", "queue", etc.
/// </summary>
public enum SemanticExtensionType
{
    Framework = 0,
    Library = 1,
    RelationalDb = 2,
    DocumentDb = 3,
    KeyValueDb = 4,
    AnalyticsDb = 5,
    SearchDb = 6,
    VectorDb = 7,
    GraphDb = 8,
    TimeSeriesDb = 9,
    Queue = 10,
    Cloud = 11,
    HttpClient = 12,
    ApiClient = 13,
    GrpcFramework = 14,
    GraphqlFramework = 15
}

public static class SemanticExtensionTypeExtensions
{
    public static bool IsDatabase(this SemanticExtensionType type) => type is
        SemanticExtensionType.RelationalDb or
        SemanticExtensionType.DocumentDb or
        SemanticExtensionType.KeyValueDb or
        SemanticExtensionType.AnalyticsDb or
        SemanticExtensionType.SearchDb or
        SemanticExtensionType.VectorDb or
        SemanticExtensionType.GraphDb or
        SemanticExtensionType.TimeSeriesDb;

    public static string ToLegacyCategoryString(this SemanticExtensionType type) => type switch
    {
        SemanticExtensionType.Framework => "framework",
        SemanticExtensionType.Library => "library",
        SemanticExtensionType.RelationalDb => "db:relational",
        SemanticExtensionType.DocumentDb => "db:document",
        SemanticExtensionType.KeyValueDb => "db:keyvalue",
        SemanticExtensionType.AnalyticsDb => "db:analytics",
        SemanticExtensionType.SearchDb => "db:search",
        SemanticExtensionType.VectorDb => "db:vector",
        SemanticExtensionType.GraphDb => "db:graph",
        SemanticExtensionType.TimeSeriesDb => "db:timeseries",
        SemanticExtensionType.Queue => "queue",
        SemanticExtensionType.Cloud => "cloud",
        SemanticExtensionType.HttpClient => "http-client",
        SemanticExtensionType.ApiClient => "api",
        SemanticExtensionType.GrpcFramework => "framework:grpc",
        SemanticExtensionType.GraphqlFramework => "framework:graphql",
        _ => "library"
    };
}
