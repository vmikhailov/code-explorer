using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Topic,
    idScheme: "{workspaceId}:topic:{brokerType}:{topicName}",
    purpose: "Represents a message queue, event exchange, or topic boundary."
)]
public record TopicNode(
    string Id,
    [property: OntologyProperty("The name of the topic or exchange.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("broker_type"), OntologyProperty("The broker system type (rabbitmq, kafka, sqs, in-memory).")] string BrokerType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Topic;
}
