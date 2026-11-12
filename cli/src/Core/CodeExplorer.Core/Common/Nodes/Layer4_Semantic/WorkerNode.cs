using System.Text.Json.Serialization;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Worker,
    idScheme: "{workspaceId}:w:{workerName}",
    purpose: "Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "gear",
    order: 4,
    pluralLabel: "Workers"
)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.SubscribesTo)]
public record WorkerNode(
    string Id,
    string Name,
    string Path,
    ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : WorkloadNode(Id, Name, Path, ProjectType, CustomProperties)
{
    public WorkerNode(string Id, string Name, string Path, string ProjectType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), CustomProperties)
    {
    }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Worker;
}
