using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class KafkaGoLibraryParser : DeclarativeSemanticExtension
{
    public KafkaGoLibraryParser()
    {
        Id = "kafka-go";
        Name = "Segmentio Kafka-Go & Franz-Go";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "github.com/segmentio/kafka-go*",
            "github.com/twmb/franz-go*",
            "github.com/confluentinc/confluent-kafka-go*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "kafka",
                PublishMethodNames = new[] { "WriteMessages", "Produce", "ProduceSync" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "writer", "producer", "client", "kafka" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "ReadMessage", "FetchMessage", "ConsumeTopics", "SubscribeTopics" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "reader", "consumer", "client", "kgo" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "kafka-topic"
            }
        ];
    }
}
