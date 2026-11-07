using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class SqlcGoLibraryParser : DeclarativeSemanticExtension
{
    public SqlcGoLibraryParser()
    {
        Id = "sqlc-go";
        Name = "sqlc (Generated Go SQL)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["database/sql", "github.com/jackc/pgx*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[] { "QueryRowContext", "QueryContext", "ExecContext" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackQueryPrefix = "sqlc SQL"
            }
        ];
    }
}
