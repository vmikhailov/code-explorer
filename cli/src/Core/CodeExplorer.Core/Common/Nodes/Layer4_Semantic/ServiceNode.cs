using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Service,
    idScheme: "{workspaceId}:s:{serviceName}",
    purpose: "Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "server-process",
    order: 2,
    pluralLabel: "Services"
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.ImplementedBy)]
[OntologyEdge<LibraryNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.UsesDb)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.PublishesTo)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.SubscribesTo)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<CloudServiceNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<CloudServiceNode>(OntologyConstants.Relationships.UsesCloud)]
[OntologyEdge<CloudServiceNode>(OntologyConstants.Relationships.ReadsFrom)]
[OntologyEdge<CloudServiceNode>(OntologyConstants.Relationships.WritesTo)]
[OntologyEdge<PeerMeshNode>(OntologyConstants.Relationships.CoordinatesWith)]
[OntologyEdge<ApiInUseNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ApiInUseNode>(OntologyConstants.Relationships.UsesApi)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.CalledBy)]
public record ServiceNode(
    string Id,
    [property: OntologyProperty("The service name.")] string Name,
    [property: OntologyProperty("The path of the service directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public ServiceNode(string Id, string Name, string Path, string ProjectType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), CustomProperties)
    {
    }

    [JsonPropertyName("role"), OntologyProperty("The primary semantic role of the service.")]
    public string? Role { get; set; }

    [JsonPropertyName("is_library"), OntologyProperty("Indicates whether the project is a library.")]
    public bool? IsLibrary { get; set; }

    [JsonPropertyName("primary_role"), OntologyProperty("The primary architectural role.")]
    public string? PrimaryRole { get; set; }

    [JsonPropertyName("secondary_roles"), OntologyProperty("Comma-separated secondary roles.", isSystem: true)]
    public string? SecondaryRoles { get; set; }

    [JsonPropertyName("all_roles"), OntologyProperty("All detected roles.", isSystem: true)]
    public string? AllRoles { get; set; }

    [JsonPropertyName("framework"), OntologyProperty("The detected service framework.")]
    public string? Framework { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Service;
}
