using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Common.Relationships;

[OntologyRelationship(OntologyConstants.Relationships.Exposes, "Indicates that a container exposes a certain service or entry point.")]
public record ExposesRelationship(
    [property: JsonIgnore] string From,
    [property: JsonIgnore] string To,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyRelationship
{
    [JsonIgnore]
    public string Kind => OntologyConstants.Relationships.Exposes;
}
