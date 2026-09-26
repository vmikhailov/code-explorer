using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Worker,
    idScheme: "{workspaceId}:w:{workerName}",
    purpose: "Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "gear",
    order: 3,
    pluralLabel: "Workers"
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
public record WorkerNode(
    string Id,
    [property: OntologyProperty("The worker name.")] string Name,
    [property: OntologyProperty("The path of the worker directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Worker;
}
