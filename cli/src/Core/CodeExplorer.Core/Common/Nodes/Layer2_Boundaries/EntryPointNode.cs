using CodeExplorer.Cypher.Common;
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
    [property: JsonPropertyName("entry_type"), OntologyProperty("The kind of binary/process entry point (e.g. main-method, top-level, script-main, bundle-entry).")] EntryPointType EntryType,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public EntryPointNode(
        string Id,
        string Name,
        string Path,
        string EntryType,
        Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtensions.ParseEntryPointType(EntryType), CustomProperties)
    {
    }

    [JsonPropertyName("file_path"), OntologyProperty("Relative path of the source file declaring the entry point.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line"), OntologyProperty("1-based starting line number of the entry point.")]
    public int? StartLine { get; set; }

    [JsonPropertyName("start_col"), OntologyProperty("1-based starting column number of the entry point.", isSystem: true)]
    public int? StartCol { get; set; }

    [JsonPropertyName("command"), OntologyProperty("Shell command executed by a lifecycle or task entry point.")]
    public string? Command { get; set; }

    [JsonPropertyName("hook_kind"), OntologyProperty("Lifecycle hook classification (e.g. install, build).")]
    public string? HookKind { get; set; }

    [JsonPropertyName("task_label"), OntologyProperty("Task label for VS Code folderOpen tasks.")]
    public string? TaskLabel { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.EntryPoint;
}
