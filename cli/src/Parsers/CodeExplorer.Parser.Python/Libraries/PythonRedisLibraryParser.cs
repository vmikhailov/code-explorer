using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public sealed class PythonRedisLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> DbRedisMethods = new[]
    {
        "get", "set", "delete", "mget", "mset", "incr", "decr", "exists",
        "hget", "hset", "hdel", "hgetall",
        "lpush", "rpush", "lpop", "rpop"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> AllRedisMethods =
        DbRedisMethods.Concat(["publish", "subscribe"]).ToFrozenSet();

    public PythonRedisLibraryParser()
    {
        Id = "redis";
        Name = "Redis";
        Type = SemanticExtensionType.KeyValueDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["redis", "redis.*", "aioredis", "aioredis.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = AllRedisMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "Redis"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = DbRedisMethods,
                StaticTargetName = "redis"
            }
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "redis",
                FallbackTopicName = "redis",
                PublishMethodNames = new[] { "publish" }.ToFrozenSet(),
                SubscribeMethodNames = new[] { "subscribe" }.ToFrozenSet()
            }
        ];
    }
}
