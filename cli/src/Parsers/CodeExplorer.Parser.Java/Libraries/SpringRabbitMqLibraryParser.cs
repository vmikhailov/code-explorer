using System.Collections.Frozen;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringRabbitMqLibraryParser : DeclarativeSemanticExtension
{
    public SpringRabbitMqLibraryParser()
    {
        Id = "rabbitmq";
        Name = "RabbitMQ";
        Type = OntologyConstants.LibraryTypes.Queue;
        PackageDescriptorTypeOverride = "library";
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["org.springframework.amqp.*", "com.rabbitmq.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "rabbitmq",
                ListenerAnnotationNames = new[] { "RabbitListener", "RabbitHandler" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "queues", "queuesToDeclare", "value", "bindings" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "unknown-queue",
                PublishMethodNames = new[] { "convertAndSend", "convertSendAndReceive", "send", "basicPublish" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "rabbit", "amqp", "template", "channel" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PreferSecondArgWhenThreeOrMoreArgs = true
            }
        ];
    }
}

