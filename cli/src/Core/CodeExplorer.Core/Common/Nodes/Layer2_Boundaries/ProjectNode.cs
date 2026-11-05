using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Project,
    idScheme: "{workspaceId}:p:{relativeProjectDir}:",
    purpose: "Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).",
    layer: OntologyConstants.Layers.ProjectBoundary,
    icon: "project",
    order: 1,
    pluralLabel: "Projects"
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.Produces)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.HasEntrypoint)]
public record ProjectNode(
    string Id,
    string Name,
    string Path,
    ProjectLanguage ProjectType,
    string Role = "Service",
    bool IsLibrary = false,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    public ProjectNode(string Id, string Name, string Path, ProjectLanguage ProjectType, Dictionary<string, string>? Extensions)
        : this(Id, Name, Path, ProjectType, "Service", false, Extensions)
    {
    }

    public ProjectNode(string Id, string Name, string Path, string ProjectType, string Role = "Service", bool IsLibrary = false, Dictionary<string, string>? Extensions = null)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), Role, IsLibrary, Extensions)
    {
    }

    public ProjectNode(string Id, string Name, string Path, string ProjectType, Dictionary<string, string>? Extensions)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), "Service", false, Extensions)
    {
    }

    [JsonPropertyName("name"), OntologyProperty("The name of the entity.")]
    public string Name { get; set; } = Name;

    [JsonPropertyName("path"), OntologyProperty("The path of the folder or file relative to its parent container.")]
    public override string Path { get; init; } = Path;

    [JsonPropertyName("project_type"), OntologyProperty("The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript').")]
    public ProjectLanguage ProjectType { get; set; } = ProjectType;

    [JsonPropertyName("language"), OntologyProperty("The programming language (e.g. 'csharp', 'go', 'python', 'typescript').")]
    public string Language
    {
        get => ProjectType.ToCanonicalString();
        set => ProjectType = TaxonomyEnumExtendedExtensions.ParseProjectLanguage(value);
    }

    [JsonPropertyName("kind"), OntologyProperty("The physical entity classification kind (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test).")]
    public string KindValue => EntityKind != ProjectEntityKind.Unknown ? EntityKind.ToString() : Role;

    [JsonPropertyName("role"), OntologyProperty("Architectural role of the project (e.g. Service, SharedLibrary, FrontendApp, Worker, CliTool, Test).")]
    public string Role { get; set; } = Role;

    private ProjectArchetype? _archetype;
    [JsonPropertyName("archetype"), OntologyProperty("Architectural archetype of the project (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon).")]
    public ProjectArchetype Archetype
    {
        get => _archetype ?? TaxonomyEnumExtendedExtensions.ParseProjectArchetype(Extensions?.GetValueOrDefault("archetype") ?? Role);
        set => _archetype = value;
    }

    private string? _primaryRole;
    [JsonPropertyName("primary_role"), OntologyProperty("Primary architectural role of the project (e.g. WebService, Scheduler, WorkerService, SharedLibrary, FrontendFramework, CliFramework, TestFramework).")]
    public string? PrimaryRole
    {
        get => _primaryRole ?? Extensions?.GetValueOrDefault("primary_role") ?? Role;
        set => _primaryRole = value;
    }

    private string? _secondaryRoles;
    [JsonPropertyName("secondary_roles"), OntologyProperty("Secondary architectural roles detected in the project (e.g. ['Scheduler', 'QueueWorker']).")]
    public string? SecondaryRoles
    {
        get => _secondaryRoles ?? Extensions?.GetValueOrDefault("secondary_roles") ?? "[]";
        set => _secondaryRoles = value;
    }

    private string? _allRoles;
    [JsonPropertyName("all_roles"), OntologyProperty("All architectural roles detected in the project.")]
    public string? AllRoles
    {
        get => _allRoles ?? Extensions?.GetValueOrDefault("all_roles");
        set => _allRoles = value;
    }

    [JsonPropertyName("framework"), OntologyProperty("Detected primary framework of the project.")]
    public string? Framework { get; set; }

    [JsonPropertyName("is_library"), OntologyProperty("Indicates whether the project is a shared library rather than an executable application.")]
    public bool IsLibrary { get; set; } = IsLibrary;

    [JsonPropertyName("entity_kind"), OntologyProperty("The physical entity classification (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test).")]
    public ProjectEntityKind EntityKind { get; set; } = ProjectEntityKind.Unknown;

    [JsonPropertyName("sub_kind"), OntologyProperty("Sub-classification for apps (e.g. None, Web, Mobile, Desktop, Cli).")]
    public ProjectEntitySubKind SubKind { get; set; } = ProjectEntitySubKind.None;

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether this project is a test project.")]
    public bool IsTest => EntityKind == ProjectEntityKind.Test || Role.Equals(OntologyConstants.ProjectRoles.Test, StringComparison.OrdinalIgnoreCase) || WorkspaceConventions.IsTestFilePath(Path);

    [JsonPropertyName("project_kind"), OntologyProperty("The physical entity classification kind.")]
    public ProjectEntityKind ProjectKind => EntityKind;

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Project;

    /// <summary>
    /// Information extracted by shared file parsers (environment files, application settings, ...).
    /// In-memory only; produced once during project creation and consumed by later layers.
    /// </summary>
    [JsonIgnore]
    public List<CodeExplorer.Core.Parser.IProjectEnrichment> Enrichments { get; } = [];
}
