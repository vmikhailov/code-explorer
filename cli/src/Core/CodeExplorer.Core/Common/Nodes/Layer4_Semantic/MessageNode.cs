using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Message,
    idScheme: "{workspaceId}:msg:{subKind}:{messageName}",
    purpose: "Represents a CQRS or application message contract (Query, Command, or Domain/Integration Event) handled via a mediator, bus, or domain dispatcher.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "mail",
    order: 12,
    pluralLabel: "CQRS & Domain Messages"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Triggers)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.ImplementedBy)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<PolicyNode>(OntologyConstants.Relationships.GuardedBy)]
public record MessageNode(
    string Id,
    [property: OntologyProperty("The message contract name (e.g. GetMyWalletQuery, CreateOrderCommand).")] string Name,
    [property: OntologyProperty("The workspace-relative path of the declaring file or handler file.")] string Path,
    [property: JsonPropertyName("sub_kind"), OntologyProperty("The semantic message role (query, command, event, notification).")] OperationType SubKind,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("framework"), OntologyProperty("Detected CQRS or messaging framework (e.g. mediatr, nestjs-cqrs, axon, symfony-messenger, spring, cqrs).")]
    public string? Framework { get; set; }

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file declaring the message or handler.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line"), OntologyProperty("1-indexed starting line number of the message declaration.")]
    public int? StartLine { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Message;
}
