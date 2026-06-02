using System.Text.Json.Serialization;

namespace CodeExplorer.Common;

public record ImplementedByRelationship(
    [property: JsonIgnore] string From,
    [property: JsonIgnore] string To,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyRelationship
{
    [JsonIgnore]
    public string Kind => OntologyConstants.Relationships.ImplementedBy;
}
