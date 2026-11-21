using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

/// <summary>
/// Semantic extension for JMS (Jakarta/Javax JMS, Spring JMS, ActiveMQ, Artemis, IBM MQ, Solace).
/// Maps @JmsListener, JmsTemplate.convertAndSend/send/receive, MessageProducer.send, and MessageListener
/// to EntryPoint and Topic/Queue pub/sub relationships.
/// </summary>
public class SpringJmsLibraryParser : DeclarativeSemanticExtension
{
    public SpringJmsLibraryParser()
    {
        Id = "spring-jms";
        Name = "JMS / ActiveMQ / IBM MQ / Solace";
        Type = SemanticExtensionType.Queue;
        PackageDescriptorTypeOverride = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "org.springframework.jms.*",
            "org.springframework.boot.spring-boot-starter-activemq*",
            "org.springframework.boot.spring-boot-starter-artemis*",
            "jakarta.jms.*",
            "javax.jms.*",
            "org.apache.activemq.*",
            "com.ibm.mq.*",
            "com.solacesystems.*",
            "*spring-jms*",
            "*activemq*",
            "*artemis*",
            "*ibm-mq*",
            "*mq-jms*",
            "*solace*",
            "*jakarta.jms*",
            "*javax.jms*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "jms",
                ListenerAnnotationNames = new[] { "JmsListener", "JmsListeners" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "destination", "value", "topic", "queue" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "unknown-destination",
                PublishMethodNames = new[] { "convertAndSend", "send", "sendAndReceive" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "jms", "jmsTemplate", "messagingTemplate", "producer", "sender" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "receive", "receiveAndConvert", "receiveSelected", "receiveSelectedAndConvert" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "jms", "jmsTemplate", "consumer", "receiver" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ConsumerInterfaceSubstrings = new[] { "MessageListener", "SessionAwareMessageListener" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
