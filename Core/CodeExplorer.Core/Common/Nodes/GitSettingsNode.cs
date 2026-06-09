using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents the Git repository configuration settings for the workspace.", "`(Workspace)-[:CONTAINS]->(GitSettings)`")]
public record GitSettingsNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The currently checked-out branch name.")] string Branch,
    [property: OntologyProperty("The remote origin repository URL.")] string OriginUrl,
    [property: OntologyProperty("The git user name.")] string UserName,
    [property: OntologyProperty("The git user email address.")] string UserEmail,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.GitSettings;
}
