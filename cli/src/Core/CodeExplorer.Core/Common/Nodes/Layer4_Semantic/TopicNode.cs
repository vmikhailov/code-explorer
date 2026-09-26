using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Topic,
    idScheme: "{workspaceId}:top:{brokerType}:{topicName}",
    purpose: "Represents a message queue, event exchange, or topic boundary.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "mail",
    order: 11,
    pluralLabel: "Message Topics & Queues"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.PublishedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.SubscribedBy)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.PublishedBy)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.SubscribedBy)]
[OntologyEdge<WorkerNode>(OntologyConstants.Relationships.SubscribedBy)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.Triggers)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.SubscribedBy)]
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
