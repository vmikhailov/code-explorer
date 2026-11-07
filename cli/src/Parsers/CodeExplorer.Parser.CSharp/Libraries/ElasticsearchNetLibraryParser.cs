using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class ElasticsearchNetLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> EsMethods = new[]
    {
        "Search", "SearchAsync", "Index", "IndexAsync", "Update", "UpdateAsync",
        "Delete", "DeleteAsync", "Count", "CountAsync", "Get", "GetAsync",
        "PerformRequest", "PerformRequestAsync"
    }.ToFrozenSet();

    public ElasticsearchNetLibraryParser()
    {
        Id = "elasticsearch-net";
        Name = "Elasticsearch (Low Level)";
        Type = SemanticExtensionType.SearchDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["Elasticsearch.Net", "Elasticsearch.Net.*"];
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
