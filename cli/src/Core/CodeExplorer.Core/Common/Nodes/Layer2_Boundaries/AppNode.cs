using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.App,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:",
    purpose: "Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular).",
    layer: OntologyConstants.Layers.ProjectBoundary
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ApiInUseNode>(OntologyConstants.Relationships.Contains)]
public record AppNode : ProjectNode
{
    public AppNode(
        string id,
        string name,
        string path,
        string projectType,
        Dictionary<string, string>? extensions = null)
        : base(id, name, path, projectType, OntologyConstants.ProjectRoles.FrontendApp, false, extensions)
    {
    }

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.App;
}
