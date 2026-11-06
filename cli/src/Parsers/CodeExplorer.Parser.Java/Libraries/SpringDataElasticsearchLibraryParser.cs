using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringDataElasticsearchLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "spring-data-elasticsearch";
    private const string ExtensionName = "Spring Data Elasticsearch";
    private const string DocumentAnnotation = "Document";
    private const string IndexNameKey = "indexName";
    private const string ValueKey = "value";

    public SpringDataElasticsearchLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.SearchDb;
        PackageDescriptorTypeOverride = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.springframework.boot.spring-boot-starter-data-elasticsearch*",
            "org.springframework.data.spring-data-elasticsearch*",
            "org.springframework.data.elasticsearch*",
            "*data-elasticsearch*",
            "*elasticsearch*",
            "co.elastic.clients*"
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { DocumentAnnotation }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { IndexNameKey, ValueKey }.ToFrozenSet()
            }
        ];
    }
}
