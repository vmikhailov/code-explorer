using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class LitestarLibraryParser : ISemanticExtension
{
    private const string PathKeyword = "path";

    public string Name => "Litestar";
    public string Id => "litestar";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["litestar", "litestar.*", "starlite", "starlite.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly HashSet<string> RouteDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "route", "websocket"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractLitestarRoute(node, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractLitestarRoute(node, out var method, out var path) ? $"{method}:{path}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!TryExtractLitestarRoute(node, out _, out _)) return;

        var parent = node.Parent;
        var fn = parent.IsValid() ? parent.FindChildOfType(TreeSitterSyntax.Python.FunctionDefinition) : default;
        var nameNode = fn.IsValid() ? fn.GetChildForField(TreeSitterSyntax.Fields.Name) : default;
        if (nameNode.IsValid() && !string.IsNullOrEmpty(nameNode.Text))
        {
            references.Add(new Reference(scopeSymbolId, nameNode.Text, OntologyConstants.Relationships.Triggers));
        }
    }

    private static bool TryExtractLitestarRoute(Node node, out string httpMethod, out string routePath)
    {
        httpMethod = "GET";
        routePath = "/";

        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;

        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return false;

        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                   (call.Children.Count > 0 ? call.Children[0] : default);
        if (!func.IsValid()) return false;

        // Litestar uses standalone `@get("/users")`, `@post("/users")`, `@websocket("/ws")`
        var fnText = func.Text.TrimStart('@').Trim();
        if (!RouteDecorators.Contains(fnText)) return false;

        httpMethod = fnText.Equals("websocket", StringComparison.OrdinalIgnoreCase) ? "WS" : fnText.ToUpperInvariant();
        var pathArg = PythonAstHelper.ResolveCallArgument(call, 0, PathKeyword) ?? "/";
        routePath = pathArg.StartsWith('/') ? pathArg : "/" + pathArg;
        return true;
    }
}
