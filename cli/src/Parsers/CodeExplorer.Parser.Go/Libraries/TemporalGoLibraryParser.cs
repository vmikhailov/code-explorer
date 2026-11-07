using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public sealed class TemporalGoLibraryParser : DeclarativeSemanticExtension
{
    private const string TemporalPrefix = "temporal:";

    public TemporalGoLibraryParser()
    {
        Id = "temporal-go";
        Name = "Temporal (Go SDK)";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns = ["go.temporal.io/sdk*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "ExecuteActivity", "ExecuteChildWorkflow", "ExecuteWorkflow"
                }.ToFrozenSet(),
                TargetArgumentIndex = 1,
                AllowIdentifierArgument = true,
                TargetPrefix = TemporalPrefix,
                TargetRelation = OntologyConstants.Relationships.PublishesTo
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "RegisterWorkflow", "RegisterActivity"
                }.ToFrozenSet(),
                TargetArgumentIndex = 0,
                AllowIdentifierArgument = true,
                TargetPrefix = TemporalPrefix,
                TargetRelation = OntologyConstants.Relationships.SubscribesTo
            }
        ];
    }
}
