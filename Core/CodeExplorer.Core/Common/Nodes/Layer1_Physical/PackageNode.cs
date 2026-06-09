using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes.Layer1_Physical;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Package,
    idScheme: "{workspaceId}:package:{packageName}",
    purpose: "Represents an external dependency package or workspace package referenced or produced by projects.",
    layer: OntologyConstants.Layers.Physical
)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.ImplementedBy)]
public record PackageNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The package version.")] string Version,
    [property: OntologyProperty("The package type or entity type.")] string Type,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Package;
}
