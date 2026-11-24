using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Scope,
    idScheme: "{workspaceId}:scope:{filePath}:{kind}:{line}:{col}",
    purpose: "Represents a lexical, control-flow, or resource-management scope block (try, catch, finally, loop, branch, resource/using/defer, lock, unsafe, task_spawn).",
    layer: OntologyConstants.Layers.Syntactic,
    icon: "shield-alert",
    order: 41,
    pluralLabel: "Control & Resource Scopes"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<ScopeNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Calls)]
public record ScopeNode(
    string Id,
    [property: OntologyProperty("The identifier or descriptor of the scope.")] string Name,
    [property: JsonPropertyName("kind"), OntologyProperty("The strongly-typed kind of the scope (try, catch, finally, loop, branch, resource, lock, unsafe, task_spawn).")] ScopeKind ScopeKind,
    [property: JsonPropertyName("target_type"), OntologyProperty("The declared exception type, resource type, or lock target associated with this scope, if specified.")] string TargetType,
    [property: OntologyProperty("The relative file path where the scope occurs.")] string FilePath,
    [property: OntologyProperty("The 1-indexed line number where the scope starts.")] int Line,
    [property: OntologyProperty("The 1-indexed column number where the scope starts.", isSystem: true)] int Column,
    bool IsEmpty,
    bool HasComment,
    int StatementCount,
    bool IsTest,
    ProjectEntityKind? ProjectKind = null,
    string? ProjectName = null,
    int EndLine = 0,
    int Depth = 1,
    int LoopDepth = 0,
    int AwaitCount = 0,
    int BranchCount = 0,
    bool IsAsync = false,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public ScopeNode(
        string id,
        string name,
        string exceptionOrTargetType,
        string filePath,
        int line,
        int column,
        bool isEmpty,
        bool hasComment,
        int statementCount,
        Dictionary<string, string>? customProperties = null)
        : this(id, name, ScopeKind.Catch, exceptionOrTargetType, filePath, line, column, isEmpty, hasComment, statementCount, false, null, null, line, 1, 0, 0, 1, false, customProperties)
    {
    }

    public ScopeNode(
        string id,
        string name,
        string exceptionOrTargetType,
        string filePath,
        int line,
        int column,
        bool isEmpty,
        bool hasComment,
        int statementCount,
        bool isTest,
        ProjectEntityKind? projectKind = null,
        string? projectName = null,
        Dictionary<string, string>? customProperties = null)
        : this(id, name, ScopeKind.Catch, exceptionOrTargetType, filePath, line, column, isEmpty, hasComment, statementCount, isTest, projectKind, projectName, line, 1, 0, 0, 1, false, customProperties)
    {
    }

    [JsonPropertyName("exception_type"), OntologyProperty("The declared exception type caught by a catch scope (alias of target_type).")]
    public string ExceptionType
    {
        get => TargetType;
        init => TargetType = value;
    }

    [JsonPropertyName("end_line"), OntologyProperty("The 1-indexed line number where the scope ends.", isSystem: true)]
    public int EndLine { get; set; } = EndLine > 0 ? EndLine : Line;

    [JsonPropertyName("depth"), OntologyProperty("The 1-indexed structural nesting depth of the scope inside its enclosing function.")]
    public int Depth { get; set; } = Depth;

    [JsonPropertyName("loop_depth"), OntologyProperty("The nesting depth of enclosing loops at this scope (0 = outside loops, 1 = O(N), 2 = O(N^2)).")]
    public int LoopDepth { get; set; } = LoopDepth;

    [JsonPropertyName("await_count"), OntologyProperty("The number of await or asynchronous suspension points inside this scope.")]
    public int AwaitCount { get; set; } = AwaitCount;

    [JsonPropertyName("branch_count"), OntologyProperty("The number of branching predicates or decision points contributed by this scope.")]
    public int BranchCount { get; set; } = BranchCount;

    [JsonPropertyName("is_async"), OntologyProperty("Indicates whether the scope uses asynchronous iteration or disposal (e.g. await using, for await).")]
    public bool IsAsync { get; set; } = IsAsync;

    [JsonPropertyName("is_empty"), OntologyProperty("Indicates whether the scope body contains no executable statements.")]
    public bool IsEmpty { get; set; } = IsEmpty;

    [JsonPropertyName("has_comment"), OntologyProperty("Indicates whether an empty scope block contains an explanatory comment.")]
    public bool HasComment { get; set; } = HasComment;

    [JsonPropertyName("statement_count"), OntologyProperty("The number of executable statements inside the scope body.")]
    public int StatementCount { get; set; } = StatementCount;

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the scope occurs within test code.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public ProjectEntityKind? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Scope;

    [JsonIgnore]
    public override string Path { get; init; } = FilePath;
}
