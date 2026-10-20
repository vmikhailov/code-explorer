using CodeExplorer.Core.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Lexical scope, variable declaration, and identifier reaching definition resolver for AST nodes.
/// </summary>
public static class AstScopeResolver
{
    public static Node? FindVariableDeclarationInScope(Node node, string varName)
    {
        var targetRow = node.StartPosition.Row;
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (IsScopeBoundary(curr.Type))
            {
                foreach (var child in curr.Children)
                {
                    if (child.StartPosition.Row > targetRow) break;

                    // C# local declaration
                    if (child.IsAny(TreeSitterSyntax.CSharp.LocalDeclarationStatement, TreeSitterSyntax.CSharp.VariableDeclaration))
                    {
                        var declarators = child.FindDescendantsOfType(TreeSitterSyntax.CSharp.VariableDeclarator);
                        foreach (var decl in declarators)
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ??
                                           decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (valNode.IsValid())
                                {
                                    if (valNode.Is(TreeSitterSyntax.CSharp.EqualsValueClause))
                                    {
                                        var inner = valNode.GetField(TreeSitterSyntax.Fields.Value) ??
                                                    valNode.Children.LastOrDefault(c => c.IsValid() && c.Type != "=");
                                        if (inner.IsValid()) return inner;
                                    }
                                    return valNode;
                                }

                                var eq = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                                if (eq.IsValid())
                                {
                                    var eqVal = eq.GetField(TreeSitterSyntax.Fields.Value) ??
                                                eq.Children.LastOrDefault(c => c.IsValid() && c.Type != "=");
                                    if (eqVal.IsValid()) return eqVal;
                                }

                                var afterEq = decl.Children.LastOrDefault(c => c.IsValid() && c.Type != "=" && c.Id != nameNode.Id);
                                if (afterEq.IsValid()) return afterEq;
                            }
                        }
                    }

                    // TS / JS lexical or variable declaration
                    if (child.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration))
                    {
                        var declarators = child.FindChildrenOfType(TreeSitterSyntax.TypeScript.VariableDeclarator);
                        foreach (var decl in declarators)
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ??
                                           decl.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);

                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (valNode.IsValid()) return valNode;

