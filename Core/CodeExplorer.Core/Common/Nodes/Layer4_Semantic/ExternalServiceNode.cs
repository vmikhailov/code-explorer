using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ExternalService,
    idScheme: "{workspaceId}:externalservice:{protocol}:{host}",
    purpose: "Represents a physical/logical external host dependency."
)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.CallsEndpoint)]
public record ExternalServiceNode(
    string Id,
    string Name,
    string Protocol,
    string DomainOrService,
    string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ExternalService;
}
