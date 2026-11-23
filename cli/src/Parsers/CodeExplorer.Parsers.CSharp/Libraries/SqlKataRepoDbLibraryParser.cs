using System.Collections.Frozen;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public class SqlKataRepoDbLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "sqlkata-repodb";
    private const string ExtensionName = "SqlKata & RepoDb";

    public SqlKataRepoDbLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "SqlKata*",
            "RepoDb*"
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "Query", "From", "Insert", "InsertAll", "Merge", "MergeAll",
                    "Update", "UpdateAll", "Delete", "DeleteAll", "ExecuteQuery", "ExecuteNonQuery"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "db", "connection", "compiler", "queryFactory" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TableArgumentIndex = 0
            }
        ];
    }
}
