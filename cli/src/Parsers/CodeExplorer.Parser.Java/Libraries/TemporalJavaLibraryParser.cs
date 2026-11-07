using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class TemporalJavaLibraryParser : DeclarativeSemanticExtension
{
    public TemporalJavaLibraryParser()
    {
        Id = "temporal-java";
        Name = "Temporal (Java SDK)";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns = ["io.temporal.*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "temporal",
                ListenerAnnotationNames = new[] { "WorkflowMethod", "ActivityMethod", "SignalMethod", "UpdateMethod" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "name", "value" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = "temporal-task",
                PublishMethodNames = new[] { "newActivityStub", "newChildWorkflowStub", "newWorkflowStub", "start", "execute" }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "Workflow", "workflowClient", "BatchRequest" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
