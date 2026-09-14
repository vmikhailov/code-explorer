using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class NextJsLibraryParser : ILibraryParser
{
    public string Name => "Next.js";
    public string Id => "nextjs";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["next", "next/*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> HttpMethodFunctions = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsNextRouteHandler(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (!IsNextRouteHandler(node)) return null;

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        var funcName = nameNode.IsValid() ? nameNode.Text : "";

        if (HttpMethodFunctions.Contains(funcName))
        {
            return $"{funcName}:route";
        }

        if (string.Equals(funcName, "handler", StringComparison.OrdinalIgnoreCase))
        {
            return "API:handler";
        }

        return $"{funcName}:route";
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

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
