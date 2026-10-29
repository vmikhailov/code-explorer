using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Universal AST value evaluator and expression resolver operating on Universal Syntax nodes.
/// Replaces heuristic regex searches with AST-driven resolution of literals, binary concatenations,
/// template strings, configuration readers, and local reaching definitions.
/// </summary>
public static class AstValueResolver
{
    private const int MaxRecursionDepth = 15;

    [ThreadStatic]
    private static Dictionary<string, string>? _localBindings;

    [ThreadStatic]
    private static bool _lastResolvedWasDecoded;

    internal static Dictionary<string, string>? LocalBindings => _localBindings;

    public static bool TryResolveStringWithDecoding(Node? node, string? contextOrProject, out string result, out bool isDecoded)
    {
        _lastResolvedWasDecoded = false;
        var ok = TryResolveStringInternal(node, contextOrProject, 0, null, out result);
        isDecoded = ok && _lastResolvedWasDecoded;
        return ok;
    }

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

        if (node is ExprSyntax { ResolvedValue: { Length: > 0 } preResolved } &&
            (_localBindings == null || _localBindings.Count == 0))
        {
            if (((ExprSyntax)node).IsDecoded) _lastResolvedWasDecoded = true;
            result = preResolved;
            return true;
        }

        // 1. Direct String Literals (only if it does not contain interpolation substitutions)
        var hasInterpolationChildren = node is InterpolatedStringExprSyntax ||
                                       node.Children.Any(c => c.IsValid() && c.Type is "interpolation" or "template_substitution");

