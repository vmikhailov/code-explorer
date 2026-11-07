using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoSqlLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "Query", "QueryRow", "Exec",
        "QueryContext", "QueryRowContext", "ExecContext"
    }.ToFrozenSet(StringComparer.Ordinal);

    public GoSqlLibraryParser()
    {
        Id = "sql";
        Name = "SQL";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["database/sql*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = SqlMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "SQL"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = SqlMethods,
                StaticTargetName = "database"
            }
        ];
    }
}
