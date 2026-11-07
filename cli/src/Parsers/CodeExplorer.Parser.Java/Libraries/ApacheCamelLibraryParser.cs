using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class ApacheCamelLibraryParser : DeclarativeSemanticExtension
{
    public ApacheCamelLibraryParser()
    {
        Id = "apache-camel";
        Name = "Apache Camel";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["org.apache.camel.*", "org.apache.camel"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "camel",
                ListenerAnnotationNames = new[] { "Consume", "Handler" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ProducerAnnotationNames = new[] { "Produce", "EndpointInject" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "value", "uri", "property" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "camel-route",
                PublishMethodNames = new[] { "to", "toD", "sendBody", "sendBodyAndHeader", "requestBody", "asyncSendBody" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "producer", "template", "fluent", "from", "route" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "from", "fromF" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "this", "route", "" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
