using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Universal AST value evaluator and expression resolver across all supported languages (C#, TypeScript, Go, Python, Java).
/// Replaces heuristic regex searches with AST-driven resolution of literals, binary concatenations,
/// template strings, configuration readers, and local reaching definitions.
/// </summary>
public static class AstValueResolver
{
    private const int MaxRecursionDepth = 15;

    [ThreadStatic]
    private static Dictionary<string, string>? _localBindings;

    internal static Dictionary<string, string>? LocalBindings => _localBindings;

    public static string? ResolveString(Node? node, string? contextOrProject = null)
    {
        return TryResolveString(node, contextOrProject, out var result) ? result : null;
    }

    public static bool TryResolveString(Node? node, string? contextOrProject, out string result)
    {
        return TryResolveStringInternal(node, contextOrProject, 0, null, out result);
    }

    public static bool TryResolveExpression(Node? node, string? contextOrProject, HashSet<string>? visitedVars, out string result)
    {
        return TryResolveStringInternal(node, contextOrProject, 0, visitedVars, out result);
    }

    public static bool TryResolveExpression(Node? node, string? contextOrProject, out string result)
    {
        return TryResolveStringInternal(node, contextOrProject, 0, null, out result);
    }

    internal static bool TryResolveStringInternal(
        Node? node,
        string? contextOrProject,
        int depth,
        HashSet<string>? visitedVars,
        out string result)
    {
        result = string.Empty;
        if (!node.IsValid() || depth > MaxRecursionDepth) return false;

        var type = node.Type;

        // 1. Direct String Literals (only if it does not contain interpolation substitutions)
        var hasInterpolationChildren = node.Children.Any(c => c.IsValid() && c.Type is TreeSitterSyntax.CSharp.Interpolation or TreeSitterSyntax.TypeScript.TemplateSubstitution);
        if (!hasInterpolationChildren && (IsStringLiteralType(type) || (node.Children.Count == 0 && IsQuoted(node.Text))))
        {
            result = Unquote(node.Text);
            return true;
        }

        // 2. Numeric and Boolean Literals
        if (type is "number" or "integer" or "float" or "number_literal" or "integer_literal" or "float_literal" or
                    "true" or "false" or "boolean_literal")
        {
            result = node.Text.Trim();
            return true;
        }

        // 2b. Argument / Attribute Argument wrapper: unwrap inner expression
        if (type is TreeSitterSyntax.Common.Argument or TreeSitterSyntax.CSharp.AttributeArgument)
        {
            var exprChild = node.GetChildForField(TreeSitterSyntax.Fields.Expression) ??
                            node.Children.FirstOrDefault(c => c.IsValid() && c.Type is not ":" and not "," and not "identifier");
            if (!exprChild.IsValid())
            {
                exprChild = node.Children.LastOrDefault(c => c.IsValid());
            }
            if (exprChild.IsValid() && exprChild.Id != node.Id)
            {
                return TryResolveStringInternal(exprChild, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 2c. Equals value clause: unwrap inner expression
        if (type is TreeSitterSyntax.CSharp.EqualsValueClause)
        {
            var valChild = node.GetField(TreeSitterSyntax.Fields.Value) ??
                           node.Children.LastOrDefault(c => c.IsValid() && c.Type != "=");
            if (valChild.IsValid() && valChild.Id != node.Id)
            {
                return TryResolveStringInternal(valChild, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 2d. Rust attribute item: #[serde(default = "default_schema_name")]
        if (type == TreeSitterSyntax.Rust.AttributeItem)
        {
            var match = Regex.Match(node.Text, @"default\s*=\s*""([A-Za-z0-9_]+)""");
            if (match.Success)
            {
                var fnName = match.Groups[1].Value;
                if (ConstantRegistry.TryResolve(contextOrProject, fnName, out result))
                {
                    return true;
                }
            }
        }

        // 2e. Rust unary/reference/await/try expressions: unwrap inner
        if (type is "reference_expression" or TreeSitterSyntax.Rust.UnaryExpression or TreeSitterSyntax.Rust.AwaitExpression or TreeSitterSyntax.Rust.TryExpression)
        {
            var inner = node.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "&" and not "mut" and not "*" and not "?" and not "." and not "await");
            if (inner.IsValid() && inner.Id != node.Id)
            {
                return TryResolveStringInternal(inner, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 2f. Rust function item (zero-arg / getter evaluation or 1-arg formatter when bound)
        if (type == TreeSitterSyntax.Rust.FunctionItem)
        {
            return TryResolveRustFunctionItem(node, contextOrProject, depth, visitedVars, out result);
        }

        // 3. Parenthesized Expression: unwrap inner
        if (type is TreeSitterSyntax.Common.ParenthesizedExpression)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var c = node.Children[i];
                if (c.IsValid() && c.Type is not "(" and not ")")
                {
                    return TryResolveStringInternal(c, contextOrProject, depth + 1, visitedVars, out result);
                }
            }
        }

        // 4. Binary Expressions (String Concatenation: Left + Right)
        if (type is TreeSitterSyntax.Common.BinaryExpression or TreeSitterSyntax.Python.BinaryOperator)
        {
            return TryResolveBinary(node, contextOrProject, depth, visitedVars, out result);
        }

        // 5. Universal Interpolated / Template / Format String ($"...", `...`, f"...")
        if (hasInterpolationChildren ||
            type is TreeSitterSyntax.CSharp.InterpolatedStringExpression or
                    TreeSitterSyntax.CSharp.InterpolatedVerbatimStringExpression or
                    TreeSitterSyntax.CSharp.InterpolatedRawStringExpression or
                    TreeSitterSyntax.TypeScript.TemplateString or
                    TreeSitterSyntax.Python.FormatString)
        {
            return TryResolveInterpolation(node, contextOrProject, depth, visitedVars, out result);
        }

        // 6. Rust format! macro invocation
        if (type == TreeSitterSyntax.Rust.MacroInvocation &&
            (node.Text.StartsWith("format!", StringComparison.Ordinal) ||
             (node.Children.Count > 0 && node.Children[0].Text == "format")))
        {
            return TryResolveRustMacro(node, contextOrProject, depth, visitedVars, out result);
        }

        // 7. Member Expression / Selector Expression / Attribute
        if (type is TreeSitterSyntax.Common.MemberExpression or
                    TreeSitterSyntax.CSharp.MemberAccessExpression or
                    TreeSitterSyntax.Go.SelectorExpression or
                    TreeSitterSyntax.Python.Attribute or
                    TreeSitterSyntax.Java.ScopedIdentifier or
                    TreeSitterSyntax.Rust.FieldExpression or
                    TreeSitterSyntax.Rust.ScopedIdentifier)
        {
            return TryResolveMember(node, contextOrProject, depth, visitedVars, out result);
        }

        // 8. Subscript / Element Access
        if (type is TreeSitterSyntax.TypeScript.SubscriptExpression or
                    TreeSitterSyntax.CSharp.ElementAccessExpression or
                    "bracket_expression" or
                    TreeSitterSyntax.Python.Subscript or
                    TreeSitterSyntax.Common.IndexExpression or
                    TreeSitterSyntax.Java.ArrayAccess)
        {
            return TryResolveSubscript(node, contextOrProject, depth, visitedVars, out result);
        }

        // 9. Call Expression
        if (type is TreeSitterSyntax.Common.CallExpression or TreeSitterSyntax.CSharp.InvocationExpression or
                    TreeSitterSyntax.Python.Call or TreeSitterSyntax.Java.MethodInvocation)
        {
            return TryResolveCall(node, contextOrProject, depth, visitedVars, out result);
        }

        // 10. Plain Identifier
        if (type is TreeSitterSyntax.Common.Identifier or TreeSitterSyntax.Common.TypeIdentifier or
                    TreeSitterSyntax.Common.VariableName)
        {
            return TryResolveIdentifier(node, contextOrProject, depth, visitedVars, out result);
        }

        return false;
    }

    private static bool TryResolveRustFunctionItem(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var parameters = node.FindChildOfType(TreeSitterSyntax.Rust.Parameters);
        var regularParamsCount = parameters.IsValid()
            ? parameters.Children.Count(c => c.IsValid() && c.Type == TreeSitterSyntax.Rust.Parameter)
            : 0;
        if (regularParamsCount == 0 || (regularParamsCount == 1 && _localBindings is { Count: > 0 }))
        {
            var block = node.FindChildOfType(TreeSitterSyntax.Rust.Block);
            if (block.IsValid())
            {
                var returnExpr = block.FindDescendantsOfType(TreeSitterSyntax.Rust.ReturnExpression).FirstOrDefault();
                Node? retChild = null;
                if (returnExpr.IsValid())
                {
                    retChild = returnExpr.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "return" and not ";");
                }
                else
                {
                    for (int i = block.Children.Count - 1; i >= 0; i--)
                    {
                        var c = block.Children[i];
                        if (c.IsValid() && c.Type is not "}" and not "{" and not ";")
                        {
                            retChild = c;
                            break;
                        }
                    }
                }

                if (retChild.IsValid() && retChild.Id != node.Id)
                {
                    return TryResolveStringInternal(retChild, contextOrProject, depth + 1, visitedVars, out result);
                }
            }
        }
        return false;
    }

    private static bool TryResolveBinary(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var op = node.GetChildForField(TreeSitterSyntax.Fields.Operator)?.Text ??
                 node.Children.FirstOrDefault(c => c.Type is "+" or ".")?.Text;

        if (op == "+" || op == ".")
        {
            var left = node.GetChildForField(TreeSitterSyntax.Fields.Left);
            var right = node.GetChildForField(TreeSitterSyntax.Fields.Right);

            if (!left.IsValid() && node.Children.Count >= 2)
            {
                left = node.Children[0];
                right = node.Children[^1];
            }

            var leftResolved = TryResolveStringInternal(left, contextOrProject, depth + 1, visitedVars, out var leftVal);
            var rightResolved = TryResolveStringInternal(right, contextOrProject, depth + 1, visitedVars, out var rightVal);

            if (leftResolved && rightResolved)
            {
                result = leftVal + rightVal;
                return true;
            }
            if (leftResolved && !rightResolved && right.IsValid() && right.Type == TreeSitterSyntax.Common.Identifier)
            {
                if (ConstantRegistry.TryResolve(contextOrProject, right.Text, out var rVal))
                {
                    result = leftVal + rVal;
                    return true;
                }
            }
            if (!leftResolved && rightResolved && left.IsValid() && left.Type == TreeSitterSyntax.Common.Identifier)
            {
                if (ConstantRegistry.TryResolve(contextOrProject, left.Text, out var lVal))
                {
                    result = lVal + rightVal;
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TryResolveInterpolation(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        var sb = new System.Text.StringBuilder();
        var allResolved = true;

        for (int i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (!child.IsValid()) continue;

            var cType = child.Type;
            if (cType is "$\"" or "@$\"" or "$@\"" or "\"" or "\"\"\"" or "`" or "f\"" or "f'" or "'" or "f" or "string_start" or "string_end" or "interpolation_start") continue;

            if (cType is TreeSitterSyntax.CSharp.Interpolation or TreeSitterSyntax.TypeScript.TemplateSubstitution)
            {
                var expr = child.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "{" and not "}" and not "${" and not "interpolation_brace");
                if (expr.IsValid() && TryResolveStringInternal(expr, contextOrProject, depth + 1, visitedVars, out var part))
                {
                    sb.Append(part);
                }
                else if (expr.IsValid() && ConstantRegistry.TryResolve(contextOrProject, expr.Text, out var cVal))
                {
                    sb.Append(cVal);
                }
                else
                {
                    allResolved = false;
                    sb.Append('{').Append(expr.IsValid() ? expr.Text : "").Append('}');
                }
            }
            else
            {
                sb.Append(child.Text);
            }
        }

        result = sb.ToString();
        return allResolved;
    }

    private static bool TryResolveRustMacro(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var tokenTree = node.FindChildOfType("token_tree");
        if (tokenTree.IsValid())
        {
            var argGroups = AstTemplateResolver.ExtractMacroArguments(tokenTree);
            if (argGroups.Count > 0)
            {
                var (firstArg, firstText) = argGroups[0];
                if (TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out var fmtStr) ||
                    (IsQuoted(firstText) && (fmtStr = Unquote(firstText)) != null))
                {
                    var remainingArgs = argGroups.Skip(1).ToList();
                    if (AstTemplateResolver.ResolveRustFormatTemplate(fmtStr, remainingArgs, contextOrProject, depth, visitedVars, out result))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool TryResolveMember(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var rawText = node.Text;

        // Direct ConstantRegistry lookup
        if (ConstantRegistry.TryResolve(contextOrProject, rawText, out var constVal))
        {
            result = constVal;
            return true;
        }

        // Environment variables: process.env.KEY
        var envMatch = Regex.Match(rawText, @"(?:process\.env|env\??)\.([A-Za-z0-9_]+)");
        if (envMatch.Success)
        {
            var envKey = envMatch.Groups[1].Value;
            if (ConstantRegistry.TryResolve(contextOrProject, envKey, out var evVal))
            {
                result = evVal;
                return true;
            }
            if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(envKey, out var der))
            {
                result = der;
                return true;
            }
        }

        // Check this.property or self.property
        if (rawText.StartWithIC("this.") ||
            rawText.StartWithIC("self."))
        {
            var propName = rawText[5..].Trim();
            // A plain field (no further member access) must not leak workspace-global constants of the same name.
            var isPlainField = propName.IndexOf('.') < 0;
            if (isPlainField
                    ? ConstantRegistry.TryResolveProjectScoped(contextOrProject, propName, out var propVal)
                    : ConstantRegistry.TryResolve(contextOrProject, propName, out propVal))
            {
                result = propVal;
                return true;
            }

            var fieldInit = AstScopeResolver.FindClassFieldInitializer(node, propName);
            if (fieldInit.IsValid())
            {
                visitedVars ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (visitedVars.Add(rawText))
                {
                    if (TryResolveStringInternal(fieldInit, contextOrProject, depth + 1, visitedVars, out result))
                    {
                        return true;
                    }
                }
            }
        }

        // Check if member object part is a config / options / broker / pubsub / env container
        if (rawText.Contains('.'))
        {
            var lastDot = rawText.LastIndexOf('.');
            var objPart = rawText[..lastDot];
            var leafProp = rawText[(lastDot + 1)..];

            if (objPart.Contains("config", StringComparison.OrdinalIgnoreCase) ||
                objPart.Contains("settings", StringComparison.OrdinalIgnoreCase) ||
                objPart.Contains("options", StringComparison.OrdinalIgnoreCase) ||
                objPart.Contains("broker", StringComparison.OrdinalIgnoreCase) ||
                objPart.Contains("pubsub", StringComparison.OrdinalIgnoreCase) ||
                objPart.Contains("env", StringComparison.OrdinalIgnoreCase))
            {
                if (ConstantRegistry.TryResolve(contextOrProject, leafProp, out var leafVal) &&
                    !WorkspaceConventions.IsPlaceholderName(leafVal))
                {
                    result = leafVal;
                    return true;
                }

                if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(leafProp, out var derivedVal))
                {
                    result = derivedVal;
                    return true;
                }
            }
        }

        // Check rightmost property
        var propNode = node.GetChildForField(TreeSitterSyntax.Fields.Property) ??
                       node.GetChildForField(TreeSitterSyntax.Fields.Field) ??
                       node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                       node.FindChildOfType(TreeSitterSyntax.Rust.FieldIdentifier);

        if (propNode.IsValid())
        {
            if (ConstantRegistry.TryResolve(contextOrProject, $"self.{propNode.Text}", out var selfFieldVal))
            {
                result = selfFieldVal;
                return true;
            }

            if ((Regex.IsMatch(propNode.Text, @"^[A-Z0-9_]{3,}$") ||
                 propNode.Text.Contains("schema", StringComparison.OrdinalIgnoreCase) ||
                 propNode.Text.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) ||
                 propNode.Text.EndsWith("Queue", StringComparison.OrdinalIgnoreCase) ||
                 propNode.Text.EndsWith("Subscription", StringComparison.OrdinalIgnoreCase)) &&
                ConstantRegistry.TryResolve(contextOrProject, propNode.Text, out var pVal))
            {
                result = pVal;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveSubscript(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        Node? argNode = null;
        var bracketList = node.FindChildOfType(TreeSitterSyntax.CSharp.BracketedArgumentList);
        if (bracketList.IsValid())
        {
            var arg = bracketList.FindChildOfType(TreeSitterSyntax.Common.Argument) ?? bracketList.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "[" and not "]");
            if (arg.IsValid())
            {
                argNode = (arg.Type == TreeSitterSyntax.Common.Argument && arg.Children.Count > 0) ? arg.Children[0] : arg;
            }
        }
        else
        {
            argNode = node.GetChildForField(TreeSitterSyntax.Fields.Index) ?? node.GetChildForField(TreeSitterSyntax.Fields.Subscript);
            if (!argNode.IsValid())
            {
                argNode = node.Children.LastOrDefault(c => c.IsValid() && c.Type is not "[" and not "]" and not ")" and not "(");
            }
        }

        if (argNode.IsValid() && TryResolveStringInternal(argNode, contextOrProject, depth + 1, visitedVars, out var keyStr))
        {
            if (ConstantRegistry.TryResolve(contextOrProject, keyStr, out var cfgVal))
            {
                result = cfgVal;
                return true;
            }
            if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(keyStr, out var derived))
            {
                result = derived;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveCall(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var func = node.GetFunctionNode();
        var funcText = func.IsValid() ? func.Text : "";

        var argsList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList) ??
                       node.FindChildOfType(TreeSitterSyntax.Common.Arguments);

        var argChildren = argsList.IsValid()
            ? argsList.Children.Where(c => c.IsValid() && c.Type is not "(" and not ")" and not ",").ToList()
            : new List<Node>();

        var firstArg = argChildren.FirstOrDefault();

        // 0-arg calls
        if (argChildren.Count == 0)
        {
            if (func.IsValid() && func.Type == TreeSitterSyntax.Rust.FieldExpression)
            {
                var recv = func.GetChildForField("value") ?? func.Children.FirstOrDefault();
                var methodIdent = func.GetChildForField("field") ?? func.FindChildOfType(TreeSitterSyntax.Rust.FieldIdentifier);
                var mName = methodIdent.IsValid() ? methodIdent.Text : "";

                if (mName is "to_string" or "to_owned" or "clone" or "as_str" or "into" or "as_ref" or "trim")
                {
                    if (recv.IsValid() && TryResolveStringInternal(recv, contextOrProject, depth + 1, visitedVars, out result))
                    {
                        return true;
                    }
                }
                else if (!string.IsNullOrEmpty(mName))
                {
                    if (ConstantRegistry.TryResolve(contextOrProject, mName, out var getterVal) ||
                        ConstantRegistry.TryResolve(contextOrProject, $"self.{mName}", out getterVal))
                    {
                        result = getterVal;
                        return true;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(funcText) && ConstantRegistry.TryResolve(contextOrProject, funcText, out var fnVal))
            {
                result = fnVal;
                return true;
            }
        }

        // 1-arg calls
        if (argChildren.Count == 1 && firstArg.IsValid())
        {
            if (funcText is "String" or "ToString")
            {
                return TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out result);
            }

            if (func.IsValid() && func.Type == TreeSitterSyntax.Rust.FieldExpression)
            {
                var methodIdent = func.GetChildForField("field") ?? func.FindChildOfType(TreeSitterSyntax.Rust.FieldIdentifier);
                var mName = methodIdent.IsValid() ? methodIdent.Text : "";
                if (!string.IsNullOrEmpty(mName))
                {
                    var targetFn = AstScopeResolver.FindEnclosingFunctionItem(node, mName);
                    if (targetFn.IsValid())
                    {
                        var paramIdent = AstScopeResolver.FindFirstParameterName(targetFn);
                        var boundVal = "";
                        if (TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out var resolvedArg))
                        {
                            boundVal = resolvedArg;
                        }
                        else if (ConstantRegistry.TryResolve(contextOrProject, firstArg.Text.TrimStart('&'), out var regVal))
                        {
                            boundVal = regVal;
                        }

                        if (!string.IsNullOrEmpty(paramIdent))
                        {
                            _localBindings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            var prevBound = _localBindings.TryGetValue(paramIdent, out var oldBound);
                            try
                            {
                                _localBindings[paramIdent] = boundVal;
                                if (TryResolveStringInternal(targetFn, contextOrProject, depth + 1, visitedVars, out result))
                                {
                                    return true;
                                }
                            }
                            finally
                            {
                                if (prevBound && oldBound != null)
                                {
                                    _localBindings[paramIdent] = oldBound;
                                }
                                else
                                {
                                    _localBindings.Remove(paramIdent);
                                }
                            }
                        }
                    }
                }
            }

            // Configuration readers
            if (funcText.EndsWith(".get", StringComparison.OrdinalIgnoreCase) ||
                funcText.EndsWith(".getString", StringComparison.OrdinalIgnoreCase) ||
                funcText.EndsWith(".GetValue", StringComparison.OrdinalIgnoreCase) ||
                funcText.EndsWith(".GetConnectionString", StringComparison.OrdinalIgnoreCase) ||
                funcText.Equals("os.Getenv", StringComparison.OrdinalIgnoreCase) ||
                funcText.Equals("viper.GetString", StringComparison.OrdinalIgnoreCase) ||
                funcText.Equals("os.getenv", StringComparison.OrdinalIgnoreCase) ||
                funcText.Equals("environ.get", StringComparison.OrdinalIgnoreCase))
            {
                if (TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out var cfgKey))
                {
                    if (ConstantRegistry.TryResolve(contextOrProject, cfgKey, out var cfgVal))
                    {
                        result = cfgVal;
                        return true;
                    }
                    if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(cfgKey, out var der))
                    {
                        result = der;
                        return true;
                    }
                }
            }
        }

        // 2-arg configuration helpers
        if (argChildren.Count == 2 &&
            (funcText.Contains("load", StringComparison.OrdinalIgnoreCase) ||
             funcText.Contains("env", StringComparison.OrdinalIgnoreCase) ||
             funcText.Contains("get", StringComparison.OrdinalIgnoreCase)))
        {
            if (TryResolveStringInternal(argChildren[0], contextOrProject, depth + 1, visitedVars, out var envKey) &&
                Regex.IsMatch(envKey, @"^[A-Z0-9_]{3,}$"))
            {
                if (ConstantRegistry.TryResolve(contextOrProject, envKey, out var regVal))
                {
                    result = regVal;
                    return true;
                }
                if (TryResolveStringInternal(argChildren[1], contextOrProject, depth + 1, visitedVars, out var defVal) && !string.IsNullOrWhiteSpace(defVal))
                {
                    result = defVal;
                    return true;
                }
            }
        }

        // Go fmt.Sprintf
        if (funcText.Equals("fmt.Sprintf", StringComparison.OrdinalIgnoreCase) && argChildren.Count >= 1)
        {
            if (TryResolveStringInternal(argChildren[0], contextOrProject, depth + 1, visitedVars, out var fmtStr) ||
                (IsQuoted(argChildren[0].Text) && (fmtStr = Unquote(argChildren[0].Text)) != null))
            {
                var remaining = argChildren.Skip(1).ToList();
                if (AstTemplateResolver.ResolveFormatTemplate(fmtStr, remaining, contextOrProject, depth, visitedVars, isRustFormat: false, out result))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryResolveIdentifier(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var varName = node.Text;

        // Thread-local bound parameter lookup
        if (_localBindings != null && _localBindings.TryGetValue(varName, out var boundParamVal))
        {
            result = boundParamVal;
            return true;
        }

        // Reaching Definition in scope
        visitedVars ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (visitedVars.Add(varName))
        {
            var declNode = AstScopeResolver.FindVariableDeclarationInScope(node, varName);
            if (declNode.IsValid())
            {
                if (TryResolveStringInternal(declNode, contextOrProject, depth + 1, visitedVars, out result))
                {
                    return true;
                }
                return false;
            }
        }

        // Formal parameter of enclosing function/method
        if (AstScopeResolver.IsEnclosingFunctionParameter(node, varName))
        {
            return false;
        }

        // Registry lookup
        if (ConstantRegistry.TryResolve(contextOrProject, varName, out var regVal))
        {
            result = regVal;
            return true;
        }

        // Check RouteDictionaryRegistry
        if (RouteDictionaryRegistry.TryResolve(varName, out var rPath, out var rService))
        {
            result = string.IsNullOrEmpty(rService) ? rPath : $"{rService}:{rPath}";
            return true;
        }

        // Check class field initializer
        if (!visitedVars.Contains($"field:{varName}"))
        {
            visitedVars.Add($"field:{varName}");
            var fieldInit = AstScopeResolver.FindClassFieldInitializer(node, varName);
            if (fieldInit.IsValid())
            {
                if (TryResolveStringInternal(fieldInit, contextOrProject, depth + 1, visitedVars, out result))
                {
                    ConstantRegistry.Register(contextOrProject, varName, result);
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryResolveTopicOrQueue(Node? node, string? contextOrProject, out string result)
    {
        result = string.Empty;
        if (!node.IsValid()) return false;

        if (TryResolveString(node, contextOrProject, out var resolved) &&
            IsValidTopicOrQueueLiteral(resolved))
        {
            result = resolved;
            return true;
        }

        if (node.Type is TreeSitterSyntax.Common.MemberExpression or
                       TreeSitterSyntax.CSharp.MemberAccessExpression or
                       TreeSitterSyntax.Go.SelectorExpression or
                       TreeSitterSyntax.Python.Attribute or
                       TreeSitterSyntax.Common.Identifier)
        {
            var propName = node.Type == TreeSitterSyntax.Common.Identifier
                ? node.Text
                : (node.GetChildForField(TreeSitterSyntax.Fields.Property)?.Text ?? node.GetChildForField(TreeSitterSyntax.Fields.Name)?.Text ?? "");

            if (ConstantRegistry.TryDeriveTopicOrQueueFromConstantName(propName, out var derived))
            {
                result = derived;
                return true;
            }
        }

        return false;
    }

    public static bool IsValidTopicOrQueueLiteral(string? s)
    {
        return WorkspaceConventions.IsValidTopicOrQueueName(s);
    }

    public static Node? FindVariableDeclarationInScope(Node node, string varName) =>
        AstScopeResolver.FindVariableDeclarationInScope(node, varName);

    public static Node? FindClassFieldInitializer(Node node, string fieldName) =>
        AstScopeResolver.FindClassFieldInitializer(node, fieldName);

    private static bool IsStringLiteralType(string type) =>
        type is TreeSitterSyntax.Common.String or
                TreeSitterSyntax.Common.StringLiteral or
                TreeSitterSyntax.CSharp.VerbatimStringLiteral or
                TreeSitterSyntax.Go.RawStringLiteral or
                TreeSitterSyntax.Go.InterpretedStringLiteral or
                TreeSitterSyntax.Java.TextBlock or
                TreeSitterSyntax.Rust.StringLiteral or
                TreeSitterSyntax.Rust.RawStringLiteral or
                TreeSitterSyntax.TypeScript.TemplateString;

    public static bool IsQuoted(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2) return false;
        var first = text[0];
        var last = text[^1];
        return (first == '"' && last == '"') ||
               (first == '\'' && last == '\'') ||
               (first == '`' && last == '`');
    }

    public static string Unquote(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var trimmed = raw.Trim();

        if (trimmed.StartsWith("@\"") && trimmed.EndsWith("\"") && trimmed.Length >= 3)
        {
            return trimmed[2..^1].Replace("\"\"", "\"");
        }

        if (trimmed.StartsWith("\"\"\"") && trimmed.EndsWith("\"\"\"") && trimmed.Length >= 6)
        {
            return trimmed[3..^3].Trim('\r', '\n');
        }

        if (trimmed.StartsWith("r#\"") && trimmed.EndsWith("\"#") && trimmed.Length >= 5)
        {
            return trimmed[3..^2];
        }

        if (trimmed.StartsWith("r\"") && trimmed.EndsWith("\"") && trimmed.Length >= 3)
        {
            return trimmed[2..^1];
        }

        if (trimmed.StartsWith('`') && trimmed.EndsWith('`') && trimmed.Length >= 2)
        {
            return trimmed[1..^1].Replace("\r", "");
        }

        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            var inner = trimmed[1..^1];
            return UnescapeQuotedString(inner);
        }

        return trimmed;
    }

    public static string UnescapeQuotedString(string inner)
    {
        if (string.IsNullOrEmpty(inner) || inner.IndexOf('\\') < 0)
        {
            return inner;
        }

        var sb = new System.Text.StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c == '\\' && i + 1 < inner.Length)
            {
                var next = inner[i + 1];
                switch (next)
                {
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': sb.Append('\r'); i++; break;
                    case 't': sb.Append('\t'); i++; break;
                    case '"': sb.Append('"'); i++; break;
                    case '\'': sb.Append('\''); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case '0': sb.Append('\0'); i++; break;
                    case 'a': sb.Append('\a'); i++; break;
                    case 'b': sb.Append('\b'); i++; break;
                    case 'f': sb.Append('\f'); i++; break;
                    case 'v': sb.Append('\v'); i++; break;
                    case 'u' when i + 5 < inner.Length && int.TryParse(inner.AsSpan(i + 2, 4), System.Globalization.NumberStyles.HexNumber, null, out var u):
                        sb.Append((char)u);
                        i += 5;
                        break;
                    case 'x' when i + 3 < inner.Length && byte.TryParse(inner.AsSpan(i + 2, 2), System.Globalization.NumberStyles.HexNumber, null, out var b):
                        sb.Append((char)b);
                        i += 3;
                        break;
                    default:
                        sb.Append('\\');
                        sb.Append(next);
                        i++;
                        break;
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
