using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

internal static class DeclarativeCallChainAstHelper
{
    private const int MaxParentBlockWalk = 16;
    private const string ClassSuffix = ".class";
    private const string DoubleColonClassSuffix = "::class";
    private const string PropertyElementType = "property_element";
    private const string ClassBodyType = "class_body";
    private const string StatementBlockType = "statement_block";
    private const string CompoundStatementType = "compound_statement";
    private const string ShortVarDeclType = "short_var_declaration";
    private const string AssignmentExprType = "assignment_expression";
    private const string LexicalDeclType = "lexical_declaration";
    private const string VariableDeclaratorType = "variable_declarator";
    private const string LocalVarDeclType = "local_variable_declaration";
    private const string LetDeclType = "let_declaration";
    private const string GetVerb = "get";
    private const string PostVerb = "post";
    private const string PutVerb = "put";
    private const string DeleteVerb = "delete";
    private const string PatchVerb = "patch";
    private const string GetUpper = "GET";
    private const string PostUpper = "POST";
    private const string PutUpper = "PUT";
    private const string DeleteUpper = "DELETE";
    private const string PatchUpper = "PATCH";
    private const char DotChar = '.';
    private const char OpenAngleChar = '<';
    private const char CloseAngleChar = '>';
    private const char OpenParenChar = '(';
    private const char CommaChar = ',';
    private const char SpaceChar = ' ';
    private const char DollarChar = '$';
    private static readonly char[] QuoteChars = ['"', '\'', '`'];

    internal static bool HasTargetArgument(Node callNode)
    {
        if (!ExtractFirstStringLiteralSpan(callNode).IsEmpty) return true;
        return !string.IsNullOrEmpty(TryExtractGenericTypeFromCall(callNode)) ||
               !string.IsNullOrEmpty(TryExtractClassLiteralArgument(callNode));
    }

    internal static ReadOnlySpan<char> ExtractFirstStringLiteralSpan(Node callNode)
    {
        if (callNode is CallExprSyntax callExpr)
        {
            for (var i = 0; i < callExpr.Arguments.Count; i++)
            {
                if (callExpr.Arguments[i].Value is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString } lit)
                {
                    return lit.AsSpan().Trim(QuoteChars);
                }
            }
            return ReadOnlySpan<char>.Empty;
        }

        var argList = DeclarativeSemanticExtension.FindArgumentsNode(callNode);
        if (!argList.IsValid()) return ReadOnlySpan<char>.Empty;

        for (var i = 0; i < argList.Children.Count; i++)
        {
            var child = argList.Children[i];
            if (DeclarativeSemanticExtension.IsStringLiteral(child))
            {
                return child.AsSpan().Trim(QuoteChars);
            }
            if (child is ArgumentSyntax { Value: { } v } && DeclarativeSemanticExtension.IsStringLiteral(v))
            {
                return v.AsSpan().Trim(QuoteChars);
            }
        }

