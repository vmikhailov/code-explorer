using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public sealed class Mysql2LibraryParser : DeclarativeSemanticExtension
{
    public Mysql2LibraryParser()
    {
        Id = "mysql";
        Name = "MySQL";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["mysql2", "mysql", "@types/mysql", "@types/mysql2"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[] { "query", "execute" }.ToFrozenSet(),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = "MySQL Query"
            }
        ];
    }
}
