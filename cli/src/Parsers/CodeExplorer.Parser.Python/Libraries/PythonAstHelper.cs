using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public static class PythonAstHelper
{
    private const int MaxRecursionDepth = 8;

    public static string? ResolveStringOrVariable(Node? argNode)
    {
        return ResolveStringOrVariable(argNode, 0, null);
    }

    private static string? ResolveStringOrVariable(Node? argNode, int depth, HashSet<string>? visitedVars)
    {
        if (depth > MaxRecursionDepth || !argNode.IsValid()) return null;

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

            var val = FindVariableInitializerInScope(argNode, varName, depth + 1, visitedVars);
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
                var rightResolved = ResolveStringOrVariable(right, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(rightResolved))
                {
                    return rightResolved;
                }
            }
            var left = argNode.GetField(TreeSitterSyntax.Fields.Left) ??
                       (argNode.Children.Count > 0 ? argNode.Children[0] : null);
            if (left.IsValid())
            {
                return ResolveStringOrVariable(left, depth + 1, visitedVars);
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

    private static string? FindVariableInitializerInScope(Node node, string varName, int depth, HashSet<string>? visitedVars)
    {
        if (depth > MaxRecursionDepth) return null;
        visitedVars ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visitedVars.Add(varName))
        {
            return null;
        }

        try
        {
            var curr = node.Parent;
            var maxScopeSteps = 50;
            var steps = 0;
            while (curr.IsValid() && ++steps <= maxScopeSteps)
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
                                    if (IsNodeContainedWithin(node, right))
                                    {
                                        continue;
                                    }

                                    return ResolveStringOrVariable(right, depth + 1, visitedVars);
                                }
                            }
                        }
                    }
                }
                curr = curr.Parent;
            }
            return null;
        }
        finally
        {
            visitedVars.Remove(varName);
        }
    }

    public static List<Node> GetCallArguments(Node? callNode)
    {
        var result = new List<Node>();
        if (!callNode.IsValid()) return result;
        var argList = callNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (argList.IsValid())
        {
            foreach (var child in argList.Children)
            {
                if (child.Type != "(" && child.Type != ")" && child.Type != ",")
                {
                    result.Add(child);
                }
            }
        }
        return result;
    }

    public static bool TryGetMemberAccess(Node callNode, out Node? objNode, out string? methodName)
    {
        objNode = null;
        methodName = null;
        if (!callNode.Is(TreeSitterSyntax.Python.Call)) return false;

        var func = callNode.GetFunctionNode();
        if (func.IsValid() && (func.Is(TreeSitterSyntax.Python.Attribute) || func.Type == "attribute"))
        {
            methodName = func.GetChildFieldText(TreeSitterSyntax.Fields.Property) ??
                         func.Children.LastOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier) || c.Type == "identifier")?.Text;
            objNode = func.GetField(TreeSitterSyntax.Fields.Object) ?? func.Children.FirstOrDefault();
            return !string.IsNullOrEmpty(methodName);
        }
        return false;
    }

    public static Node? GetKeywordArgument(Node callNode, string argName)
    {
        var argList = callNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (!argList.IsValid()) return null;
        foreach (var child in argList.Children)
        {
            if (child.Type == "keyword_argument")
            {
                var nameNode = child.GetField("name") ?? (child.Children.Count > 0 ? child.Children[0] : null);
                if (nameNode.IsValid() && nameNode.Text == argName)
                {
                    return child.GetField("value") ?? (child.Children.Count > 2 ? child.Children[2] : null);
                }
            }
        }
        return null;
    }

    public static string? ResolveCallArgument(Node callNode, int positionalIndex, string? keywordName = null)
    {
        if (keywordName != null)
        {
            var kwNode = GetKeywordArgument(callNode, keywordName);
            if (kwNode.IsValid())
            {
                var resolved = ResolveStringOrVariable(kwNode);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
                return kwNode.Text.Trim('\'', '"');
            }
        }
        var args = GetCallArguments(callNode);
        if (positionalIndex >= 0 && positionalIndex < args.Count)
        {
            var arg = args[positionalIndex];
            if (arg.Type == "keyword_argument")
            {
                var valNode = arg.GetField("value") ?? (arg.Children.Count > 2 ? arg.Children[2] : null);
                var resolved = ResolveStringOrVariable(valNode);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
                return valNode?.Text.Trim('\'', '"');
            }
            var resolvedVal = ResolveStringOrVariable(arg);
            if (!string.IsNullOrEmpty(resolvedVal)) return resolvedVal;
            return arg.Text.Trim('\'', '"');
        }
        return null;
    }

    private static bool IsNodeContainedWithin(Node inner, Node outer) => inner.IsContainedWithin(outer);
}

