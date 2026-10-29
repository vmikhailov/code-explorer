using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.EntryPoint,
    idScheme: "{workspaceId}:entry:{entryType}:{name}",
    purpose: "Represents the binary, process, or bundle bootstrap entry point of an executable project (e.g. Program.Main, func main, __main__, index.tsx root mount).",
    layer: OntologyConstants.Layers.ProjectBoundary,
    icon: "zap",
    order: 2,
    pluralLabel: "Entry Points"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.Triggers)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.BelongsTo)]
public record EntryPointNode(
    string Id,
    [property: OntologyProperty("The name of the binary or process entry point.")] string Name,
    [property: OntologyProperty("The path of the entry point file relative to its parent container.")] string Path,
    [property: JsonPropertyName("entry_type"), OntologyProperty("The kind of binary/process entry point (e.g. main-method, top-level, script-main, bundle-entry).")] string EntryType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.EntryPoint;
}
