using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Type,
    idScheme: "{workspaceId}:symbol:{filePath}:Type:{name}:{line}",
    purpose: "Represents a type declaration (Class, Interface, Struct, Record, Enum, or Union type).",
    layer: OntologyConstants.Layers.Syntactic
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.UsesType)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Implements)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.InheritsFrom)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.PotentialType)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.HasMethod)]
[OntologyEdge<MemberNode>(OntologyConstants.Relationships.HasMember)]
public record TypeNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("A globally unique ID for this symbol scope.")] string Symbol,
    [property: OntologyProperty("The relative path of the declaring file.")] string FilePath,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: OntologyProperty("The starting line number (1-indexed) of the declaration.")] int StartLine,
    [property: OntologyProperty("The ending line number (1-indexed) of the declaration.")] int EndLine,
    [property: OntologyProperty("The starting column number of the declaration.")] int StartCol,
    [property: OntologyProperty("The ending column number of the declaration.")] int EndCol,
    [property: JsonPropertyName("kind"), OntologyProperty("The specific type kind (class, interface, struct, record, enum).")] string TypeKind,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Type;
}
