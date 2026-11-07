using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class PythonSqlite3LibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> ExecMethods = new[]
    {
        "execute", "executemany", "executescript", "execute_sql", "raw", "fetch", "fetchrow", "fetchval"
    }.ToFrozenSet(StringComparer.Ordinal);

    public PythonSqlite3LibraryParser()
    {
        Id = "sqlite3";
        Name = "SQLite3";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["sqlite3"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = ExecMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "SQLite"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = ExecMethods,
                StaticTargetName = "sqlite"
            }
        ];
    }
}
