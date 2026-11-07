using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class JdbiLibraryParser : DeclarativeSemanticExtension
{
    public JdbiLibraryParser()
    {
        Id = "jdbi";
        Name = "JDBI3";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["org.jdbi.*", "org.jdbi.v3.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                QueryAnnotationSuffixes = new[] { "SqlQuery", "SqlUpdate", "SqlBatch", "SqlCall", "SqlScript" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                CallMethodNames = new[] { "createQuery", "createUpdate", "createBatch", "createCall", "execute", "select" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackQueryPrefix = "JDBI SQL"
            }
        ];
    }
}
