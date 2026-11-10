using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
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
public record TopicNode(
    string Id,
    [property: OntologyProperty("The name of the topic or exchange.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("broker_type"), OntologyProperty("The broker system type (rabbitmq, kafka, sqs, in-memory).")] BrokerType BrokerType,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public TopicNode(string Id, string Name, string Path, string BrokerType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtensions.ParseBrokerType(BrokerType), CustomProperties)
    {
    }

    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("line_end"), OntologyProperty("The ending line number of the topic usage.", isSystem: true)]
    public int? LineEnd { get; set; }

    [JsonPropertyName("messaging_direction")]
    public string? MessagingDirection { get; set; }

    [JsonPropertyName("is_internal")]
    public bool? IsInternal { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("message_type")]
    public string? MessageType { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Topic;
}
