using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringDataMongoLibraryParser : DeclarativeSemanticExtension
{
    public SpringDataMongoLibraryParser()
    {
        Id = "spring-data-mongo";
        Name = "Spring Data MongoDB";
        Type = SemanticExtensionType.DocumentDb;
        PackageDescriptorTypeOverride = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.springframework.boot.spring-boot-starter-data-mongodb*",
            "org.springframework.data.spring-data-mongodb*",
            "org.springframework.data.mongodb*",
            "*data-mongodb*",
            "*mongodb*"
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { "Document" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { "collection", "value" }.ToFrozenSet()
            }
        ];
    }
}
