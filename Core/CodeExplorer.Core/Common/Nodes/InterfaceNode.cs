using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode("Represents a parsed OOP interface contract (e.g. C# interface, Go interface, TypeScript interface).", "`(File)-[:CONTAINS]->(Interface)`", "`(Class)-[:IMPLEMENTS]->(Interface)`", "`(Interface)-[:INHERITS_FROM]->(Interface)`")]
public record InterfaceNode(
    string Id,
    [property: OntologyProperty("The name of the entity.")] string Name,
    [property: OntologyProperty("A globally unique ID for this symbol scope.")] string Symbol,
    [property: OntologyProperty("The relative path of the declaring file.")] string FilePath,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: OntologyProperty("The starting line number (1-indexed) of the declaration.")] int StartLine,
    [property: OntologyProperty("The ending line number (1-indexed) of the declaration.")] int EndLine,
    [property: OntologyProperty("The starting column number of the declaration.")] int StartCol,
    [property: OntologyProperty("The ending column number of the declaration.")] int EndCol,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Interface;
}
