using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class WatermillLibraryParser : DeclarativeSemanticExtension
{
    private const string WatermillPrefix = "watermill:";

    public WatermillLibraryParser()
    {
        Id = "watermill";
        Name = "Watermill";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "github.com/ThreeDotsLabs/watermill*",
            "*watermill*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "AddHandler", "AddNoPublisherHandler" }.ToFrozenSet(StringComparer.Ordinal),
                TargetPrefix = WatermillPrefix,
                TargetRelation = OntologyConstants.Relationships.SubscribesTo,
                AllowIdentifierArgument = true,
                TargetArgumentIndex = 1
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "AddHandler" }.ToFrozenSet(StringComparer.Ordinal),
                TargetPrefix = WatermillPrefix,
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                AllowIdentifierArgument = true,
                TargetArgumentIndex = 3
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "Publish" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "pub" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetPrefix = WatermillPrefix,
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                AllowIdentifierArgument = true
            }
        ];
    }
}
