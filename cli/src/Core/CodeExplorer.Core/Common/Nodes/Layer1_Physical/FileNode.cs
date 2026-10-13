using System.Text.Json.Serialization;

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
public record FileNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    string FullPath,
    bool IsTest,
    string? ProjectKind = null,
    string? ProjectName = null,
    int? LineCount = null,
    long? SizeBytes = null,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    public FileNode(string id, string name, string path, string fullPath, Dictionary<string, string>? extensions = null)
        : this(id, name, path, fullPath, false, null, null, null, null, extensions)
    {
    }

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the file is part of a test suite or test project.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public string? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonPropertyName("line_count"), OntologyProperty("Total lines of code in this file.")]
    public int? LineCount { get; set; } = LineCount;

    [JsonPropertyName("size_bytes"), OntologyProperty("Size of the file in bytes.")]
    public long? SizeBytes { get; set; } = SizeBytes;

    [JsonIgnore]
    public string? ContentHash { get; set; }

    [JsonIgnore]
    public DateTime? LastModifiedUtc { get; set; }

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.File;
}
