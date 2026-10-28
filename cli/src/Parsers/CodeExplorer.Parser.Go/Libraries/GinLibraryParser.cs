using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GinLibraryParser : ISemanticExtension
{
    public string Type => "framework";
    public string Name => "Gin";
    public string Id => "gin";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/gin-gonic/gin*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly HashSet<string> HttpMethods = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD", "Any", "Match"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGinRouteCall(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsGinRouteCall(node, out var httpMethod, out var fullRoute, out _))
        {
            return $"{httpMethod}:{fullRoute}";
        }
        return null;
    }

    public void CollectReferences(Node node, string containerName, List<Reference> references, ParsingContext ctx)
    {
        if (IsGinRouteCall(node, out _, out var fullRoute, out var handlerName) && !string.IsNullOrEmpty(handlerName))
        {
            references.Add(new Reference(handlerName, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
        }
    }

    public static bool IsGinRouteCall(Node node, out string httpMethod, out string fullRoute, out string? handlerName)
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
        if (receiver.IsValid() && (receiver.Text == "http" || receiver.Text.Contains("client", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        if (!args.IsValid()) return false;

        var exprArgs = args.Children.Where(c => !c.Type.Equals("(") && !c.Type.Equals(")") && !c.Type.Equals(",")).ToList();
        if (exprArgs.Count < 2) return false;

        var pathArg = exprArgs[0];
        var routeVal = GoAstHelper.ResolveStringOrVariable(pathArg);
        if (string.IsNullOrEmpty(routeVal) ||
            routeVal.StartWithIC("http://") ||
            routeVal.StartWithIC("https://"))
        {
            return false;
        }

        // Resolve group prefix from receiver operand
        var prefix = receiver.IsValid() ? GoAstHelper.ResolveRoutePrefix(node, receiver) : "";

        fullRoute = GoAstHelper.CombineRoutes(prefix, routeVal);
        httpMethod = methodName == "Any" ? "GET" : methodName.ToUpperInvariant();

        // Extract handler name from last argument if it's an identifier or selector
        var lastArg = exprArgs[^1];
        if (lastArg.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.SelectorExpression))
        {
            var hText = lastArg.Text;
            handlerName = hText.Contains('.') ? hText.Split('.').Last() : hText;
        }

        return true;
    }
}
