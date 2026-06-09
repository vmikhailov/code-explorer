using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Database,
    idScheme: "{workspaceId}:database:{dbType}:{dbName}",
    purpose: "Represents a database instance, catalog, or physical schema."
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.QueriedBy)]
[OntologyEdge<QueryNode>(OntologyConstants.Relationships.QueriedBy)]
public record DatabaseNode(
    string Id,
    [property: OntologyProperty("The database engine or catalog name.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("db_type"), OntologyProperty("The database system type (sqlserver, postgres, sqlite, mongodb, neo4j).")] string DbType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore]
    public override string Kind => OntologyConstants.NodeLabels.Database;
}
