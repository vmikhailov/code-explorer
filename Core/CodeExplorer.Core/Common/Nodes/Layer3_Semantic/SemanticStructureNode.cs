using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.SemanticStructure,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:semantic_structure",
    purpose: "Represents an intermediate node grouping all runtime entry points, databases, endpoints, cloud services, and APIs used by a project.",
    layer: OntologyConstants.Layers.Semantic
)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<EndpointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<TopicNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<CloudServiceNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ApiInUseNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.BelongsTo)]
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
