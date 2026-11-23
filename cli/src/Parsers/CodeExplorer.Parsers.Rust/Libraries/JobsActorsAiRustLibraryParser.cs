using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Rust.Libraries;

public class JobsActorsAiRustLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "jobs-actors-ai-rust";
    private const string ExtensionName = "Apalis, Tokio-Cron, Fang, Ractor, Actix-Actors, Rig-AI & Async-OpenAI";
    private const string ProtocolQueue = "rust-worker";
    private const string FallbackTopic = "rust-job";

    public JobsActorsAiRustLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "apalis*",
            "tokio-cron-scheduler*",
            "tokio_cron_scheduler*",
            "fang*",
            "sea-streamer*",
            "sea_streamer*",
            "rumqttc*",
            "amqprs*",
            "actix",
            "ractor*",
            "bastion*",
            "lunatic*",
            "rig-core*",
            "rig_core*",
            "async-openai*",
            "async_openai*",
            "langchain-rust*",
            "langchain_rust*",
            "tower*",
            "ureq*",
            "isahc*",
            "reqwest-middleware*",
            "lettre*",
            "s3s*",
            "casbin*",
            "axum-login*",
            "axum_login*",
            "jsonwebtoken*",
            "oauth2*",
            "tracing*",
            "opentelemetry*",
            "metrics*",
            "sentry*",
            "utoipa*",
            "schemars*",
            "validator*",
            "garde*",
            "prost*",
            "rkyv*",
            "serde*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolQueue,
                PublishMethodNames = new[]
                {
                    "push", "enqueue", "enqueue_task", "schedule_task", "do_send", "send_message", "cast", "call"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "queue", "storage", "apalis", "scheduler", "actor", "addr", "recipient", "mqtt", "streamer"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = FallbackTopic
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "prompt", "chat", "completion", "embeddings"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "agent", "openai", "llm", "model", "client" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "llm:rust-ai"
            }
        ];
    }
}
