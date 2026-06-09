using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.SyntaxStructure,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:syntax_structure",
    purpose: "Represents an intermediate node grouping all AST/syntactic declarations of a project.",
    layer: OntologyConstants.Layers.Syntactic
)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.BelongsTo)]
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
