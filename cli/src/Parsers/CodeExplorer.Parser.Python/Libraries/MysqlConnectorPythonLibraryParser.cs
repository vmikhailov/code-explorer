using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class MysqlConnectorPythonLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> ExecMethods = new[]
    {
        "execute", "executemany", "executescript", "execute_sql", "raw", "fetch", "fetchrow", "fetchval"
    }.ToFrozenSet(StringComparer.Ordinal);

    public MysqlConnectorPythonLibraryParser()
    {
        Id = "mysql-connector";
        Name = "MySQL (mysql-connector)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["mysql.connector", "mysql.connector.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = ExecMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "MySQL"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = ExecMethods,
                StaticTargetName = "mysql"
            }
        ];
    }
}
