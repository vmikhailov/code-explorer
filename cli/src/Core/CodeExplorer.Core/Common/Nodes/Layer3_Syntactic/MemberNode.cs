using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Member,
    idScheme: "{workspaceId}:sym:{filePath}:Member:{name}:{line}",
    purpose: "Represents a declared field, property, parameter, or local variable.",
    layer: OntologyConstants.Layers.Syntactic,
    icon: "symbol-field",
    order: 3,
    pluralLabel: "Members & Fields"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.OfType)]
public record MemberNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("A globally unique ID for this symbol scope.")] string Symbol,
    [property: OntologyProperty("The relative path of the declaring file.")] string FilePath,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: OntologyProperty("The starting line number (1-indexed) of the declaration.")] int StartLine,
    [property: OntologyProperty("The ending line number (1-indexed) of the declaration.")] int EndLine,
    [property: OntologyProperty("The starting column number of the declaration.")] int StartCol,
    [property: OntologyProperty("The ending column number of the declaration.")] int EndCol,
    [property: JsonPropertyName("kind"), OntologyProperty("The specific member kind (field, property, parameter, variable).")] MemberKind MemberKind,
    [property: JsonPropertyName("data_type"), OntologyProperty("The declared data type of the member.")] string? DataType = null,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public MemberNode(
        string Id,
        string Name,
        string Symbol,
        string FilePath,
        string Path,
        int StartLine,
        int EndLine,
        int StartCol,
        int EndCol,
        string MemberKind,
        string? DataType = null,
        Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Symbol, FilePath, Path, StartLine, EndLine, StartCol, EndCol, TaxonomyEnumExtensions.ParseMemberKind(MemberKind), DataType, CustomProperties)
    {
    }

    [JsonPropertyName("parameter_index"), OntologyProperty("0-based formal parameter index when the member is a function parameter.")]
    public int? ParameterIndex { get; set; }

    [JsonPropertyName("variable_type"), OntologyProperty("Variable classification tags (e.g. parameter, config, etl, constant, global, property, field).")]
    public string? VariableClassification { get; set; }

    [JsonPropertyName("initializer_expression"), OntologyProperty("Bounded initializer expression text.")]
    public string? InitializerExpression { get; set; }

    [JsonPropertyName("is_constant"), OntologyProperty("Indicates whether the member is a compile-time or immutable constant.")]
    public bool? IsConstant { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Member;
}
