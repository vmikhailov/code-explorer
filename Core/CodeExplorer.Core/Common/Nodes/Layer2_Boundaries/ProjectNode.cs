using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes;

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
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: OntologyProperty("The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript').")] string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Project;
}
