using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Library,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:",
    purpose: "Represents a shared library, utility module, DTO package, or domain contract reused across services.",
    layer: OntologyConstants.Layers.ProjectBoundary
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.Contains)]
public record LibraryNode : ProjectNode
{
    public LibraryNode(
        string id,
        string name,
        string path,
        string projectType,
        Dictionary<string, string>? extensions = null)
        : base(id, name, path, projectType, OntologyConstants.ProjectRoles.SharedLibrary, true, extensions)
    {
    }

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Library;
}
