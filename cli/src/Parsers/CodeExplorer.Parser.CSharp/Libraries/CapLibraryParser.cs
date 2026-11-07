using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class CapLibraryParser : DeclarativeSemanticExtension
{
    public CapLibraryParser()
    {
        Id = "cap";
        Name = "DotNetCore.CAP";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["DotNetCore.CAP", "DotNetCore.CAP.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "cap",
                ListenerAnnotationNames = new[] { "CapSubscribe", "CapSubscribeAttribute" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "name", "value" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "cap-event",
                PublishMethodNames = new[] { "Publish", "PublishAsync", "PublishDelay", "PublishDelayAsync" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "cap", "capPublisher", "capBus", "_capBus", "_capPublisher" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
