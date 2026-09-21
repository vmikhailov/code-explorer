using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public static class PythonAstHelper
{
    public static string? ResolveStringOrVariable(Node? argNode)
    {
        if (!argNode.IsValid()) return null;

        // 1. String literal / f-string
        if (argNode.Is(TreeSitterSyntax.Python.String))
        {
            var text = argNode.Text.Trim('\'', '"');
            if (text.StartsWith("f'", StringComparison.OrdinalIgnoreCase) || text.StartsWith("f\"", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("r'", StringComparison.OrdinalIgnoreCase) || text.StartsWith("r\"", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("b'", StringComparison.OrdinalIgnoreCase) || text.StartsWith("b\"", StringComparison.OrdinalIgnoreCase))
            {
                text = text[1..].Trim('\'', '"');
            }
            if (text.Contains('\n') || text.Length > 500) return null;
            return RouteDictionaryRegistry.NormalizeResolvedUrl(text);
        }

        // 2. Identifier (variable)
        if (argNode.IsAny(TreeSitterSyntax.Python.Identifier, TreeSitterSyntax.Python.VariableName))
        {
            var varName = argNode.Text;
            if (RouteDictionaryRegistry.TryResolve(varName, out var rPath, out var rService))
            {
                var cleanPath = rPath.Split('?')[0];
                return !string.IsNullOrEmpty(rService) ? $"{rService}{cleanPath}" : cleanPath;
            }

            var val = FindVariableInitializerInScope(argNode, varName);
            if (val != null)
            {
                return RouteDictionaryRegistry.NormalizeResolvedUrl(val);
            }
        }

        // 3. Binary operator: BASE_URL + "/api/v1/..."
        if (argNode.IsAny(TreeSitterSyntax.Python.BinaryOperator, TreeSitterSyntax.Common.BinaryExpression))
        {
            var right = argNode.GetField(TreeSitterSyntax.Fields.Right) ??
                        (argNode.Children.Count >= 3 ? argNode.Children[2] : null);
            if (right.IsValid())
            {
                var rightResolved = ResolveStringOrVariable(right);
                if (!string.IsNullOrEmpty(rightResolved))
                {
                    return rightResolved;
                }
            }
            var left = argNode.GetField(TreeSitterSyntax.Fields.Left) ??
                       (argNode.Children.Count > 0 ? argNode.Children[0] : null);
            if (left.IsValid())
            {
                return ResolveStringOrVariable(left);
            }
        }

        // 4. Subscript / Dictionary lookup: API_ROUTES['ORDER_DETAILS'] or ROUTES[key]
        if (argNode.Is(TreeSitterSyntax.Python.Subscript))
        {
            var subscriptNode = argNode.GetField("subscript") ??
                                (argNode.Children.Count >= 3 ? argNode.Children[2] : null);
            if (subscriptNode.IsValid())
            {
                var key = subscriptNode.Text.Trim('\'', '"');
                if (RouteDictionaryRegistry.TryResolve(key, out var rPath, out var rService))
                {
                    var cleanPath = rPath.Split('?')[0];
                    return !string.IsNullOrEmpty(rService) ? $"{rService}{cleanPath}" : cleanPath;
                }
            }
        }

        // 5. Call expression: urljoin(base, path) or format()
        if (argNode.Is(TreeSitterSyntax.Python.Call))
        {
            var args = argNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
            if (args.IsValid())
            {
                foreach (var child in args.Children)
                {
                    if (child.Is(TreeSitterSyntax.Python.String))
                    {
                        var text = child.Text.Trim('\'', '"');
                        if (text.Contains('/'))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(text);
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? FindVariableInitializerInScope(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny("block", TreeSitterSyntax.Python.FunctionDefinition, TreeSitterSyntax.Python.ClassDefinition, "module"))
            {
                foreach (var child in curr.Children)
                {
                    var assign = child.Is(TreeSitterSyntax.Python.Assignment) ? child : child.FindChildOfType(TreeSitterSyntax.Python.Assignment);
                    if (assign.IsValid())
                    {
                        var left = assign.GetField(TreeSitterSyntax.Fields.Left) ??
                                   assign.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Python.Identifier, TreeSitterSyntax.Python.VariableName));
                        if (left.IsValid() && left.Text == varName)
                        {
                            var right = assign.GetField(TreeSitterSyntax.Fields.Right);
                            if (!right.IsValid())
                            {
                                var eqIdx = -1;
                                for (var i = 0; i < assign.Children.Count; i++)
                                {
                                    if (assign.Children[i].Text == "=")
                                    {
                                        eqIdx = i;
                                        break;
                                    }
                                }
                                if (eqIdx >= 0 && eqIdx + 1 < assign.Children.Count)
                                {
                                    right = assign.Children[eqIdx + 1];
                                }
                            }

                            if (right.IsValid())
                            {
                                return ResolveStringOrVariable(right);
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }
}
