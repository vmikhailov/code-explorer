using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
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
[OntologyEdge<TableNode>(OntologyConstants.Relationships.ReadsFrom)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.WritesTo)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.AccessedBy)]
public record QueryNode(
    string Id,
    string Name,
    string QueryText,
    string Path,
    int StartLine = 0,
    [property: OntologyProperty("The ending line number of the query.", isSystem: true)] int EndLine = 0,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("verb")]
    public string? Verb { get; set; }

    [JsonPropertyName("operation")]
    public string? Operation { get; set; }

    [JsonPropertyName("evidence_kind")]
    public string? EvidenceKind { get; set; }

    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("line_end"), OntologyProperty("The ending line number of the query.", isSystem: true)]
    public int? LineEnd { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Query;
}
