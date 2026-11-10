using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Relationships;

[OntologyRelationship(OntologyConstants.Relationships.TargetedBy, "Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it.")]
public record TargetedByRelationship(
    string From,
    string To,
    Dictionary<string, string>? CustomProperties = null
) : OntologyRelationship(From, To, CustomProperties)
{
    [JsonIgnore]
    public override RelationshipKind Kind => RelationshipKind.TargetedBy;
}
