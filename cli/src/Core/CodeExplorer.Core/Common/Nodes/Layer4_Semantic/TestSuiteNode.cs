using System.Text.Json.Serialization;
using CodeExplorer.Cypher.Common;

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
[OntologyEdge<ModuleNode>(OntologyConstants.Relationships.Tests)]
public record TestSuiteNode(
    string Id,
    string Name,
    string Path,
    ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : ModuleNode(Id, Name, Path, ProjectType, CustomProperties)
{
    public TestSuiteNode(string Id, string Name, string Path, string ProjectType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), CustomProperties)
    {
    }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.TestSuite;
}
