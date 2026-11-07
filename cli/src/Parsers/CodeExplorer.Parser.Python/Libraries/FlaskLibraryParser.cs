using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class FlaskLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> RouteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "route", "get", "post", "put", "delete", "patch"
    };

    public FlaskLibraryParser()
    {
        Name = "Flask";
        Id = "flask";
        Type = SemanticExtensionType.Framework;
        SupportedPatterns = ["flask", "flask.*"];
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Decorator }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsFlaskDecorator(node) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, _, _, _) => IsFlaskDecorator(node) ? ExtractRoute(node) : null,
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (!IsFlaskDecorator(node)) return;
                    var fn = FastApiLibraryParser.GetDecoratedFunction(node);
                    if (!fn.IsValid()) return;
                    var nameNode = fn.GetChildForField(TreeSitterSyntax.Fields.Name);
                    if (nameNode.IsValid() && !string.IsNullOrEmpty(nameNode.Text))
                    {
                        references.Add(new Reference(scopeSymbolId, nameNode.Text, OntologyConstants.Relationships.Triggers));
                    }
                },
                EnrichSymbol = static (node, symbol, ctx) =>
                {
                    if (!IsFlaskDecorator(node)) return;
                    symbol.Protocol = "REST";
                }
            }
        ];
    }

    public static bool IsFlaskDecorator(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;
        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return false;
        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && call.Children.Count > 0) func = call.Children[0];
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Python.Attribute)) return false;

        var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
        if (!attr.IsValid()) return false;

        return RouteMethods.Contains(attr.Text);
    }

    public static string? ExtractRoute(Node decoratorNode)
    {
        var call = decoratorNode.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return null;
        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && call.Children.Count > 0) func = call.Children[0];
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Python.Attribute)) return null;

        var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
        if (!attr.IsValid()) return null;
        var attrName = attr.Text;

        var method = "GET";
        if (!attrName.Equals("route", StringComparison.OrdinalIgnoreCase))
        {
            method = attrName.ToUpperInvariant();
        }
        else
        {
            var argList = call.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
            if (argList.IsValid())
            {
                var keywordArg = argList.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.KeywordArgument) && c.Text.StartsWith("methods"));
                if (keywordArg.IsValid())
                {
                    var strNode = keywordArg.FindDescendantOfType(TreeSitterSyntax.Python.String);
                    if (strNode.IsValid())
                    {
                        method = strNode.Text.Trim('\'', '"').ToUpperInvariant();
                    }
                }
            }
        }

        var routeVal = "/";
        var callArgs = call.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (callArgs.IsValid() && callArgs.Children.Count > 0)
        {
            var firstArg = callArgs.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.String));
            if (firstArg.IsValid())
            {
                routeVal = firstArg.Text.Trim('\'', '"');
            }
        }

        // Check if receiver has Blueprint url_prefix
        var prefix = "";
        var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object) ?? func.Children.FirstOrDefault();
        if (obj.IsValid() && !obj.Text.Equals("app", StringComparison.OrdinalIgnoreCase))
        {
            prefix = ResolveBlueprintPrefix(decoratorNode, obj.Text);
        }

        var fullRoute = FastApiLibraryParser.CombineRoutes(prefix, routeVal);
        return $"{method}:{fullRoute}";
    }

    private static string ResolveBlueprintPrefix(Node node, string bpName)
    {
        var curr = node;
        while (curr.Parent.IsValid())
        {
            curr = curr.Parent;
        }

        foreach (var stmt in curr.Children)
        {
            var text = stmt.Text;
            if (text.Contains(bpName) && text.Contains("url_prefix"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, @"url_prefix\s*=\s*['""]([^'""]+)['""]");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return "";
    }
}
