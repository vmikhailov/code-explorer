using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer1_Physical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.File,
    idScheme: "{workspaceId}:f:{relativeFilePath}",
    purpose: "Represents a source code file containing parsable content.",
    layer: OntologyConstants.Layers.Physical,
    icon: "file-code",
    order: 1,
    pluralLabel: "Files"
)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.BelongsTo)]
public record FileNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    string FullPath,
    bool IsTest,
    string? ProjectKind = null,
    string? ProjectName = null,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    public FileNode(string id, string name, string path, string fullPath, Dictionary<string, string>? extensions = null)
        : this(id, name, path, fullPath, false, null, null, extensions)
    {
    }

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the file is part of a test suite or test project.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public string? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.File;
}
