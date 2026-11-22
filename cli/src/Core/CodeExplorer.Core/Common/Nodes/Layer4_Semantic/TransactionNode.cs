using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Transaction,
    idScheme: "{workspaceId}:tx:{subKind}:{scopeName}",
    purpose: "Represents an explicit, declarative, ambient, or pipeline-behavior database transaction boundary.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "shield-check",
    order: 25,
    pluralLabel: "Transaction Boundaries"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Wraps)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record TransactionNode(
    string Id,
    [property: OntologyProperty("The transaction boundary name (e.g. enclosing function or pipeline behavior).")] string Name,
    [property: OntologyProperty("The workspace-relative path of the declaring file.")] string Path,
    [property: JsonPropertyName("sub_kind"), OntologyProperty("How the transaction boundary is established (explicit, declarative, pipeline_behavior, ambient).")] TransactionKind SubKind,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("is_read_only"), OntologyProperty("Indicates whether the transaction is explicitly configured as read-only.")]
    public bool IsReadOnly { get; set; }

    [JsonPropertyName("isolation_level"), OntologyProperty("Configured transaction isolation level when specified.")]
    public string? IsolationLevel { get; set; }

    [JsonPropertyName("framework"), OntologyProperty("Detected transaction API or framework (e.g. efcore, spring-tx, typeorm, prisma, database-sql).")]
    public string? Framework { get; set; }

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file declaring the transaction boundary.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line"), OntologyProperty("1-indexed starting line number of the transaction boundary.")]
    public int? StartLine { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Transaction;
}
