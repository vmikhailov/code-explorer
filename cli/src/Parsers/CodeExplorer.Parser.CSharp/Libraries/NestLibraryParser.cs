using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class NestLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> EsMethods = new[]
    {
        "Search", "SearchAsync", "Index", "IndexAsync", "IndexDocument", "IndexDocumentAsync",
        "Update", "UpdateAsync", "Delete", "DeleteAsync", "Count", "CountAsync", "Get", "GetAsync"
    }.ToFrozenSet();

    public NestLibraryParser()
    {
        Id = "elasticsearch-nest";
        Name = "Elasticsearch (NEST)";
        Type = SemanticExtensionType.SearchDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Nest", "Nest.*", "Elastic.Clients.Elasticsearch", "Elastic.Clients.Elasticsearch.*"];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = EsMethods,
                RequireExtractableSqlOnCall = false,
                AppendMethodNameWhenNoSql = true,
                FallbackQueryPrefix = "Elasticsearch"
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = EsMethods,
                StaticTargetName = "elasticsearch"
            }
        ];
    }
}
