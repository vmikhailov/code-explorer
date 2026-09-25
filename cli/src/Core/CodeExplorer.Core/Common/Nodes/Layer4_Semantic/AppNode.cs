using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.App,
    idScheme: "{workspaceId}:app:{appName}",
    purpose: "Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "browser",
    order: 1,
    pluralLabel: "Applications"
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DeployedBy)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ApiInUseNode>(OntologyConstants.Relationships.Contains)]
public record AppNode(
    string Id,
    [property: OntologyProperty("The application name.")] string Name,
    [property: OntologyProperty("The path of the application directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.App;
}
