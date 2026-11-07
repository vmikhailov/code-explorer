using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class PollyResilienceLibraryParser : DeclarativeSemanticExtension
{
    public PollyResilienceLibraryParser()
    {
        Id = "polly";
        Name = "Polly & .NET Resilience";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "Polly",
            "Polly.*",
            "Microsoft.Extensions.Http.Resilience*",
            "Microsoft.Extensions.Resilience*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "WaitAndRetryAsync", "WaitAndRetry", "RetryAsync", "Retry",
                    "CircuitBreakerAsync", "CircuitBreaker", "AddRetry", "AddCircuitBreaker",
                    "AddTimeout", "AddStandardResilienceHandler", "AddResilienceHandler"
                }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.Calls,
                TargetPrefix = "polly:",
                UseMethodNameAsTarget = true
            }
        ];
    }
}
