using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Axum web framework in Rust.
/// Detects declarative routing constructs such as Router::new().route("/path", get(handler).post(handler))
/// and nested route scopes via .nest("/prefix", router).
/// </summary>
public class AxumLibraryParser : ISemanticExtension
{
    public string Id => "axum";
    public string Name => "Axum";
    public string Type => "framework";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;
    public IReadOnlyList<string> SupportedPatterns => ["axum*"];

    private static readonly HashSet<string> AxumHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options", "trace", "any"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsAxumRoute(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsAxumRoute(node, out var primaryMethod, out var fullRoute, out _))
        {
            return $"{primaryMethod}:{fullRoute}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsAxumRoute(node, out _, out var fullRoute, out var handlers))
        {
            foreach (var (_, handlerName) in handlers)
            {
                if (!string.IsNullOrEmpty(handlerName))
                {
                    references.Add(new Reference(handlerName, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
                }
            }
        }
    }

    public static bool IsAxumRoute(
        Node node,
        out string primaryMethod,
        out string fullRoute,
        out List<(string Method, string HandlerName)> handlers)
    {
        primaryMethod = "GET";
        fullRoute = "/";
        handlers = [];

        if (node.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out var receiver, out var methodName, out var args))
        {
            return false;
        }

        if (methodName != "route" || args.Count < 2)
        {
            return false;
        }

        var pathArg = args[0];
        var rawPath = RustAstHelper.ExtractStringLiteral(pathArg);
        if (string.IsNullOrEmpty(rawPath))
        {
            return false;
        }

        var nestPrefix = ExtractNestedPrefix(receiver);
        fullRoute = RustAstHelper.CombineRoutes(nestPrefix, rawPath);

        var handlerArg = args[1];
        ExtractMethodRouters(handlerArg, handlers);

        if (handlers.Count > 0)
        {
            primaryMethod = handlers[0].Method;
        }

        return true;
    }

    private static void ExtractMethodRouters(Node handlerArg, List<(string Method, string HandlerName)> handlers)
    {
        var unwrapped = RustAstHelper.UnwrapCallExpression(handlerArg);
        if (unwrapped.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            var text = CleanHandlerName(unwrapped.Text);
            if (!string.IsNullOrEmpty(text))
            {
                handlers.Add(("GET", text));
            }
            return;
        }

        if (RustAstHelper.GetMethodCallInfo(unwrapped, out var receiver, out var method, out var args))
        {
            if (!string.IsNullOrEmpty(method) && AxumHttpMethods.Contains(method))
            {
                var upperMethod = method.Equals("any", StringComparison.OrdinalIgnoreCase) ? "GET" : method.ToUpperInvariant();
                var handlerName = args.Count > 0 ? CleanHandlerName(args[0].Text) : string.Empty;
                handlers.Add((upperMethod, handlerName));
            }

            if (receiver != null && receiver.IsValid())
            {
                ExtractMethodRouters(receiver, handlers);
            }
        }
    }

    private static string ExtractNestedPrefix(Node? receiver)
    {
        var current = receiver;
        while (current != null && current.IsValid())
        {
            if (RustAstHelper.GetMethodCallInfo(current, out var parentReceiver, out var parentMethod, out var parentArgs))
            {
                if (parentMethod == "nest" && parentArgs.Count > 0)
                {
                    var nestPrefix = RustAstHelper.ExtractStringLiteral(parentArgs[0]);
                    if (!string.IsNullOrEmpty(nestPrefix))
                    {
                        var grandPrefix = ExtractNestedPrefix(parentReceiver);
                        return RustAstHelper.CombineRoutes(grandPrefix, nestPrefix);
                    }
                }
                current = parentReceiver;
            }
            else
            {
                break;
            }
        }
        return string.Empty;
    }

    private static string CleanHandlerName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var trimmed = raw.Trim();
        var doubleColon = trimmed.LastIndexOf("::", StringComparison.Ordinal);
        if (doubleColon >= 0)
        {
            trimmed = trimmed[(doubleColon + 2)..];
        }
        return trimmed.Trim();
    }
}
