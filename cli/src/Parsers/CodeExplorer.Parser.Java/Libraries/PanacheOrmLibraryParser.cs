using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class PanacheOrmLibraryParser : DeclarativeSemanticExtension
{
    public PanacheOrmLibraryParser()
    {
        Id = "quarkus-panache";
        Name = "Quarkus Panache ORM / MongoDB with Panache";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "io.quarkus.hibernate.orm.panache*",
            "io.quarkus.hibernate.reactive.panache*",
            "io.quarkus.mongodb.panache*",
            "io.quarkus.panache*"
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { "Table", "Entity", "MongoEntity" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { "name", "value", "collection" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                BaseClassNames = new[] { "PanacheEntity", "PanacheEntityBase", "ReactivePanacheMongoEntity", "PanacheMongoEntity", "PanacheMongoEntityBase" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                RepositoryInterfaceNames = new[] { "PanacheRepository", "PanacheRepositoryBase", "PanacheMongoRepository", "ReactivePanacheMongoRepository" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[]
                {
                    "list", "listAll", "find", "findAll", "findById", "findByIdOptional",
                    "count", "delete", "deleteById", "persist", "update"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = "Panache Query"
            }
        ];
    }
}
