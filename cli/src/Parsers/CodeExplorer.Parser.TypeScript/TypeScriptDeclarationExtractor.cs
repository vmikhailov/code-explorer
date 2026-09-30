using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript;

/// <summary>
/// Extracts TypeScript/JavaScript symbol, constant, and enum declarations from AST nodes for early-phase propagation.
/// </summary>
public static class TypeScriptDeclarationExtractor
{
    public static void Extract(Node node, Action<string, Node?, string?> registerDeclaration)
    {
        ExtractInternal(node, null, registerDeclaration);
    }

    private static void ExtractInternal(Node node, string? currentScope, Action<string, Node?, string?> register)
    {
        var type = node.Type;

        if (type is TreeSitterSyntax.TypeScript.EnumDeclaration)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var enumName = nameNode.IsValid() ? nameNode.Text : "Enum";

            var body = node.FindChildOfType("enum_body");
            if (body.IsValid())
            {
                foreach (var child in body.Children)
                {
                    if (child.Type is "enum_assignment" or TreeSitterSyntax.TypeScript.PropertyIdentifier)
                    {
                        var prop = child.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                   child.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.TypeScript.PropertyIdentifier);
                        if (prop.IsValid())
                        {
                            var valNode = child.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                                          (child.Children.Count > 2 ? child.Children[2] : null);

                            var keyWithScope = $"{enumName}.{prop.Text}";
                            register(keyWithScope, valNode, valNode == null ? prop.Text : null);
                        }
                    }
                }
            }
            return;
        }

        if (type is TreeSitterSyntax.TypeScript.LexicalDeclaration or TreeSitterSyntax.TypeScript.VariableDeclaration)
        {
            var declarators = node.FindChildrenOfType(TreeSitterSyntax.TypeScript.VariableDeclarator);

            foreach (var decl in declarators)
            {
                var nameNode = decl.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                               decl.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);

                if (nameNode.IsValid())
                {
                    var varName = nameNode.Text;
                    var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value);
                    if (!valNode.IsValid() && decl.Children.Count >= 3 && decl.Children[1].Type == "=")
                    {
                        valNode = decl.Children[2];
                    }

                    if (valNode.IsValid())
                    {
                        // Handle const Objects: const Topics = { OrderCreated: "...", ... }
                        if (valNode.Type is TreeSitterSyntax.TypeScript.Object or "object")
                        {
                            foreach (var pair in valNode.Children.Where(c => c.Type is TreeSitterSyntax.TypeScript.Pair or "pair"))
                            {
                                var keyNode = pair.GetChildForField(TreeSitterSyntax.Fields.Key) ?? pair.Children[0];
                                var pairValNode = pair.GetChildForField(TreeSitterSyntax.Fields.Value) ?? pair.Children[^1];

                                if (keyNode.IsValid())
                                {
                                    var cleanKey = keyNode.Text.Trim('\'', '"', '`');
                                    register($"{varName}.{cleanKey}", pairValNode, null);
                                }
                            }
                        }
                        else
                        {
                            register(varName, valNode, null);
                        }
                    }
                }
            }
            return;
        }

        // Do not recurse into local scopes (functions, methods, arrow functions, blocks)
        if (type is TreeSitterSyntax.TypeScript.FunctionDeclaration or
                    TreeSitterSyntax.TypeScript.MethodDefinition or
                    TreeSitterSyntax.TypeScript.ArrowFunction or
                    TreeSitterSyntax.TypeScript.StatementBlock or
                    "function" or "generator_function" or "constructor")
        {
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }
}
