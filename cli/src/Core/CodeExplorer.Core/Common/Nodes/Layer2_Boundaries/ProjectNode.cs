using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Project,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:",
    purpose: "Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).",
    layer: OntologyConstants.Layers.ProjectBoundary
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
public record ProjectNode(
    string Id,
    string Name,
    string Path,
    string ProjectType,
    string Role = "Service",
    bool IsLibrary = false,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    public ProjectNode(string id, string name, string path, string projectType, Dictionary<string, string>? extensions)
        : this(id, name, path, projectType, "Service", false, extensions)
    {
    }

    [OntologyProperty("The name of the entity.")]
    public string Name { get; set; } = Name;

    [OntologyProperty("The path of the folder or file relative to its parent container.")]
    public override string Path { get; init; } = Path;

    [OntologyProperty("The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript').")]
    public string ProjectType { get; set; } = ProjectType;

    [JsonPropertyName("role"), OntologyProperty("Architectural role of the project (e.g. Service, SharedLibrary, FrontendApp, Worker, CliTool, Test).")]
    public string Role { get; set; } = Role;

    [JsonPropertyName("is_library"), OntologyProperty("Indicates whether the project is a shared library rather than an executable application.")]
    public bool IsLibrary { get; set; } = IsLibrary;

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Project;
}
