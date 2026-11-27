using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Function,
    idScheme: "{workspaceId}:sym:{filePath}:Function:{name}:{line}",
    purpose: "Represents a parsed method, function, subroutine, or procedure.",
    layer: OntologyConstants.Layers.Syntactic,
    icon: "symbol-method",
    order: 2,
    pluralLabel: "Functions & Methods"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Calls)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Calls)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.UsesType)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Instantiates)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Instantiates)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Instantiates)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Implements)]
[OntologyEdge<ParameterNode>(OntologyConstants.Relationships.HasParameter)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.PassesArgument)]
[OntologyEdge<CallLiteralNode>(OntologyConstants.Relationships.PassesLiteral)]
[OntologyEdge<ScopeNode>(OntologyConstants.Relationships.Declares)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Declares)]
[OntologyEdge<MemberNode>(OntologyConstants.Relationships.Declares)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Declares)]
public record FunctionNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("A globally unique ID for this symbol scope.")] string Symbol,
    [property: OntologyProperty("The relative path of the declaring file.")] string FilePath,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: OntologyProperty("The starting line number (1-indexed) of the declaration.")] int StartLine,
    [property: OntologyProperty("The ending line number (1-indexed) of the declaration.", isSystem: true)] int EndLine,
    [property: OntologyProperty("The starting column number of the declaration.", isSystem: true)] int StartCol,
    [property: OntologyProperty("The ending column number of the declaration.", isSystem: true)] int EndCol,
    bool IsTest,
    ProjectEntityKind? ProjectKind = null,
    string? ProjectName = null,
    int? StageOrder = null,
    int? Loc = null,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public FunctionNode(string id, string name, string symbol, string filePath, string path, int startLine, int endLine, int startCol, int endCol, Dictionary<string, string>? customProperties = null)
        : this(id, name, symbol, filePath, path, startLine, endLine, startCol, endCol, false, null, null, null, null, customProperties)
    {
    }

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the function is declared within test code.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("test_framework"), OntologyProperty("Detected test runner or framework (e.g. xunit, nunit, mstest, jest, pytest, go-test, junit).")]
    public string? TestFramework { get; set; }

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public ProjectEntityKind? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonPropertyName("stage_order"), OntologyProperty("Strict pipeline stage ordering index (e.g. 1=Layer1, 2=Layer2, 3=Layer3, 4=Layer4, 5=Layer5, 6=PostIndex).", isSystem: true)]
    public int? StageOrder { get; set; } = StageOrder;

    [JsonPropertyName("loc"), OntologyProperty("Lines of code span for this function.")]
    public int? Loc { get; set; } = Loc ?? Math.Max(1, EndLine - StartLine + 1);

    [JsonPropertyName("is_exported"), OntologyProperty("Indicates whether the function is publicly exported from its module.")]
    public bool? IsExported { get; set; }

    [JsonPropertyName("is_module_init"), OntologyProperty("Indicates whether this node represents top-level module initialization code.")]
    public bool? IsModuleInit { get; set; }

    [JsonPropertyName("is_async"), OntologyProperty("Indicates whether the function is asynchronous.")]
    public bool? IsAsync { get; set; }

    [JsonPropertyName("is_unsafe"), OntologyProperty("Indicates whether the function uses unsafe code blocks or modifiers.")]
    public bool? IsUnsafe { get; set; }

    [JsonPropertyName("visibility"), OntologyProperty("Declared visibility or access modifier of the function (e.g. public, private, remote).")]
    public string? Visibility { get; set; }

    [JsonPropertyName("graphql_operation"), OntologyProperty("GraphQL client operation name or flag.")]
    public string? GraphqlOperation { get; set; }

    [JsonPropertyName("operation_type"), OntologyProperty("Operation type for GraphQL or RPC operations.")]
    public string? OperationType { get; set; }

    [JsonPropertyName("graphql_fields"), OntologyProperty("Comma-separated GraphQL root selection fields.")]
    public string? GraphqlFields { get; set; }

    [JsonPropertyName("attributes"), OntologyProperty("Comma-separated annotations, attributes, or decorators applied to this function.")]
    public string? Attributes { get; set; }

    [JsonPropertyName("cyclomatic_complexity"), OntologyProperty("McCabe cyclomatic complexity of the function (1 + total decision points across nested scopes).")]
    public int? CyclomaticComplexity { get; set; }

    [JsonPropertyName("cognitive_complexity"), OntologyProperty("Depth-weighted cognitive complexity score across nested control-flow scopes.")]
    public int? CognitiveComplexity { get; set; }

    [JsonPropertyName("max_nesting_depth"), OntologyProperty("Maximum structural control-flow nesting depth inside the function.")]
    public int? MaxNestingDepth { get; set; }

    [JsonPropertyName("max_loop_depth"), OntologyProperty("Maximum loop nesting depth inside the function (0 = no loops, 1 = O(N), 2 = O(N^2)).")]
    public int? MaxLoopDepth { get; set; }

    [JsonPropertyName("loop_await_count"), OntologyProperty("Total number of await suspension points executed inside loop scopes of this function.")]
    public int? LoopAwaitCount { get; set; }

    [JsonPropertyName("effective_loop_depth"), OntologyProperty("Maximum intra-procedural polynomial loop order inside the function, discounting constant-bounded loops and counting linear collection scans inside loops.")]
    public int? EffectiveLoopDepth { get; set; }

    [JsonPropertyName("loop_linear_scan_count"), OntologyProperty("Total number of linear collection scan calls (e.g. FirstOrDefault, Any, Where, List.Contains) executed inside non-bounded loops of this function.")]
    public int? LoopLinearScanCount { get; set; }

    [JsonPropertyName("has_unpartitioned_loop"), OntologyProperty("Indicates whether the function directly iterates over an unpartitioned lookup dictionary or workspace-wide collection.")]
    public bool? HasUnpartitionedLoop { get; set; }

    [JsonPropertyName("caller_loop_depth"), OntologyProperty("Maximum non-recursive loop nesting depth accumulated along incoming CALLS paths in the condensation DAG.")]
    public int? CallerLoopDepth { get; set; }

    [JsonPropertyName("transitive_loop_depth"), OntologyProperty("Inter-procedural polynomial complexity order (caller_loop_depth + effective_loop_depth) across non-recursive CALLS chains.")]
    public int? TransitiveLoopDepth { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Function;
}
