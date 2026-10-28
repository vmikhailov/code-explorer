using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class JdbcTemplateLibraryParser : DeclarativeSemanticExtension
{
    public JdbcTemplateLibraryParser()
    {
        Id = "jdbc";
        Name = "Spring JdbcTemplate / JDBC";
        Type = "db:relational";
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.springframework.jdbc*",
            "java.sql*",
            "javax.sql*"
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[]
                {
                    "query", "queryForObject", "queryForList", "queryForRowSet",
                    "update", "batchUpdate", "execute", "prepareStatement", "prepareCall"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                RequireExtractableSqlOnCall = true,
                FallbackQueryPrefix = "JDBC Query"
            }
        ];
    }
}

