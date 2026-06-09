using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ProjectSemantic,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:project_semantic",
    purpose: "Represents an intermediate node grouping semantic runtime declarations of a specific project.",
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
public record ProjectSemanticNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ProjectSemantic;
}
