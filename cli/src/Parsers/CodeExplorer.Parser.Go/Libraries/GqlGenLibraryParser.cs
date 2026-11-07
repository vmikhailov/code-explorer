using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GqlGenLibraryParser : ISemanticExtension
{
    public string Name => "gqlgen";
    public string Id => "gqlgen";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["github.com/99designs/gqlgen*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsGqlGenServerCall(node) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsGqlGenServerCall(node) ? "GRAPHQL:/graphql" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }

    private static bool IsGqlGenServerCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var text = func.Text;
        return text.EndsWith("handler.NewDefaultServer", StringComparison.Ordinal) ||
               text.EndsWith("handler.New", StringComparison.Ordinal) ||
               text.EndsWith("playground.Handler", StringComparison.Ordinal);
    }
}
