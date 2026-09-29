using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Package,
    idScheme: "{workspaceId}:pkg:{ecosystem}:{packageName}",
    purpose: "Represents an external dependency package or workspace package referenced or produced by projects.",
    layer: OntologyConstants.Layers.ProjectBoundary,
    icon: "package",
    order: 2,
    pluralLabel: "Packages & Dependencies"
)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.Produces)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.ImplementedBy)]
public record PackageNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The package version.")] string Version,
    [property: OntologyProperty("Whether this package is produced internally by a workspace project.")] bool IsInternal,
    [property: OntologyProperty("The package ecosystem (e.g. nuget, npm, maven, pip, go).")] string Ecosystem,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path = "",
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Package;

    [property: OntologyProperty("Whether this package is an external third-party dependency.")]
    public bool IsExternal => !IsInternal;

    public string Type => Ecosystem;
}
