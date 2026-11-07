using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringBatchLibraryParser : DeclarativeSemanticExtension
{
    public SpringBatchLibraryParser()
    {
        Id = "spring-batch";
        Name = "Spring Batch";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns = ["org.springframework.batch.*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "JobBuilder", "StepBuilder", "chunk", "tasklet" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Exposes,
                TargetPrefix = "spring-batch:"
            }
        ];
    }
}
