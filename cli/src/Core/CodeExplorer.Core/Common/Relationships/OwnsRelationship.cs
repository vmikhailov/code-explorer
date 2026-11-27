using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Common.Relationships;

[OntologyRelationship(OntologyConstants.Relationships.Owns, "Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.")]
public record OwnsRelationship(
    string From,
    string To,
    Dictionary<string, string>? CustomProperties = null
) : OntologyRelationship(From, To, CustomProperties)
{
    [JsonIgnore]
    public override RelationshipKind Kind => RelationshipKind.Owns;
}
