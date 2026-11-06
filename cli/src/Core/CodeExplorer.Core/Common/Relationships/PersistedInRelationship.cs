using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Common.Relationships;

[OntologyRelationship(OntologyConstants.Relationships.PersistedIn, "Links an ORM entity or model class to its physical database table.")]
public record PersistedInRelationship(
    string From,
    string To,
    Dictionary<string, string>? CustomProperties = null
) : OntologyRelationship(From, To, CustomProperties)
{
    [JsonIgnore]
    public override RelationshipKind Kind => RelationshipKind.PersistedIn;
}
