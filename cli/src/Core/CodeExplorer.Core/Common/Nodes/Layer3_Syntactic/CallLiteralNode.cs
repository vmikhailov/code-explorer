using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.CallLiteral,
    idScheme: "{workspaceId}:lit:{filePath}:{line}:{col}",
    purpose: "Represents a raw literal argument passed directly into a method or constructor invocation.",
    layer: OntologyConstants.Layers.Syntactic,
    icon: "quote",
    order: 40,
    pluralLabel: "Call Literals"
)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record CallLiteralNode(
    string Id,
    [property: OntologyProperty("The name or representation of the literal argument.")] string Name,
    [property: OntologyProperty("The raw string value of the literal.")] string Value,
    [property: OntologyProperty("The method or callee being invoked.")] string Callee,
    [property: OntologyProperty("The index of the argument (0-indexed).")] int ArgIndex,
    [property: OntologyProperty("The relative file path where the call occurs.")] string FilePath,
    [property: OntologyProperty("The 1-indexed line number of the argument.")] int Line,
    [property: OntologyProperty("The 1-indexed column number of the argument.")] int Column,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.CallLiteral;

    [JsonIgnore]
    public override string Path { get; init; } = FilePath;
}
