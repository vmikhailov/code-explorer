using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python;

/// <summary>
/// Extracts Python symbol, constant, and enum declarations from AST nodes for early-phase propagation.
/// </summary>
public static class PythonDeclarationExtractor
{
    public static void Extract(Node node, Action<string, Node?, string?> registerDeclaration)
    {
        ExtractInternal(node, null, registerDeclaration);
    }

    private static void ExtractInternal(Node node, string? currentScope, Action<string, Node?, string?> register)
    {
        if (node is CodeExplorer.Core.Parser.Syntax.AssignmentExprSyntax)
        {
            var left = node.GetChildForField(TreeSitterSyntax.Fields.Left) ??
                       node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier));
            var right = node.GetChildForField(TreeSitterSyntax.Fields.Right) ??
                        node.Children.LastOrDefault(c => c.IsValid() && c.Type != "=" && !c.Is(TreeSitterSyntax.Python.Identifier));

            if (left.IsValid() && right.IsValid())
            {
                var varName = left.Text;
                // Only consider uppercase/constant names or within enum/class scope
                if (varName.Length > 0 && (char.IsUpper(varName[0]) || !string.IsNullOrEmpty(currentScope)))
                {
                    if (!string.IsNullOrEmpty(currentScope))
                    {
                        register($"{currentScope}.{varName}", right, null);
                    }
                    register(varName, right, null);
                }
            }
            return;
        }

        if (node is CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var className = nameNode.IsValid() ? nameNode.Text : currentScope;

            foreach (var child in node.Children)
            {
                ExtractInternal(child, className, register);
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }
}
