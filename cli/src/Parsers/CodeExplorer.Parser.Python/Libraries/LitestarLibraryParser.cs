using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class LitestarLibraryParser : DeclarativeSemanticExtension
{
    private const string PathKeyword = "path";

    private static readonly HashSet<string> RouteDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "route", "websocket"
    };

    public LitestarLibraryParser()
    {
        Name = "Litestar";
        Id = "litestar";
        Type = SemanticExtensionType.Framework;
        SupportedPatterns = ["litestar", "litestar.*", "starlite", "starlite.*"];
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Decorator }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => TryExtractLitestarRoute(node, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, _, _, _) => TryExtractLitestarRoute(node, out var method, out var path) ? $"{method}:{path}" : null,
                CollectReferences = CollectLitestarReferences
            }
        ];
    }

    private static void CollectLitestarReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
