using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class RebusLibraryParser : DeclarativeSemanticExtension
{
    public RebusLibraryParser()
    {
        Id = "rebus";
        Name = "Rebus";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["Rebus", "Rebus.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "rebus",
                PublishMethodNames = new[] { "Send", "SendLocal", "Publish", "Reply", "Defer", "DeferLocal" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "bus", "rebus" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "Subscribe" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "bus", "rebus" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "rebus-queue"
            }
        ];
    }
}
