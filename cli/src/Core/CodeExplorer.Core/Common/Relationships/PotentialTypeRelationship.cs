using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Common.Relationships;

[OntologyRelationship(OntologyConstants.Relationships.PotentialType, "Links a variable or parameter to concrete classes that implement its declared interface type.")]
public record PotentialTypeRelationship(
    string From,
    string To,
    Dictionary<string, string>? CustomProperties = null
) : OntologyRelationship(From, To, CustomProperties)
{
    [JsonIgnore]
    public override RelationshipKind Kind => RelationshipKind.PotentialType;
}
