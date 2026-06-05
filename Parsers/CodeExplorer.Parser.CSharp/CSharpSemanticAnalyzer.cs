using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

public class CSharpSemanticAnalyzer : BaseSemanticAnalyzer
{
    private static readonly Dictionary<string, HashSet<string>> DbPackagesByKind = new(StringComparer.OrdinalIgnoreCase)
    {
        ["relational"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Dapper", "Dapper.FastCRUD", "DapperQueryBuilder",
            "Microsoft.EntityFrameworkCore", "EntityFrameworkCore.SqlServer.SimpleBulks", "EFCore.BulkExtensions", "EntityFramework.Exceptions",
            "NHibernate", "FluentNHibernate", "linq2db", "PetaPoco", "NPoco", "Insight.Database", "RepoDb",
            "SqlSugar", "FreeSql", "ServiceStack.OrmLite",
            "Npgsql", "System.Data.SqlClient", "Microsoft.Data.SqlClient", "MySqlConnector", "MySql.Data",
            "FirebirdSql.Data.FirebirdClient", "DuckDB.NET", "rqlite-dotnet", "Cassandra"
        },
        ["document"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "LiteDB", "Raven.Client.Documents", "RavenDB", "Marten", "MongoFramework", "MongoDB.Driver", "Couchbase.NetClient"
        },
        ["keyvalue"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "rocksdb-sharp", "RocksDB", "StackExchange.Redis", "ServiceStack.Redis"
        },
        ["search_analytic"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "ClickHouse.Client"
        }
    };

    protected override IReadOnlyDictionary<string, HashSet<string>> DbPackages => DbPackagesByKind;

    private static readonly HashSet<string> ApiPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Net.Http", "RestSharp", "Flurl", "Flurl.Http", "Refit", "WebApiClient", "Apizr", "NotoriousClient"
    };

    private static readonly HashSet<string> CloudPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Amazon.S3", "Stripe"
    };

    protected override bool IsApiPackage(string importPath)
    {
        return ApiPackages.Contains(importPath) || ApiPackages.Contains(Path.GetFileName(importPath));
    }

    protected override bool IsCloudPackage(string importPath)
    {
        return CloudPackages.Contains(importPath) || 
               CloudPackages.Contains(Path.GetFileName(importPath)) ||
               importPath.StartsWith("Google.Cloud.", StringComparison.OrdinalIgnoreCase) ||
               importPath.StartsWith("Azure.", StringComparison.OrdinalIgnoreCase);
    }
}
