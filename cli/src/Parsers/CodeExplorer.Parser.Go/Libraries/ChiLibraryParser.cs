using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class ChiLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public string Name => "Chi";
    public string Id => "chi";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/go-chi/chi*", "github.com/go-chi/chi/v5*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD", "CONNECT", "TRACE", "HandleFunc", "Handle", "Method"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsChiRouteCall(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsChiRouteCall(node, out var httpMethod, out var fullRoute, out _))
        {
            return $"{httpMethod}:{fullRoute}";
        }
        return null;
    }

    public void CollectReferences(Node node, string containerName, List<Reference> references, ParsingContext ctx)
    {
        if (IsChiRouteCall(node, out _, out var fullRoute, out var handlerName) && !string.IsNullOrEmpty(handlerName))
        {
            references.Add(new Reference(handlerName, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsChiRouteCall(node, out var httpMethod, out var fullRoute, out _))
        {
            symbol.Protocol = "REST";
            symbol.Properties["protocol"] = "REST";
            symbol.Properties["httpMethod"] = httpMethod;
            symbol.Properties["routeTemplate"] = fullRoute;
        }
    }

    public static bool IsChiRouteCall(Node node, out string httpMethod, out string fullRoute, out string? handlerName)
    {
        httpMethod = "GET";
        fullRoute = "/";
        handlerName = null;

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid()) return false;

        var methodName = field.Text;
        if (!HttpMethods.Contains(methodName)) return false;

        var receiver = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        if (receiver.IsValid())
        {
            var rText = receiver.Text;
            if (rText == "http" ||
                rText.Contains("client", StringComparison.OrdinalIgnoreCase) ||
                rText.Contains("header", StringComparison.OrdinalIgnoreCase) ||
                rText.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                rText.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
                rText.Contains("context", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        if (!args.IsValid()) return false;

        var exprArgs = args.Children.Where(c => !c.Type.Equals("(") && !c.Type.Equals(")") && !c.Type.Equals(",")).ToList();

        var pathArg = default(Node);
        var handlerArg = default(Node);

        if (methodName.Equals("Method", StringComparison.OrdinalIgnoreCase))
        {
            if (exprArgs.Count < 3) return false;
            var mStr = GoAstHelper.ResolveStringOrVariable(exprArgs[0]);
            httpMethod = !string.IsNullOrEmpty(mStr) ? mStr.ToUpperInvariant() : "GET";
            pathArg = exprArgs[1];
            handlerArg = exprArgs[2];
        }
        else
        {
            if (exprArgs.Count < 2) return false;
            httpMethod = methodName.Equals("HandleFunc", StringComparison.OrdinalIgnoreCase) ||
                         methodName.Equals("Handle", StringComparison.OrdinalIgnoreCase)
                ? "GET"
                : methodName.ToUpperInvariant();
            pathArg = exprArgs[0];
            handlerArg = exprArgs[^1];
        }

        if (!pathArg.IsValid()) return false;

        var routeVal = GoAstHelper.ResolveStringOrVariable(pathArg);
        if (string.IsNullOrEmpty(routeVal) ||
            routeVal.StartWithIC("http://") ||
            routeVal.StartWithIC("https://"))
        {
            return false;
        }

        // Clean up leading/trailing quotes
        routeVal = routeVal.Trim('"', '`');
        if (string.IsNullOrEmpty(routeVal)) return false;

        // Resolve route prefix from receiver operand (e.g. r.Route, r.Mount)
        var prefix = receiver.IsValid() ? GoAstHelper.ResolveRoutePrefix(node, receiver) : "";
        fullRoute = GoAstHelper.CombineRoutes(prefix, routeVal);

        if (handlerArg.IsValid())
        {
            if (handlerArg.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.SelectorExpression))
            {
                var hText = handlerArg.Text;
                handlerName = hText.Contains('.') ? hText.Split('.').Last() : hText;
            }
        }

        return true;
    }
}
