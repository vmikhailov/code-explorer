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
    bool IsTest,
    string? ProjectKind = null,
    string? ProjectName = null,
    string Role = "Business",
    bool IsDecorator = false,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    public CallLiteralNode(string id, string name, string value, string callee, int argIndex, string filePath, int line, int column, Dictionary<string, string>? extensions = null)
        : this(id, name, value, callee, argIndex, filePath, line, column, false, null, null, "Business", false, extensions)
    {
    }

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether the call occurs within test code.")]
    public bool IsTest { get; set; } = IsTest;

    [JsonPropertyName("project_kind"), OntologyProperty("The architectural kind of the enclosing project.")]
    public string? ProjectKind { get; set; } = ProjectKind;

    [JsonPropertyName("project_name"), OntologyProperty("The name of the enclosing project.")]
    public string? ProjectName { get; set; } = ProjectName;

    [JsonPropertyName("role"), OntologyProperty("The semantic role of the literal argument (e.g. Business, Routing, ParamBinding, Config, Logging, QueryBuilder, DI).")]
    public string Role { get; set; } = Role;

    [JsonPropertyName("is_decorator"), OntologyProperty("Indicates whether the literal argument occurs within a decorator or attribute.")]
    public bool IsDecorator { get; set; } = IsDecorator;

    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.CallLiteral;

    [JsonIgnore]
    public override string Path { get; init; } = FilePath;
}
