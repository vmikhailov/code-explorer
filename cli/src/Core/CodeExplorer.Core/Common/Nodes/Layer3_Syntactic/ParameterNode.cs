using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Semantics;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Parameter,
    idScheme: "{workspaceId}:param:{functionSymbol}:{index}:{name}",
    purpose: "Represents a semantically colored function or method parameter that flows into an architectural sink.",
    layer: OntologyConstants.Layers.Syntactic,
    icon: "symbol-parameter",
    order: 38,
    pluralLabel: "Semantic Parameters"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.FlowsTo)]
public record ParameterNode(
    string Id,
    [property: OntologyProperty("The parameter identifier name.")] string Name,
    [property: JsonPropertyName("function_symbol"), OntologyProperty("The symbol ID of the declaring function or method.")] string FunctionSymbol,
    [property: JsonPropertyName("index"), OntologyProperty("The 0-indexed position of the parameter in the function signature.")] int ParamIndex,
    [property: JsonPropertyName("data_type"), OntologyProperty("The declared parameter type annotation, if present.")] string? DataType,
    [property: JsonPropertyName("file_path"), OntologyProperty("The relative file path of the declaring function.")] string FilePath,
    [property: JsonPropertyName("line"), OntologyProperty("The 1-indexed declaration line number.")] int Line,
    [property: JsonPropertyName("nature"), OntologyProperty("The semantic nature inferred from intra- or inter-procedural sink usage.")] ParameterNature Nature,
    [property: JsonPropertyName("sink_callee"), OntologyProperty("The primary sink callee that determined the parameter's semantic nature.")] string? SinkCallee = null,
    bool IsTest = false,
    ProjectEntityKind? ProjectKind = null,
    string? ProjectName = null,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the parameter belongs to test code.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public ProjectEntityKind? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Parameter;

    [JsonIgnore]
    public override string Path { get; init; } = FilePath;
}