        return ReadOnlySpan<char>.Empty;
    }

    internal static string? ExtractStepTargetArgument(Node? callNode)
    {
        if (callNode == null || !callNode.IsValid()) return null;
        var strArg = DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(callNode, 0);
        if (!string.IsNullOrEmpty(strArg))
        {
            return strArg;
        }

        var genericArg = TryExtractGenericTypeFromCall(callNode);
        if (!string.IsNullOrEmpty(genericArg))
        {
            return genericArg;
        }

        var classLit = TryExtractClassLiteralArgument(callNode);
        if (!string.IsNullOrEmpty(classLit))
        {
            return classLit;
        }

        return null;
    }

    internal static string? TryExtractGenericTypeFromCall(Node callNode)
    {
        ReadOnlySpan<char> span;
        if (callNode is CallExprSyntax { Callee: { } callee })
        {
            span = callee.AsSpan();
        }
        else
        {
            var funcNode = callNode.GetField(SyntaxFields.Function)
                           ?? callNode.GetField(SyntaxFields.Name)
                           ?? (callNode.Children.Count > 0 ? callNode.Children[0] : null);
            span = funcNode != null ? funcNode.AsSpan() : callNode.AsSpan();
        }
        var openIdx = span.IndexOf(OpenAngleChar);
        if (openIdx <= 0) return null;

        var parenIdx = span.IndexOf(OpenParenChar);
        if (parenIdx > 0 && openIdx > parenIdx) return null;

        var closeIdx = span.LastIndexOf(CloseAngleChar);
        if (closeIdx <= openIdx + 1) return null;

        var inner = span[(openIdx + 1)..closeIdx].Trim();
        var commaIdx = inner.IndexOf(CommaChar);
        if (commaIdx > 0) inner = inner[..commaIdx].Trim();

        if (inner.IsEmpty || inner.Contains(SpaceChar) || inner.Contains(OpenAngleChar) || inner.Contains(OpenParenChar))
        {
            return null;
        }

        var dotIdx = inner.LastIndexOf(DotChar);
        if (dotIdx >= 0) inner = inner[(dotIdx + 1)..];
        return inner.IsEmpty ? null : SyntaxStringPool.Intern(inner);
    }

    internal static string? TryExtractClassLiteralArgument(Node callNode)
    {
        if (callNode is CallExprSyntax { Arguments.Count: > 0 } callExpr)
        {
            for (var i = 0; i < callExpr.Arguments.Count; i++)
            {
                var argVal = callExpr.Arguments[i].Value ?? (Node)callExpr.Arguments[i];
                var extracted = ExtractClassLiteralFromSpan(argVal.AsSpan().Trim());
                if (extracted != null) return extracted;
            }
            return null;
        }

        var argList = DeclarativeSemanticExtension.FindArgumentsNode(callNode);
        if (argList == null) return null;

        for (var i = 0; i < argList.Children.Count; i++)
        {
            var extracted = ExtractClassLiteralFromSpan(argList.Children[i].AsSpan().Trim());
            if (extracted != null) return extracted;
        }
        return null;
    }

    private static string? ExtractClassLiteralFromSpan(ReadOnlySpan<char> span)
    {
        if (span.EndsWith(ClassSuffix.AsSpan(), StringComparison.Ordinal))
        {
            var typeSpan = span[..^ClassSuffix.Length].Trim();
            var dot = typeSpan.LastIndexOf(DotChar);
            if (dot >= 0) typeSpan = typeSpan[(dot + 1)..];
            if (!typeSpan.IsEmpty) return SyntaxStringPool.Intern(typeSpan);
        }
        else if (span.EndsWith(DoubleColonClassSuffix.AsSpan(), StringComparison.Ordinal))
        {
            var typeSpan = span[..^DoubleColonClassSuffix.Length].Trim();
            var dot = typeSpan.LastIndexOf(DotChar);
            if (dot >= 0) typeSpan = typeSpan[(dot + 1)..];
            if (!typeSpan.IsEmpty) return SyntaxStringPool.Intern(typeSpan);
        }
        return null;
    }

    internal static Node? GetReceiverNode(Node? callNode)
    {
        if (callNode == null) return null;

        if (callNode is CallExprSyntax callExpr)
        {
            if (callExpr.Receiver != null) return callExpr.Receiver;
            if (callExpr.Callee is MemberAccessExprSyntax { Object: { } memObj }) return memObj;
        }

        var objNode = callNode.GetField(SyntaxFields.Object)
                      ?? callNode.GetField(SyntaxFields.Operand);
        if (objNode != null) return objNode;

        var funcNode = callNode.GetField(SyntaxFields.Function)
                       ?? (callNode.Children.Count > 0 ? callNode.Children[0] : null);
        if (funcNode != null)
        {
            if (funcNode is MemberAccessExprSyntax { Object: { } fnObj }) return fnObj;

            var innerObj = funcNode.GetField(SyntaxFields.Object)
                           ?? funcNode.GetField(SyntaxFields.Expression)
                           ?? funcNode.GetField(SyntaxFields.Operand);
            if (innerObj != null) return innerObj;

            if (funcNode.Children.Count > 0 && funcNode.Children[0] != callNode)
            {
                var firstChild = funcNode.Children[0];
                if (IsCallLikeNode(firstChild) || firstChild is IdentifierExprSyntax)
                {
                    return firstChild;
                }
            }
        }

        return null;
    }

    internal static Node? UnwrapTransparentReceiver(Node? node)
    {
        var curr = node;
        var guard = 0;
        while (curr != null && guard++ < 4)
        {
            if (curr is ParenthesizedExprSyntax { Expression: { } innerExpr })
            {
                curr = innerExpr;
                continue;
            }
            if (curr is UnaryExprSyntax { IsAwait: true, Operand: { } awaitOp })
            {
                curr = awaitOp;
                continue;
            }
            if (curr is UnaryExprSyntax { IsTryPropagate: true, Operand: { } tryOp })
            {
                curr = tryOp;
                continue;
            }
            break;
        }
        return curr;
    }

    internal static bool IsCallLikeNode(Node? node) =>
        node is CallExprSyntax || (node != null && node.SyntaxKind == SyntaxKind.CallExpression);

    internal static Node? TryFindVariableInitializerCallNode(Node contextNode, string varName) =>
        TryFindVariableInitializerCallNodeSpan(contextNode, varName.AsSpan());

    internal static Node? TryFindVariableInitializerCallNodeSpan(Node contextNode, ReadOnlySpan<char> varName)
    {
        var doc = contextNode.Document;
        if (doc is { AllVariables.Count: > 0 })
        {
            var beforeRow = contextNode.Span.StartLine;
            var allVars = doc.AllVariables;
            for (var i = allVars.Count - 1; i >= 0; i--)
            {
                var v = allVars[i];
                if (v.Span.StartLine > beforeRow) continue;
                var vName = v.NameSpan.TrimStart(DollarChar);
                if (vName.IsEmpty && v.Children.Count > 0)
                {
                    vName = v.Children[0].AsSpan().Trim().TrimStart(DollarChar);
                }
                if (vName.Equals(varName, StringComparison.Ordinal))
                {
                    var init = UnwrapTransparentReceiver(v.Initializer ?? (v.Children.Count >= 2 ? v.Children[^1] : null));
                    if (IsCallLikeNode(init)) return init;
                    if (init is { Children.Count: > 0 })
                    {
                        var firstChild = UnwrapTransparentReceiver(init.Children[0]);
                        if (IsCallLikeNode(firstChild)) return firstChild;
                    }
                }
            }
        }

        var curr = contextNode.Parent;
        var depth = 0;
        while (curr != null && depth++ < MaxParentBlockWalk)
        {
            if (curr is BlockStmtSyntax or FunctionDeclSyntax or TypeDeclSyntax or SyntaxList { Kind: SyntaxListKind.Declarations })
            {
                var found = SearchBlockForVariableInitializerCallSpan(curr, varName, contextNode.Span.StartLine);
                if (found != null) return found;
            }
            curr = curr.Parent;
        }
        return null;
    }

    internal static Node? SearchBlockForVariableInitializerCallSpan(Node blockNode, ReadOnlySpan<char> varName, int beforeRow)
    {
        for (var i = 0; i < blockNode.Children.Count; i++)
        {
            var stmt = blockNode.Children[i];
            if (stmt.Span.StartLine > beforeRow) continue;

            if (TryExtractAssignedCallFromStatement(stmt, varName, out var callInit) && callInit != null)
            {
                return callInit;
            }

            for (var j = 0; j < stmt.Children.Count; j++)
            {
                var inner = stmt.Children[j];
                if (TryExtractAssignedCallFromStatement(inner, varName, out callInit) && callInit != null)
                {
                    return callInit;
                }
            }
        }
        return null;
    }

    private static bool TryExtractAssignedCallFromStatement(Node node, ReadOnlySpan<char> varName, out Node? callNode)
    {
        callNode = null;
        if (node is VariableDeclSyntax varDecl)
        {
            var vName = varDecl.NameSpan.TrimStart(DollarChar);
            if (vName.IsEmpty && varDecl.Children.Count > 0)
            {
                vName = varDecl.Children[0].AsSpan().Trim().TrimStart(DollarChar);
            }
            if (!vName.IsEmpty && vName.Equals(varName, StringComparison.Ordinal))
            {
                var valNode = UnwrapTransparentReceiver(varDecl.Initializer ?? (varDecl.Children.Count >= 2 ? varDecl.Children[^1] : null));
                if (IsCallLikeNode(valNode))
                {
                    callNode = valNode;
                    return true;
                }
                if (valNode is { Children.Count: > 0 })
                {
                    var firstVal = UnwrapTransparentReceiver(valNode.Children[0]);
                    if (IsCallLikeNode(firstVal))
                    {
                        callNode = firstVal;
                        return true;
                    }
                }
            }
            return false;
        }

        if (node is AssignmentExprSyntax assignExpr)
        {
            var left = assignExpr.Left ?? (assignExpr.Children.Count > 0 ? assignExpr.Children[0] : null);
            if (left != null && MatchesVariableName(left, varName))
            {
                var valNode = UnwrapTransparentReceiver(assignExpr.Right ?? (assignExpr.Children.Count >= 2 ? assignExpr.Children[^1] : null));
                if (IsCallLikeNode(valNode))
                {
                    callNode = valNode;
                    return true;
                }
                if (valNode is { Children.Count: > 0 })
                {
                    var firstVal = UnwrapTransparentReceiver(valNode.Children[0]);
                    if (IsCallLikeNode(firstVal))
                    {
                        callNode = firstVal;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool MatchesVariableName(Node nameNode, ReadOnlySpan<char> varName)
    {
        var text = nameNode.AsSpan().Trim().TrimStart(DollarChar);
        return text.Equals(varName, StringComparison.Ordinal);
    }

    internal static string? NormalizeHttpVerbSpan(ReadOnlySpan<char> methodSpan)
    {
        if (methodSpan.IsEmpty) return null;
        if (methodSpan.StartsWith(GetVerb.AsSpan(), StringComparison.OrdinalIgnoreCase) || methodSpan.EndsWith(GetVerb.AsSpan(), StringComparison.OrdinalIgnoreCase)) return GetUpper;
        if (methodSpan.StartsWith(PostVerb.AsSpan(), StringComparison.OrdinalIgnoreCase) || methodSpan.EndsWith(PostVerb.AsSpan(), StringComparison.OrdinalIgnoreCase)) return PostUpper;
        if (methodSpan.StartsWith(PutVerb.AsSpan(), StringComparison.OrdinalIgnoreCase) || methodSpan.EndsWith(PutVerb.AsSpan(), StringComparison.OrdinalIgnoreCase)) return PutUpper;
        if (methodSpan.StartsWith(DeleteVerb.AsSpan(), StringComparison.OrdinalIgnoreCase) || methodSpan.EndsWith(DeleteVerb.AsSpan(), StringComparison.OrdinalIgnoreCase)) return DeleteUpper;
        if (methodSpan.StartsWith(PatchVerb.AsSpan(), StringComparison.OrdinalIgnoreCase) || methodSpan.EndsWith(PatchVerb.AsSpan(), StringComparison.OrdinalIgnoreCase)) return PatchUpper;
        return null;
    }
}
