using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class SpringDataMongoLibraryParser : DeclarativeSemanticExtension
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ParsingContext, SpringDataRepositoryHelper.WorkspaceEntityMaps> _contextMaps = new();

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
                EnrichSymbol = (node, symbol, ctx) =>
                {
                    var maps = SpringDataRepositoryHelper.GetOrCreateContextMaps(ctx, _contextMaps);
                    SpringDataRepositoryHelper.EnrichRepositorySymbol(node, symbol, SpringDataRepositoryHelper.MongoRepositoryInterfaces, maps.EntityToTable);
                },
                CollectReferences = (node, scopeId, refs, ctx) =>
                {
                    var maps = SpringDataRepositoryHelper.GetOrCreateContextMaps(ctx, _contextMaps);
                    SpringDataRepositoryHelper.CollectRepositoryCallReferences(node, scopeId, refs, maps.EntityToTable, maps.RepoToEntity);
                }
            }
        ];
    }

    public override void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx)
    {
        var maps = SpringDataRepositoryHelper.GetOrCreateContextMaps(ctx, _contextMaps);
        SpringDataRepositoryHelper.DiscoverEntityTables(content, maps.EntityToTable, maps.RepoToEntity);
    }

    public override void DiscoverDeclarations(CodeExplorer.Core.Parser.Syntax.SyntaxNode rootNode, string relativePath, string? projectName, ParsingContext? ctx)
    {
        var maps = SpringDataRepositoryHelper.GetOrCreateContextMaps(ctx, _contextMaps);
        SpringDataRepositoryHelper.DiscoverEntityTables(rootNode.Text, maps.EntityToTable, maps.RepoToEntity);
    }
}
