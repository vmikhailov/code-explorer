using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public static class GoAstHelper
{
    public static string? ResolveStringOrVariable(Node? argNode)
    {
        if (!argNode.IsValid()) return null;

        // 0. Unwrap expression_list if passed
        if (argNode.Is(TreeSitterSyntax.Go.ExpressionList) || argNode.Type == "expression_list")
        {
            var firstExpr = argNode.Children.FirstOrDefault(c => c.IsValid() && (char.IsLetter(c.Type[0]) || c.Type[0] == '_'));
            if (firstExpr.IsValid()) return ResolveStringOrVariable(firstExpr);
        }

        // 1. String literal
        if (argNode.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral,
                           TreeSitterSyntax.Go.RawStringLiteral,
                           TreeSitterSyntax.Go.StringLiteral))
        {
            var text = argNode.Text.Trim('"', '`');
            if (text.Contains('\n') || text.Length > 500) return null;
            return RouteDictionaryRegistry.NormalizeResolvedUrl(text);
        }

        // 2. Identifier (variable / constant)
        if (argNode.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.VariableName))
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

        // 3. fmt.Sprintf call or helper call
        if (argNode.Is(TreeSitterSyntax.Go.CallExpression))
        {
            var func = argNode.GetFunctionNode();
            if (func.IsValid() && func.Text.Contains("Sprintf"))
            {
                var args = GetCallArguments(argNode);
                if (args.Count > 0)
                {
                    var formatStr = ResolveStringOrVariable(args[0]);
                    if (!string.IsNullOrEmpty(formatStr))
                    {
                        if (args.Count > 1)
                        {
                            var hostArg = ResolveStringOrVariable(args[1]);
                            if (!string.IsNullOrEmpty(hostArg) && (hostArg.StartsWith("http") || hostArg.EndsWith("service") || hostArg.Contains('.')))
                            {
                                var pathPart = formatStr.TrimStart('*', '%', 's', '/');
                                return RouteDictionaryRegistry.NormalizeResolvedUrl($"{hostArg.TrimEnd('/')}/{pathPart}");
                            }
                        }
                        return formatStr;
                    }
                }
            }
            else
            {
                var args = GetCallArguments(argNode);
                foreach (var arg in args)
                {
                    var resolved = ResolveStringOrVariable(arg);
                    if (!string.IsNullOrEmpty(resolved) && (resolved.Contains('/') || resolved.StartsWith("http")))
                    {
                        return resolved;
                    }
                }
            }
        }

        // 4. Binary expression: host + "/api/v1/..."
        if (argNode.Type is "binary_expression" or TreeSitterSyntax.Common.BinaryExpression)
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

        // 5. Index expression / Map lookup: routes["GET_USER"]
        if (argNode.Type is "index_expression")
        {
            var indexNode = argNode.GetField("index") ??
                            (argNode.Children.Count >= 3 ? argNode.Children[2] : null);
            if (indexNode.IsValid())
            {
                var key = indexNode.Text.Trim('"', '`');
                if (RouteDictionaryRegistry.TryResolve(key, out var rPath, out var rService))
                {
                    var cleanPath = rPath.Split('?')[0];
                    return !string.IsNullOrEmpty(rService) ? $"{rService}{cleanPath}" : cleanPath;
                }
            }
        }

        return null;
    }

    public static List<Node> GetCallArguments(Node? callNode)
    {
        var result = new List<Node>();
        if (!callNode.IsValid()) return result;

        var argList = callNode.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        if (argList.IsValid())
        {
            foreach (var child in argList.Children)
            {
                if (child.IsValid() && !string.IsNullOrEmpty(child.Type) &&
                    (char.IsLetter(child.Type[0]) || child.Type[0] == '_'))
                {
                    result.Add(child);
                }
            }
        }

        return result;
    }

    private static string? FindVariableInitializerInScope(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny("statement_list", TreeSitterSyntax.Go.Block, TreeSitterSyntax.Go.FunctionDeclaration, "source_file"))
            {
                foreach (var child in curr.Children)
                {
                    // short_var_declaration: url := ...
                    if (child.Is(TreeSitterSyntax.Go.ShortVarDeclaration))
                    {
                        var leftList = child.GetField(TreeSitterSyntax.Fields.Left) ??
                                       (child.Children.Count > 0 ? child.Children[0] : null);
                        var rightList = child.GetField(TreeSitterSyntax.Fields.Right) ??
                                         (child.Children.Count > 2 ? child.Children[2] : null);

                        if (leftList.IsValid() && rightList.IsValid())
                        {
                            var lefts = leftList.Is(TreeSitterSyntax.Go.ExpressionList)
                                ? leftList.Children.Where(c => c.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.VariableName)).ToList()
                                : new List<Node> { leftList };

                            var rights = rightList.Is(TreeSitterSyntax.Go.ExpressionList)
                                ? rightList.Children.Where(c => char.IsLetter(c.Type[0]) || c.Type[0] == '_').ToList()
                                : new List<Node> { rightList };

                            for (var i = 0; i < lefts.Count && i < rights.Count; i++)
                            {
                                if (lefts[i].Text == varName)
                                {
                                    return ResolveStringOrVariable(rights[i]);
                                }
                            }
                        }
                    }
                    // var_declaration / const_declaration: var url = ... or const url = ...
                    else if (child.IsAny(TreeSitterSyntax.Go.VarSpec, TreeSitterSyntax.Go.ConstSpec, "var_declaration", "const_declaration"))
                    {
                        var specs = child.IsAny(TreeSitterSyntax.Go.VarSpec, TreeSitterSyntax.Go.ConstSpec)
                            ? new List<Node> { child }
                            : child.Children.Where(c => c.IsAny(TreeSitterSyntax.Go.VarSpec, TreeSitterSyntax.Go.ConstSpec)).ToList();

                        foreach (var spec in specs)
                        {
                            var nameNode = spec.GetField(TreeSitterSyntax.Fields.Name) ??
                                           spec.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.VariableName));
                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = spec.GetField(TreeSitterSyntax.Fields.Value) ??
                                              (spec.Children.Count >= 3 ? spec.Children[^1] : null);
                                if (valNode.IsValid())
                                {
                                    return ResolveStringOrVariable(valNode);
                                }
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
