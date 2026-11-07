using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class KafkaPythonLibraryParser : DeclarativeSemanticExtension
{
    public KafkaPythonLibraryParser()
    {
        Id = "kafka-python";
        Name = "Confluent Kafka & AIOKafka";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["confluent_kafka*", "aiokafka*", "kafka", "kafka.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "kafka",
                PublishMethodNames = new[] { "produce", "send", "send_and_wait" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "producer", "kafka" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "subscribe" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "consumer", "kafka" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "kafka-topic"
            }
        ];
    }
}
