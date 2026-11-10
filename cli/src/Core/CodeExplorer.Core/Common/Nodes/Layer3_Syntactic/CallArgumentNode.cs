using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Semantics;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.CallArgument,
    idScheme: "{workspaceId}:arg:{filePath}:{line}:{col}:{slot}",
    purpose: "Represents a semantically colored invocation argument sliced backward to its provenance origin.",
    layer: OntologyConstants.Layers.Syntactic,
    icon: "symbol-variable",
    order: 39,
    pluralLabel: "Semantic Arguments"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<ParameterNode>(OntologyConstants.Relationships.FlowsTo)]
public record CallArgumentNode(
    string Id,
    [property: OntologyProperty("Display label or slot representation of the argument.")] string Name,
    [property: JsonPropertyName("callee"), OntologyProperty("The method, function, or constructor being invoked.")] string Callee,
    [property: JsonPropertyName("arg_index"), OntologyProperty("The 0-indexed positional slot of the argument.")] int ArgIndex,
    [property: JsonPropertyName("property_key"), OntologyProperty("The object property key when passed via an options object (e.g. sql, text, url, topic).")] string? PropertyKey,
    [property: JsonPropertyName("raw_expression"), OntologyProperty("The syntactic expression passed at the call site.")] string RawExpression,
    [property: JsonPropertyName("resolved_value"), OntologyProperty("The concrete value resolved via backward constant/template slicing, if determinable.")] string? ResolvedValue,
    [property: JsonPropertyName("nature"), OntologyProperty("The semantic nature of the argument slot.")] ParameterNature Nature,
    [property: JsonPropertyName("provenance"), OntologyProperty("Where the argument's value originates (InternalConstant, CallerParameter, ConfigOrEnv, MemberOrField, etc.).")] ArgumentProvenance Provenance,
    [property: JsonPropertyName("expression_role"), OntologyProperty("How the expression participates in constructing the argument value.")] ExpressionRole Role,
    [property: JsonPropertyName("value_kind"), OntologyProperty("Fine-grained classification of the resolved constant or template value.")] ConstantValueKind ValueKind,
    [property: JsonPropertyName("file_path"), OntologyProperty("The relative file path where the call occurs.")] string FilePath,
    [property: JsonPropertyName("line"), OntologyProperty("The 1-indexed line number of the call argument.")] int Line,
    [property: JsonPropertyName("column"), OntologyProperty("The 1-indexed column number of the call argument.", isSystem: true)] int Column,
    [property: JsonPropertyName("origin_file"), OntologyProperty("The relative file path where the value was originally defined, if distinct.")] string? OriginFile = null,
    [property: JsonPropertyName("origin_line"), OntologyProperty("The 1-indexed line number where the value was originally defined.")] int? OriginLine = null,
    [property: JsonPropertyName("origin_symbol"), OntologyProperty("The variable, constant, field, parameter, or config key from which the value originated.")] string? OriginSymbol = null,
    bool IsTest = false,
    ProjectEntityKind? ProjectKind = null,
    string? ProjectName = null,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the call occurs within test code.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public ProjectEntityKind? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.CallArgument;

    [JsonIgnore]
    public override string Path { get; init; } = FilePath;
}
