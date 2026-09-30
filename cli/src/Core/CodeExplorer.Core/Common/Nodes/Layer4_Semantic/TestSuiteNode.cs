using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.TestSuite,
    idScheme: "{workspaceId}:suite:{suiteName}",
    purpose: "Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite).",
    layer: OntologyConstants.Layers.Semantic,
    icon: "flask",
    order: 7,
    pluralLabel: "Test Suites"
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DeployedBy)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.Tests)]
[OntologyEdge<LibraryNode>(OntologyConstants.Relationships.Tests)]
[OntologyEdge<AppNode>(OntologyConstants.Relationships.Tests)]
[OntologyEdge<WorkerNode>(OntologyConstants.Relationships.Tests)]
[OntologyEdge<CliToolNode>(OntologyConstants.Relationships.Tests)]
public record TestSuiteNode(
    string Id,
    [property: OntologyProperty("The test suite name.")] string Name,
    [property: OntologyProperty("The path of the test suite directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.TestSuite;
}
