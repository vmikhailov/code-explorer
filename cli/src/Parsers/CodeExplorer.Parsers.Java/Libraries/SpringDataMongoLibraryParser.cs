using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class SpringDataMongoLibraryParser : DeclarativeSemanticExtension
{
    private readonly ConcurrentDictionary<string, string> _entityToTable = new(StringComparer.OrdinalIgnoreCase);

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
                NameArgumentKeys = new[] { "collection", "value" }.ToFrozenSet(),
                ClassDeclarationNodeTypes = new[] { "class_declaration", "interface_declaration" }.ToFrozenSet(StringComparer.Ordinal),
                RepositoryInterfaceNames = SpringDataRepositoryHelper.MongoRepositoryInterfaces
            }
        ];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = SpringDataRepositoryHelper.RepositoryNodeTypes,
                EnrichSymbol = (node, symbol, _) => SpringDataRepositoryHelper.EnrichRepositorySymbol(
                    node, symbol, SpringDataRepositoryHelper.MongoRepositoryInterfaces, _entityToTable)
            }
        ];
    }

    public override void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx)
    {
        SpringDataRepositoryHelper.DiscoverEntityTables(content, _entityToTable);
    }
}
