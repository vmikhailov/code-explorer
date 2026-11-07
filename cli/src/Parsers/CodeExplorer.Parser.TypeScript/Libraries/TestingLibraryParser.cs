using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class TestingLibraryParser : DeclarativeSemanticExtension
{
    public TestingLibraryParser()
    {
        Name = "Testing Library";
        Id = "testing-library";
        Type = SemanticExtensionType.Library;
        SupportedPatterns =
        [
            "@testing-library/react*",
            "@testing-library/vue*",
            "@testing-library/svelte*",
            "@testing-library/user-event*",
            "@testing-library/dom*"
        ];
        LibraryRole = LibraryRole.TestFramework;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
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
        ];
    }
}
