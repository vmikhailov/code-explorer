using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer2_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ExternalService,
    idScheme: "{workspaceId}:externalservice:{protocol}:{host}",
    purpose: "Represents a physical/logical external host dependency."
)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.CallsEndpoint)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.CalledBy)]
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
