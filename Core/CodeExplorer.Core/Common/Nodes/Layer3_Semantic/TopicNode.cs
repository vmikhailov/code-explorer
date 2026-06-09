using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer2_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Topic,
    idScheme: "{workspaceId}:topic:{brokerType}:{topicName}",
    purpose: "Represents a message queue, event exchange, or topic boundary."
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.PublishedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.SubscribedBy)]
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
