using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).", "`(Workspace|WorkspaceFolder)-[:CONTAINS]->(Project)`", "`(Project)-[:CONTAINS]->(Files)`", "`(Project)-[:CONTAINS]->(DataBases)`", "`(Project)-[:CONTAINS]->(ApisInUse)`", "`(Project)-[:CONTAINS]->(CloudServices)`", "`(Project)-[:CONTAINS]->(Dependencies)`", "`(Project)-[:CONTAINS]->(EntryPoints)`", "`(Project)-[:DEPENDS_ON]->(Project)`", "`(Project)-[:DEPENDS_ON]->(Package)`")]
public record ProjectNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: OntologyProperty("The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript').")] string ProjectType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Project;
}
