using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Rust.Libraries;

public class RedisMongoNoSqlRustLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "redis-mongo-nosql-rust";
    private const string ExtensionName = "Redis-rs, Fred, MongoDB, Scylla, Qdrant, Redb, Sled & RocksDB";
    private const string FallbackStore = "rust-kv-nosql";

    public RedisMongoNoSqlRustLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.KeyValueDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "redis*",
            "fred*",
            "moka*",
            "foyer*",
            "mongodb*",
            "scylla*",
            "qdrant-client*",
            "qdrant_client*",
            "clickhouse*",
            "surrealdb*",
            "redb*",
            "sled*",
            "rocksdb*",
            "rusqlite*",
            "tokio-postgres*",
            "tokio_postgres*",
            "deadpool*",
            "bb8*",
            "cornucopia*",
            "rbatis*"
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "collection", "find", "find_one", "insert_one", "insert_many",
                    "update_one", "update_many", "delete_one", "delete_many", "aggregate",
                    "hget", "hset", "hdel", "lpush", "rpush", "sadd", "zadd",
                    "open_table", "open_tree", "put_cf", "get_cf", "search_points", "upsert_points"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[]
                {
                    "redis", "fred", "cache", "moka", "mongo", "db", "database", "collection",
                    "scylla", "session", "qdrant", "clickhouse", "surreal", "redb", "sled", "rocks"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticTargetName = FallbackStore
            }
        ];
    }
}
