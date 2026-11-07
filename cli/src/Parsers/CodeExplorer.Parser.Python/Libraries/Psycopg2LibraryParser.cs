using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class Psycopg2LibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> ExecMethods = new[]
    {
        "execute", "executemany", "executescript", "execute_sql", "raw", "fetch", "fetchrow", "fetchval"
    }.ToFrozenSet(StringComparer.Ordinal);

    public Psycopg2LibraryParser()
    {
        Id = "postgres";
        Name = "PostgreSQL";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["psycopg2", "psycopg", "psycopg2.*", "psycopg.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = ExecMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "PostgreSQL"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = ExecMethods,
                StaticTargetName = "postgres"
            }
        ];
    }
}
