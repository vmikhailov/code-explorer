using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoSqlDriverMysqlLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "Query", "QueryRow", "Exec",
        "QueryContext", "QueryRowContext", "ExecContext"
    }.ToFrozenSet(StringComparer.Ordinal);

    public GoSqlDriverMysqlLibraryParser()
    {
        Id = "mysql";
        Name = "MySQL";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["github.com/go-sql-driver/mysql*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = SqlMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "MySQL"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = SqlMethods,
                StaticTargetName = "mysql"
            }
        ];
    }
}
