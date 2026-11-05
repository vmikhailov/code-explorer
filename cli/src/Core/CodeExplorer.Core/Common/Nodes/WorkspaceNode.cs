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
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonPropertyName("indexed_at"), OntologyProperty("Timestamp when the workspace was indexed (ISO 8601 UTC).")]
    public string? IndexedAt { get; set; }

    [JsonPropertyName("has_native_node"), OntologyProperty("Indicates whether native .node binary addons were discovered in the workspace.")]
    public bool? HasNativeNode { get; set; }

    [JsonPropertyName("has_native_addon"), OntologyProperty("Indicates whether native binary addons (.node, .wasm, .dylib, .so) were discovered in the workspace.")]
    public bool? HasNativeAddon { get; set; }

    [JsonPropertyName("has_dotnet_dll"), OntologyProperty("Indicates whether compiled .NET .dll artifacts were discovered in the workspace.")]
    public bool? HasDotNetDll { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Workspace;
}
