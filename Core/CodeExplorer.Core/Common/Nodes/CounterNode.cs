using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Counter,
    idScheme: "workspace_id",
    purpose: "Represents an internal database counter used for auto-incrementing identifiers.",
    layer: OntologyConstants.Layers.Physical
)]
public record CounterNode(
    string Id,
    [property: OntologyProperty("The name of the counter.")] string Name,
    [property: OntologyProperty("The current counter value.")] int Value,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Counter;
}
