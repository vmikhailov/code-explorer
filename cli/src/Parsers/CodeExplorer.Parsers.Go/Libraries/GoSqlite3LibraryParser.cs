using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Go.Libraries;

public class GoSqlite3LibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "Query", "QueryRow", "Exec",
        "QueryContext", "QueryRowContext", "ExecContext"
    }.ToFrozenSet(StringComparer.Ordinal);

    public GoSqlite3LibraryParser()
    {
        Id = "sqlite3";
        Name = "SQLite3";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "github.com/mattn/go-sqlite3*",
            "modernc.org/sqlite*",
            "zombiezen.com/go/sqlite*",
            "github.com/glebarez/sqlite*",
            "github.com/glebarez/go-sqlite*"
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = SqlMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "SQLite"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = SqlMethods,
                StaticTargetName = "sqlite"
            }
        ];
    }
}
