using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class PyMysqlLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> ExecMethods = new[]
    {
        "execute", "executemany", "executescript", "execute_sql", "raw", "fetch", "fetchrow", "fetchval"
    }.ToFrozenSet(StringComparer.Ordinal);

    public PyMysqlLibraryParser()
    {
        Id = "pymysql";
        Name = "MySQL (PyMySQL)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["pymysql", "pymysql.*"];
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
