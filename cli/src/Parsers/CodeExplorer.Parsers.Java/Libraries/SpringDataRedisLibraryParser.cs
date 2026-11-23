using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class SpringDataRedisLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "spring-data-redis";
    private const string ExtensionName = "Spring Data Redis";
    private const string RedisHashAnnotation = "RedisHash";
    private const string ValueKey = "value";
    private const string FallbackCacheStore = "redis";

    public SpringDataRedisLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.KeyValueDb;
        PackageDescriptorTypeOverride = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.springframework.boot.spring-boot-starter-data-redis*",
            "org.springframework.data.spring-data-redis*",
            "org.springframework.data.redis*",
            "*data-redis*",
            "*redis*",
            "redis.clients.jedis*",
            "io.lettuce*"
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { RedisHashAnnotation }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { ValueKey }.ToFrozenSet()
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "opsForValue", "opsForHash", "opsForList", "opsForSet", "opsForZSet", "opsForStream",
                    "boundValueOps", "boundHashOps", "boundListOps", "boundSetOps",
                    "setIfAbsent", "setIfPresent", "multiGet", "multiSet", "hget", "hset", "hdel", "hgetAll",
                    "lpush", "rpush", "lpop", "rpop", "sadd", "srem", "smembers", "zadd", "zrange"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "redis", "jedis", "lettuce", "template", "cache", "ops" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticTargetName = FallbackCacheStore
            }
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = FallbackCacheStore,
                PublishMethodNames = new[] { "convertAndSend" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "redis", "template" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "redis-channel"
            }
        ];
    }
}
