using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ProjectSyntax,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:project_syntax",
    purpose: "Represents an intermediate node grouping AST/syntactic declarations of a specific project.",
    layer: OntologyConstants.Layers.Syntactic
)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.BelongsTo)]
public record ProjectSyntaxNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ProjectSyntax;
}
