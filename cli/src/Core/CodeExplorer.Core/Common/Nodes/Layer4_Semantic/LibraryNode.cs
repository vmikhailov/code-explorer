using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Library,
    idScheme: "{workspaceId}:library:{libraryName}",
    purpose: "Represents a shared library, utility module, DTO package, or domain contract reused across services.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "library",
    order: 5,
    pluralLabel: "Libraries & SDKs"
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DeployedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Contains)]
public record LibraryNode(
    string Id,
    [property: OntologyProperty("The library name.")] string Name,
    [property: OntologyProperty("The path of the library directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Library;
}
