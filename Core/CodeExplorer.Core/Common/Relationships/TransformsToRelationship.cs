using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Relationships;

public record TransformsToRelationship(
    [property: JsonIgnore] string From,
    [property: JsonIgnore] string To,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyRelationship
{
    [JsonIgnore]
    public string Kind => OntologyConstants.Relationships.TransformsTo;
}
