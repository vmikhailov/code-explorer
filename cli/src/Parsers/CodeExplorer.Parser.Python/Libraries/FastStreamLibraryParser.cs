using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class FastStreamLibraryParser : DeclarativeSemanticExtension
{
    public FastStreamLibraryParser()
    {
        Id = "faststream";
        Name = "FastStream";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["faststream", "faststream.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "",
                SubscribeMethodNames = new[] { "subscriber" }.ToFrozenSet(StringComparer.Ordinal),
                PublishMethodNames = new[] { "publisher", "publish" }.ToFrozenSet(StringComparer.Ordinal),
                PublishTopicArgumentIndex = 1
            }
        ];
    }
}
