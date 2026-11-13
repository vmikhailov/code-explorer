using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Workload,
    idScheme: "{workspaceId}:{workloadPrefix}:{workloadName}",
    purpose: "Abstract executable runtime workload encompassing backend services, client applications, background workers, and CLI tools.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "server",
    order: 1,
    pluralLabel: "Runtime Workloads",
    isAbstract: true
)]
[OntologyEdge<CloudNode>(OntologyConstants.Relationships.HostedIn)]
[OntologyEdge<CloudResourceNode>(OntologyConstants.Relationships.ImplementedBy)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.UsesDb)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.PublishesTo)]
public abstract record WorkloadNode(
    string Id,
    string Name,
    string Path,
    ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : ModuleNode(Id, Name, Path, ProjectType, CustomProperties);
