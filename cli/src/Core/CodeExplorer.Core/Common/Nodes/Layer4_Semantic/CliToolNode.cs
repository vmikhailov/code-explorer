using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.CliTool,
    idScheme: "{workspaceId}:clitool:{toolName}",
    purpose: "Represents a command-line tool, developer script, or administrative CLI utility.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "terminal",
    order: 4,
    pluralLabel: "CLI Tools"
)]
[OntologyEdge<FolderNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<WorkspaceNode>(OntologyConstants.Relationships.LocatedIn)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DeployedBy)]
[OntologyEdge<ServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<ExternalServiceNode>(OntologyConstants.Relationships.ServiceCall)]
[OntologyEdge<DatabaseNode>(OntologyConstants.Relationships.UsesDb)]
[OntologyEdge<EntryPointNode>(OntologyConstants.Relationships.Contains)]
public record CliToolNode(
    string Id,
    [property: OntologyProperty("The CLI tool name.")] string Name,
    [property: OntologyProperty("The path of the tool directory relative to the workspace.")] string Path,
    [property: OntologyProperty("The project type or language.")] string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.CliTool;
}
