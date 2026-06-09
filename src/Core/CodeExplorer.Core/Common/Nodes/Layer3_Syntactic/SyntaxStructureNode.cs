using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.SyntaxStructure,
    idScheme: "{workspaceId}:syntax_structure",
    purpose: "Represents an intermediate node grouping all AST/syntactic declarations of the entire workspace.",
    layer: OntologyConstants.Layers.Syntactic
)]
[OntologyEdge<ProjectSyntaxNode>(OntologyConstants.Relationships.Contains)]
public record SyntaxStructureNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.SyntaxStructure;
}
