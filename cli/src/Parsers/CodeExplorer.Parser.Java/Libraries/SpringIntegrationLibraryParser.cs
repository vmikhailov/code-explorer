using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringIntegrationLibraryParser : DeclarativeSemanticExtension
{
    public SpringIntegrationLibraryParser()
    {
        Id = "spring-integration";
        Name = "Spring Integration";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["org.springframework.integration.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "spring-integration",
                ListenerAnnotationNames = new[] { "ServiceActivator", "Transformer", "Splitter", "Aggregator", "Filter", "InboundChannelAdapter" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "inputChannel", "outputChannel", "value" }.ToFrozenSet(StringComparer.Ordinal),
                PublishMethodNames = new[] { "send" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "channel", "gateway", "template" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "integration-channel"
            }
        ];
    }
}
