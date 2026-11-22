using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Relationships;

[OntologyRelationship(OntologyConstants.Relationships.GuardedBy, "Links an Endpoint or CQRS Message contract to a Layer 4 authorization Policy or Guard.")]
public record GuardedByRelationship(
    string From,
    string To,
    Dictionary<string, string>? CustomProperties = null
) : OntologyRelationship(From, To, CustomProperties)
{
    [JsonIgnore]
    public override RelationshipKind Kind => RelationshipKind.GuardedBy;
}
