using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class BunGoLibraryParser : ISemanticExtension
{
    private const string BunSqlTarget = "bun:sql";

    public string Name => "Bun (Go SQL-first ORM)";
    public string Id => "bun-go";
    public SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public IReadOnlyList<string> SupportedPatterns => ["github.com/uptrace/bun*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> BunBuilderMethods = new(StringComparer.Ordinal)
    {
        "NewSelect", "NewInsert", "NewUpdate", "NewDelete", "NewRaw", "NewCreateTable", "NewDropTable"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsBunCall(node, out _) ? OntologyConstants.NodeLabels.Query : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsBunCall(node, out var method) ? $"Bun: {method}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsBunCall(node, out _))
        {
            references.Add(new Reference(scopeSymbolId, BunSqlTarget, OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsBunCall(Node node, out string methodName)
    {
        methodName = string.Empty;
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid() || !BunBuilderMethods.Contains(field.Text)) return false;

        methodName = field.Text;
        return true;
    }
}
