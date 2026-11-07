using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class PikaRabbitMqLibraryParser : DeclarativeSemanticExtension
{
    public PikaRabbitMqLibraryParser()
    {
        Id = "pika-rabbitmq";
        Name = "Pika, aio-pika & Kombu (RabbitMQ)";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["pika", "pika.*", "aio_pika*", "kombu*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "rabbitmq",
                PublishMethodNames = new[] { "basic_publish", "publish" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "channel", "exchange", "producer" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "basic_consume", "consume", "declare_queue" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "channel", "queue", "consumer" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "rabbitmq-queue"
            }
        ];
    }
}
