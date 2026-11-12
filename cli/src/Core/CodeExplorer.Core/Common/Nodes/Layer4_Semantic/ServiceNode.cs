using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Service,
    idScheme: "{workspaceId}:s:{serviceName}",
    purpose: "Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "server-process",
    order: 3,
    pluralLabel: "Services"
)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.Exposes)]
[OntologyEdge<PeerMeshNode>(OntologyConstants.Relationships.CoordinatesWith)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.CalledBy)]
public record ServiceNode(
    string Id,
    string Name,
    string Path,
    ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : WorkloadNode(Id, Name, Path, ProjectType, CustomProperties)
{
    public ServiceNode(string Id, string Name, string Path, string ProjectType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), CustomProperties)
    {
    }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Service;
}
