using CodeExplorer.Cypher.Common;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Database,
    idScheme: "{workspaceId}:db:{dbType}:{dbName}",
    purpose: "Represents a database server instance or catalog configured by a workload and containing one or more DbSchema namespaces.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "database",
    order: 20,
    pluralLabel: "Databases & Storage"
)]
[OntologyEdge<CloudNode>(OntologyConstants.Relationships.HostedIn)]
[OntologyEdge<DbSchemaNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
[OntologyEdge<CallArgumentNode>(OntologyConstants.Relationships.TargetedBy)]
public record DatabaseNode(
    string Id,
    [property: OntologyProperty("The database engine or catalog name.")] string Name,
    [property: OntologyProperty("The path of the folder or file relative to its parent container.")] string Path,
    [property: JsonPropertyName("db_type"), OntologyProperty("The database system type (sqlserver, postgres, sqlite, mongodb, neo4j).")] DatabaseType DbType,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "relational", "document", "keyvalue", "search", "cache", "vector", "graph",
        "analytics", "timeseries", "columnar", "multimodel"
    };

    public DatabaseNode(string Id, string Name, string Path, string DbType, Dictionary<string, string>? CustomProperties = null)
        : this(Id, Name, Path, ValidateDbType(DbType), CustomProperties)
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

    [JsonPropertyName("display_name"), OntologyProperty("Human-readable database or catalog display name.")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("engine"), OntologyProperty("Detected database engine (e.g. postgresql, sqlserver, sqlite, mysql, mongodb, redis).")]
    public string? Engine { get; set; }

    [JsonPropertyName("technology"), OntologyProperty("ORM, driver, or client technology accessing the database (e.g. EF Core, Dapper, Prisma, TypeORM).")]
    public string? Technology { get; set; }

    [JsonPropertyName("schema"), OntologyProperty("Physical database schema name when declared (e.g. public, dbo, billing).")]
    public string? Schema { get; set; }

    [JsonPropertyName("provider"), OntologyProperty("Database driver or provider package identifier.")]
    public string? Provider { get; set; }

    [JsonPropertyName("role"), OntologyProperty("Storage role classification (primary, cache, analytics, read-replica).")]
    public string? Role { get; set; }

    [JsonPropertyName("config_key"), OntologyProperty("Configuration key or connection string name bound to this database.")]
    public string? ConfigKey { get; set; }

    [JsonPropertyName("is_explicit_schema"), OntologyProperty("Indicates whether the database schema was explicitly declared.", isSystem: true)]
    public bool? IsExplicitSchema { get; set; }

    [JsonPropertyName("from_config"), OntologyProperty("Indicates whether the database connection was discovered from configuration files.")]
    public bool? FromConfig { get; set; }

    [JsonPropertyName("is_test"), OntologyProperty("Indicates whether this database instance is used exclusively in tests.")]
    public bool? IsTest { get; set; }

    [JsonPropertyName("is_orm"), OntologyProperty("Indicates whether access was inferred via an ORM entity/DbContext mapping.")]
    public bool? IsOrm { get; set; }

    [JsonPropertyName("is_canonical"), OntologyProperty("Indicates whether this node is the canonical database instance.", isSystem: true)]
    public bool? IsCanonical { get; set; }

    [JsonPropertyName("is_implicit_schema"), OntologyProperty("Indicates whether the database node was synthesized from an implicit default schema.", isSystem: true)]
    public bool? IsImplicitSchema { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Database;
}
