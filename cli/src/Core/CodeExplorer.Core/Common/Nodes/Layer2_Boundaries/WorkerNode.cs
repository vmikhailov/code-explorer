using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Worker,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:",
    purpose: "Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).",
    layer: OntologyConstants.Layers.ProjectBoundary
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.UsesDb)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.PublishesTo)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.SubscribesTo)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Contains)]
public record WorkerNode : ProjectNode
{
    public WorkerNode(
        string id,
        string name,
        string path,
        string projectType,
        Dictionary<string, string>? extensions = null)
        : base(id, name, path, projectType, OntologyConstants.ProjectRoles.Worker, false, extensions)
    {
    }

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Worker;
}
