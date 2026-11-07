using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public static class GoAstHelper
{
    private const int MaxRecursionDepth = 8;

    public static string? ResolveStringOrVariable(Node? argNode)
    {
        return ResolveStringOrVariable(argNode, 0, null);
    }

    private static string? ResolveStringOrVariable(Node? argNode, int depth, HashSet<string>? visitedVars)
    {
        if (depth > MaxRecursionDepth || !argNode.IsValid()) return null;

        // 0. Unwrap expression_list if passed
        if (argNode.Is(TreeSitterSyntax.Go.ExpressionList))
        {
            var firstExpr =
                argNode.Children.FirstOrDefault(c => c.IsValid() && (char.IsLetter(c.Type[0]) || c.Type[0] == '_'));
            if (firstExpr.IsValid()) return ResolveStringOrVariable(firstExpr, depth + 1, visitedVars);
        }

        // 1. String literal
        if (argNode.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.RawStringLiteral,
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

            var val = FindVariableInitializerInScope(argNode, varName, depth + 1, visitedVars);

            if (val != null)
            {
                return RouteDictionaryRegistry.NormalizeResolvedUrl(val);
            }

            if (!WorkspaceConventions.IsPlaceholderName(varName) && (Regex.IsMatch(varName, @"^[A-Z0-9_]{3,}$") ||
                                                                     varName.EndsWith("Topic",
                                                                         StringComparison.OrdinalIgnoreCase) ||
                                                                     varName.EndsWith("Queue",
                                                                         StringComparison.OrdinalIgnoreCase) ||
                                                                     varName.EndsWith("Subscription",
                                                                         StringComparison.OrdinalIgnoreCase)))
            {
                return varName;
            }
        }

        // 2b. Selector expression (e.g. r.config.impressionQueue, p.base.pc.ImpressionTopic, s.cfg.PubSubSubscription)
        if (argNode.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var field = argNode.GetChildForField(TreeSitterSyntax.Fields.Field);

            if (field.IsValid())
            {
                var fieldText = field.Text;
                var val = FindVariableInitializerInScope(argNode, fieldText, depth + 1, visitedVars);

                if (val != null)
                {
                    return RouteDictionaryRegistry.NormalizeResolvedUrl(val);
                }

                if (!WorkspaceConventions.IsPlaceholderName(fieldText) &&
                    (Regex.IsMatch(fieldText, @"^[A-Z0-9_]{3,}$") ||
                     fieldText.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) ||
                     fieldText.EndsWith("Queue", StringComparison.OrdinalIgnoreCase) ||
                     fieldText.EndsWith("Exchange", StringComparison.OrdinalIgnoreCase) ||
                     fieldText.EndsWith("Subscription", StringComparison.OrdinalIgnoreCase) ||
                     fieldText.EndsWith("TopicID", StringComparison.OrdinalIgnoreCase) ||
                     fieldText.EndsWith("SubID", StringComparison.OrdinalIgnoreCase)))
                {
                    return fieldText;
                }
            }
        }

        // 3. fmt.Sprintf call or helper call
        if (argNode.Is(TreeSitterSyntax.Go.CallExpression))
        {
            var func = argNode.GetFunctionNode();

            if (func.IsValid() && (func.Text.Contains("getEnv", StringComparison.OrdinalIgnoreCase) ||
                                   func.Text.EndsWith("Getenv")))
            {
                var args = GetCallArguments(argNode);

                if (args.Count > 1)
                {
                    var defaultVal = ResolveStringOrVariable(args[1], depth + 1, visitedVars);
                    if (!string.IsNullOrEmpty(defaultVal)) return defaultVal;
                }

                if (args.Count > 0)
                {
                    var envKey = ResolveStringOrVariable(args[0], depth + 1, visitedVars);
                    if (!string.IsNullOrEmpty(envKey)) return envKey;
                }
            }

            if (func.IsValid() && func.Text.Contains("Sprintf"))
            {
                var args = GetCallArguments(argNode);

                if (args.Count > 0)
                {
                    var formatStr = ResolveStringOrVariable(args[0], depth + 1, visitedVars);

                    if (!string.IsNullOrEmpty(formatStr))
                    {
                        if (args.Count > 1)
                        {
                            var hostArg = ResolveStringOrVariable(args[1], depth + 1, visitedVars);

                            if (!string.IsNullOrEmpty(hostArg) && (hostArg.StartsWith("http") ||
                                                                   hostArg.EndsWith("service") ||
                                                                   hostArg.Contains('.')))
                            {
                                var pathPart = formatStr.TrimStart('*', '%', 's', '/');

                                return RouteDictionaryRegistry.NormalizeResolvedUrl(
                                    $"{hostArg.TrimEnd('/')}/{pathPart}");
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
                    var resolved = ResolveStringOrVariable(arg, depth + 1, visitedVars);

                    if (!string.IsNullOrEmpty(resolved) && (resolved.Contains('/') || resolved.StartsWith("http")))
                    {
                        return resolved;
                    }
                }
            }
        }

        // 4. Binary expression: host + "/api/v1/..."
        if (argNode.Is(TreeSitterSyntax.Common.BinaryExpression))
        {
            var left = argNode.GetField(TreeSitterSyntax.Fields.Left) ??
                       (argNode.Children.Count > 0 ? argNode.Children[0] : null);
            var right = argNode.GetField(TreeSitterSyntax.Fields.Right) ??
                        (argNode.Children.Count >= 2 ? argNode.Children[^1] : null);

            var leftResolved = left.IsValid() ? ResolveStringOrVariable(left, depth + 1, visitedVars) : null;
            var rightResolved = right.IsValid() ? ResolveStringOrVariable(right, depth + 1, visitedVars) : null;

            if (!string.IsNullOrEmpty(leftResolved) && !string.IsNullOrEmpty(rightResolved))
            {
                return CombineRoutes(leftResolved, rightResolved);
            }

            if (!string.IsNullOrEmpty(rightResolved))
            {
                return rightResolved;
            }

            if (!string.IsNullOrEmpty(leftResolved))
            {
                return leftResolved;
            }
        }

        // 5. Index expression / Map lookup: routes["GET_USER"]
        if (argNode.Is(TreeSitterSyntax.Common.IndexExpression))
        {
            var indexNode = argNode.GetField(TreeSitterSyntax.Fields.Index) ?? (argNode.Children.Count >= 2 ? argNode.Children[^1] : null);

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

    public static Node? FindVariableInitializerNodeInScope(Node node, string varName)
    {
        var curr = node.Parent;
        var maxScopeSteps = 50;
        var steps = 0;

        while (curr.IsValid() && ++steps <= maxScopeSteps)
        {
            if (curr.IsAny("statement_list", TreeSitterSyntax.Go.Block, TreeSitterSyntax.Go.FunctionDeclaration,
                    "source_file"))
            {
                foreach (var child in curr.Children)
                {
                    // short_var_declaration: url := ...
                    if (child.Is(TreeSitterSyntax.Go.ShortVarDeclaration))
                    {
                        var leftList = child.GetField(TreeSitterSyntax.Fields.Left) ??
                                       (child.Children.Count > 0 ? child.Children[0] : null);

                        var rightList = child.GetField(TreeSitterSyntax.Fields.Right) ??
                                        (child.Children.Count >= 2 ? child.Children[^1] : null);

                        if (leftList.IsValid() && rightList.IsValid())
                        {
                            var lefts = leftList.Is(TreeSitterSyntax.Go.ExpressionList)
                                ? leftList.Children.Where(c =>
                                        c.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.VariableName))
                                    .ToList()
                                : new List<Node> { leftList };

                            var rights = rightList.Is(TreeSitterSyntax.Go.ExpressionList)
                                ? rightList.Children.Where(c => char.IsLetter(c.Type[0]) || c.Type[0] == '_')
                                    .ToList()
                                : new List<Node> { rightList };

                            for (var i = 0; i < lefts.Count && i < rights.Count; i++)
                            {
                                if (lefts[i].Text == varName)
                                {
                                    if (IsNodeContainedWithin(node, rights[i]))
                                    {
                                        continue;
                                    }

                                    var rNode = rights[i];
                                    if (rNode.Is(TreeSitterSyntax.Go.ExpressionList))
                                    {
                                        var expr = rNode.Children.FirstOrDefault(c => c.IsValid() && (char.IsLetter(c.Type[0]) || c.Type[0] == '_'));
                                        if (expr.IsValid()) rNode = expr;
                                    }
                                    return rNode;
                                }
                            }
                        }
                    }

                    // var_declaration / const_declaration: var url = ... or const url = ...
                    else if (child.IsAny(TreeSitterSyntax.Go.VarSpec, TreeSitterSyntax.Go.ConstSpec,
                                 "var_declaration", "const_declaration"))
                    {
                        var specs = child.IsAny(TreeSitterSyntax.Go.VarSpec, TreeSitterSyntax.Go.ConstSpec)
                            ? new List<Node> { child }
                            : child.Children.Where(c =>
                                c.IsAny(TreeSitterSyntax.Go.VarSpec, TreeSitterSyntax.Go.ConstSpec)).ToList();

                        foreach (var spec in specs)
                        {
                            var nameNode = spec.GetField(TreeSitterSyntax.Fields.Name) ??
                                           spec.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Go.Identifier,
                                               TreeSitterSyntax.Go.VariableName));

                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = spec.GetField(TreeSitterSyntax.Fields.Value) ??
                                              (spec.Children.Count >= 2 ? spec.Children[^1] : null);

                                if (valNode.IsValid())
                                {
                                    if (IsNodeContainedWithin(node, valNode))
                                    {
                                        continue;
                                    }

                                    var vNode = valNode;
                                    if (vNode.Is(TreeSitterSyntax.Go.ExpressionList))
                                    {
                                        var expr = vNode.Children.FirstOrDefault(c => c.IsValid() && (char.IsLetter(c.Type[0]) || c.Type[0] == '_'));
                                        if (expr.IsValid()) vNode = expr;
                                    }
                                    return vNode;
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

    private static string? FindVariableInitializerInScope(
        Node node,
        string varName,
        int depth,
        HashSet<string>? visitedVars)
    {
        if (depth > MaxRecursionDepth) return null;

        visitedVars ??= new HashSet<string>(StringComparer.Ordinal);

        if (!visitedVars.Add(varName))
        {
            return null;
        }

        try
        {
            var initNode = FindVariableInitializerNodeInScope(node, varName);
            if (initNode.IsValid())
            {
                return ResolveStringOrVariable(initNode, depth + 1, visitedVars);
            }

            return null;
        }
        finally
        {
            visitedVars.Remove(varName);
        }
    }

    private static bool IsNodeContainedWithin(Node inner, Node outer) => inner.IsContainedWithin(outer);

    public static string ResolveRoutePrefix(Node callNode, Node? receiverNode)
    {
        if (!receiverNode.IsValid()) return "";

        var prefixes = new List<string>();

        // Check if callNode is inside an enclosing func_literal passed to .Route("/prefix", func(r) {})
        var enc = callNode.Parent;
        while (enc.IsValid())
        {
            if (enc.Is(TreeSitterSyntax.Go.FuncLiteral))
            {
                var argList = enc.Parent;
                if (argList.IsValid() && argList.Is(TreeSitterSyntax.Go.ArgumentList))
                {
                    var outerCall = argList.Parent;
                    if (outerCall.IsValid() && outerCall.Is(TreeSitterSyntax.Go.CallExpression))
                    {
                        var oFunc = outerCall.GetFunctionNode();
                        if (oFunc.IsValid() && oFunc.Is(TreeSitterSyntax.Go.SelectorExpression))
                        {
                            var fld = oFunc.GetChildForField(TreeSitterSyntax.Fields.Field);
                            if (fld.IsValid() && (fld.Text == "Route" || fld.Text == "Mount" || fld.Text == "Group"))
                            {
                                var oArgs = outerCall.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
                                if (oArgs.IsValid() && oArgs.Children.Count > 0)
                                {
                                    var firstArg = oArgs.Children.FirstOrDefault(c =>
                                        c.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral,
                                                TreeSitterSyntax.Go.RawStringLiteral,
                                                TreeSitterSyntax.Go.StringLiteral,
                                                TreeSitterSyntax.Go.Identifier));
                                    var pStr = ResolveStringOrVariable(firstArg);
                                    if (!string.IsNullOrEmpty(pStr) && (pStr.StartsWith('/') || pStr.Contains('/')))
                                    {
                                        prefixes.Insert(0, pStr.Trim('"', '`'));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            enc = enc.Parent;
        }

        var currReceiver = receiverNode;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var step = 0; step < MaxRecursionDepth; step++)
        {
            if (!currReceiver.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.VariableName))
                break;

            var varName = currReceiver.Text;
            if (!visited.Add(varName)) break;

            var initNode = FindVariableInitializerNodeInScope(callNode, varName);
            if (!initNode.IsValid() || !initNode.Is(TreeSitterSyntax.Go.CallExpression))
                break;

            var initCall = initNode;
            var func = initCall.GetFunctionNode();
            if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression))
                break;

            var methodField = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (!methodField.IsValid() || (methodField.Text != "Group" && methodField.Text != "Route" && methodField.Text != "Mount"))
                break;

            var args = initCall.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
            if (args.IsValid() && args.Children.Count > 0)
            {
                var firstArg = args.Children.FirstOrDefault(c =>
                    c.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral,
                            TreeSitterSyntax.Go.RawStringLiteral,
                            TreeSitterSyntax.Go.StringLiteral,
                            TreeSitterSyntax.Go.Identifier));

                var groupStr = ResolveStringOrVariable(firstArg);
                if (!string.IsNullOrEmpty(groupStr))
                {
                    prefixes.Insert(0, groupStr.Trim('"', '`'));
                }
            }

            var nextReceiver = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
            if (nextReceiver.IsValid())
            {
                currReceiver = nextReceiver;
            }
            else
            {
                break;
            }
        }

        if (currReceiver.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.VariableName))
        {
            var endVar = currReceiver.Text;
            var vMatch = Regex.Match(endVar, @"^(?:api)?[vV](\d+)(?:Group|Router)?$");
            if (vMatch.Success)
            {
                var vPrefix = "/v" + vMatch.Groups[1].Value;
                if (!prefixes.Contains(vPrefix, StringComparer.OrdinalIgnoreCase))
                {
                    prefixes.Insert(0, vPrefix);
                }
            }
        }

        if (prefixes.Count == 0) return "";

        var combined = "";
        foreach (var p in prefixes)
        {
            combined = CombineRoutes(combined, p);
        }
        return combined;
    }

    public static string CombineRoutes(string prefix, string path)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return path.StartsWith('/') ? path : "/" + path;
        }

        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return prefix.StartsWith('/') ? prefix : "/" + prefix;
        }

        prefix = prefix.TrimEnd('/');
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var combined = prefix + path;
        return combined.StartsWith('/') ? combined : "/" + combined;
    }

    public static bool TryGetIndexAssignment(Node node, out string operandText, out string indexText)
    {
        operandText = string.Empty;
        indexText = string.Empty;

        var targetNode = node;
        if (node.Is(TreeSitterSyntax.Go.AssignmentStatement))
        {
            var left = node.Children.FirstOrDefault();
            if (!left.IsValid()) return false;
            targetNode = left;
        }

        if (targetNode.Is(TreeSitterSyntax.Common.IndexExpression) || targetNode.Type == "index_expression")
        {
            var operand = targetNode.GetChildForField(TreeSitterSyntax.Fields.Operand);
            var index = targetNode.GetChildForField(TreeSitterSyntax.Fields.Index);

            if (operand.IsValid() && index.IsValid())
            {
                operandText = operand.Text;
                var resolved = ResolveStringOrVariable(index);
                if (!string.IsNullOrEmpty(resolved))
                {
                    indexText = resolved.Trim('"', '`');
                    return true;
                }
            }
        }

        return false;
    }
}
