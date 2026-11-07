using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MartenLibraryParser : ISemanticExtension
{
    private const string MartenDbTarget = "marten:postgres";
    private const string MartenEventPrefix = "marten-events:";

    public string Name => "Marten";
    public string Id => "marten";
    public SemanticExtensionType Type => SemanticExtensionType.DocumentDb;
    public IReadOnlyList<string> SupportedPatterns => ["Marten", "Marten.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> DocumentMethods = new(StringComparer.Ordinal)
    {
        "Store", "Insert", "Update", "Delete", "LoadAsync", "Query", "SaveChangesAsync"
    };

    private static readonly HashSet<string> EventStoreMethods = new(StringComparer.Ordinal)
    {
        "Append", "StartStream", "FetchStreamAsync", "AggregateStreamAsync"
    };

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return;

        var exprNode = func.GetField(TreeSitterSyntax.Fields.Expression) ?? (func.Children.Count > 0 ? func.Children[0] : default);
        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name) ?? (func.Children.Count > 0 ? func.Children[^1] : default);
        if (!nameNode.IsValid() || !exprNode.IsValid()) return;

        var idChild = nameNode.Is(TreeSitterSyntax.CSharp.GenericName)
            ? nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)
            : default;
        var methodName = idChild.IsValid() ? idChild.Text : nameNode.Text;
        var receiverText = exprNode.Text;

        if (EventStoreMethods.Contains(methodName) && receiverText.Contains("Events", StringComparison.Ordinal))
        {
            references.Add(new Reference(scopeSymbolId, MartenEventPrefix + methodName, OntologyConstants.Relationships.PublishesTo));
            references.Add(new Reference(scopeSymbolId, MartenDbTarget, OntologyConstants.Relationships.UsesDb));
        }
        else if (DocumentMethods.Contains(methodName) &&
                 (receiverText.Contains("session", StringComparison.OrdinalIgnoreCase) ||
                  receiverText.Contains("store", StringComparison.OrdinalIgnoreCase)))
        {
            references.Add(new Reference(scopeSymbolId, MartenDbTarget, OntologyConstants.Relationships.UsesDb));
        }
    }
}
