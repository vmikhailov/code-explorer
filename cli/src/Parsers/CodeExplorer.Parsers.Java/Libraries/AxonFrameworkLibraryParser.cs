using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class AxonFrameworkLibraryParser : DeclarativeSemanticExtension
{
    public AxonFrameworkLibraryParser()
    {
        Id = "axon";
        Name = "Axon Framework";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["org.axonframework.*", "org.axonframework", "io.eventuate*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "axon",
                ListenerAnnotationNames = new[]
                {
                    "CommandHandler", "EventHandler", "EventSourcingHandler", "QueryHandler", "SagaEventHandler"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                MethodLevelEventAnnotations = new[]
                {
                    "CommandHandler", "EventHandler", "EventSourcingHandler", "QueryHandler", "SagaEventHandler"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "commandName", "payloadType", "associationProperty", "value" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "axon-message",
                PublishMethodNames = new[] { "send", "sendAndWait", "publish", "apply", "query" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "commandGateway", "eventGateway", "queryGateway", "commandBus", "eventBus", "AggregateLifecycle"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ExtractEventTypeFromObjectCreationArg = true
            }
        ];
    }
}
