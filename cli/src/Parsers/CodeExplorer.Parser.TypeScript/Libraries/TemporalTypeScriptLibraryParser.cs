using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public sealed class TemporalTypeScriptLibraryParser : DeclarativeSemanticExtension
{
    private const string TemporalPrefix = "temporal:";

    public TemporalTypeScriptLibraryParser()
    {
        Id = "temporal-ts";
        Name = "Temporal (TypeScript SDK)";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "@temporalio/workflow*",
            "@temporalio/client*",
            "@temporalio/worker*",
            "@temporalio/activity*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "proxyActivities", "proxyLocalActivities" }.ToFrozenSet(),
                StaticTargetName = TemporalPrefix + "activities",
                TargetRelation = OntologyConstants.Relationships.PublishesTo
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "executeChild", "startChild" }.ToFrozenSet(),
                TargetArgumentIndex = 0,
                AllowIdentifierArgument = true,
                TargetPrefix = TemporalPrefix,
                TargetRelation = OntologyConstants.Relationships.PublishesTo
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "start", "execute", "signalWithStart" }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "workflow" }.ToFrozenSet(),
                TargetArgumentIndex = 0,
                AllowIdentifierArgument = true,
                TargetPrefix = TemporalPrefix,
                TargetRelation = OntologyConstants.Relationships.PublishesTo
            }
        ];
    }
}
