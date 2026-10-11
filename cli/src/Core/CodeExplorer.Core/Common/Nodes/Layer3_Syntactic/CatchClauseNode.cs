using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.CatchClause,
    idScheme: "{workspaceId}:catch:{filePath}:{line}:{col}",
    purpose: "Represents a catch or except block in structured exception handling.",
    layer: OntologyConstants.Layers.Syntactic,
    icon: "shield-alert",
    order: 41,
    pluralLabel: "Catch Clauses"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record CatchClauseNode(
    string Id,
    [property: OntologyProperty("The identifier or descriptor of the catch clause.")] string Name,
    [property: OntologyProperty("The declared exception type caught by this block, if specified.")] string ExceptionType,
    [property: OntologyProperty("The relative file path where the catch clause occurs.")] string FilePath,
    [property: OntologyProperty("The 1-indexed line number where the catch clause starts.")] int Line,
    [property: OntologyProperty("The 1-indexed column number where the catch clause starts.")] int Column,
    bool IsEmpty,
    bool HasComment,
    int StatementCount,
    bool IsTest,
    string? ProjectKind = null,
    string? ProjectName = null,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    public CatchClauseNode(string id, string name, string exceptionType, string filePath, int line, int column, bool isEmpty, bool hasComment, int statementCount, Dictionary<string, string>? extensions = null)
        : this(id, name, exceptionType, filePath, line, column, isEmpty, hasComment, statementCount, false, null, null, extensions)
    {
    }

    [JsonPropertyName("is_empty"), OntologyProperty("Indicates whether the catch body contains no executable statements.")]
    public bool IsEmpty { get; set; } = IsEmpty;

    [JsonPropertyName("has_comment"), OntologyProperty("Indicates whether an empty catch block contains an explanatory comment.")]
    public bool HasComment { get; set; } = HasComment;

    [JsonPropertyName("statement_count"), OntologyProperty("The number of executable statements inside the catch body.")]
    public int StatementCount { get; set; } = StatementCount;

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the catch clause occurs within test code.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public string? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.CatchClause;

    [JsonIgnore]
    public override string Path { get; init; } = FilePath;
}
