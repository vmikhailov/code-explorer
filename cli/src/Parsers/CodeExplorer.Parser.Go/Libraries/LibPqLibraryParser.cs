using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class LibPqLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> SqlMethods = new[]
    {
        "Query", "QueryRow", "Exec",
        "QueryContext", "QueryRowContext", "ExecContext"
    }.ToFrozenSet(StringComparer.Ordinal);

    public LibPqLibraryParser()
    {
        Id = "lib-pq";
        Name = "PostgreSQL (lib/pq)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["github.com/lib/pq*"];
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
