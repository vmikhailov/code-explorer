using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Database,
    idScheme: "{workspaceId}:db:{dbType}:{dbName}",
    purpose: "Represents a database instance, catalog, or physical schema.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "database",
    order: 20,
    pluralLabel: "Databases & Storage"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.AccessedBy)]
[OntologyEdge<TableNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProcedureNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<QueryNode>(OntologyConstants.Relationships.Contains)]
public record DatabaseNode(
    string Id,
    [property: OntologyProperty("The database engine or catalog name.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("db_type"), OntologyProperty("The database system type (sqlserver, postgres, sqlite, mongodb, neo4j).")] DatabaseType DbType,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "relational", "document", "keyvalue", "search", "cache", "vector", "graph",
        "analytics", "timeseries", "columnar", "multimodel"
    };

    public DatabaseNode(string Id, string Name, string Path, string DbType, Dictionary<string, string>? Extensions = null)
        : this(Id, Name, Path, ValidateDbType(DbType), Extensions)
    {
    }

    private static DatabaseType ValidateDbType(string dbType)
    {
        if (!AllowedTypes.Contains(dbType))
        {
            throw new ArgumentException($"Unsupported DatabaseNode type: {dbType}. Allowed types are: {string.Join(", ", AllowedTypes)}");
        }
        return EnumExtensions.ParseDatabaseType(dbType);
    }

    [JsonPropertyName("is_implicit_schema"), OntologyProperty("Indicates whether the database node was synthesized from an implicit default schema.")]
    public bool? IsImplicitSchema { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Database;
}
