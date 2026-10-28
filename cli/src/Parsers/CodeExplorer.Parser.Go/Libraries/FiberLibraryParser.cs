using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class FiberLibraryParser : ISemanticExtension
{
    public string Type => "framework";
    public string Name => "Fiber";
    public string Id => "fiber";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/gofiber/fiber*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly HashSet<string> FiberMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get", "Post", "Put", "Delete", "Patch", "Options", "Head", "All", "Add"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsFiberRouteCall(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsFiberRouteCall(node, out var httpMethod, out var fullRoute, out _))
        {
            return $"{httpMethod}:{fullRoute}";
        }
        return null;
    }

    public void CollectReferences(Node node, string containerName, List<Reference> references, ParsingContext ctx)
    {
        if (IsFiberRouteCall(node, out _, out var fullRoute, out var handlerName) && !string.IsNullOrEmpty(handlerName))
        {
            references.Add(new Reference(handlerName, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
        }
    }

    public static bool IsFiberRouteCall(Node node, out string httpMethod, out string fullRoute, out string? handlerName)
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
        if (!FiberMethods.Contains(methodName)) return false;

        var receiver = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        if (receiver.IsValid() && (receiver.Text == "http" || receiver.Text.Contains("client", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        if (!args.IsValid()) return false;

        var exprArgs = args.Children.Where(c => !c.Type.Equals("(") && !c.Type.Equals(")") && !c.Type.Equals(",")).ToList();
        if (exprArgs.Count < 2) return false;

        string? routeVal;
        if (methodName.Equals("Add", StringComparison.OrdinalIgnoreCase))
        {
            if (exprArgs.Count < 3) return false;
            var methodArg = exprArgs[0];
            httpMethod = GoAstHelper.ResolveStringOrVariable(methodArg)?.Trim('"', '`').ToUpperInvariant() ?? "GET";

            var pathArg = exprArgs[1];
            routeVal = GoAstHelper.ResolveStringOrVariable(pathArg);
        }
        else
        {
            var pathArg = exprArgs[0];
            routeVal = GoAstHelper.ResolveStringOrVariable(pathArg);
            httpMethod = methodName.Equals("All", StringComparison.OrdinalIgnoreCase) ? "GET" : methodName.ToUpperInvariant();
        }

        if (string.IsNullOrEmpty(routeVal) ||
            routeVal.StartWithIC("http://") ||
            routeVal.StartWithIC("https://"))
        {
            return false;
        }

        // Resolve group prefix from receiver operand
        var prefix = receiver.IsValid() ? GoAstHelper.ResolveRoutePrefix(node, receiver) : "";

        fullRoute = GoAstHelper.CombineRoutes(prefix, routeVal);

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
