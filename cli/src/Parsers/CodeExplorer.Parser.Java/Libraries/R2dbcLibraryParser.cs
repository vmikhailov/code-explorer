using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class R2dbcLibraryParser : DeclarativeSemanticExtension
{
    public R2dbcLibraryParser()
    {
        Id = "r2dbc";
        Name = "Spring Data R2DBC";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "io.r2dbc.*",
            "org.springframework.data.r2dbc.*",
            "org.springframework.r2dbc.*"
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                QueryAnnotationSuffixes = new[] { "Query" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                CallMethodNames = new[] { "sql", "execute" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackQueryPrefix = "R2DBC SQL"
            }
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { "Table" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                RepositoryInterfaceNames = new[] { "ReactiveCrudRepository", "R2dbcRepository", "ReactiveSortingRepository" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
