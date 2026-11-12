using System.Text.Json.Serialization;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.CliTool,
    idScheme: "{workspaceId}:cli:{toolName}",
    purpose: "Represents a command-line tool, developer script, or administrative CLI utility.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "terminal",
    order: 5,
    pluralLabel: "CLI Tools"
)]
public record CliToolNode(
    string Id,
    string Name,
    string Path,
    ProjectLanguage ProjectType,
    Dictionary<string, string>? CustomProperties = null
) : WorkloadNode(Id, Name, Path, ProjectType, CustomProperties)
{
    public CliToolNode(string Id, string Name, string Path, string ProjectType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), CustomProperties)
    {
    }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.CliTool;
}
