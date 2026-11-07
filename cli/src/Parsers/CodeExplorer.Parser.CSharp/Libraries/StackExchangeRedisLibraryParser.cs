using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class StackExchangeRedisLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> UnconditionalRedisMethods = new[]
    {
        "StringGet", "StringGetAsync", "StringSet", "StringSetAsync",
        "KeyDelete", "KeyDeleteAsync", "KeyExists", "KeyExistsAsync",
        "StringIncrement", "StringIncrementAsync", "StringDecrement", "StringDecrementAsync",
        "HashGet", "HashGetAsync", "HashSet", "HashSetAsync", "HashDelete", "HashDeleteAsync",
        "ListLeftPush", "ListLeftPushAsync", "ListRightPop", "ListRightPopAsync",
        "AddStackExchangeRedisCache"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> ReceiverScopedRedisMethods = new[]
    {
        "Get", "GetAsync", "Set", "SetAsync", "Refresh", "RefreshAsync", "Remove", "RemoveAsync"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> AllRedisQueryMethods =
        UnconditionalRedisMethods.Concat(ReceiverScopedRedisMethods).Concat(["Publish", "PublishAsync"]).ToFrozenSet();

    public StackExchangeRedisLibraryParser()
    {
        Id = "redis";
        Name = "Redis";
        Type = SemanticExtensionType.KeyValueDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "StackExchange.Redis",
            "StackExchange.Redis.*",
            "Microsoft.Extensions.Caching.StackExchangeRedis",
            "Microsoft.Extensions.Caching.StackExchangeRedis.*",
            "Microsoft.Extensions.Caching.Distributed"
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = AllRedisQueryMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "Redis"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = UnconditionalRedisMethods,
                StaticTargetName = "redis"
            },
            new DataStoreAccessRule
            {
                CallMethodNames = ReceiverScopedRedisMethods,
                ReceiverSubstrings = new[] { "cache", "redis", "store" }.ToFrozenSet(),
                StaticTargetName = "redis"
            }
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "redis",
                FallbackTopicName = "redis",
                PublishMethodNames = new[] { "Publish", "PublishAsync" }.ToFrozenSet()
            }
        ];
    }
}
