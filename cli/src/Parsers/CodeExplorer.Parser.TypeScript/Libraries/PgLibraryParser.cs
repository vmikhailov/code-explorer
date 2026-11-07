using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public sealed class PgLibraryParser : DeclarativeSemanticExtension
{
    public PgLibraryParser()
    {
        Id = "postgres";
        Name = "PostgreSQL";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["pg", "pg-promise", "@types/pg"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[]
                {
                    "query", "any", "one", "oneOrNone", "many", "none", "multi"
                }.ToFrozenSet(),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = "PostgreSQL Query"
            }
        ];
    }
}
