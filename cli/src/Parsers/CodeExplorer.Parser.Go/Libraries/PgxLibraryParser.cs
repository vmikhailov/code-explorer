using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class PgxLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "Query", "QueryRow", "Exec",
        "QueryContext", "QueryRowContext", "ExecContext"
    }.ToFrozenSet(StringComparer.Ordinal);

    public PgxLibraryParser()
    {
        Id = "postgres-pgx";
        Name = "PostgreSQL (pgx)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["github.com/jackc/pgx*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = SqlMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "PostgreSQL"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = SqlMethods,
                StaticTargetName = "postgres"
            }
        ];
    }
}
