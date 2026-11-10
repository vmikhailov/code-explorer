using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Nodes.Layer5_Logical;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Workspace,
    idScheme: "{workspaceId}",
    purpose: "Represents the absolute root of the workspace directory hierarchy.",
    layer: OntologyConstants.Layers.Workspace
)]
[OntologyEdge<FilesStructureNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProjectsStructureNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<SyntaxStructureNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<SemanticStructureNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<LogicalStructureNode>(OntologyConstants.Relationships.Contains)]
public record WorkspaceNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("indexed_at"), OntologyProperty("Timestamp when the workspace was indexed (ISO 8601 UTC).")]
    public string? IndexedAt { get; set; }

    [JsonPropertyName("package_name"), OntologyProperty("Primary package name declared in the workspace root manifest.")]
    public string? PackageName { get; set; }

    [JsonPropertyName("package_version"), OntologyProperty("Primary package version declared in the workspace root manifest.")]
    public string? PackageVersion { get; set; }

    [JsonPropertyName("publisher"), OntologyProperty("Publisher identifier declared in the workspace root manifest.", isSystem: true)]
    public string? Publisher { get; set; }

    [JsonPropertyName("has_vscode_engine"), OntologyProperty("Whether the workspace root manifest targets the VS Code extension engine.", isSystem: true)]
    public bool? HasVsCodeEngine { get; set; }

    [JsonPropertyName("has_contributes"), OntologyProperty("Whether the workspace root manifest declares VS Code extension contributions.", isSystem: true)]
    public bool? HasContributes { get; set; }

    [JsonPropertyName("activation_events"), OntologyProperty("Comma-separated VS Code extension activation events.", isSystem: true)]
    public string? ActivationEvents { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Workspace;
}
