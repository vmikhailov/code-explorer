using System.Collections.Frozen;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public class FusionCacheDotNetLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "fusioncache-dotnet";
    private const string ExtensionName = "FusionCache, CacheManager, LazyCache & EasyCaching";
    private const string StaticCacheTarget = "memory-distributed-cache";

    public FusionCacheDotNetLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.KeyValueDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "ZiggyCreatures.FusionCache*",
            "CacheManager*",
            "LazyCache*",
            "EasyCaching*",
            "Microsoft.Extensions.Caching*"
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "GetOrSetAsync", "GetOrSet", "GetOrDefaultAsync", "GetOrDefault",
                    "SetAsync", "RemoveAsync", "ExpireAsync", "GetOrAddAsync", "GetOrAdd",
                    "GetOrCreateAsync", "GetOrCreate"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "cache", "fusionCache", "_cache", "_fusionCache" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticTargetName = StaticCacheTarget
            }
        ];
    }
}
