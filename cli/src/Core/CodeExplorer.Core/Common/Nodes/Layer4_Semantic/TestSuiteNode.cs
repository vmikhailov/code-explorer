using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.TestSuite,
    idScheme: "{workspaceId}:suite:{suiteName}",
    purpose: "Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "beaker",
    order: 7,
    pluralLabel: "Test Suites"
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.ImplementedBy)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.Tests)]
[OntologyEdge<LibraryNode>(OntologyConstants.Relationships.Tests)]
[OntologyEdge<AppNode>(OntologyConstants.Relationships.Tests)]
[OntologyEdge<WorkerNode>(OntologyConstants.Relationships.Tests)]
[OntologyEdge<CliToolNode>(OntologyConstants.Relationships.Tests)]
public record TestSuiteNode(
    string Id,
    [property: OntologyProperty("The test suite name.")] string Name,
    [property: OntologyProperty("The path of the test suite directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public TestSuiteNode(string Id, string Name, string Path, string ProjectType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), CustomProperties)
    {
    }

    [JsonPropertyName("role"), OntologyProperty("The primary semantic role of the test suite.")]
    public string? Role { get; set; }

    [JsonPropertyName("is_library"), OntologyProperty("Indicates whether the project is a library.")]
    public bool? IsLibrary { get; set; }

    [JsonPropertyName("primary_role"), OntologyProperty("The primary architectural role.")]
    public string? PrimaryRole { get; set; }

    [JsonPropertyName("secondary_roles"), OntologyProperty("Comma-separated secondary roles.", isSystem: true)]
    public string? SecondaryRoles { get; set; }

    [JsonPropertyName("all_roles"), OntologyProperty("All detected roles.", isSystem: true)]
    public string? AllRoles { get; set; }

    [JsonPropertyName("framework"), OntologyProperty("The detected test framework.")]
    public string? Framework { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.TestSuite;
}
