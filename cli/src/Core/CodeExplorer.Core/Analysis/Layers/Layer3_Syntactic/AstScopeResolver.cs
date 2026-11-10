using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Universal lexical scope, variable declaration, and identifier reaching definition resolver for Universal Syntax nodes.
/// </summary>
public static class AstScopeResolver
{
    public static Node? FindVariableDeclarationInScope(Node node, string varName)
    {
        var targetRow = node.StartPosition.Row;
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (IsScopeBoundary(curr))
            {
                foreach (var child in curr.Children)
                {
                    if (child.StartPosition.Row > targetRow) break;

                    var resolved = TryExtractInitializerFromNode(child, varName);
                    if (resolved.IsValid())
                    {
                        return resolved;
                    }
                }
            }

            curr = curr.Parent;
        }

        return null;
    }

    private static Node? TryExtractInitializerFromNode(Node node, string varName)
    {
        if (!node.IsValid()) return null;

        if (node is VariableDeclSyntax varDecl)
        {
            // Check direct match on VariableDeclSyntax
            var nameMatch = varDecl.Name == varName;
            if (!nameMatch)
            {
                var nameNode = varDecl.GetField(SyntaxFields.Name) ??
                               varDecl.GetField(SyntaxFields.Pattern) ??
                               varDecl.GetField(SyntaxFields.Left) ??
                               varDecl.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                if (nameNode.IsValid() && nameNode.Text == varName)
                {
                    nameMatch = true;
                }
            }

            if (nameMatch)
            {
                if (varDecl.Initializer.IsValid())
                {
                    return UnwrapInitializerClause(varDecl.Initializer);
                }

                var valField = varDecl.GetField(SyntaxFields.Value) ?? varDecl.GetField(SyntaxFields.Right);
                if (valField.IsValid())
                {
                    return UnwrapInitializerClause(valField);
                }

                var eqIdx = -1;
                for (int i = 0; i < varDecl.Children.Count; i++)
                {
                    if (varDecl.Children[i].Type == "=" || varDecl.Children[i].Text == "=")
                    {
                        eqIdx = i;
                        break;
                    }
                    if (varDecl.Children[i].Children.Count > 1 && varDecl.Children[i].Children[0].Text == "=")
                    {
                        return UnwrapInitializerClause(varDecl.Children[i]);
                    }
                }
                if (eqIdx >= 0 && eqIdx + 1 < varDecl.Children.Count)
                {
                    return UnwrapInitializerClause(varDecl.Children[eqIdx + 1]);
                }
            }

            // Recurse into nested VariableDeclSyntax children (e.g. statement -> declaration -> declarator)
            foreach (var sub in node.Children)
            {
                if (sub is VariableDeclSyntax)
                {
                    var nested = TryExtractInitializerFromNode(sub, varName);
                    if (nested.IsValid()) return nested;
                }
            }
        }

        if (node is AssignmentExprSyntax assign)
        {
            var left = assign.Left ?? assign.GetField(SyntaxFields.Left);
            var right = assign.Right ?? assign.GetField(SyntaxFields.Right);
            if (left.IsValid() && left.Text == varName && right.IsValid())
            {
                return right;
            }
        }

        if (node is ControlFlowStmtSyntax { Kind: ControlFlowKind.Expression } exprStmt &&
            exprStmt.FirstChildOfType<AssignmentExprSyntax>() is { } exprAssign)
        {
            var left = exprAssign.Left ?? exprAssign.GetField(SyntaxFields.Left);
            var right = exprAssign.Right ?? exprAssign.GetField(SyntaxFields.Right);
            if (left.IsValid() && left.Text == varName && right.IsValid())
            {
                return right;
            }
        }

        return null;
    }

    private static Node? UnwrapInitializerClause(Node? valNode)
    {
        if (!valNode.IsValid()) return null;
        if (valNode is ExprSyntax) return valNode;
        var inner = valNode.GetField(SyntaxFields.Value) ??
                    valNode.Children.LastOrDefault(c => c.IsValid() && c.Type != "=" && c.Text != "=");
        return inner.IsValid() ? inner : valNode;
    }

    public static Node? FindClassFieldInitializer(Node node, string fieldName)
    {
        var cleanField = fieldName.TrimStart('_');
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (curr is TypeDeclSyntax)
            {
                var found = SearchMemberInitializer(curr, fieldName, cleanField)
                            ?? SearchMemberAssignmentOrGetter(curr, fieldName, cleanField);
                if (found.IsValid()) return found;
            }

            curr = curr.Parent;
        }

        return null;
    }

    private static Node? SearchMemberAssignmentOrGetter(Node container, string fieldName, string cleanField)
    {
        foreach (var child in container.Children)
        {
            if (!child.IsValid()) continue;

            if (child is AssignmentExprSyntax { Left: MemberAccessExprSyntax mem, Right: { } right } &&
                mem.Object is IdentifierExprSyntax { IsSelfOrSuper: true } &&
                (mem.MemberName == fieldName || mem.MemberName.TrimStart('_') == cleanField))
            {
                return right;
            }

            if (child is FunctionDeclSyntax fn)
            {
                var fnName = !string.IsNullOrEmpty(fn.Name)
                    ? fn.Name
                    : (fn.GetField(SyntaxFields.Name) ?? fn.GetField(SyntaxFields.Property))?.Text;

                if (!string.IsNullOrEmpty(fnName) &&
                    (fnName == fieldName || fnName.TrimStart('_') == cleanField) &&
                    fn.Parameters.Count == 0)
                {
                    if (fn.ExpressionBody != null) return fn.ExpressionBody;
                    if (fn.Body != null)
                    {
                        foreach (var stmt in fn.Body.Children)
                        {
                            if (stmt is ReturnStmtSyntax { Value: { } retVal })
                            {
                                return retVal;
                            }
                        }
                    }
                }
            }

            if (child is not TypeDeclSyntax)
            {
                var nested = SearchMemberAssignmentOrGetter(child, fieldName, cleanField);
                if (nested.IsValid()) return nested;
            }
        }

        return null;
    }

    private static Node? SearchMemberInitializer(Node container, string fieldName, string cleanField)
    {
        foreach (var child in container.Children)
        {
            if (!child.IsValid()) continue;

            if (child is MemberDeclSyntax or VariableDeclSyntax)
            {
                var decl = (DeclarationSyntax)child;
                var nameText = !string.IsNullOrEmpty(decl.Name)
                    ? decl.Name
                    : (child.GetField(SyntaxFields.Property) ?? child.GetField(SyntaxFields.Name))?.Text;

                if (!string.IsNullOrEmpty(nameText) && (nameText == fieldName || nameText.TrimStart('_') == cleanField))
                {
                    if (child is MemberDeclSyntax { Initializer: { } memInit })
                    {
                        return UnwrapInitializerClause(memInit);
                    }
                    if (child is VariableDeclSyntax { Initializer: { } varInit })
                    {
                        return UnwrapInitializerClause(varInit);
                    }
                    var valField = child.GetField(SyntaxFields.Value);
                    if (valField.IsValid()) return UnwrapInitializerClause(valField);

                    foreach (var sub in child.Children)
                    {
                        if (sub.Children.Count > 1 && (sub.Children[0].Text == "=" || sub.Children[0].Text == "=>"))
                        {
                            return sub.Children[1];
                        }
                    }
                }

                // Check nested declarators inside a field declaration
                foreach (var sub in child.Children)
                {
                    if (sub is VariableDeclSyntax or MemberDeclSyntax)
                    {
                        var nested = SearchMemberInitializer(child, fieldName, cleanField);
                        if (nested.IsValid()) return nested;
                        break;
                    }
                }
            }
            else if (child is not TypeDeclSyntax and not FunctionDeclSyntax)
            {
                var nested = SearchMemberInitializer(child, fieldName, cleanField);
                if (nested.IsValid()) return nested;
            }
        }

        return null;
    }

    public static bool IsScopeBoundary(Node node) =>
        node.IsScopeBoundary;

    public static string? TryResolveExpressionTypeName(Node? expr)
    {
        if (!expr.IsValid()) return null;

        var canonicalName = expr.GetCanonicalMemberPath();
        if (string.IsNullOrEmpty(canonicalName)) return null;

        // If chained (e.g. options.Value), resolve the root identifier first
        var rootIdx = canonicalName.IndexOf('.');
        var targetName = rootIdx > 0 ? canonicalName[..rootIdx] : canonicalName;
        var cleanField = targetName.TrimStart('_');

        var curr = expr.Parent;
        while (curr.IsValid())
        {
            if (curr is FunctionDeclSyntax fn)
            {
                for (int i = 0; i < fn.Parameters.Count; i++)
                {
                    var p = fn.Parameters[i];
                    if (p.Name == targetName || p.Name.TrimStart('_') == cleanField)
                    {
                        var unwrapped = UnwrapDeclaredTypeName(p.ParameterType);
                        if (!string.IsNullOrEmpty(unwrapped)) return unwrapped;
                    }
                }
            }

            if (curr.IsScopeBoundary)
            {
                for (int i = 0; i < curr.Children.Count; i++)
                {
                    var child = curr.Children[i];
                    if (child is VariableDeclSyntax varDecl && (varDecl.Name == targetName || varDecl.Name.TrimStart('_') == cleanField))
                    {
                        var unwrapped = UnwrapDeclaredTypeName(varDecl.VariableType);
                        if (!string.IsNullOrEmpty(unwrapped)) return unwrapped;
                    }
                }
            }

            if (curr is TypeDeclSyntax typeDecl)
            {
                var foundFieldType = SearchMemberDeclaredType(typeDecl, targetName, cleanField);
                if (!string.IsNullOrEmpty(foundFieldType)) return foundFieldType;
            }

            curr = curr.Parent;
        }

        return null;
    }

    private static string? SearchMemberDeclaredType(Node container, string fieldName, string cleanField)
    {
        for (int i = 0; i < container.Children.Count; i++)
        {
            var child = container.Children[i];
            if (child is MemberDeclSyntax memDecl)
            {
                if (memDecl.Name == fieldName || memDecl.Name.TrimStart('_') == cleanField)
                {
                    var unwrapped = UnwrapDeclaredTypeName(memDecl.MemberType);
                    if (!string.IsNullOrEmpty(unwrapped)) return unwrapped;
                }
            }
            else if (child is FunctionDeclSyntax { Kind: FunctionSyntaxKind.Constructor } ctor)
            {
                for (int j = 0; j < ctor.Parameters.Count; j++)
                {
                    var p = ctor.Parameters[j];
                    if (p.Name == fieldName || p.Name.TrimStart('_') == cleanField)
                    {
                        var unwrapped = UnwrapDeclaredTypeName(p.ParameterType);
                        if (!string.IsNullOrEmpty(unwrapped)) return unwrapped;
                    }
                }
            }
            else if (child is not TypeDeclSyntax and not FunctionDeclSyntax)
            {
                var nested = SearchMemberDeclaredType(child, fieldName, cleanField);
                if (!string.IsNullOrEmpty(nested)) return nested;
            }
        }
        return null;
    }

    private static string? UnwrapDeclaredTypeName(TypeRefSyntax? typeRef)
    {
        if (typeRef == null) return null;
        var raw = typeRef.Name.Trim().TrimStart('&').TrimEnd('?');
        if (raw.StartsWith("mut ", StringComparison.Ordinal))
        {
            raw = raw[4..].Trim();
        }
        var openAngle = raw.IndexOf('<');
        var closeAngle = raw.LastIndexOf('>');
        if (openAngle > 0 && closeAngle > openAngle + 1)
        {
            var inner = raw[(openAngle + 1)..closeAngle].Trim();
            if (!inner.Contains(','))
            {
                raw = inner;
            }
        }
        var lastSep = Math.Max(raw.LastIndexOf('.'), raw.LastIndexOf(':'));
        if (lastSep >= 0 && lastSep + 1 < raw.Length)
        {
            raw = raw[(lastSep + 1)..];
        }
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    public static Node? FindEnclosingFunctionItem(Node node, string funcName)
    {
        if (string.IsNullOrEmpty(funcName)) return null;

        var doc = node.Document;
        if (doc != null)
        {
            var fn = doc.FindFunctionByName(funcName);
            if (fn == null) return null;

            var curr = node.Parent;
            while (curr.IsValid())
            {
                if (curr is ImplDeclSyntax or TypeDeclSyntax or SyntaxDocument or NamespaceDeclSyntax)
                {
                    if (IsDescendantOf(fn, curr)) return fn;
                }
                curr = curr.Parent;
            }
            return null;
        }

        var current = node.Parent;
        while (current.IsValid())
        {
            if (current is ImplDeclSyntax or TypeDeclSyntax or SyntaxDocument or NamespaceDeclSyntax)
            {
                var fn = FindFunctionRecursive(current, funcName);
                if (fn.IsValid()) return fn;
            }
            current = current.Parent;
        }
        return null;
    }

    private static bool IsDescendantOf(Node child, Node ancestor)
    {
        var curr = child.Parent;
        while (curr.IsValid())
        {
            if (ReferenceEquals(curr, ancestor)) return true;
            curr = curr.Parent;
        }
        return false;
    }

    private static Node? FindFunctionRecursive(Node container, string funcName)
    {
        foreach (var child in container.Children)
        {
            if (!child.IsValid()) continue;
            if (child is FunctionDeclSyntax fn)
            {
                var name = !string.IsNullOrEmpty(fn.Name)
                    ? fn.Name
                    : (fn.GetChildForField(SyntaxFields.Name) ?? fn.Children.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text;
                if (name == funcName) return fn;
            }
            var sub = FindFunctionRecursive(child, funcName);
            if (sub.IsValid()) return sub;
        }
        return null;
    }

    public static bool IsEnclosingFunctionParameter(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr is FunctionDeclSyntax fn)
            {
                foreach (var p in fn.Parameters)
                {
                    if (p.Name == varName) return true;
                    var pat = p.GetField(SyntaxFields.Pattern) ?? p.GetField(SyntaxFields.Name) ?? p.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                    if (pat.IsValid() && pat.Text == varName) return true;
                }

                var paramsNode = fn.GetField(SyntaxFields.Parameters) ?? fn.Children.FirstOrDefault(c => c.Type.Contains("parameter", StringComparison.Ordinal));
                if (paramsNode.IsValid())
                {
                    foreach (var p in paramsNode.Children)
                    {
                        if (!p.IsValid()) continue;
                        if (p is ParameterDeclSyntax pd && pd.Name == varName) return true;
                        var pat = p.GetField(SyntaxFields.Pattern) ?? p.GetField(SyntaxFields.Name) ?? p.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                        if (pat.IsValid() && pat.Text == varName) return true;
                    }
                }
            }
            curr = curr.Parent;
        }
        return false;
    }

    public static string? FindFirstParameterName(Node funcItem)
    {
        if (funcItem is FunctionDeclSyntax { Parameters.Count: > 0 } fn)
        {
            foreach (var p in fn.Parameters)
            {
                if (p.PassingMode is ParameterPassingMode.Receiver or ParameterPassingMode.MutBorrow)
                    continue;
                if (!string.IsNullOrEmpty(p.Name)) return p.Name;
                var pat = p.GetField(SyntaxFields.Pattern) ?? p.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                if (pat.IsValid() && !pat.IsSelfOrSuper) return pat.Text;
            }
        }

        foreach (var child in funcItem.Children)
        {
            if (!child.IsValid()) continue;
            foreach (var p in child.Children)
            {
                if (p is ParameterDeclSyntax pd && pd.PassingMode is not (ParameterPassingMode.Receiver or ParameterPassingMode.MutBorrow))
                {
                    if (!string.IsNullOrEmpty(pd.Name)) return pd.Name;
                    var pat = pd.GetField(SyntaxFields.Pattern) ?? pd.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                    if (pat.IsValid() && !pat.IsSelfOrSuper) return pat.Text;
                }
            }
        }
        return null;
    }
}
