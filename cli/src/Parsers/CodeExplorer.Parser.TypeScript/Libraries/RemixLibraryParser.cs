using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class RemixLibraryParser : DeclarativeSemanticExtension
{
    public RemixLibraryParser()
    {
        Name = "Remix / React Router";
        Id = "remix";
        Type = SemanticExtensionType.Framework;
        SupportedPatterns = ["@remix-run/*", "react-router", "react-router-dom", "@react-router/*"];
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.FunctionDeclaration }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => TryExtractRemixRouteHandler(node, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, _, _, _) => TryExtractRemixRouteHandler(node, out var method, out var fnName) ? $"{method}:/{fnName}" : null,
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (TryExtractRemixRouteHandler(node, out _, out var fnName))
                    {
                        references.Add(new Reference(scopeSymbolId, fnName, OntologyConstants.Relationships.Triggers));
                    }
                }
            }
        ];
    }

    private static bool TryExtractRemixRouteHandler(Node node, out string httpMethod, out string fnName)
    {
        httpMethod = "GET";
        fnName = string.Empty;

        if (node.Is(TreeSitterSyntax.TypeScript.FunctionDeclaration))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            if (!nameNode.IsValid()) return false;
            var name = nameNode.Text;
            if (name is "loader" or "clientLoader")
            {
                httpMethod = "GET";
                fnName = name;
                return true;
            }
            if (name is "action" or "clientAction")
            {
                httpMethod = "POST";
                fnName = name;
                return true;
            }
        }

        return false;
    }
}
