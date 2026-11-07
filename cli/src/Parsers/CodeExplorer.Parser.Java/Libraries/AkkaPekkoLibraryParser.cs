using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class AkkaPekkoLibraryParser : DeclarativeSemanticExtension
{
    public AkkaPekkoLibraryParser()
    {
        Id = "akka-pekko";
        Name = "Akka & Apache Pekko";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["akka.actor.*", "org.apache.pekko.actor.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "actor",
                PublishMethodNames = new[] { "tell", "ask", "forward" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "actor", "ref", "router", "selection" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "onMessage", "match" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "actor-mailbox"
            }
        ];
    }
}
