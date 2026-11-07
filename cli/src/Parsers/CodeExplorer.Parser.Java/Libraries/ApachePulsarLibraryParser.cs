using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class ApachePulsarLibraryParser : DeclarativeSemanticExtension
{
    public ApachePulsarLibraryParser()
    {
        Id = "apache-pulsar";
        Name = "Apache Pulsar";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["org.apache.pulsar.*", "org.springframework.pulsar.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "pulsar",
                ListenerAnnotationNames = new[] { "PulsarListener", "ReactivePulsarListener", "PulsarReader" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "topics", "topicPattern", "value" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "pulsar-topic",
                PublishMethodNames = new[] { "send", "sendAsync", "newMessage", "topic" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "pulsar", "producer", "template" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "topic", "topicsPattern", "subscribe" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "consumer", "reader", "pulsar" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
