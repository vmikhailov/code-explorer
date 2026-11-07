using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp;

/// <summary>
/// Extracts C# symbol, constant, and enum declarations from AST nodes for early-phase propagation.
/// </summary>
public static class CSharpDeclarationExtractor
{
    public static void Extract(Node node, Action<string, Node?, string?> registerDeclaration)
    {
        ExtractInternal(node, null, registerDeclaration);
    }

    private static void ExtractInternal(Node node, string? currentScope, Action<string, Node?, string?> register)
    {
        if (node is CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Class or CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Struct or CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Record } typeDecl)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var className = nameNode.IsValid() ? nameNode.Text : (!string.IsNullOrEmpty(typeDecl.Name) ? typeDecl.Name : currentScope);

            foreach (var child in node.Children)
            {
                ExtractInternal(child, className, register);
            }
            return;
        }

        if (node is CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Enum } enumDecl)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var enumName = nameNode.IsValid() ? nameNode.Text : (!string.IsNullOrEmpty(enumDecl.Name) ? enumDecl.Name : "Enum");

            foreach (var child in node.FindDescendantsOfType(TreeSitterSyntax.CSharp.EnumMemberDeclaration))
            {
                var memNameNode = child.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                  child.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Common.Identifier));

                if (memNameNode.IsValid())
                {
                    var memName = memNameNode.Text;
                    var valNode = child.GetChildForField(TreeSitterSyntax.Fields.Value);

                    var keyWithScope = $"{enumName}.{memName}";
                    register(keyWithScope, valNode, valNode == null ? memName : null);
                }
            }
            return;
        }

        if (node is CodeExplorer.Core.Parser.Syntax.MemberDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.MemberSyntaxKind.Field or CodeExplorer.Core.Parser.Syntax.MemberSyntaxKind.Constant } or
            CodeExplorer.Core.Parser.Syntax.VariableDeclSyntax { IsDeclarator: false })
        {
            if (HasAnyModifier(node, "const", "readonly", "static"))
            {
                var declarators = node.FindDescendantsOfType(TreeSitterSyntax.CSharp.VariableDeclarator);
                foreach (var decl in declarators)
                {
                    var nameNode = decl.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                   decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                    if (nameNode.IsValid())
                    {
                        var name = nameNode.Text;
                        var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value);

                        if (!string.IsNullOrEmpty(currentScope))
                        {
                            register($"{currentScope}.{name}", valNode, null);
                        }
                        register(name, valNode, null);
                    }
                }
            }
            return;
        }

        if (node is CodeExplorer.Core.Parser.Syntax.MemberDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.MemberSyntaxKind.Property } propDecl)
        {
            if (HasAnyModifier(node, "static"))
            {
                var arrow = node.FindChildOfType(TreeSitterSyntax.CSharp.ArrowExpressionClause);
                var valNode = propDecl.Initializer ?? arrow?.Children.LastOrDefault(c => c.IsValid());
                var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                               node.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                if (nameNode.IsValid() && valNode.IsValid())
                {
                    var name = nameNode.Text;
                    if (!string.IsNullOrEmpty(currentScope))
                    {
                        register($"{currentScope}.{name}", valNode, null);
                    }
                    register(name, valNode, null);
                }
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }

    private static bool HasAnyModifier(Node node, params string[] modifiers)
    {
        if (node is CodeExplorer.Core.Parser.Syntax.DeclarationSyntax decl)
        {
            if (modifiers.Contains("const") && decl.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.Const)) return true;
            if (modifiers.Contains("readonly") && decl.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.ReadOnly)) return true;
            if (modifiers.Contains("static") && decl.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.Static)) return true;
        }
        return false;
    }
}
