using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.SemanticStructure,
    idScheme: "{workspaceId}:semantic_structure",
    purpose: "Represents an intermediate node grouping all runtime workloads, databases, endpoints, cloud services, and APIs used in the entire workspace.",
    layer: OntologyConstants.Layers.Workspace
)]
[OntologyEdge<AppNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<WorkerNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<LibraryNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<CliToolNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<CloudServiceNode>(OntologyConstants.Relationships.Contains)]
public record SemanticStructureNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.SemanticStructure;
}
