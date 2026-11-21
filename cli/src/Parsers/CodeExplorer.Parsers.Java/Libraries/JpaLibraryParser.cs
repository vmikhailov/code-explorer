using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class JpaLibraryParser : DeclarativeSemanticExtension
{
    private static readonly ConcurrentDictionary<string, string> _entityToTable = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> _repoToEntity = new(StringComparer.OrdinalIgnoreCase);

    public JpaLibraryParser()
    {
        Id = "jpa";
        Name = "JPA / Hibernate / Spring Data";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.springframework.data.jpa*",
            "org.springframework.data:spring-data-jpa*",
            "org.springframework.data.repository*",
            "org.springframework.boot.spring-boot-starter-data-jpa*",
            "org.springframework.boot:spring-boot-starter-data-jpa*",
            "*spring-boot-starter-data-jpa*",
            "jakarta.persistence*",
            "javax.persistence*",
            "org.hibernate*"
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[] { "createQuery", "createNativeQuery", "createNamedQuery" }.ToFrozenSet(StringComparer.Ordinal),
                QueryAnnotationSuffixes = new[] { "Query" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = "JPA Query"
            }
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { "Table", "Entity" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ClassDeclarationNodeTypes = new[] { "class_declaration", "interface_declaration" }.ToFrozenSet(StringComparer.Ordinal),
                RepositoryInterfaceNames = SpringDataRepositoryHelper.JpaRepositoryInterfaces
            }
        ];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = SpringDataRepositoryHelper.RepositoryNodeTypes,
                EnrichSymbol = (node, symbol, _) => SpringDataRepositoryHelper.EnrichRepositorySymbol(
                    node, symbol, SpringDataRepositoryHelper.JpaRepositoryInterfaces, _entityToTable),
                CollectReferences = (node, scopeId, refs, _) => SpringDataRepositoryHelper.CollectRepositoryCallReferences(
                    node, scopeId, refs, _entityToTable, _repoToEntity)
            }
        ];
    }

    public override void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx)
    {
        SpringDataRepositoryHelper.DiscoverEntityTables(content, _entityToTable, _repoToEntity);
    }

    public override void DiscoverDeclarations(CodeExplorer.Core.Parser.Syntax.SyntaxNode rootNode, string relativePath, string? projectName, ParsingContext? ctx)
    {
        SpringDataRepositoryHelper.DiscoverEntityTables(rootNode.Text, _entityToTable, _repoToEntity);
    }
}

