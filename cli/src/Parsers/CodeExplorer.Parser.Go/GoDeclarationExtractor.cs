using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go;

/// <summary>
/// Extracts Go symbol, constant, and var declarations from AST nodes for early-phase propagation.
/// </summary>
public static class GoDeclarationExtractor
{
    public static void Extract(Node node, Action<string, Node?, string?> registerDeclaration)
    {
        ExtractInternal(node, null, registerDeclaration);
    }

    private static void ExtractInternal(Node node, string? currentScope, Action<string, Node?, string?> register)
    {
        if (node is CodeExplorer.Core.Parser.Syntax.MemberDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.MemberSyntaxKind.Constant } or
            CodeExplorer.Core.Parser.Syntax.VariableDeclSyntax { IsDeclarator: true })
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Go.Identifier));

            var valNode = node.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                          node.Children.FirstOrDefault(c => c.IsAny(
                              TreeSitterSyntax.Go.RawStringLiteral,
                              TreeSitterSyntax.Go.InterpretedStringLiteral,
                              TreeSitterSyntax.Go.ExpressionList,
                              TreeSitterSyntax.Common.BinaryExpression,
                              TreeSitterSyntax.Common.CallExpression));

            if (valNode.IsValid() && valNode.Is(TreeSitterSyntax.Go.ExpressionList) && valNode.Children.Count > 0)
            {
                valNode = valNode.Children[0];
            }

            if (nameNode.IsValid() && valNode.IsValid())
            {
                register(nameNode.Text, valNode, null);
            }
            return;
        }

        if (node is CodeExplorer.Core.Parser.Syntax.KeyValueExprSyntax && node.Children.Count >= 2)
        {
            var keyChild = node.Children[0];
            if (keyChild.IsValid() && keyChild is CodeExplorer.Core.Parser.Syntax.ParenthesizedExprSyntax && keyChild.Children.Count > 0)
            {
                keyChild = keyChild.Children[0];
            }
            var valChild = node.Children[^1];
            if (valChild.IsValid() && valChild is CodeExplorer.Core.Parser.Syntax.ParenthesizedExprSyntax && valChild.Children.Count > 0)
            {
                valChild = valChild.Children[0];
            }
            if (keyChild.IsValid() && valChild.IsValid())
            {
                var keyName = keyChild.Text.Trim();
                if (!string.IsNullOrEmpty(keyName))
                {
                    register(keyName, valChild, null);
                    register($"config.{keyName}", valChild, null);
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
