using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class SqlxGoLibraryParser : DeclarativeSemanticExtension
{
    public SqlxGoLibraryParser()
    {
        Id = "sqlx-go";
        Name = "sqlx (Go)";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["github.com/jmoiron/sqlx*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[]
                {
                    "Select", "SelectContext", "Get", "GetContext",
                    "NamedExec", "NamedExecContext", "NamedQuery", "NamedQueryContext",
                    "MustExec", "MustExecContext", "Queryx", "QueryxContext", "QueryRowx", "QueryRowxContext"
                }.ToFrozenSet(StringComparer.Ordinal),
                FallbackQueryPrefix = "sqlx Query"
            }
        ];
    }
}
