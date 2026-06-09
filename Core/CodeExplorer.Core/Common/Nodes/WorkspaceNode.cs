using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents the absolute root of the workspace directory hierarchy.", "`(Workspace)-[:CONTAINS]->(WorkspaceFolder)`", "`(Workspace)-[:CONTAINS]->(Project)`", "`(Workspace)-[:CONTAINS]->(File)` (if a source file sits at the root directory)")]
public record WorkspaceNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Workspace;
}
