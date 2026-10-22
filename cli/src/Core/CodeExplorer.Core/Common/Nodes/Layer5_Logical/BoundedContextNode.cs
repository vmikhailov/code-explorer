using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Common.Nodes.Layer5_Logical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.BoundedContext,
    idScheme: "{workspaceId}:bc:{contextKey}",
    purpose: "Represents a DDD Bounded Context in Level 5 (Logical Structure) that encapsulates semantic workloads, data stores, topics, and domain types.",
    layer: OntologyConstants.Layers.Logical,
    icon: "layers",
    order: 2,
    pluralLabel: "Bounded Contexts"
)]
[OntologyEdge<BoundedContextNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<AppNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<WorkerNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<LibraryNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<CliToolNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Defines)]
public record BoundedContextNode(
    string Id,
    [property: OntologyProperty("The canonical bounded context name.")] string Name,
    [property: OntologyProperty("The human-readable display name of the bounded context.")] string DisplayName,
    [property: OntologyProperty("The parent domain ID that owns this bounded context.")] string DomainId,
    [property: OntologyProperty("A concise summary of the bounded context's business responsibility.")] string? Summary = null,
    [property: OntologyProperty("Background color for visual rendering.")] string? BgColor = null,
    [property: OntologyProperty("Border color for visual rendering.")] string? BorderColor = null,
    [property: OntologyProperty("Domain purity percentage (0-100).")] double PurityPercentage = 0.0,
    [property: OntologyProperty("Primary path associated with the bounded context.")] string Path = "",
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.BoundedContext;
}
