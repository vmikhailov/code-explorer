using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class TestingLibraryParser : ISemanticExtension
{
    public string Name => "Testing Library";
    public string Id => "testing-library";
    public SemanticExtensionType Type => SemanticExtensionType.Library;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@testing-library/react*",
        "@testing-library/vue*",
        "@testing-library/svelte*",
        "@testing-library/user-event*",
        "@testing-library/dom*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.TestFramework;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || func.Text != "render") return;

        var args = AstHelper.GetCallArguments(node);
        if (args.Count > 0)
        {
            var jsx = args[0].Text.TrimStart('<').Split(' ', '/', '>')[0].Trim();
            if (!string.IsNullOrEmpty(jsx) && char.IsUpper(jsx[0]))
            {
                references.Add(new Reference(scopeSymbolId, jsx, OntologyConstants.Relationships.Calls));
            }
        }
    }
}
