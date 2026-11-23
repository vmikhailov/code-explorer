using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Python.Libraries;

public class DjangoLibraryParser : DeclarativeSemanticExtension
{
    private const string PermissionClassesAttr = "permission_classes";

    public DjangoLibraryParser()
    {
        Name = "Django";
        Id = "django";
        Type = SemanticExtensionType.Framework;
        SupportedPatterns = ["django", "django.*", "rest_framework", "rest_framework.*"];
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsDjangoPathCall(node) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, _, _, _) => IsDjangoPathCall(node) ? ExtractDjangoRoute(node) : null,
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (!IsDjangoPathCall(node)) return;
                    var handlerName = ExtractHandlerName(node);
                    if (!string.IsNullOrEmpty(handlerName))
                    {
                        references.Add(new Reference(scopeSymbolId, handlerName, OntologyConstants.Relationships.Triggers));
                    }
                },
                EnrichSymbol = static (node, symbol, ctx) =>
                {
                    if (!IsDjangoPathCall(node)) return;
                    symbol.Protocol = "REST";
                }
            },
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.ClassDefinition }.ToFrozenSet(),
                EnrichSymbol = static (node, symbol, ctx) =>
                {
                    var perms = ExtractDrfPermissionClasses(node);
                    if (perms.Count == 0) return;

                    var joined = string.Join(", ", perms);
                    if (symbol.Properties.TryGetValue(OntologyConstants.PropertyNames.Attributes, out var existingObj) &&
                        existingObj is string existing && !string.IsNullOrWhiteSpace(existing))
                    {
                        symbol.Properties[OntologyConstants.PropertyNames.Attributes] = $"{existing}, {joined}";
                    }
                    else
                    {
                        symbol.Properties[OntologyConstants.PropertyNames.Attributes] = joined;
                    }
                }
            }
        ];
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
        if (args.IsValid() && args.Children.Count > 0)
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
        if (!args.IsValid() || args.Children.Count < 2) return null;

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

    private static List<string> ExtractDrfPermissionClasses(Node classNode)
    {
        var result = new List<string>();
        var body = classNode.GetChildForField(TreeSitterSyntax.Fields.Body)
            ?? classNode.FindChildOfType(TreeSitterSyntax.Python.Block);
        if (!body.IsValid()) return result;

        for (var i = 0; i < body.Children.Count; i++)
        {
            var stmt = body.Children[i];
            var assign = stmt.Is(TreeSitterSyntax.Python.Assignment)
                ? stmt
                : stmt.FindChildOfType(TreeSitterSyntax.Python.Assignment);
            if (!assign.IsValid()) continue;

            var left = assign.GetChildForField(TreeSitterSyntax.Fields.Left)
                ?? (assign.Children.Count > 0 ? assign.Children[0] : null);
            if (!left.IsValid() || !string.Equals(left.Text, PermissionClassesAttr, StringComparison.Ordinal))
                continue;

            var right = assign.GetChildForField(TreeSitterSyntax.Fields.Right)
                ?? (assign.Children.Count > 2 ? assign.Children[^1] : null);
            if (!right.IsValid()) continue;

            CollectPermissionIdentifiers(right, result);
        }

        return result;
    }

    private static void CollectPermissionIdentifiers(Node node, List<string> result)
    {
        if (node.Is(TreeSitterSyntax.Python.Identifier))
        {
            var text = node.Text;
            if (!string.IsNullOrWhiteSpace(text) && !result.Contains(text))
            {
                result.Add(text);
            }
            return;
        }

        if (node.Is(TreeSitterSyntax.Python.Attribute))
        {
            var attr = node.GetChildForField(TreeSitterSyntax.Python.Attribute);
            var text = attr.IsValid() ? attr.Text : node.Text;
            if (!string.IsNullOrWhiteSpace(text) && !result.Contains(text))
            {
                result.Add(text);
            }
            return;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectPermissionIdentifiers(node.Children[i], result);
        }
    }
}
