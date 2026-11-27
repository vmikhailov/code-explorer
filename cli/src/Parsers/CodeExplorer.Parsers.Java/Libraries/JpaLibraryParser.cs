using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class JpaLibraryParser : DeclarativeSemanticExtension
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SemanticParseContext, SpringDataRepositoryHelper.WorkspaceEntityMaps> _contextMaps = new();

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
                EnrichSymbol = (node, symbol, ctx) =>
                {
                    var maps = SpringDataRepositoryHelper.GetOrCreateContextMaps(ctx, _contextMaps);
                    SpringDataRepositoryHelper.EnrichRepositorySymbol(node, symbol, SpringDataRepositoryHelper.JpaRepositoryInterfaces, maps.EntityToTable, maps.RepoToEntity);
                },
                CollectReferences = (node, scopeId, refs, ctx) =>
                {
                    var maps = SpringDataRepositoryHelper.GetOrCreateContextMaps(ctx, _contextMaps);
                    SpringDataRepositoryHelper.CollectRepositoryCallReferences(node, scopeId, refs, maps.EntityToTable, maps.RepoToEntity);
                }
            }
        ];
    }

    public override void DiscoverDeclarations(string filePath, string content, string? projectName, SemanticParseContext? ctx)
    {
        var maps = SpringDataRepositoryHelper.GetOrCreateContextMaps(ctx, _contextMaps);
        SpringDataRepositoryHelper.DiscoverEntityTables(content, maps.EntityToTable, maps.RepoToEntity);
    }

    public override void DiscoverDeclarations(Core.Parser.Syntax.SyntaxNode rootNode, string relativePath, string? projectName, SemanticParseContext? ctx)
    {
        var maps = SpringDataRepositoryHelper.GetOrCreateContextMaps(ctx, _contextMaps);
        var sourceText = (rootNode as Core.Parser.Syntax.SyntaxDocument)?.SourceText
                         ?? rootNode.Document?.SourceText
                         ?? rootNode.Text;
        SpringDataRepositoryHelper.DiscoverEntityTables(sourceText, maps.EntityToTable, maps.RepoToEntity);
    }
}

