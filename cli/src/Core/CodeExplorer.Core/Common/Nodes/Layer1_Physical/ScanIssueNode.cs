using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer1_Physical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.ScanIssue,
    idScheme: "{workspaceId}:issue:{relativeFilePath}:{stage}:{line}:{col}",
    purpose: "Represents a file scanning, manifest, or AST parsing issue encountered during indexing so affected files can be inspected or reparsed.",
    layer: OntologyConstants.Layers.Physical,
    icon: "warning",
    order: 4,
    pluralLabel: "Scan Issues"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record ScanIssueNode(
    string Id,
    [property: OntologyProperty("The short summary or title of the scanning/parsing issue.")] string Name,
    [property: OntologyProperty("The relative path of the file or folder where the issue occurred.")] string Path,
    [property: OntologyProperty("The indexing pipeline stage where the issue occurred (e.g. Layer1_Physical, Layer2_Boundaries, Layer3_Syntactic, Layer3_Visitor, Layer4_Semantic, Layer5_Global).")] string Stage,
    [property: OntologyProperty("The issue category (e.g. syntax_error, parse_exception, visitor_exception, manifest_error, io_error, reference_error).")] string Category,
    [property: OntologyProperty("The severity of the issue (error or warning).")] string Severity,
    [property: OntologyProperty("The detailed error message or syntax snippet.")] string Message,
    [property: OntologyProperty("The 1-based starting line number of the issue, if known.")] int? StartLine = null,
    [property: OntologyProperty("The 1-based ending line number of the issue, if known.")] int? EndLine = null,
    [property: OntologyProperty("The 1-based starting column number of the issue, if known.")] int? StartCol = null,
    [property: OntologyProperty("The 1-based ending column number of the issue, if known.")] int? EndCol = null,
    [property: OntologyProperty("The CLR exception type name if thrown.")] string? ExceptionType = null,
    [property: OntologyProperty("The parser or language associated with the file.")] string? Language = null,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.ScanIssue;
}
