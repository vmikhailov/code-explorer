using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class CamundaZeebeJavaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "camunda-zeebe";
    private const string ExtensionName = "Camunda, Zeebe & Flowable BPMN";
    private const string ProtocolZeebe = "zeebe";
    private const string FallbackWorkerTopic = "bpmn-task";
    private const string JobWorkerAnnotation = "JobWorker";
    private const string ZeebeWorkerAnnotation = "ZeebeWorker";
    private const string TypeKey = "type";
    private const string ValueKey = "value";

    public CamundaZeebeJavaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "io.camunda*",
            "io.zeebe*",
            "org.camunda.bpm*",
            "org.flowable*",
            "org.activiti*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolZeebe,
                ListenerAnnotationNames = new[] { JobWorkerAnnotation, ZeebeWorkerAnnotation }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { TypeKey, ValueKey }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = FallbackWorkerTopic,
                PublishMethodNames = new[]
                {
                    "newCreateInstanceCommand", "newPublishMessageCommand", "startProcessInstanceByKey",
                    "startProcessInstanceByMessage", "correlateMessage"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "zeebe", "camunda", "runtimeService", "processEngine", "client"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
