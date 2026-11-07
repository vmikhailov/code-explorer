using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class CouchbaseLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> CouchbaseMethods = new[]
    {
        "GetAsync", "InsertAsync", "UpsertAsync", "ReplaceAsync", "RemoveAsync", "QueryAsync",
        "Get", "Insert", "Upsert", "Replace", "Remove", "Query"
    }.ToFrozenSet(StringComparer.Ordinal);

    public CouchbaseLibraryParser()
    {
        Id = "couchbase";
        Name = "Couchbase";
        Type = SemanticExtensionType.DocumentDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Couchbase", "Couchbase.NetClient", "Couchbase.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = CouchbaseMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "Couchbase"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = CouchbaseMethods,
                StaticTargetName = "couchbase"
            }
        ];
    }
}
