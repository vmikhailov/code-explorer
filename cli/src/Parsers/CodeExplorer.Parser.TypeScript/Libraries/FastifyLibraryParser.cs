using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class FastifyLibraryParser : ILibraryParser
{
    public string Name => "Fastify";
    public string Id => "fastify";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["fastify"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "options", "head", "all"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsFastifyRouteCall(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (!IsFastifyRouteCall(node)) return null;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            if (propName != null && HttpMethods.Contains(propName))
            {
                var method = propName.ToUpperInvariant();
                var routeVal = AstHelper.ExtractFirstStringArgument(node) ?? "/";
                return $"{method}:{routeVal}";
            }

            if (string.Equals(propName, "route", StringComparison.OrdinalIgnoreCase))
            {
                var (method, url) = ExtractRouteFromConfigObject(node);
                return $"{method}:{url}";
            }
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsFastifyRouteCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            if (propName == null) return false;
            return HttpMethods.Contains(propName) || string.Equals(propName, "route", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static (string Method, string Url) ExtractRouteFromConfigObject(Node callNode)
    {
        var args = AstHelper.GetCallArguments(callNode);
        if (args.Count == 0) return ("GET", "/");

        var firstArg = args[0];
        var method = "GET";
        var url = "/";

        if (AstHelper.TryGetObjectProperty(firstArg, "method", out var methodVal))
        {
            var resolvedMethod = AstHelper.ResolveStringOrTemplate(methodVal);
            if (!string.IsNullOrEmpty(resolvedMethod))
            {
                method = resolvedMethod.ToUpperInvariant();
            }
        }

        if (AstHelper.TryGetObjectProperty(firstArg, "url", out var urlVal) ||
            AstHelper.TryGetObjectProperty(firstArg, "path", out urlVal))
        {
            var resolvedUrl = AstHelper.ResolveStringOrTemplate(urlVal);
            if (!string.IsNullOrEmpty(resolvedUrl))
            {
                url = resolvedUrl;
            }
        }

        return (method, url);
    }
}
