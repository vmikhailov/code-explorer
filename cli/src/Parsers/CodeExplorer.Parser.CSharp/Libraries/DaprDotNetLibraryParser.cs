using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class DaprDotNetLibraryParser : DeclarativeSemanticExtension
{
    public DaprDotNetLibraryParser()
    {
        Id = "dapr-dotnet";
        Name = "Dapr .NET SDK";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["Dapr", "Dapr.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "dapr",
                ListenerAnnotationNames = new[] { "Topic" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "name", "value" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "dapr-topic",
                PublishMethodNames = new[] { "PublishEventAsync", "BulkPublishEventAsync" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "dapr", "client" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
