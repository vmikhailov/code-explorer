using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringRabbitMqLibraryParser : DeclarativeSemanticExtension
{
    public SpringRabbitMqLibraryParser()
    {
        Id = "rabbitmq";
        Name = "RabbitMQ";
        Type = SemanticExtensionType.Queue;
        PackageDescriptorTypeOverride = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "org.springframework.amqp.*",
            "com.rabbitmq.*",
            "org.springframework.boot.spring-boot-starter-amqp*",
            "*spring-boot-starter-amqp*",
            "*spring-rabbit*",
            "*amqp*"
        ];
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

