using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Service,
    idScheme: "{workspaceId}:service:{serviceName}",
    purpose: "Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "server-process",
    order: 2,
    pluralLabel: "Services"
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DeployedBy)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.UsesDb)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.PublishesTo)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.SubscribesTo)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<CloudServiceNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ApiInUseNode>(OntologyConstants.Relationships.Contains)]
public record ServiceNode(
    string Id,
    [property: OntologyProperty("The service name.")] string Name,
    [property: OntologyProperty("The path of the service directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Service;
}
