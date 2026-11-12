using System.Text.Json.Serialization;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Library,
    idScheme: "{workspaceId}:lib:{libraryName}",
    purpose: "Represents a shared library, utility module, DTO package, or domain contract reused across services.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "library",
    order: 6,
    pluralLabel: "Libraries & SDKs"
)]
public record LibraryNode(
    string Id,
    string Name,
    string Path,
    ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : ModuleNode(Id, Name, Path, ProjectType, CustomProperties)
{
    public LibraryNode(string Id, string Name, string Path, string ProjectType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), CustomProperties)
    {
    }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Library;
}
