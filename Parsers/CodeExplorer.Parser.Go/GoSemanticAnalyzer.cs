using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Go;

public class GoSemanticAnalyzer : BaseSemanticAnalyzer
{
    private static readonly Dictionary<string, HashSet<string>> DbPackagesByKind = new(StringComparer.OrdinalIgnoreCase)
    {
        ["relational"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "database/sql", "github.com/lib/pq", "github.com/go-sql-driver/mysql", "github.com/jackc/pgx", "gorm.io/gorm",
            "github.com/mattn/go-sqlite3", "github.com/pressly/goose", "github.com/amacneil/dbmate", "github.com/Masterminds/squirrel", 
            "github.com/doug-martin/goqu", "github.com/uptrace/bun", "github.com/go-gormigrate/gormigrate"
        },
        ["document"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "go.mongodb.org/mongo-driver"
        },
        ["keyvalue"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "github.com/redis/go-redis", "github.com/gomodule/redigo", "github.com/dgraph-io/badger", "github.com/etcd-io/bbolt", 
            "github.com/syndtr/goleveldb"
        },
        ["search_analytic"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "github.com/ClickHouse/clickhouse-go"
        }
    };

    protected override IReadOnlyDictionary<string, HashSet<string>> DbPackages => DbPackagesByKind;

    private static readonly HashSet<string> ApiPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "net/http", "github.com/go-resty/resty", "github.com/imroc/req", "github.com/levigross/grequests", 
        "github.com/parnurzeal/gorequest", "github.com/go-surf/surf"
    };

    private static readonly HashSet<string> CloudPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com/stripe/stripe-go"
    };

    protected override bool IsApiPackage(string importPath)
    {
        return ApiPackages.Contains(importPath) || ApiPackages.Contains(Path.GetFileName(importPath));
    }

    protected override bool IsCloudPackage(string importPath)
    {
        return CloudPackages.Contains(importPath) ||
               CloudPackages.Contains(Path.GetFileName(importPath)) ||
               importPath.StartsWith("github.com/aws/aws-sdk-go", StringComparison.OrdinalIgnoreCase) ||
               importPath.StartsWith("cloud.google.com/", StringComparison.OrdinalIgnoreCase) ||
               importPath.StartsWith("firebase.google.com/", StringComparison.OrdinalIgnoreCase) ||
               importPath.Contains("/Azure/") ||
               importPath.Contains("/azure-sdk-for-go");
    }
}
