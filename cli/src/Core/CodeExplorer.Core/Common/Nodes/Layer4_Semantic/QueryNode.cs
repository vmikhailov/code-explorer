using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Query,
    idScheme: "{workspaceId}:q:{queryHash}",
    purpose: "Represents a SQL query.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "search",
    order: 30,
    pluralLabel: "SQL Queries"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.ReadsFrom)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.WritesTo)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.TransitivelyCalls)]
public record QueryNode(
    string Id,
    [property: OntologyProperty("Short query identifier or operation summary.")] string Name,
    [property: JsonPropertyName("query_text"), OntologyProperty("Extracted SQL or query statement text.")] string QueryText,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("start_line"), OntologyProperty("1-indexed starting line number of the query in source.")] int StartLine = 0,
    [property: OntologyProperty("The ending line number of the query.", isSystem: true)] int EndLine = 0,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("verb"), OntologyProperty("SQL DML/DDL verb (SELECT, INSERT, UPDATE, DELETE, MERGE, EXEC).")]
    public string? Verb { get; set; }

    [JsonPropertyName("operation"), OntologyProperty("Normalized data access operation (read, write, schema).")]
    public string? Operation { get; set; }

    [JsonPropertyName("evidence_kind"), OntologyProperty("Extraction provenance kind (query, orm, migration, procedure).")]
    public string? EvidenceKind { get; set; }

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file path declaring the query.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("line"), OntologyProperty("1-indexed source line of the query call site.")]
    public int? Line { get; set; }

    [JsonPropertyName("line_end"), OntologyProperty("The ending line number of the query.", isSystem: true)]
    public int? LineEnd { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Query;
}
