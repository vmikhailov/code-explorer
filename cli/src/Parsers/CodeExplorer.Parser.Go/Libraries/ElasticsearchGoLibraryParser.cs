using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public sealed class ElasticsearchGoLibraryParser : DeclarativeSemanticExtension
{
    private static readonly FrozenSet<string> EsMethods = new[]
    {
        "Search", "Index", "Update", "Delete", "Get", "Count"
    }.ToFrozenSet();

    public ElasticsearchGoLibraryParser()
    {
        Id = "elasticsearch";
        Name = "Elasticsearch";
        Type = SemanticExtensionType.SearchDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["github.com/elastic/go-elasticsearch", "github.com/elastic/go-elasticsearch/*"];
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
