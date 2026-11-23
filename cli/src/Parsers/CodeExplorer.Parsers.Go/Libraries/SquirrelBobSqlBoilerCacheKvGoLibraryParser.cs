using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Go.Libraries;

public class SquirrelBobSqlBoilerCacheKvGoLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "squirrel-bob-sqlboiler-cache-kv-go";
    private const string ExtensionName = "Squirrel, Goqu, SQLBoiler, Bob, Jet, XORM, Rueidis/Redigo, BigCache/Ristretto & Bbolt/Badger/Pebble";
    private const string StaticStoreTarget = "go-datastore";

    public SquirrelBobSqlBoilerCacheKvGoLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "github.com/Masterminds/squirrel*",
            "github.com/doug-martin/goqu*",
            "github.com/volatiletech/sqlboiler*",
            "github.com/stephenafamo/bob*",
            "github.com/go-jet/jet*",
            "github.com/upper/db*",
            "xorm.io/xorm*",
            "github.com/go-xorm/xorm*",
            "github.com/redis/rueidis*",
            "github.com/gomodule/redigo*",
            "github.com/allegro/bigcache*",
            "github.com/dgraph-io/ristretto*",
            "github.com/patrickmn/go-cache*",
            "github.com/eko/gocache*",
            "go.etcd.io/bbolt*",
            "github.com/dgraph-io/badger*",
            "github.com/cockroachdb/pebble*",
            "github.com/golang-migrate/migrate*",
            "github.com/pressly/goose*"
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "From", "Insert", "Update", "Delete", "Select", "Table",
                    "Bucket", "CreateBucket", "CreateBucketIfNotExists", "Do", "DoCache"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[]
                {
                    "sq", "squirrel", "goqu", "db", "tx", "engine", "cache", "client", "rueidis"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TableArgumentIndex = 0,
                StaticTargetName = StaticStoreTarget
            }
        ];
    }
}
