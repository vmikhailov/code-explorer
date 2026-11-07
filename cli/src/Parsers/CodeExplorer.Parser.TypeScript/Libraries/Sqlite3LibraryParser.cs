using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public sealed class Sqlite3LibraryParser : DeclarativeSemanticExtension
{
    public Sqlite3LibraryParser()
    {
        Id = "sqlite";
        Name = "SQLite";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["sqlite3", "better-sqlite3", "@types/better-sqlite3"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[]
                {
                    "prepare", "run", "all", "get", "exec", "each"
                }.ToFrozenSet(),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = "SQLite Query"
            }
        ];
    }
}
