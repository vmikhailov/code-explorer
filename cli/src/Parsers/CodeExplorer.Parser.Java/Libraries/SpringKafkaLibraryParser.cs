using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringKafkaLibraryParser : DeclarativeSemanticExtension
{
    public SpringKafkaLibraryParser()
    {
        Id = "kafka";
        Name = "Apache Kafka";
        Type = SemanticExtensionType.Queue;
        PackageDescriptorTypeOverride = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["org.springframework.kafka.*", "org.apache.kafka.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "kafka",
                ListenerAnnotationNames = new[] { "KafkaListener", "KafkaHandler" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "topics", "topicPattern", "value" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "unknown-topic",
                PublishMethodNames = new[] { "send", "sendDefault", "execute", "to" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "kafka", "producer", "template", "stream", "kstream" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "stream", "table", "globalTable" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "builder", "stream", "streamsBuilder", "kstream" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}

