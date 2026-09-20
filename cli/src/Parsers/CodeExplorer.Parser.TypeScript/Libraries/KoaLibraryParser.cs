using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class KoaLibraryParser : ILibraryParser
{
    public string Name => "Koa";
    public string Id => "koa";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["@koa/router", "koa-router", "koa"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "options", "head", "all"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsKoaRouteCall(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (!IsKoaRouteCall(node)) return null;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            if (propName != null && HttpMethods.Contains(propName))
            {
                var method = propName.ToUpperInvariant();
                var routeVal = AstHelper.ExtractFirstStringArgument(node) ?? "/";
                return $"{method}:{routeVal}";
            }
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsKoaRouteCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
        {
            if (propName == null || !HttpMethods.Contains(propName)) return false;

            if (objNode.IsValid())
            {
                var objText = objNode.Text;
                return objText.Contains("router", StringComparison.OrdinalIgnoreCase) ||
                       objText.Contains("app", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
