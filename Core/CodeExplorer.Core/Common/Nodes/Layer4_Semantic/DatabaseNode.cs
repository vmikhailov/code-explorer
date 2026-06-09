using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common.Nodes;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Database,
    idScheme: "{workspaceId}:database:{dbType}:{dbName}",
    purpose: "Represents a database instance, catalog, or physical schema."
)]
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
