using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class QuarkusMessagingLibraryParser : DeclarativeSemanticExtension
{
    public QuarkusMessagingLibraryParser()
    {
        Id = "quarkus-messaging";
        Name = "SmallRye Reactive Messaging / Kafka";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "org.eclipse.microprofile.reactive.messaging*",
            "io.smallrye.reactive.messaging*",
            "io.smallrye.mutiny*",
            "io.quarkus.kafka*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "kafka",
                ListenerAnnotationNames = new[] { "Incoming" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ProducerAnnotationNames = new[] { "Outgoing" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ChannelAnnotationNames = new[] { "Channel" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "value", "topics", "topicPattern" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "unknown-channel"
            }
        ];
    }
}
