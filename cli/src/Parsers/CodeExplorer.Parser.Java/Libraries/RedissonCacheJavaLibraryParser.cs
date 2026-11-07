using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class RedissonCacheJavaLibraryParser : DeclarativeSemanticExtension
{
    public RedissonCacheJavaLibraryParser()
    {
        Id = "redisson-caffeine";
        Name = "Redisson & Caffeine Cache";
        Type = SemanticExtensionType.DocumentDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.redisson.*",
            "com.github.benmanes.caffeine.*",
            "org.springframework.cache.*"
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "getBucket", "getMap", "getSet", "getLock" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "redisson",
                PublishMethodNames = new[] { "getTopic" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
