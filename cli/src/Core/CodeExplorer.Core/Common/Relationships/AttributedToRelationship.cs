using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Relationships;

[OntologyRelationship(OntologyConstants.Relationships.AttributedTo, "Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.")]
public record AttributedToRelationship(
    string From,
    string To,
    Dictionary<string, string>? CustomProperties = null
) : OntologyRelationship(From, To, CustomProperties)
{
    [JsonIgnore]
    public override RelationshipKind Kind => RelationshipKind.AttributedTo;
}
