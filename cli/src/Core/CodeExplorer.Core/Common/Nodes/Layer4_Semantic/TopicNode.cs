using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
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
[OntologyEdge<CloudNode>(OntologyConstants.Relationships.HostedIn)]
[OntologyEdge<CloudResourceNode>(OntologyConstants.Relationships.ImplementedBy)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.PublishedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.SubscribedBy)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.TargetedBy)]
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

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file declaring or referencing the topic.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("line"), OntologyProperty("1-indexed source line of the publish or subscribe site.")]
    public int? Line { get; set; }

    [JsonPropertyName("line_end"), OntologyProperty("The ending line number of the topic usage.", isSystem: true)]
    public int? LineEnd { get; set; }

    [JsonPropertyName("messaging_direction"), OntologyProperty("Direction of broker interaction (publish, subscribe, both).")]
    public string? MessagingDirection { get; set; }

    [JsonPropertyName("is_internal"), OntologyProperty("Indicates an in-process mediator bus rather than an external broker.", isSystem: true)]
    public bool? IsInternal { get; set; }

    [JsonPropertyName("scope"), OntologyProperty("Broker scope or virtual host / namespace.")]
    public string? Scope { get; set; }

    [JsonPropertyName("message_type"), OntologyProperty("Payload or event contract type published/consumed on this topic.")]
    public string? MessageType { get; set; }

    [JsonPropertyName("resource_type"), OntologyProperty("IaC resource type when provisioned by Terraform or cloud templates.")]
    public string? ResourceType { get; init; }

    [JsonPropertyName("provisioned_by"), OntologyProperty("IaC tool or engine provisioning this topic (e.g. terraform).")]
    public string? ProvisionedBy { get; init; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Topic;
}
