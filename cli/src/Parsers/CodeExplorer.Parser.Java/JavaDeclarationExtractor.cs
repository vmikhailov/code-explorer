using CodeExplorer.Core.Parser;
using TreeSitter;

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
        var type = node.Type;

        if (type is TreeSitterSyntax.Java.ClassDeclaration)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var className = nameNode.IsValid() ? nameNode.Text : currentScope;

            foreach (var child in node.Children)
            {
                ExtractInternal(child, className, register);
            }
            return;
        }

        if (type is TreeSitterSyntax.Java.EnumDeclaration)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var enumName = nameNode.IsValid() ? nameNode.Text : "Enum";

            foreach (var child in node.Children)
            {
                if (child.Type is "enum_constant")
                {
                    var memNameNode = child.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                      child.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Common.Identifier);

                    if (memNameNode.IsValid())
                    {
                        var memName = memNameNode.Text;
                        register($"{enumName}.{memName}", null, memName);
                    }
                }
            }
            return;
        }

        if (type is TreeSitterSyntax.Java.FieldDeclaration)
        {
            var isStaticFinal = node.FindDescendantsOfType("modifier").Any(m => m.Text is "static" or "final");
            if (isStaticFinal)
            {
                var declarators = node.FindDescendantsOfType("variable_declarator");
                foreach (var decl in declarators)
                {
                    var nameNode = decl.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                   decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                    if (nameNode.IsValid())
                    {
                        var name = nameNode.Text;
                        var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                                      decl.Children.LastOrDefault(c => c.IsValid() && c.Type != "=" && c.Type != TreeSitterSyntax.Common.Identifier);

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
