using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Module,
    idScheme: "{workspaceId}:{kindPrefix}:{moduleName}",
    purpose: "Abstract base software module encompassing executable workloads, shared libraries, and automated test suites.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "package",
    order: 0,
    pluralLabel: "Software Modules",
    isAbstract: true
)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.ImplementedBy)]
[OntologyEdge<LibraryNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
public abstract record ModuleNode(
    string Id,
    [property: OntologyProperty("The module name.")] string Name,
    [property: OntologyProperty("The path of the module directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("role"), OntologyProperty("The primary semantic role of the module.")]
    public string? Role { get; set; }

    [JsonPropertyName("is_library"), OntologyProperty("Indicates whether the module is a library.", isSystem: true)]
    public bool? IsLibrary { get; set; }

    [JsonPropertyName("primary_role"), OntologyProperty("The primary architectural role.", isSystem: true)]
    public string? PrimaryRole { get; set; }

    [JsonPropertyName("secondary_roles"), OntologyProperty("Comma-separated secondary roles.", isSystem: true)]
    public string? SecondaryRoles { get; set; }

    [JsonPropertyName("all_roles"), OntologyProperty("All detected roles.", isSystem: true)]
    public string? AllRoles { get; set; }

    [JsonPropertyName("framework"), OntologyProperty("The detected application, service, or test framework.")]
    public string? Framework { get; set; }
}
