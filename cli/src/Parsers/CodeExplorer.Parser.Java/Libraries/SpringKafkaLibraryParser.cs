using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringKafkaLibraryParser : DeclarativeSemanticExtension
{
    public SpringKafkaLibraryParser()
    {
        Id = "kafka";
        Name = "Apache Kafka";
        Type = OntologyConstants.LibraryTypes.Queue;
        PackageDescriptorTypeOverride = "library";
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
                PublishMethodNames = new[] { "send", "sendDefault", "execute" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "kafka", "producer", "template" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}

