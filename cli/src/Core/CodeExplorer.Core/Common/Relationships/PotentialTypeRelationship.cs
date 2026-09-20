using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Common.Relationships;

[OntologyRelationship(OntologyConstants.Relationships.PotentialType, "Links a variable or parameter to concrete classes that implement its declared interface type.")]
public record PotentialTypeRelationship(
    [property: JsonIgnore] string From,
    [property: JsonIgnore] string To,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyRelationship
{
    [JsonIgnore]
    public string Kind => OntologyConstants.Relationships.PotentialType;
}
