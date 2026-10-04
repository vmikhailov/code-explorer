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

            var body = node.FindChildOfType(TreeSitterSyntax.TypeScript.EnumBody);
            if (body.IsValid())
            {
                foreach (var child in body.Children)
                {
                    if (child.Type is TreeSitterSyntax.TypeScript.EnumAssignment or TreeSitterSyntax.TypeScript.PropertyIdentifier)
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

                if (!nameNode.IsValid())
                {
                    nameNode = decl.FindChildOfType("object_pattern") ??
                               decl.Children.FirstOrDefault(c => c.Type == "object_pattern");
                }

                if (nameNode.IsValid())
                {
                    var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value);
                    if (!valNode.IsValid() && decl.Children.Count >= 3 && decl.Children[1].Type == "=")
                    {
                        valNode = decl.Children[2];
                    }

                    if (nameNode.Type == "object_pattern")
                    {
                        if (valNode.IsValid())
                        {
                            var rhsText = valNode.Text.Trim();
                            foreach (var patChild in nameNode.Children)
                            {
                                if (patChild.Type is "shorthand_property_identifier" or "shorthand_property_identifier_pattern" or TreeSitterSyntax.TypeScript.Identifier || patChild.Type.Contains("shorthand"))
                                {
                                    var propName = patChild.Text;
                                    var lookupKey = $"{rhsText}.{propName}";
                                    if (ConstantRegistry.TryResolve(null, lookupKey, out var resVal) && !string.IsNullOrEmpty(resVal))
                                    {
                                        register(propName, null, resVal);
                                    }
                                }
                                else if (patChild.Type is "pair_pattern" or TreeSitterSyntax.TypeScript.Pair)
                                {
                                    var key = patChild.GetChildForField(TreeSitterSyntax.Fields.Key) ?? patChild.Children[0];
                                    var val = patChild.GetChildForField(TreeSitterSyntax.Fields.Value) ?? patChild.Children[^1];
                                    var propName = key.IsValid() ? key.Text : val.Text;
                                    var aliasName = val.IsValid() ? val.Text : propName;
                                    var lookupKey = $"{rhsText}.{propName}";
                                    if (ConstantRegistry.TryResolve(null, lookupKey, out var resVal) && !string.IsNullOrEmpty(resVal))
                                    {
                                        register(aliasName, null, resVal);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var varName = nameNode.Text;

                        if (valNode.IsValid())
                        {
                            // Handle const Objects: const Topics = { OrderCreated: "...", ... }
                            if (valNode.Type is TreeSitterSyntax.TypeScript.Object)
                            {
                                void FlattenObject(string prefix, Node objNode)
                                {
                                    foreach (var pair in objNode.Children.Where(c => c.Type is TreeSitterSyntax.TypeScript.Pair))
                                    {
                                        var keyNode = pair.GetChildForField(TreeSitterSyntax.Fields.Key) ?? pair.Children[0];
                                        var pairValNode = pair.GetChildForField(TreeSitterSyntax.Fields.Value) ?? pair.Children[^1];

                                        if (keyNode.IsValid() && pairValNode.IsValid())
                                        {
                                            var cleanKey = keyNode.Text.Trim('\'', '"', '`');
                                            var propPath = $"{prefix}.{cleanKey}";
                                            if (pairValNode.Type is TreeSitterSyntax.TypeScript.Object)
                                            {
                                                FlattenObject(propPath, pairValNode);
                                            }
                                            else
                                            {
                                                register(propPath, pairValNode, null);
                                            }
                                        }
                                    }
                                }

                                FlattenObject(varName, valNode);
                            }
                            else
                            {
                                register(varName, valNode, null);
                            }
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
                    TreeSitterSyntax.TypeScript.Function or
                    TreeSitterSyntax.TypeScript.GeneratorFunction or
                    TreeSitterSyntax.TypeScript.Constructor)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }
}
