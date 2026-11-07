using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class CloudflareWorkersLibraryParser : DeclarativeSemanticExtension
{
    public CloudflareWorkersLibraryParser()
    {
        Id = "cloudflare-workers";
        Name = "Cloudflare Workers, D1, KV & Queues";
        Type = SemanticExtensionType.Cloud;
        LibraryRole = LibraryRole.CloudSdk;
        SupportedPatterns =
        [
            "@cloudflare/workers-types*",
            "cloudflare:workers*",
            "wrangler*"
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "prepare", "exec", "batch" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "DB", "D1" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticTargetName = "cloudflare_d1"
            },
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "get", "put", "delete" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "KV" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticTargetName = "cloudflare_kv"
            }
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "",
                PublishMethodNames = new[] { "send", "sendBatch" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "QUEUE" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                StaticPublishTopic = "cloudflare-worker-queue"
            }
        ];
    }
}
