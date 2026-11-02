using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringEventsLibraryParser : DeclarativeSemanticExtension
{
    public SpringEventsLibraryParser()
    {
        Id = "spring-events";
        Name = "Spring Events";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["org.springframework.context.event.*", "org.springframework.context.ApplicationEventPublisher"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "spring",
                MethodLevelEventAnnotations = new[] { "EventListener", "TransactionalEventListener" }.ToFrozenSet(StringComparer.Ordinal),
                PublishMethodNames = new[] { "publishEvent" }.ToFrozenSet(StringComparer.Ordinal),
                ExtractEventTypeFromObjectCreationArg = true
            }
        ];
    }
}

