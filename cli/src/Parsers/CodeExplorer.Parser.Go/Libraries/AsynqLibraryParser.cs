using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class AsynqLibraryParser : DeclarativeSemanticExtension
{
    private const string AsynqPrefix = "asynq:";

    public AsynqLibraryParser()
    {
        Id = "asynq";
        Name = "Asynq";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "github.com/hibiken/asynq*",
            "*hibiken/asynq*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "NewTask" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "asynq" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetPrefix = AsynqPrefix,
                TargetRelation = OntologyConstants.Relationships.PublishesTo,
                AllowIdentifierArgument = true
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "HandleFunc", "Handle" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "mux" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetPrefix = AsynqPrefix,
                TargetRelation = OntologyConstants.Relationships.SubscribesTo,
                AllowIdentifierArgument = true
            }
        ];
    }
}