        if (!hasInterpolationChildren &&
            (node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.Char or LiteralSyntaxKind.RawString } ||
             (node.Children.Count == 0 && IsQuoted(node.Text))))
        {
            if (ContainsObfuscatedHexOrUnicodeEscapes(node.Text))
            {
                _lastResolvedWasDecoded = true;
            }
            result = Unquote(node.Text);
            return true;
        }

        // 2. Numeric and Boolean Literals
        if (node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.Integer or LiteralSyntaxKind.Float or LiteralSyntaxKind.Boolean })
        {
            result = node.Text.Trim();
            return true;
        }

        // 2b. Argument wrapper: unwrap inner expression
        if (node is ArgumentSyntax argSyntax)
        {
            var exprChild = argSyntax.Value ??
                            node.GetChildForField(SyntaxFields.Expression) ??
                            node.Children.FirstOrDefault(c => c.IsValid() && c.Type is not ":" and not "," && c is not IdentifierExprSyntax);
            if (!exprChild.IsValid())
            {
                exprChild = node.Children.LastOrDefault(c => c.IsValid());
            }
            if (exprChild.IsValid() && exprChild.Id != node.Id)
            {
                return TryResolveStringInternal(exprChild, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 2c. Initializer value wrapper (e.g. `= expr`): unwrap inner expression
        if (node.Children.Count > 1 && node.Children[0].Text == "=")
        {
            var valChild = node.GetField(SyntaxFields.Value) ??
                           node.Children.LastOrDefault(c => c.IsValid() && c.Text != "=");
            if (valChild.IsValid() && valChild.Id != node.Id)
            {
                return TryResolveStringInternal(valChild, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 2d. Annotation / Attribute default reference (e.g. default = "fn_name")
        if (node is AnnotationSyntax)
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

        // 2e. Unary / reference / await / try expressions: unwrap inner
        if (node is UnaryExprSyntax or AwaitOrTryExprSyntax)
        {
            var inner = (node as UnaryExprSyntax)?.Operand ??
                        (node as AwaitOrTryExprSyntax)?.Operand ??
                        node.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "&" and not "mut" and not "*" and not "?" and not "." and not "await");
            if (inner.IsValid() && inner.Id != node.Id)
            {
                return TryResolveStringInternal(inner, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 2f. Function declaration (zero-arg / getter evaluation or 1-arg formatter when bound)
        if (node is FunctionDeclSyntax)
        {
            return TryResolveFunctionDeclaration(node, contextOrProject, depth, visitedVars, out result);
        }

        // 3. Parenthesized Expression: unwrap inner
        if (node.Children.Count >= 3 && node.Children[0].Text == "(" && node.Children[^1].Text == ")")
        {
            for (int i = 1; i < node.Children.Count - 1; i++)
            {
                var c = node.Children[i];
                if (c.IsValid() && c.Text is not "(" and not ")")
                {
                    return TryResolveStringInternal(c, contextOrProject, depth + 1, visitedVars, out result);
                }
            }
        }

        // 4. Binary Expressions (String Concatenation: Left + Right)
        if (node is BinaryExprSyntax)
        {
            return TryResolveBinary(node, contextOrProject, depth, visitedVars, out result);
        }

        // 5. Universal Interpolated / Template / Format String ($"...", `...`, f"...")
        if (hasInterpolationChildren)
        {
            return TryResolveInterpolation(node, contextOrProject, depth, visitedVars, out result);
        }

        // 6. Format macro invocation (e.g. format!(...))
        if (node is CallExprSyntax { CallKind: CallSyntaxKind.Macro } &&
            (node.Text.StartsWith("format!", StringComparison.Ordinal) ||
             (node.Children.Count > 0 && node.Children[0].Text == "format")))
        {
            return TryResolveFormatMacro(node, contextOrProject, depth, visitedVars, out result);
        }

        // 7. Member Access Expression
        if (node is MemberAccessExprSyntax)
        {
            return TryResolveMember(node, contextOrProject, depth, visitedVars, out result);
        }

        // 8. Index / Slice Access
        if (node is IndexOrSliceExprSyntax)
        {
            return TryResolveSubscript(node, contextOrProject, depth, visitedVars, out result);
        }

        // 9. Call Expression
        if (node is CallExprSyntax)
        {
            return TryResolveCall(node, contextOrProject, depth, visitedVars, out result);
        }

        // 10. Plain Identifier
        if (node is IdentifierExprSyntax or IdentifierPatternSyntax or TypeRefSyntax { TypeArguments.Count: 0 })
        {
            return TryResolveIdentifier(node, contextOrProject, depth, visitedVars, out result);
        }

        return false;
    }

    private static bool TryResolveFunctionDeclaration(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var fn = node as FunctionDeclSyntax;
        var regularParamsCount = fn != null
            ? fn.Parameters.Count(p => p.PassingMode != ParameterPassingMode.Receiver && p.Name is not "self" and not "&self" and not "&mut self")
            : 0;

        if (regularParamsCount == 0 || (regularParamsCount == 1 && _localBindings is { Count: > 0 }))
        {
            Node? block = fn?.Body ?? node.Children.FirstOrDefault(c => c is BlockStmtSyntax);
            if (block.IsValid())
            {
                var returnStmt = FindFirstReturnStmt(block);
                Node? retChild = null;
                if (returnStmt.IsValid())
                {
                    retChild = (returnStmt as ReturnOrYieldStmtSyntax)?.Value ??
                               returnStmt.Children.FirstOrDefault(c => c.IsValid() && c.Text is not "return" and not ";");
                }
                else
                {
                    for (int i = block.Children.Count - 1; i >= 0; i--)
                    {
                        var c = block.Children[i];
                        if (c.IsValid() && c.Text is not "}" and not "{" and not ";")
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

    private static Node? FindFirstReturnStmt(Node root)
    {
        foreach (var child in root.Children)
        {
            if (child is ReturnOrYieldStmtSyntax) return child;
            var sub = FindFirstReturnStmt(child);
            if (sub.IsValid()) return sub;
        }
        return null;
    }

    private static bool TryResolveBinary(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var bin = node as BinaryExprSyntax;
        var op = !string.IsNullOrEmpty(bin?.Operator)
            ? bin.Operator
            : (node.GetChildForField(SyntaxFields.Operator)?.Text ??
               node.Children.FirstOrDefault(c => c.Text is "+" or ".")?.Text);

        if (op == "+" || op == ".")
        {
            var left = bin?.Left ?? node.GetChildForField(SyntaxFields.Left);
            var right = bin?.Right ?? node.GetChildForField(SyntaxFields.Right);

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
            if (leftResolved && !rightResolved && right is IdentifierExprSyntax)
            {
                if (ConstantRegistry.TryResolve(contextOrProject, right.Text, out var rVal))
                {
                    result = leftVal + rVal;
                    return true;
                }
            }
            if (!leftResolved && rightResolved && left is IdentifierExprSyntax)
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

            if (cType is "interpolation" or "template_substitution")
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

    private static bool TryResolveFormatMacro(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
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
        var memberSyntax = node as MemberAccessExprSyntax;
        var propText = !string.IsNullOrEmpty(memberSyntax?.MemberName)
            ? memberSyntax.MemberName
            : (node.GetChildForField(SyntaxFields.Property) ??
               node.GetChildForField(SyntaxFields.Field) ??
               node.GetChildForField(SyntaxFields.Name) ??
               node.Children.LastOrDefault(c => c is IdentifierExprSyntax))?.Text;

        if (!string.IsNullOrEmpty(propText))
        {
            if (ConstantRegistry.TryResolve(contextOrProject, $"self.{propText}", out var selfFieldVal))
            {
                result = selfFieldVal;
                return true;
            }

            if ((Regex.IsMatch(propText, @"^[A-Z0-9_]{3,}$") ||
                 propText.Contains("schema", StringComparison.OrdinalIgnoreCase) ||
                 propText.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) ||
                 propText.EndsWith("Queue", StringComparison.OrdinalIgnoreCase) ||
                 propText.EndsWith("Subscription", StringComparison.OrdinalIgnoreCase)) &&
                ConstantRegistry.TryResolve(contextOrProject, propText, out var pVal))
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
        var idxSyntax = node as IndexOrSliceExprSyntax;
        Node? argNode = idxSyntax?.Index;

        if (!argNode.IsValid())
        {
            var bracketList = node.Children.FirstOrDefault(c => c.Children.Count > 0 && c.Text.StartsWith('['));
            if (bracketList.IsValid())
            {
                var arg = bracketList.Children.FirstOrDefault(c => c is ArgumentSyntax) ??
                          bracketList.Children.FirstOrDefault(c => c.IsValid() && c.Text is not "[" and not "]");
                if (arg.IsValid())
                {
                    argNode = arg is ArgumentSyntax { Value: { } argVal } ? argVal : (arg is ArgumentSyntax && arg.Children.Count > 0 ? arg.Children[0] : arg);
                }
            }
            else
            {
                argNode = node.GetChildForField(SyntaxFields.Index) ?? node.GetChildForField(SyntaxFields.Subscript);
                if (!argNode.IsValid())
                {
                    argNode = node.Children.LastOrDefault(c => c.IsValid() && c.Text is not "[" and not "]" and not ")" and not "(");
                }
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

        var argsList = node.GetChildForField(SyntaxFields.Arguments) ??
                       node.FindChildOfType("argument_list") ??
                       node.FindChildOfType("arguments");

        var argChildren = argsList.IsValid()
            ? argsList.Children.Where(c => c.IsValid() && c.Text is not "(" and not ")" and not ",").ToList()
            : (node is CallExprSyntax { Arguments.Count: > 0 } ce ? ce.Arguments.Cast<Node>().ToList() : []);

        var firstArg = argChildren.FirstOrDefault();

        if (TryDecodeConstantCall(func, funcText, argChildren, contextOrProject, depth, visitedVars, out result))
        {
            _lastResolvedWasDecoded = true;
            return true;
        }

        // 0-arg calls
        if (argChildren.Count == 0)
        {
            if (func is MemberAccessExprSyntax funcMember)
            {
                var recv = funcMember.Object ?? func.GetChildForField(SyntaxFields.Value) ?? func.Children.FirstOrDefault();
                var mName = !string.IsNullOrEmpty(funcMember.MemberName)
                    ? funcMember.MemberName
                    : (func.GetChildForField(SyntaxFields.Field)?.Text ?? "");

                if (mName is "to_string" or "toString" or "to_owned" or "clone" or "as_str" or "into" or "as_ref" or "trim")
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

            if (func is MemberAccessExprSyntax funcMember)
            {
                var mName = !string.IsNullOrEmpty(funcMember.MemberName)
                    ? funcMember.MemberName
                    : (func.GetChildForField(SyntaxFields.Field)?.Text ?? "");
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

        // Format functions (e.g. fmt.Sprintf)
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

        if (node is MemberAccessExprSyntax or IdentifierExprSyntax)
        {
            var propName = node is IdentifierExprSyntax
                ? node.Text
                : ((node as MemberAccessExprSyntax)?.MemberName ??
                   node.GetChildForField(SyntaxFields.Property)?.Text ??
                   node.GetChildForField(SyntaxFields.Name)?.Text ?? "");

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

    private static bool TryDecodeConstantCall(
        Node? func,
        string funcText,
        List<Node> argChildren,
        string? contextOrProject,
        int depth,
        HashSet<string>? visitedVars,
        out string result)
    {
        result = string.Empty;
        if (argChildren.Count == 0) return false;

        // 1. `.toString(...)` on a resolved Buffer/string receiver (`Buffer.from(..., 'base64').toString('utf8')`)
        if (func is MemberAccessExprSyntax funcMem)
        {
            var mName = !string.IsNullOrEmpty(funcMem.MemberName)
                ? funcMem.MemberName
                : (func.GetChildForField(SyntaxFields.Property) ?? func.GetChildForField(SyntaxFields.Field))?.Text ?? "";
            if (mName == "toString")
            {
                var recv = funcMem.Object ?? func.GetChildForField(SyntaxFields.Object) ?? func.Children.FirstOrDefault();
                if (recv.IsValid() && TryResolveStringInternal(recv, contextOrProject, depth + 1, visitedVars, out result))
                {
                    return true;
                }
            }
        }

        // 2. `atob(b64)`
        if ((funcText == "atob" || funcText.EndsWith(".atob", StringComparison.Ordinal)) && argChildren.Count >= 1)
        {
            if (TryResolveStringInternal(argChildren[0], contextOrProject, depth + 1, visitedVars, out var b64) &&
                TryDecodeBase64Utf8(b64, out result))
            {
                return true;
            }
        }

        // 3. `Buffer.from(payload, 'base64' | 'hex')`
        if (funcText is "Buffer.from" or "Buffer.alloc" && argChildren.Count >= 1)
        {
            if (TryResolveStringInternal(argChildren[0], contextOrProject, depth + 1, visitedVars, out var payload))
            {
                var encoding = "utf8";
                if (argChildren.Count >= 2 && TryResolveStringInternal(argChildren[1], contextOrProject, depth + 1, visitedVars, out var encArg))
                {
                    encoding = encArg.Trim().ToLowerInvariant();
                }

                if (encoding is "base64" or "base64url" && TryDecodeBase64Utf8(payload, out result))
                {
                    return true;
                }
                if (encoding == "hex" && TryDecodeHexUtf8(payload, out result))
                {
                    return true;
                }
            }
        }

        // 4. `String.fromCharCode(c1, c2, ...)`
        if (funcText == "String.fromCharCode" && argChildren.Count >= 1)
        {
            var sb = new System.Text.StringBuilder(argChildren.Count);
            foreach (var arg in argChildren)
            {
                var raw = arg is ArgumentSyntax { Value: { } v } ? v.Text.Trim() : arg.Text.Trim();
                if (int.TryParse(raw, out var code) && code is >= 0 and <= 0xFFFF)
                {
                    sb.Append((char)code);
                }
                else
                {
                    return false;
                }
            }
            if (sb.Length > 0)
            {
                result = sb.ToString();
                return true;
            }
        }

        return false;
    }

    private static bool TryDecodeBase64Utf8(string input, out string decoded)
    {
        decoded = string.Empty;
        var clean = input.Trim();
        if (clean.Length < 4) return false;
        try
        {
            var bytes = Convert.FromBase64String(clean);
            if (bytes.Length == 0) return false;
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            if (str.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            {
                return false;
            }
            decoded = str;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeHexUtf8(string input, out string decoded)
    {
        decoded = string.Empty;
        var clean = input.Trim();
        if (clean.Length < 4 || clean.Length % 2 != 0) return false;
        try
        {
            var bytes = Convert.FromHexString(clean);
            if (bytes.Length == 0) return false;
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            if (str.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            {
                return false;
            }
            decoded = str;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsObfuscatedHexOrUnicodeEscapes(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.IndexOf('\\') < 0) return false;
        var count = 0;
        for (var i = 0; i < raw.Length - 3; i++)
        {
            if (raw[i] == '\\' && (raw[i + 1] == 'x' || raw[i + 1] == 'u'))
            {
                count++;
                if (count >= 2) return true;
            }
        }
        return false;
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
