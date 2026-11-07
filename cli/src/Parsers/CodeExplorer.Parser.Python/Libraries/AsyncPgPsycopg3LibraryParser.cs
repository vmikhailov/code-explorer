using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class AsyncPgPsycopg3LibraryParser : DeclarativeSemanticExtension
{
    public AsyncPgPsycopg3LibraryParser()
    {
        Id = "asyncpg-psycopg3";
        Name = "asyncpg & psycopg3";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["asyncpg*", "psycopg*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[] { "fetch", "fetchrow", "fetchval", "execute", "executemany" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackQueryPrefix = "PostgreSQL Async SQL"
            }
        ];
    }
}
