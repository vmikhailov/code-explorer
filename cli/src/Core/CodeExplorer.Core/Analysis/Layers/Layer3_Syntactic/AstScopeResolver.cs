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
                               varDecl.Children.FirstOrDefault(c => c is IdentifierExprSyntax or IdentifierPatternSyntax);
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

        if (node is ExprStmtSyntax { Expression: AssignmentExprSyntax exprAssign })
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
                var found = SearchMemberInitializer(curr, fieldName, cleanField);
                if (found.IsValid()) return found;
            }

            curr = curr.Parent;
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
        node is BlockStmtSyntax or FunctionDeclSyntax or LambdaExprSyntax or SyntaxDocument or NamespaceDeclSyntax
        || IsScopeBoundary(node.Type);

    public static bool IsScopeBoundary(string type) =>
        type is "block" or
                "statement_block" or
                "compound_statement" or
                "method_declaration" or
                "function_declaration" or
                "function_definition" or
                "function_item" or
                "local_function_statement" or
                "arrow_function" or
                "compilation_unit" or
                "program" or
                "module" or
                "source_file" or
                "document";

    public static Node? FindEnclosingFunctionItem(Node node, string funcName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr is ImplDeclSyntax or TypeDeclSyntax or SyntaxDocument or NamespaceDeclSyntax)
            {
                var fn = FindFunctionRecursive(curr, funcName);
                if (fn.IsValid()) return fn;
            }
            curr = curr.Parent;
        }
        return null;
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
                    var pat = p.GetField(SyntaxFields.Pattern) ?? p.GetField(SyntaxFields.Name) ?? p.Children.FirstOrDefault(c => c is IdentifierExprSyntax or IdentifierPatternSyntax);
                    if (pat.IsValid() && pat.Text == varName) return true;
                }

                var paramsNode = fn.GetField(SyntaxFields.Parameters) ?? fn.Children.FirstOrDefault(c => c.Type.Contains("parameter", StringComparison.Ordinal));
                if (paramsNode.IsValid())
                {
                    foreach (var p in paramsNode.Children)
                    {
                        if (!p.IsValid()) continue;
                        if (p is ParameterDeclSyntax pd && pd.Name == varName) return true;
                        var pat = p.GetField(SyntaxFields.Pattern) ?? p.GetField(SyntaxFields.Name) ?? p.Children.FirstOrDefault(c => c is IdentifierExprSyntax or IdentifierPatternSyntax);
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
                if (p.PassingMode is ParameterPassingMode.Receiver or ParameterPassingMode.RefBorrow or ParameterPassingMode.MutBorrow && p.Name is "self" or "&self" or "&mut self")
                    continue;
                if (!string.IsNullOrEmpty(p.Name) && p.Name != "self") return p.Name;
                var pat = p.GetField(SyntaxFields.Pattern) ?? p.Children.FirstOrDefault(c => c is IdentifierExprSyntax or IdentifierPatternSyntax);
                if (pat.IsValid() && pat.Text != "self") return pat.Text;
            }
        }

        foreach (var child in funcItem.Children)
        {
            if (!child.IsValid()) continue;
            foreach (var p in child.Children)
            {
                if (p is ParameterDeclSyntax pd && pd.PassingMode != ParameterPassingMode.Receiver)
                {
                    if (!string.IsNullOrEmpty(pd.Name) && pd.Name != "self") return pd.Name;
                    var pat = pd.GetField(SyntaxFields.Pattern) ?? pd.Children.FirstOrDefault(c => c is IdentifierExprSyntax or IdentifierPatternSyntax);
                    if (pat.IsValid() && pat.Text != "self") return pat.Text;
                }
            }
        }
        return null;
    }
}
