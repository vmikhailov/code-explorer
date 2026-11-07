using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

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
        if (node is CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Enum } enumDecl)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var enumName = nameNode.IsValid() ? nameNode.Text : (!string.IsNullOrEmpty(enumDecl.Name) ? enumDecl.Name : "Enum");

            var body = node.FindChildOfType(TreeSitterSyntax.TypeScript.EnumBody) ?? node;
            if (body.IsValid())
            {
                foreach (var child in body.Children)
                {
                    if (child is CodeExplorer.Core.Parser.Syntax.MemberDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.MemberSyntaxKind.EnumMember } ||
                        child.IsAny(TreeSitterSyntax.TypeScript.EnumAssignment, TreeSitterSyntax.TypeScript.PropertyIdentifier))
                    {
                        var prop = child.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                   child.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.PropertyIdentifier)) ??
                                   (child.Is(TreeSitterSyntax.TypeScript.PropertyIdentifier) ? child : null);
                        if (prop.IsValid())
                        {
                            var valNode = child.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                                          (child.Children.Count >= 2 ? child.Children[^1] : null);

                            var keyWithScope = $"{enumName}.{prop.Text}";
                            register(keyWithScope, valNode, valNode == null ? prop.Text : null);
                        }
                    }
                }
            }
            return;
        }

        if (node is CodeExplorer.Core.Parser.Syntax.VariableDeclSyntax { IsDeclarator: false })
        {
            var declarators = node.FindChildrenOfType(TreeSitterSyntax.TypeScript.VariableDeclarator);

            foreach (var decl in declarators)
            {
                var nameNode = decl.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                               decl.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);

                if (nameNode.IsValid() && nameNode is not CodeExplorer.Core.Parser.Syntax.PatternSyntax { Kind: CodeExplorer.Core.Parser.Syntax.PatternKind.Object })
                {
                    var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value);
                    if (!valNode.IsValid() && decl.Children.Count >= 3 && decl.Children[1].Type == "=")
                    {
                        valNode = decl.Children[2];
                    }

                    var varName = nameNode.Text;

                    if (valNode.IsValid())
                    {
                        // Handle const Objects: const Topics = { OrderCreated: "...", ... }
                        if (valNode.Is(TreeSitterSyntax.TypeScript.Object))
                        {
                            void FlattenObject(string prefix, Node objNode)
                            {
                                foreach (var pair in objNode.Children.Where(c => c.Is(TreeSitterSyntax.TypeScript.Pair)))
                                {
                                    var keyNode = pair.GetChildForField(TreeSitterSyntax.Fields.Key) ?? pair.Children[0];
                                    var pairValNode = pair.GetChildForField(TreeSitterSyntax.Fields.Value) ?? pair.Children[^1];

                                    if (keyNode.IsValid() && pairValNode.IsValid())
                                    {
                                        var cleanKey = keyNode.Text.Trim('\'', '"', '`');
                                        var propPath = $"{prefix}.{cleanKey}";
                                        if (pairValNode.Is(TreeSitterSyntax.TypeScript.Object))
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
            return;
        }

        // Do not recurse into local scopes (functions, methods, arrow functions, blocks)
        if (node is CodeExplorer.Core.Parser.Syntax.FunctionDeclSyntax or
                    CodeExplorer.Core.Parser.Syntax.LambdaExprSyntax or
                    CodeExplorer.Core.Parser.Syntax.BlockStmtSyntax)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }
}
