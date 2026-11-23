using System.Collections.Frozen;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public class ElsaWorkflowCoreLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "elsa-workflowcore";
    private const string ExtensionName = "Elsa Workflows & WorkflowCore";
    private const string ProtocolWorkflow = "workflow";
    private const string FallbackTopic = "workflow-step";

    public ElsaWorkflowCoreLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "Elsa*",
            "WorkflowCore*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolWorkflow,
                ConsumerInterfaceSubstrings = new[]
                {
                    "IWorkflow", "CodeActivity", "Activity", "StepBody", "StepBodyAsync"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishMethodNames = new[]
                {
                    "StartWorkflow", "StartWorkflowAsync", "PublishEvent", "RunAsync", "DispatchAsync"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "workflowHost", "workflowRunner", "workflowDispatcher", "stimulusSender"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = FallbackTopic
            }
        ];
    }
}
