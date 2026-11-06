using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.SyntaxStructure,
    idScheme: "{workspaceId}:ss",
    purpose: "Represents an intermediate node grouping all AST/syntactic declarations of the entire workspace.",
    layer: OntologyConstants.Layers.Syntactic,
    isSystemNode: true
)]
[OntologyEdge<ProjectSyntaxNode>(OntologyConstants.Relationships.Contains)]
public record SyntaxStructureNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonIgnore]
    public override NodeKind Kind => NodeKind.SyntaxStructure;
}
