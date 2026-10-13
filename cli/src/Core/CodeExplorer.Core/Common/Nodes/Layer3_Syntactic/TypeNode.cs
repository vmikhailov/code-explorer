using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Type,
    idScheme: "{workspaceId}:sym:{filePath}:Type:{name}:{line}",
    purpose: "Represents a type declaration (Class, Interface, Struct, Record, Enum, or Union type).",
    layer: OntologyConstants.Layers.Syntactic,
    icon: "symbol-class",
    order: 1,
    pluralLabel: "Types (Classes, Interfaces)"
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
    bool IsTest,
    string? ProjectKind = null,
    string? ProjectName = null,
    int? StageOrder = null,
    int? Loc = null,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    public TypeNode(string id, string name, string symbol, string filePath, string path, int startLine, int endLine, int startCol, int endCol, string typeKind, Dictionary<string, string>? extensions = null)
        : this(id, name, symbol, filePath, path, startLine, endLine, startCol, endCol, typeKind, false, null, null, null, null, extensions)
    {
    }

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the type is declared within test code.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public string? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonPropertyName("stage_order"), OntologyProperty("Strict pipeline stage ordering index (e.g. 1=Layer1, 2=Layer2, 3=Layer3, 4=Layer4, 5=Layer5, 6=PostIndex).")]
    public int? StageOrder { get; set; } = StageOrder;

    [JsonPropertyName("loc"), OntologyProperty("Lines of code span for this type declaration.")]
    public int? Loc { get; set; } = Loc ?? Math.Max(1, EndLine - StartLine + 1);

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Type;
}
