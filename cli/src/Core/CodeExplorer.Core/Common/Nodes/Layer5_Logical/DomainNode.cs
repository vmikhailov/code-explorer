using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer5_Logical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Domain,
    idScheme: "{workspaceId}:dom:{domainKey}",
    purpose: "Represents a high-level business domain (e.g. Core, Supporting, Generic, Shared) containing one or more bounded contexts in Level 5 (Logical Structure).",
    layer: OntologyConstants.Layers.Logical,
    icon: "globe",
    order: 1,
    pluralLabel: "Domains"
)]
[OntologyEdge<BoundedContextNode>(OntologyConstants.Relationships.Contains)]
public record DomainNode(
    string Id,
    [property: OntologyProperty("The canonical domain name.")] string Name,
    [property: OntologyProperty("The human-readable display name of the domain.")] string DisplayName,
    [property: OntologyProperty("The DDD classification of the domain (Core, Supporting, Generic, Shared).")] string DomainType,
    [property: OntologyProperty("A concise summary of the domain's business responsibility.")] string? Summary = null,
    [property: OntologyProperty("Representative icon for the domain.")] string? Icon = null,
    [property: OntologyProperty("Primary path associated with the domain.")] string Path = "",
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Domain;
}
