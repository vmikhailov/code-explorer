using CodeExplorer.Core.Parser;
using TreeSitter;

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
                                                                    "expression_list" or "binary_expression");

            if (valNode.IsValid() && valNode.Type == "expression_list" && valNode.Children.Count > 0)
            {
                valNode = valNode.Children[0];
            }

            if (nameNode.IsValid() && valNode.IsValid())
            {
                register(nameNode.Text, valNode, null);
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }
}