                                if (decl.Children.Count >= 3 && decl.Children[1].Type == "=")
                                {
                                    return decl.Children[2];
                                }
                            }
                        }
                    }

                    // Rust let_declaration (let schema = ...)
                    if (child.Is(TreeSitterSyntax.Rust.LetDeclaration))
                    {
                        var pat = child.GetField("pattern") ?? child.FindChildOfType(TreeSitterSyntax.Rust.Identifier);
                        var val = child.GetField(TreeSitterSyntax.Fields.Value);
                        if (pat.IsValid() && pat.Text == varName && val.IsValid())
                        {
                            return val;
                        }
                    }

                    // Go var_declaration / var_spec
                    if (child.Is("var_declaration"))
                    {
                        var specs = child.FindDescendantsOfType("var_spec");
                        foreach (var spec in specs)
                        {
                            var pat = spec.GetField(TreeSitterSyntax.Fields.Name) ?? spec.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                            var val = spec.GetField(TreeSitterSyntax.Fields.Value) ?? spec.Children.LastOrDefault(c => c.IsValid() && (!pat.IsValid() || c.Id != pat.Id));
                            if (pat.IsValid() && pat.Text == varName && val.IsValid())
                            {
                                return val;
                            }
                        }
                    }

                    // Go short_var_declaration (topic := "orders")
                    if (child.Is(TreeSitterSyntax.Go.ShortVarDeclaration))
                    {
                        var left = child.GetField(TreeSitterSyntax.Fields.Left);
                        var right = child.GetField(TreeSitterSyntax.Fields.Right);

                        if (left.IsValid() && left.Text == varName && right.IsValid())
                        {
                            return right;
                        }
                    }

                    // Python / JS / Go Assignment Expression
                    if (child.IsAny("assignment", "assignment_statement", TreeSitterSyntax.Common.AssignmentExpression))
                    {
                        var left = child.GetField(TreeSitterSyntax.Fields.Left);
                        var right = child.GetField(TreeSitterSyntax.Fields.Right);

                        if (left.IsValid() && left.Text == varName && right.IsValid())
                        {
                            return right;
                        }
                    }
                }
            }

            curr = curr.Parent;
        }

        return null;
    }

    public static Node? FindClassFieldInitializer(Node node, string fieldName)
    {
        var cleanField = fieldName.TrimStart('_');
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.Common.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration,
                           TreeSitterSyntax.CSharp.RecordDeclaration, TreeSitterSyntax.Python.ClassDefinition))
            {
                foreach (var child in curr.Children)
                {
                    // C# field or property
                    if (child.Is(TreeSitterSyntax.CSharp.FieldDeclaration))
                    {
                        var declarators = child.FindChildrenOfType(TreeSitterSyntax.CSharp.VariableDeclarator);
                        foreach (var decl in declarators)
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ??
                                           decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                            if (nameNode.IsValid() && (nameNode.Text == fieldName || nameNode.Text.TrimStart('_') == cleanField))
                            {
                                var eq = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                                if (eq.IsValid() && eq.Children.Count > 1) return eq.Children[1];
                            }
                        }
                    }
                    if (child.Is(TreeSitterSyntax.CSharp.PropertyDeclaration))
                    {
                        var nameNode = child.GetField(TreeSitterSyntax.Fields.Name);
                        if (nameNode.IsValid() && (nameNode.Text == fieldName || nameNode.Text.TrimStart('_') == cleanField))
                        {
                            var arrow = child.FindChildOfType(TreeSitterSyntax.CSharp.ArrowExpressionClause);
                            if (arrow.IsValid() && arrow.Children.Count > 1) return arrow.Children[1];
                        }
                    }

                    // TS / JS property / field definition
                    if (child.IsAny(TreeSitterSyntax.TypeScript.PropertyDefinition, TreeSitterSyntax.TypeScript.PublicFieldDefinition))
                    {
                        var nameNode = child.GetField(TreeSitterSyntax.Fields.Property) ??
                                       child.GetField(TreeSitterSyntax.Fields.Name);

                        if (nameNode.IsValid() && (nameNode.Text == fieldName || nameNode.Text.TrimStart('_') == cleanField))
                        {
                            var valNode = child.GetField(TreeSitterSyntax.Fields.Value);
                            if (valNode.IsValid()) return valNode;
                        }
                    }
                }
            }

            curr = curr.Parent;
        }

        return null;
    }

    public static bool IsScopeBoundary(string type) =>
        type is TreeSitterSyntax.Common.Block or
                TreeSitterSyntax.Common.MethodDeclaration or
                TreeSitterSyntax.CSharp.LocalFunctionStatement or
                TreeSitterSyntax.CSharp.CompilationUnit or
                TreeSitterSyntax.TypeScript.StatementBlock or
                TreeSitterSyntax.Common.FunctionDeclaration or
                TreeSitterSyntax.TypeScript.ArrowFunction or
                TreeSitterSyntax.TypeScript.Program or
                TreeSitterSyntax.Python.FunctionDefinition or
                TreeSitterSyntax.Rust.FunctionItem or
                TreeSitterSyntax.Rust.SourceFile or
                "source_file";

    public static Node? FindEnclosingFunctionItem(Node node, string funcName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.Type is TreeSitterSyntax.Rust.ImplItem or TreeSitterSyntax.Rust.SourceFile or "source_file")
            {
                var fns = curr.FindDescendantsOfType(TreeSitterSyntax.Rust.FunctionItem);
                foreach (var fn in fns)
                {
                    var nameChild = fn.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                    fn.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);
                    if (nameChild.IsValid() && nameChild.Text == funcName)
                    {
                        return fn;
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }

    public static bool IsEnclosingFunctionParameter(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.Type is TreeSitterSyntax.Rust.FunctionItem or
                             TreeSitterSyntax.Common.MethodDeclaration or
                             TreeSitterSyntax.Common.FunctionDeclaration or
                             TreeSitterSyntax.Python.FunctionDefinition or
                             TreeSitterSyntax.CSharp.LocalFunctionStatement)
            {
                var paramsNode = curr.FindChildOfType(TreeSitterSyntax.Rust.Parameters) ??
                                 curr.FindChildOfType("parameter_list") ??
                                 curr.FindChildOfType("formal_parameters");
                if (paramsNode.IsValid())
                {
                    foreach (var p in paramsNode.Children)
                    {
                        if (!p.IsValid()) continue;
                        var pat = p.GetField("pattern") ??
                                  p.GetField(TreeSitterSyntax.Fields.Name) ??
                                  p.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                        if (pat.IsValid() && pat.Text == varName)
                        {
                            return true;
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return false;
    }

    public static string? FindFirstParameterName(Node funcItem)
    {
        var parameters = funcItem.FindChildOfType(TreeSitterSyntax.Rust.Parameters);
        if (!parameters.IsValid()) return null;

        foreach (var p in parameters.Children)
        {
            if (p.IsValid() && p.Type == TreeSitterSyntax.Rust.Parameter)
            {
                var pat = p.GetField("pattern") ?? p.FindChildOfType(TreeSitterSyntax.Rust.Identifier);
                if (pat.IsValid())
                {
                    return pat.Text;
                }
            }
        }
        return null;
    }
}
