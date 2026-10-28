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
        var type = node.Type;

        if (type is TreeSitterSyntax.Go.ConstSpec or TreeSitterSyntax.Go.VarSpec)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Go.Identifier);

            var valNode = node.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                          node.Children.FirstOrDefault(c => c.Type is TreeSitterSyntax.Go.RawStringLiteral or
                                                                    TreeSitterSyntax.Go.InterpretedStringLiteral or
                                                                    TreeSitterSyntax.Go.ExpressionList or
                                                                    TreeSitterSyntax.Common.BinaryExpression or
                                                                    TreeSitterSyntax.Common.CallExpression);

            if (valNode.IsValid() && valNode.Type == TreeSitterSyntax.Go.ExpressionList && valNode.Children.Count > 0)
            {
                valNode = valNode.Children[0];
            }

            if (nameNode.IsValid() && valNode.IsValid())
            {
                register(nameNode.Text, valNode, null);
            }
            return;
        }

        if (type == "keyed_element" && node.Children.Count >= 3)
        {
            var keyChild = node.Children[0];
            if (keyChild.IsValid() && keyChild.Type == "literal_element" && keyChild.Children.Count > 0)
            {
                keyChild = keyChild.Children[0];
            }
            var valChild = node.Children[^1];
            if (valChild.IsValid() && valChild.Type == "literal_element" && valChild.Children.Count > 0)
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
