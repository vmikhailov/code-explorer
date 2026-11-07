using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MikroOrmKyselyLibraryParser : ISemanticExtension
{
    private const string MikroOrmTarget = "mikro-orm";

    public string Name => "MikroORM & Kysely";
    public string Id => "mikro-orm-kysely";
    public SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public IReadOnlyList<string> SupportedPatterns => ["@mikro-orm/*", "kysely", "kysely-*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> KyselyTableMethods = new(StringComparer.Ordinal)
    {
        "selectFrom", "insertInto", "updateTable", "deleteFrom"
    };

    private static readonly HashSet<string> MikroOrmMethods = new(StringComparer.Ordinal)
    {
        "find", "findOne", "findOneOrFail", "findAndCount", "persistAndFlush", "nativeInsert", "nativeUpdate", "nativeDelete"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractKyselyTable(node, out _, out _) ? OntologyConstants.NodeLabels.Query : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractKyselyTable(node, out var op, out var table) ? $"Kysely: {op} {table}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractKyselyTable(node, out _, out var table))
        {
            references.Add(new Reference(scopeSymbolId, table, OntologyConstants.Relationships.UsesDb));
            return;
        }

        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
            AstHelper.TryGetMemberAccess(node, out var objNode, out var propName) &&
            !string.IsNullOrEmpty(propName) &&
            MikroOrmMethods.Contains(propName) &&
            objNode.IsValid() &&
            (objNode.Text.EndsWith("em", StringComparison.OrdinalIgnoreCase) ||
             objNode.Text.Contains("entityManager", StringComparison.OrdinalIgnoreCase)))
        {
            references.Add(new Reference(scopeSymbolId, MikroOrmTarget, OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool TryExtractKyselyTable(Node node, out string opName, out string tableName)
    {
        opName = string.Empty;
        tableName = string.Empty;

        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        if (!AstHelper.TryGetMemberAccess(node, out _, out var propName) ||
            string.IsNullOrEmpty(propName) ||
            !KyselyTableMethods.Contains(propName))
        {
            return false;
        }

        var table = AstHelper.ExtractFirstStringArgument(node);
        if (string.IsNullOrEmpty(table)) return false;

        opName = propName;
        tableName = table;
        return true;
    }
}
