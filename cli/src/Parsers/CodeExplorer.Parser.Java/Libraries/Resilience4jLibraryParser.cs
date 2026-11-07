using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class Resilience4jLibraryParser : DeclarativeSemanticExtension
{
    public Resilience4jLibraryParser()
    {
        Id = "resilience4j";
        Name = "Resilience4j";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["io.github.resilience4j.*"];
        AnnotationSinkRules =
        [
            new AnnotationSinkRule
            {
                AnnotationNames = new[]
                {
                    "CircuitBreaker", "Retry", "RateLimiter", "Bulkhead", "TimeLimiter"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetPrefix = "resilience4j:",
                TargetRelation = OntologyConstants.Relationships.Calls
            }
        ];
    }
}
