using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class DaprGoLibraryParser : DeclarativeSemanticExtension
{
    public DaprGoLibraryParser()
    {
        Id = "dapr-go";
        Name = "Dapr Go SDK";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["github.com/dapr/go-sdk*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "",
                PublishMethodNames = new[] { "PublishEvent", "PublishEventFromCustomContent" }.ToFrozenSet(StringComparer.Ordinal),
                PublishTopicArgumentIndex = 2,
                SubscribeMethodNames = new[] { "AddTopicEventHandler" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "SaveState", "GetState", "DeleteState" }.ToFrozenSet(StringComparer.Ordinal),
                StaticTargetName = "dapr_state_store"
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "InvokeMethod", "InvokeMethodWithContent" }.ToFrozenSet(StringComparer.Ordinal),
                TargetArgumentIndex = 1,
                TargetPrefix = "dapr://",
                TargetRelation = OntologyConstants.Relationships.Calls
            }
        ];
    }
}
