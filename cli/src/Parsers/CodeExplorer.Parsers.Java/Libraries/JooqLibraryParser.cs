using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class JooqLibraryParser : DeclarativeSemanticExtension
{
    public JooqLibraryParser()
    {
        Id = "jooq";
        Name = "jOOQ";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["org.jooq.*"];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "selectFrom", "insertInto", "update", "deleteFrom", "from" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
