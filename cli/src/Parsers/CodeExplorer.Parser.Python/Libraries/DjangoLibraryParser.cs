using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class DjangoLibraryParser : ISemanticExtension
{
    public string Name => "Django";
    public string Id => "django";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["django", "django.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsDjangoPathCall(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsDjangoPathCall(node))
        {
            return ExtractDjangoRoute(node);
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!IsDjangoPathCall(node)) return;

        var handlerName = ExtractHandlerName(node);
        if (!string.IsNullOrEmpty(handlerName))
        {
            references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!IsDjangoPathCall(node)) return;
        symbol.Protocol = "REST";
    }

    public static bool IsDjangoPathCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && node.Children.Count > 0) func = node.Children[0];
        if (!func.IsValid()) return false;

        var funcText = func.Text;
        return funcText is "path" or "re_path" or "django.urls.path" or "django.urls.re_path";
    }

    public static string ExtractDjangoRoute(Node callNode)
    {
        var args = callNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (args.IsValid() && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.String));
            if (firstArg.IsValid())
            {
                var routeVal = firstArg.Text.Trim('\'', '"');
                if (routeVal.StartsWith('^')) routeVal = routeVal[1..];
                if (routeVal.EndsWith('$')) routeVal = routeVal[..^1];
                return $"GET:/{routeVal.TrimStart('/')}";
            }
        }
        return "GET:/";
    }

    public static string? ExtractHandlerName(Node callNode)
    {
        var args = callNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (!args.IsValid() || args.Children.Count < 3) return null;

        var argNodes = args.Children.Where(c => !c.Type.Equals("(") && !c.Type.Equals(")") && !c.Type.Equals(",")).ToList();
        if (argNodes.Count < 2) return null;

        var handlerArg = argNodes[1];
        if (handlerArg.Is(TreeSitterSyntax.Python.Call))
        {
            var callFunc = handlerArg.GetChildForField(TreeSitterSyntax.Fields.Function) ?? (handlerArg.Children.Count > 0 ? handlerArg.Children[0] : null);
            if (callFunc.IsValid())
            {
                if (callFunc.Is(TreeSitterSyntax.Python.Attribute))
                {
                    var obj = callFunc.GetChildForField(TreeSitterSyntax.Fields.Object) ?? callFunc.Children.FirstOrDefault();
                    if (obj.IsValid()) return obj.Text;
                }
                return callFunc.Text;
            }
        }

        if (handlerArg.Is(TreeSitterSyntax.Python.Attribute))
        {
            var attr = handlerArg.GetChildForField(TreeSitterSyntax.Python.Attribute);
            if (attr.IsValid()) return attr.Text;
            return handlerArg.Text;
        }

        if (handlerArg.Is(TreeSitterSyntax.Python.Identifier))
        {
            return handlerArg.Text;
        }

        return null;
    }
}
