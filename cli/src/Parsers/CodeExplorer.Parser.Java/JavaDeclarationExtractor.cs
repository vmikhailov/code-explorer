using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java;

/// <summary>
/// Extracts Java symbol, constant, and enum declarations from AST nodes for early-phase propagation.
/// </summary>
public static class JavaDeclarationExtractor
{
    public static void Extract(Node node, Action<string, Node?, string?> registerDeclaration)
    {
        ExtractInternal(node, null, registerDeclaration);
    }

    private static void ExtractInternal(Node node, string? currentScope, Action<string, Node?, string?> register)
    {
        if (node is CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Class } classDecl)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var className = nameNode.IsValid() ? nameNode.Text : (!string.IsNullOrEmpty(classDecl.Name) ? classDecl.Name : currentScope);

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

            foreach (var child in node.FindDescendantsOfType(TreeSitterSyntax.Java.EnumConstant))
            {
                var memNameNode = child.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                  child.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Common.Identifier));

                if (memNameNode.IsValid())
                {
                    var memName = memNameNode.Text;
                    var strLiterals = child.FindDescendantsOfType(TreeSitterSyntax.Java.StringLiteral).ToList();
                    if (strLiterals.Count > 0)
                    {
                        var val = strLiterals.Count >= 2 ? strLiterals[1].Text.Trim('"', '\'') : strLiterals[0].Text.Trim('"', '\'');
                        register($"{enumName}.{memName}", null, val);
                        register(memName, null, val);
                    }
                    else
                    {
                        register($"{enumName}.{memName}", null, memName);
                    }
                }
            }
            return;
        }

        if (node is CodeExplorer.Core.Parser.Syntax.MemberDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.MemberSyntaxKind.Field or CodeExplorer.Core.Parser.Syntax.MemberSyntaxKind.Constant } memDecl)
        {
            var isStaticFinal = memDecl.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.Static) ||
                                memDecl.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.Sealed) ||
                                memDecl.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.Const);
            if (isStaticFinal)
            {
                var declarators = node.FindDescendantsOfType(TreeSitterSyntax.Java.VariableDeclarator);
                foreach (var decl in declarators)
                {
                    var nameNode = decl.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                   decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                    if (nameNode.IsValid())
                    {
                        var name = nameNode.Text;
                        var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                                      decl.Children.LastOrDefault(c => c.IsValid() && c.Type != "=" && !c.Is(TreeSitterSyntax.Common.Identifier));

                        if (valNode.IsValid())
                        {
                            if (!string.IsNullOrEmpty(currentScope))
                            {
                                register($"{currentScope}.{name}", valNode, null);
                            }
                            register(name, valNode, null);
                        }
                    }
                }
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }
}
