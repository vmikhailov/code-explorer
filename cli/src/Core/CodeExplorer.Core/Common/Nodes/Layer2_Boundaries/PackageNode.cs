using CodeExplorer.Cypher.Common;
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
    [property: OntologyProperty("The architectural archetype of the package (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon).")] ProjectArchetype Archetype = ProjectArchetype.Unclassified,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    public PackageNode(
        string Id,
        string Name,
        string Version,
        bool IsInternal,
        string Ecosystem,
        string Path,
        string Archetype,
        Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Version, IsInternal, Ecosystem, Path, TaxonomyEnumExtendedExtensions.ParseProjectArchetype(Archetype), CustomProperties)
    {
    }

    [JsonPropertyName("dependency_scope"), OntologyProperty("Dependency scope (e.g. runtime, dev, test, build).")]
    public string? DependencyScope { get; set; }

    [JsonPropertyName("raw_version"), OntologyProperty("Raw version or URL specifier from manifest.", isSystem: true)]
    public string? RawVersion { get; set; }

    [JsonPropertyName("is_external_tarball"), OntologyProperty("Whether the dependency is fetched from an external tarball or git URL.", isSystem: true)]
    public bool? IsExternalTarball { get; set; }

    [JsonPropertyName("publisher"), OntologyProperty("Publisher identifier for internal packages.", isSystem: true)]
    public string? Publisher { get; set; }

    [JsonPropertyName("has_vscode_engine"), OntologyProperty("Whether the package targets the VS Code extension engine.", isSystem: true)]
    public bool? HasVsCodeEngine { get; set; }

    [JsonPropertyName("activation_events"), OntologyProperty("Comma-separated VS Code extension activation events.", isSystem: true)]
    public string? ActivationEvents { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Package;

    [property: OntologyProperty("Whether this package is an external third-party dependency.")]
    public bool IsExternal => !IsInternal;
}
