using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class HonoLibraryParser : ISemanticExtension
{
    public string Name => "Hono";
    public string Id => "hono";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["hono", "hono/*", "@hono/*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly HashSet<string> HttpVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "options", "all", "on"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractHonoRoute(node, out _, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractHonoRoute(node, out var method, out var path, out _) ? $"{method}:{path}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractHonoRoute(node, out _, out _, out var handlerName) && !string.IsNullOrEmpty(handlerName))
        {
            references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
        }
    }

    private static bool TryExtractHonoRoute(Node node, out string httpMethod, out string routePath, out string? handlerName)
    {
        httpMethod = "GET";
        routePath = "/";
        handlerName = null;

        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        if (!AstHelper.TryGetMemberAccess(node, out var objNode, out var propName) ||
            string.IsNullOrEmpty(propName) ||
            !HttpVerbs.Contains(propName))
        {
            return false;
        }

        if (objNode.IsValid() && objNode.Text is "axios" or "http" or "https" or "fetch")
        {
            return false;
        }

        var args = AstHelper.GetCallArguments(node);
        if (args.Count < 2) return false;

        var path = AstHelper.ExtractFirstStringArgument(node);
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/')) return false;

        httpMethod = propName.ToUpperInvariant();
        routePath = path;

        var lastArg = args[^1];
        if (lastArg.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            handlerName = lastArg.Text;
        }

        return true;
    }
}
