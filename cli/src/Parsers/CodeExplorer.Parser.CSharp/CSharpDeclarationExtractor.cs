using CodeExplorer.Core.Parser;
using TreeSitter;

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
        var type = node.Type;

        if (type is TreeSitterSyntax.CSharp.ClassDeclaration or
                    TreeSitterSyntax.CSharp.StructDeclaration or
                    TreeSitterSyntax.CSharp.RecordDeclaration)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var className = nameNode.IsValid() ? nameNode.Text : currentScope;

            foreach (var child in node.Children)
            {
                ExtractInternal(child, className, register);
            }
            return;
        }

        if (type is TreeSitterSyntax.CSharp.EnumDeclaration)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var enumName = nameNode.IsValid() ? nameNode.Text : "Enum";

            foreach (var child in node.Children)
            {
                if (child.Type is "enum_member_declaration")
                {
                    var memNameNode = child.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                      child.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Common.Identifier);

                    if (memNameNode.IsValid())
                    {
                        var memName = memNameNode.Text;
                        var eq = child.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                        Node? valNode = eq.IsValid() && eq.Children.Count > 1 ? eq.Children[1] : null;

                        var keyWithScope = $"{enumName}.{memName}";
                        register(keyWithScope, valNode, valNode == null ? memName : null);
                    }
                }
            }
            return;
        }

        if (type is TreeSitterSyntax.CSharp.FieldDeclaration)
        {
            var isConstOrReadonly = node.Children.Any(c => c.Text is "const" or "readonly" or "static" || c.Type is "const" or "readonly" or "static") ||
                                    node.FindDescendantsOfType("modifier").Any(m => m.Text is "const" or "readonly" or "static");
            if (isConstOrReadonly)
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
                        if (!valNode.IsValid())
                        {
                            var eq = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause) ??
                                     decl.FindDescendantOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                            if (eq.IsValid())
                            {
                                valNode = eq.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                                          eq.Children.LastOrDefault(c => c.IsValid() && c.Type != "=");
                            }
                            else
                            {
                                valNode = decl.Children.LastOrDefault(c => c.IsValid() && c.Type != "=" && c.Type != TreeSitterSyntax.Common.Identifier);
                            }
                        }

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

        if (type is TreeSitterSyntax.CSharp.PropertyDeclaration)
        {
            var isStatic = node.Children.Any(c => c.Text is "static" || c.Type is "static");
            if (isStatic)
            {
                var arrow = node.FindChildOfType(TreeSitterSyntax.CSharp.ArrowExpressionClause);
                if (arrow.IsValid())
                {
                    var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                   node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                    var valNode = arrow.Children.LastOrDefault(c => c.IsValid() && c.Type != "=>");

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
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }
}
