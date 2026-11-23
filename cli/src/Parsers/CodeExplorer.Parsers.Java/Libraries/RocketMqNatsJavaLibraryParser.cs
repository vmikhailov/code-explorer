using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class RocketMqNatsJavaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "rocketmq-nats-java";
    private const string ExtensionName = "Apache RocketMQ & NATS Java";
    private const string ProtocolRocketMq = "rocketmq";
    private const string RocketMqListenerAnnotation = "RocketMQMessageListener";
    private const string TopicKey = "topic";
    private const string FallbackTopic = "rocketmq-topic";

    public RocketMqNatsJavaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "org.apache.rocketmq*",
            "io.nats*",
            "com.aliyun.openservices.ons*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolRocketMq,
                ListenerAnnotationNames = new[] { RocketMqListenerAnnotation }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { TopicKey }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = FallbackTopic,
                PublishMethodNames = new[]
                {
                    "syncSend", "asyncSend", "sendOneWay", "sendMessageInTransaction", "publish", "request"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "rocketMQTemplate", "producer", "nats", "jetStream", "connection"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "subscribe", "createDispatcher" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "consumer", "nats", "jetStream", "dispatcher" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
