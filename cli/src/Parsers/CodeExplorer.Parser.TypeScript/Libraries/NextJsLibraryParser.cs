using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class NextJsLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> HttpMethodFunctions = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"
    };

    public NextJsLibraryParser()
    {
        Name = "Next.js";
        Id = "nextjs";
        Type = SemanticExtensionType.Framework;
        SupportedPatterns = ["next", "next/*"];
        LibraryRole = LibraryRole.FrontendFramework;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.FunctionDeclaration }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsNextRouteHandler(node) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsNextRouteHandler(node)) return null;
                    var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
                    var funcName = nameNode.IsValid() ? nameNode.Text : "";
                    if (HttpMethodFunctions.Contains(funcName)) return $"{funcName}:route";
                    if (string.Equals(funcName, "handler", StringComparison.OrdinalIgnoreCase)) return "API:handler";
                    return $"{funcName}:route";
                }
            }
        ];
    }

    private static bool IsNextRouteHandler(Node node)
    {
        // Only match the function_declaration itself (not the enclosing export_statement)
        if (!node.Is(TreeSitterSyntax.TypeScript.FunctionDeclaration)) return false;

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        var funcName = nameNode.Text;
        if (HttpMethodFunctions.Contains(funcName))
        {
            return true;
        }

        if (string.Equals(funcName, "handler", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
