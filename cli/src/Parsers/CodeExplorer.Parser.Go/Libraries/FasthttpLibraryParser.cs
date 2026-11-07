using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class FasthttpLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> FasthttpRouterMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD", "Handle"
    };

    public FasthttpLibraryParser()
    {
        Type = SemanticExtensionType.Framework;
        Name = "FastHTTP";
        Id = "fasthttp";
        SupportedPatterns =
        [
            "github.com/valyala/fasthttp*",
            "github.com/buaazp/fasthttprouter*",
            "github.com/fasthttp/router*",
            "*fasthttp*"
        ];
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) =>
                {
                    if (IsFasthttpRouteCall(node, out _, out _, out _)) return OntologyConstants.NodeLabels.EntryPoint;
                    if (IsFasthttpClientCall(node, out _)) return OntologyConstants.NodeLabels.ExternalService;
                    return null;
                },
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (IsFasthttpRouteCall(node, out var httpMethod, out var fullRoute, out _)) return $"{httpMethod}:{fullRoute}";
                    if (IsFasthttpClientCall(node, out var target)) return target;
                    return null;
                },
                CollectReferences = static (node, _, references, _) =>
                {
                    if (IsFasthttpRouteCall(node, out _, out var fullRoute, out var handlerName) && !string.IsNullOrEmpty(handlerName))
                    {
                        references.Add(new Reference(handlerName, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    }
                },
                EnrichSymbol = EnrichFasthttpSymbol
            }
        ];
    }

    private static void EnrichFasthttpSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsFasthttpRouteCall(node, out var httpMethod, out var fullRoute, out _))
        {
            symbol.Protocol = "REST";
            symbol.Properties["protocol"] = "REST";
            symbol.Properties["httpMethod"] = httpMethod;
            symbol.Properties["routeTemplate"] = fullRoute;
        }
        else if (IsFasthttpClientCall(node, out var target))
        {
            symbol.Protocol = "HTTP";
            symbol.Properties["protocol"] = "HTTP";
            symbol.Properties["target"] = target;
        }
    }

    public static bool IsFasthttpRouteCall(Node node, out string httpMethod, out string fullRoute, out string? handlerName)
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
        if (!FasthttpRouterMethods.Contains(methodName)) return false;

        var receiver = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        if (receiver.IsValid())
        {
            var rText = receiver.Text;
            if (rText is "http" or "nethttp" ||
                rText.Contains("client", StringComparison.OrdinalIgnoreCase) ||
                rText.Contains("header", StringComparison.OrdinalIgnoreCase) ||
                rText.Contains("response", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        if (!args.IsValid()) return false;

        var exprArgs = args.Children.Where(c => !c.Type.Equals("(") && !c.Type.Equals(")") && !c.Type.Equals(",")).ToList();
        if (exprArgs.Count < 2) return false;

        if (methodName.Equals("Handle", StringComparison.OrdinalIgnoreCase))
        {
            if (exprArgs.Count < 2) return false;
            var methodVal = GoAstHelper.ResolveStringOrVariable(exprArgs[0]);
            if (!string.IsNullOrEmpty(methodVal))
            {
                httpMethod = methodVal.ToUpperInvariant();
            }
            var pathVal = GoAstHelper.ResolveStringOrVariable(exprArgs[1]);
            if (string.IsNullOrEmpty(pathVal)) return false;
            fullRoute = pathVal.StartsWith('/') ? pathVal : "/" + pathVal;

            if (exprArgs.Count >= 3)
            {
                handlerName = ExtractHandlerName(exprArgs[2]);
            }

            var pfx = receiver.IsValid() ? GoAstHelper.ResolveRoutePrefix(node, receiver) : "";
            fullRoute = GoAstHelper.CombineRoutes(pfx, fullRoute);
            return true;
        }

        httpMethod = methodName.ToUpperInvariant();
        var routeArg = exprArgs[0];
        var routeVal = GoAstHelper.ResolveStringOrVariable(routeArg);
        if (string.IsNullOrEmpty(routeVal)) return false;
        fullRoute = routeVal.StartsWith('/') ? routeVal : "/" + routeVal;

        if (exprArgs.Count >= 2)
        {
            handlerName = ExtractHandlerName(exprArgs[1]);
        }

        var prefix = receiver.IsValid() ? GoAstHelper.ResolveRoutePrefix(node, receiver) : "";
        fullRoute = GoAstHelper.CombineRoutes(prefix, fullRoute);
        return true;
    }

    public static bool IsFasthttpClientCall(Node node, out string target)
    {
        target = "";
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var fnText = func.Text;
        if (fnText.Equals("fasthttp.Get", StringComparison.Ordinal) ||
            fnText.Equals("fasthttp.Post", StringComparison.Ordinal) ||
            fnText.Equals("fasthttp.GetTimeout", StringComparison.Ordinal) ||
            fnText.Equals("fasthttp.PostTimeout", StringComparison.Ordinal))
        {
            var args = GoAstHelper.GetCallArguments(node);
            if (args.Count >= 2)
            {
                var resolved = GoAstHelper.ResolveStringOrVariable(args[1]);
                if (!string.IsNullOrEmpty(resolved))
                {
                    target = resolved;
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ExtractHandlerName(Node node)
    {
        if (node.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var f = node.GetChildForField(TreeSitterSyntax.Fields.Field);
            return f.IsValid() ? f.Text : node.Text;
        }
        if (node.Is(TreeSitterSyntax.Go.Identifier))
        {
            return node.Text;
        }
        return null;
    }
}
