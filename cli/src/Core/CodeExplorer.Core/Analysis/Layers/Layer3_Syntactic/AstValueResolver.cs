using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Universal AST value evaluator and expression resolver operating on Universal Syntax nodes.
/// Replaces heuristic regex searches with AST-driven resolution of literals, binary concatenations,
/// template strings, configuration readers, and local reaching definitions.
/// </summary>
public static partial class AstValueResolver
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
                                       node.Children.Any(c => c is ParenthesizedExprSyntax { IsInterpolation: true });

        if (!hasInterpolationChildren &&
            (node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.Char or LiteralSyntaxKind.RawString } ||
             (node.Children.Count == 0 && IsQuoted(node.AsSpan()))))
        {
            if (node is LiteralExprSyntax litNode)
            {
                if (litNode.IsDecoded)
                {
                    _lastResolvedWasDecoded = true;
                }
                result = litNode.ValueText;
                return true;
            }
            var rawSpan = node.AsSpan();
            if (ContainsObfuscatedHexOrUnicodeEscapes(rawSpan))
            {
                _lastResolvedWasDecoded = true;
            }
            result = Unquote(rawSpan);
            return true;
        }

        // 2. Numeric and Boolean Literals
        if (node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.Integer or LiteralSyntaxKind.Float or LiteralSyntaxKind.Boolean } numLit)
        {
            result = numLit.ValueText;
            return true;
        }

        // 2b. Argument wrapper: unwrap inner expression
        if (node is ArgumentSyntax argSyntax)
        {
            var exprChild = argSyntax.Value ??
                            node.GetChildForField(SyntaxFields.Expression) ??
                            node.Children.LastOrDefault(c => c.IsValid() && c is ExprSyntax && !ReferenceEquals(c, node.GetChildForField(SyntaxFields.Name)));
            if (exprChild.IsValid() && exprChild.Id != node.Id)
            {
                return TryResolveStringInternal(exprChild, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 2c. Initializer value wrapper: unwrap inner expression
        if (node is SyntaxList { Kind: SyntaxListKind.Initializer, Children.Count: > 0 } initClause)
        {
            var valChild = initClause.Children[^1];
            if (valChild.IsValid() && valChild.Id != node.Id)
            {
                return TryResolveStringInternal(valChild, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 2d. Annotation / Attribute default reference via UAST Arguments
        if (node is AnnotationSyntax ann)
        {
            for (int i = 0; i < ann.Arguments.Count; i++)
            {
                var arg = ann.Arguments[i];
                if (arg.NameSpan is "default" && arg.Value is LiteralExprSyntax { ValueText: { Length: > 0 } fnName })
                {
                    if (ConstantRegistry.TryResolve(contextOrProject, fnName, out result))
                    {
                        return true;
                    }
                    var fnDecl = AstScopeResolver.FindEnclosingFunctionItem(node, fnName);
                    if (fnDecl.IsValid() && TryResolveStringInternal(fnDecl, contextOrProject, depth + 1, visitedVars, out result))
                    {
                        return true;
                    }
                }
            }
        }

        // 2e. Unary / reference / await / try expressions: unwrap inner Operand
        if (node is UnaryExprSyntax unaryNode)
        {
            var inner = unaryNode.Operand ?? node.FirstChildOfType<ExprSyntax>();
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

        // 3. Parenthesized Expression: unwrap inner Expression
        if (node is ParenthesizedExprSyntax { IsInterpolation: false } parenNode)
        {
            var inner = parenNode.Expression ?? parenNode.FirstChildOfType<ExprSyntax>();
            if (inner.IsValid() && inner.Id != node.Id)
            {
                return TryResolveStringInternal(inner, contextOrProject, depth + 1, visitedVars, out result);
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

        // 6. Format macro invocation classified via CallValueSemantics
        if (node is CallExprSyntax { CallKind: CallSyntaxKind.Macro, ValueSemantics: CallValueSemantics.StringFormat })
        {
            return TryResolveFormatMacro(node, contextOrProject, depth, visitedVars, out result);
        }

        // 7. Member Access Expression
        if (node is MemberAccessExprSyntax)
        {
            return TryResolveMember(node, contextOrProject, depth, visitedVars, out result);
        }

        // 8. Index / Slice Access
        if (node is IndexExprSyntax)
        {
            return TryResolveSubscript(node, contextOrProject, depth, visitedVars, out result);
        }

        // 9. Call Expression
        if (node is CallExprSyntax)
        {
            return TryResolveCall(node, contextOrProject, depth, visitedVars, out result);
        }

        // 10. Plain Identifier
        if (node is IdentifierExprSyntax or TypeRefSyntax)
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
            ? fn.Parameters.Count(p => p.PassingMode is not (ParameterPassingMode.Receiver or ParameterPassingMode.MutBorrow))
            : 0;

        if (regularParamsCount == 0 || (regularParamsCount == 1 && _localBindings is { Count: > 0 }))
        {
            if (fn?.ExpressionBody is { } exprBody && exprBody.Id != node.Id)
            {
                return TryResolveStringInternal(exprBody, contextOrProject, depth + 1, visitedVars, out result);
            }

            Node? block = fn?.Body ?? node.FirstChildOfType<BlockStmtSyntax>();
            if (block.IsValid())
            {
                var returnStmt = FindFirstReturnStmt(block);
                Node? retChild = null;
                if (returnStmt is ReturnStmtSyntax retSyntax)
                {
                    retChild = retSyntax.Value ?? retSyntax.FirstChildOfType<ExprSyntax>();
                }
                else if (block.Children.Count > 0)
                {
                    retChild = block.Children[^1];
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
            if (child is ReturnStmtSyntax) return child;
            if (child is FunctionDeclSyntax or LambdaExprSyntax or TypeDeclSyntax) continue;
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
            : node.GetChildForField(SyntaxFields.Operator)?.Text;

        if (op is "||" or "??")
        {
            var left = bin?.Left ?? node.GetChildForField(SyntaxFields.Left);
            var right = bin?.Right ?? node.GetChildForField(SyntaxFields.Right);

            if (!left.IsValid() && node.Children.Count >= 2)
            {
                left = node.Children[0];
                right = node.Children[^1];
            }

            if (TryResolveStringInternal(left, contextOrProject, depth + 1, visitedVars, out var leftVal) &&
                !string.IsNullOrWhiteSpace(leftVal))
            {
                result = leftVal;
                return true;
            }
            if (TryResolveStringInternal(right, contextOrProject, depth + 1, visitedVars, out var rightVal) &&
                !string.IsNullOrWhiteSpace(rightVal))
            {
                result = rightVal;
                return true;
            }
            return false;
        }

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

            if (child is ParenthesizedExprSyntax { IsInterpolation: true } interpChild)
            {
                var expr = interpChild.Expression ?? child.FirstChildOfType<ExprSyntax>();
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
            else if (child is LiteralExprSyntax litPart)
            {
                sb.Append(litPart.ValueText);
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
        var argsList = node.GetChildForField(SyntaxFields.Arguments) ??
                       node.Children.FirstOrDefault(c => c is SyntaxList { Kind: SyntaxListKind.Arguments });
        if (argsList.IsValid())
        {
            var argGroups = AstTemplateResolver.ExtractMacroArguments(argsList);
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
        if ((node.Span.EndByte - node.Span.StartByte) > 256) return false;

        var memberSyntax = node as MemberAccessExprSyntax;

        // 1. Environment property lookup (e.g. process.env.KEY or env.KEY) marked at UAST construction
        if (memberSyntax is { IsEnvironmentLookup: true })
        {
            var envKey = memberSyntax.MemberName;
            if (!string.IsNullOrEmpty(envKey))
            {
                if (ConstantRegistry.TryDeriveParentTopicFromSubscriptionKey(envKey, out var parentTopic))
                {
                    result = parentTopic;
                    return true;
                }
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
        }

        // 2. Canonical member path lookup (with any leading self/this/super/base structurally stripped)
        var canonicalPath = node.GetCanonicalMemberPath();
        if (!string.IsNullOrEmpty(canonicalPath))
        {
            var isPlainField = !canonicalPath.Contains('.') && !canonicalPath.Contains(':');
            if (isPlainField
                    ? ConstantRegistry.TryResolveProjectScoped(contextOrProject, canonicalPath, out var propVal)
                    : ConstantRegistry.TryResolve(contextOrProject, canonicalPath, out propVal))
            {
                result = propVal;
                return true;
            }
        }

        // 3. Enclosing class field initializer when rooted at self/super
        if (node.StartsWithSelfOrSuper() && !string.IsNullOrEmpty(canonicalPath))
        {
            var fieldInit = AstScopeResolver.FindClassFieldInitializer(node, canonicalPath);
            if (fieldInit.IsValid())
            {
                visitedVars ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (visitedVars.Add(canonicalPath))
                {
                    if (TryResolveStringInternal(fieldInit, contextOrProject, depth + 1, visitedVars, out result))
                    {
                        return true;
                    }
                }
            }
        }

        // 4. Receiver type-based resolution via lexical/class scope (e.g. brokerConfig.OrdersTopic -> BrokerConfig.OrdersTopic)
        var propText = !string.IsNullOrEmpty(memberSyntax?.MemberName)
            ? memberSyntax.MemberName
            : (node.GetChildForField(SyntaxFields.Property) ??
               node.GetChildForField(SyntaxFields.Field) ??
               node.GetChildForField(SyntaxFields.Name) ??
               node.Children.LastOrDefault(c => c is IdentifierExprSyntax))?.Text;

        if (!string.IsNullOrEmpty(propText))
        {
            var receiverNode = memberSyntax?.Object ?? (node.Children.Count > 0 ? node.Children[0] : null);
            var receiverType = AstScopeResolver.TryResolveExpressionTypeName(receiverNode);
            if (!string.IsNullOrEmpty(receiverType))
            {
                if (ConstantRegistry.TryResolve(contextOrProject, $"{receiverType}.{propText}", out var typedVal) ||
                    ConstantRegistry.TryResolve(contextOrProject, $"{receiverType}::{propText}", out typedVal))
                {
                    result = typedVal;
                    return true;
                }
            }

            if (ConstantRegistry.TryResolveProjectScoped(contextOrProject, propText, out var projMemberVal) &&
                !WorkspaceConventions.IsPlaceholderName(projMemberVal))
            {
                result = projMemberVal;
                return true;
            }

            if (IsAllCapsConstantIdentifier(propText) &&
                ConstantRegistry.TryResolve(contextOrProject, propText, out var globalConstVal))
            {
                result = globalConstVal;
                return true;
            }
        }

        return false;
    }

    private static bool IsAllCapsConstantIdentifier(string identifier)
    {
        if (identifier.Length < 3) return false;
        bool hasLetter = false;
        for (int i = 0; i < identifier.Length; i++)
        {
            var ch = identifier[i];
            if (ch is >= 'A' and <= 'Z')
            {
                hasLetter = true;
            }
            else if (ch is not ((>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }
        return hasLetter;
    }

    private static bool TryResolveSubscript(Node node, string? contextOrProject, int depth, HashSet<string>? visitedVars, out string result)
    {
        result = string.Empty;
        var idxSyntax = node as IndexExprSyntax;
        Node? argNode = idxSyntax?.Index ??
                        node.GetChildForField(SyntaxFields.Index) ??
                        node.GetChildForField(SyntaxFields.Subscript);

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
        var callExpr = node as CallExprSyntax;
        var func = callExpr?.Callee ?? node.GetFunctionNode();
        if (func.IsValid() && (func.Span.EndByte - func.Span.StartByte) > 256)
        {
            return false;
        }
        var funcText = func.IsValid() ? func.Text : string.Empty;

        IReadOnlyList<Node> argChildren = callExpr is { Arguments.Count: > 0 }
            ? callExpr.Arguments
            : [];

        var firstArg = argChildren.Count > 0 ? argChildren[0] : null;
        var semantics = callExpr?.ValueSemantics ?? CallValueSemantics.None;

        // 1. Byte / char / Base64 decoding
        if (semantics == CallValueSemantics.ByteOrBase64Decode &&
            TryDecodeConstantCall(func, funcText, argChildren, contextOrProject, depth, visitedVars, out result))
        {
            _lastResolvedWasDecoded = true;
            return true;
        }

        // 2. Identity or string conversion (e.g. x.to_string(), x.as_str(), String(x))
        if (semantics == CallValueSemantics.IdentityConversion)
        {
            if (argChildren.Count == 0 && callExpr?.Receiver is { } recv && recv is not CallExprSyntax)
            {
                return TryResolveStringInternal(recv, contextOrProject, depth + 1, visitedVars, out result);
            }
            if (argChildren.Count == 1 && firstArg.IsValid())
            {
                return TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 3. String formatting (e.g. fmt.Sprintf, string.Format)
        if (semantics == CallValueSemantics.StringFormat && argChildren.Count >= 1)
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

        // 4. Environment or configuration key lookup (1-arg or 2-arg with fallback default)
        if (semantics == CallValueSemantics.EnvironmentOrConfigLookup && firstArg.IsValid())
        {
            if (TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out var cfgKey))
            {
                if (ConstantRegistry.TryDeriveParentTopicFromSubscriptionKey(cfgKey, out var parentTopic))
                {
                    result = parentTopic;
                    return true;
                }
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
                if (argChildren.Count >= 2 &&
                    TryResolveStringInternal(argChildren[1], contextOrProject, depth + 1, visitedVars, out var defVal) &&
                    !string.IsNullOrWhiteSpace(defVal))
                {
                    result = defVal;
                    return true;
                }
            }
        }

        // 5. User-defined function / method resolution via UAST scope & ConstantRegistry
        var mName = callExpr?.MethodName;
        if (string.IsNullOrEmpty(mName) && func is MemberAccessExprSyntax funcMember)
        {
            mName = !string.IsNullOrEmpty(funcMember.MemberName)
                ? funcMember.MemberName
                : func.GetChildForField(SyntaxFields.Field)?.Text;
        }
        if (string.IsNullOrEmpty(mName))
        {
            mName = funcText;
        }

        if (argChildren.Count == 0 && !string.IsNullOrEmpty(mName))
        {
            var canonicalCallTarget = func.IsValid() ? func.GetCanonicalMemberPath() : mName;
            if (ConstantRegistry.TryResolve(contextOrProject, canonicalCallTarget, out var fnVal) ||
                ConstantRegistry.TryResolve(contextOrProject, mName, out fnVal))
            {
                result = fnVal;
                return true;
            }

            var targetZeroArgFn = AstScopeResolver.FindEnclosingFunctionItem(node, mName);
            if (targetZeroArgFn.IsValid() && TryResolveStringInternal(targetZeroArgFn, contextOrProject, depth + 1, visitedVars, out result))
            {
                return true;
            }
        }

        if (argChildren.Count == 1 && firstArg.IsValid() && !string.IsNullOrEmpty(mName))
        {
            var targetFn = AstScopeResolver.FindEnclosingFunctionItem(node, mName);
            if (targetFn.IsValid())
            {
                var paramIdent = AstScopeResolver.FindFirstParameterName(targetFn);
                string? boundVal = null;
                if (TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out var resolvedArg) &&
                    !string.IsNullOrEmpty(resolvedArg))
                {
                    boundVal = resolvedArg;
                }
                else if (firstArg is IdentifierExprSyntax or UnaryExprSyntax &&
                         ConstantRegistry.TryResolve(contextOrProject, firstArg.Text.TrimStart('&'), out var regVal) &&
                         !string.IsNullOrEmpty(regVal))
                {
                    boundVal = regVal;
                }

                if (!string.IsNullOrEmpty(boundVal) && !string.IsNullOrEmpty(paramIdent))
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

        if (argChildren.Count == 2 &&
            TryResolveStringInternal(argChildren[0], contextOrProject, depth + 1, visitedVars, out var keyCandidate) &&
            IsAllCapsConstantIdentifier(keyCandidate))
        {
            if (ConstantRegistry.TryResolve(contextOrProject, keyCandidate, out var regVal))
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

}
