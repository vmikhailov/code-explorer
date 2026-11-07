using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class NServiceBusLibraryParser : DeclarativeSemanticExtension
{
    public NServiceBusLibraryParser()
    {
        Id = "nservicebus";
        Name = "NServiceBus";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["NServiceBus", "NServiceBus.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "nservicebus",
                ConsumerInterfaceSubstrings = new[] { "IHandleMessages", "IAmStartedByMessages", "IHandleTimeouts" }.ToFrozenSet(StringComparer.Ordinal),
                PublishMethodNames = new[] { "Send", "SendLocal", "Publish", "Reply" }.ToFrozenSet(StringComparer.Ordinal),
                StaticPublishTopic = "message"
            }
        ];
    }
}
