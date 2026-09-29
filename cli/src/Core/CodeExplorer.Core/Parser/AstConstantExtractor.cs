using CodeExplorer.Core.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Universal AST-driven constant, enum, and compile-time expression extractor.
/// Replaces heuristic regex searches with full syntax tree exploration across C#, TypeScript, Go, Python, and Java.
/// Performs topological multi-pass evaluation for dependent constants (e.g. A + B, template strings).
/// </summary>
public static class AstConstantExtractor
{
    private record PendingConstant(string Key, Node? ValueNode, string? InitialLiteral);

    public static void ExtractAndRegister(SyntaxTree syntaxTree, string? projectName = null)
    {
        if (syntaxTree?.Tree?.RootNode == null || syntaxTree.Tree.RootNode.Children.Count == 0) return;
        var langName = syntaxTree.FileParser.LanguageName;
        var pending = new List<PendingConstant>();
        CollectConstantsFromNode(syntaxTree.Tree.RootNode, langName, null, pending);

        if (pending.Count > 0)
        {
            ResolveTopologicalConstants(pending, projectName);
        }
    }

    public static void ExtractAndRegister(string filePath, string content, string? projectName = null, string? languageName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var langName = languageName;
        if (string.IsNullOrEmpty(langName))
        {
            var parser = WorkspaceIndexer.GetParserForFile(filePath);
            langName = parser?.LanguageName;
        }

        if (string.IsNullOrEmpty(langName))
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            langName = ext switch
            {
                ".cs" => "c-sharp",
                ".ts" or ".tsx" => "typescript",
                ".js" or ".jsx" => "javascript",
                ".go" => "go",
                ".py" => "python",
                ".java" => "java",
                _ => null
            };
        }

        if (langName == null) return;

        try
        {
            var language = SyntaxTree.GetLanguage(langName);
            using var parser = new TreeSitter.Parser(language);
            using var tree = parser.Parse(content);

            if (tree?.RootNode != null && tree.RootNode.Children.Count > 0)
            {
                var pending = new List<PendingConstant>();
                CollectConstantsFromNode(tree.RootNode, langName, null, pending);

                if (pending.Count > 0)
                {
                    ResolveTopologicalConstants(pending, projectName);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"ExtractAndRegister failed for {filePath}: {ex}", ex);
        }
    }

    private static void CollectConstantsFromNode(
        Node node,
        string langName,
        string? currentScope,
        List<PendingConstant> pending)
    {
        switch (langName)
        {
            case "c-sharp":
                CollectCSharp(node, currentScope, pending);
                break;
            case "typescript":
            case "javascript":
                CollectTypeScript(node, currentScope, pending);
                break;
            case "go":
                CollectGo(node, currentScope, pending);
                break;
            case "python":
                CollectPython(node, currentScope, pending);
                break;
            case "java":
                CollectJava(node, currentScope, pending);
                break;
        }
    }

    private static void CollectCSharp(Node node, string? currentScope, List<PendingConstant> pending)
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
                CollectCSharp(child, className, pending);
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
                        pending.Add(new PendingConstant(keyWithScope, valNode, valNode == null ? memName : null));
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
                            pending.Add(new PendingConstant($"{currentScope}.{name}", valNode, null));
                        }
                        pending.Add(new PendingConstant(name, valNode, null));
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
                var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
                var arrow = node.FindChildOfType("arrow_expression_clause");
                if (nameNode.IsValid() && arrow.IsValid() && arrow.Children.Count > 1)
                {
                    var name = nameNode.Text;
                    var valNode = arrow.Children[1];

                    if (!string.IsNullOrEmpty(currentScope))
                    {
                        pending.Add(new PendingConstant($"{currentScope}.{name}", valNode, null));
                    }
                    pending.Add(new PendingConstant(name, valNode, null));
                }
            }
            return;
        }

        foreach (var child in node.Children)
        {
            CollectCSharp(child, currentScope, pending);
        }
    }

    private static void CollectTypeScript(Node node, string? currentScope, List<PendingConstant> pending)
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
                            pending.Add(new PendingConstant(keyWithScope, valNode, valNode == null ? prop.Text : null));
                        }
                    }
                }
            }
            return;
        }

        if (type is TreeSitterSyntax.TypeScript.LexicalDeclaration or TreeSitterSyntax.TypeScript.VariableDeclaration)
        {
            var isConst = node.Children.Any(c => c.Type is "const");
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
                                    pending.Add(new PendingConstant($"{varName}.{cleanKey}", pairValNode, null));
                                }
                            }
                        }
                        else
                        {
                            pending.Add(new PendingConstant(varName, valNode, null));
                        }
                    }
                }
            }
            return;
        }

        foreach (var child in node.Children)
        {
            CollectTypeScript(child, currentScope, pending);
        }
    }

    private static void CollectGo(Node node, string? currentScope, List<PendingConstant> pending)
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
                pending.Add(new PendingConstant(nameNode.Text, valNode, null));
            }
            return;
        }

        foreach (var child in node.Children)
        {
            CollectGo(child, currentScope, pending);
        }
    }

    private static void CollectPython(Node node, string? currentScope, List<PendingConstant> pending)
    {
        var type = node.Type;

        if (type is TreeSitterSyntax.Python.ClassDefinition)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var className = nameNode.IsValid() ? nameNode.Text : currentScope;

            foreach (var child in node.Children)
            {
                CollectPython(child, className, pending);
            }
            return;
        }

        if (type is "assignment")
        {
            var left = node.GetChildForField(TreeSitterSyntax.Fields.Left);
            var right = node.GetChildForField(TreeSitterSyntax.Fields.Right);

            if (left.IsValid() && right.IsValid())
            {
                var varName = left.Text;
                // In Python, conventional constants are UPPERCASE (e.g. ORDER_TOPIC, DB_NAME)
                var isConstNaming = varName.Length >= 2 && varName.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_');

                if (isConstNaming || !string.IsNullOrEmpty(currentScope))
                {
                    if (!string.IsNullOrEmpty(currentScope))
                    {
                        pending.Add(new PendingConstant($"{currentScope}.{varName}", right, null));
                    }
                    pending.Add(new PendingConstant(varName, right, null));
                }
            }
            return;
        }

        foreach (var child in node.Children)
        {
            CollectPython(child, currentScope, pending);
        }
    }

    private static void CollectJava(Node node, string? currentScope, List<PendingConstant> pending)
    {
        var type = node.Type;

        if (type is TreeSitterSyntax.Java.ClassDeclaration or TreeSitterSyntax.Java.EnumDeclaration)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
            var className = nameNode.IsValid() ? nameNode.Text : currentScope;

            foreach (var child in node.Children)
            {
                CollectJava(child, className, pending);
            }
            return;
        }

        if (type is TreeSitterSyntax.Java.FieldDeclaration or TreeSitterSyntax.Java.ConstantDeclaration)
        {
            var isStaticFinal = node.Children.Any(c => c.Type == TreeSitterSyntax.Java.Modifiers &&
                                                      c.Text.Contains("static") && c.Text.Contains("final"));

            var declarators = node.FindChildrenOfType(TreeSitterSyntax.Java.VariableDeclarator);
            foreach (var decl in declarators)
            {
                var nameNode = decl.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                               decl.FindChildOfType(TreeSitterSyntax.Java.Identifier);

                var valNode = decl.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                              (decl.Children.Count > 2 ? decl.Children[2] : null);

                if (nameNode.IsValid() && valNode.IsValid())
                {
                    var name = nameNode.Text;
                    if (!string.IsNullOrEmpty(currentScope))
                    {
                        pending.Add(new PendingConstant($"{currentScope}.{name}", valNode, null));
                    }
                    pending.Add(new PendingConstant(name, valNode, null));
                }
            }
            return;
        }

        foreach (var child in node.Children)
        {
            CollectJava(child, currentScope, pending);
        }
    }

    private static void ResolveTopologicalConstants(List<PendingConstant> pending, string? projectName)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Multi-pass topological resolution
        for (int pass = 0; pass < 10; pass++)
        {
            var progress = false;

            foreach (var item in pending)
            {
                if (resolved.Contains(item.Key)) continue;

                if (item.InitialLiteral != null)
                {
                    ConstantRegistry.Register(projectName, item.Key, item.InitialLiteral);
                    resolved.Add(item.Key);
                    progress = true;
                    continue;
                }

                if (item.ValueNode.IsValid())
                {
                    if (AstValueResolver.TryResolveString(item.ValueNode, projectName, out var val) &&
                        !string.IsNullOrWhiteSpace(val))
                    {
                        ConstantRegistry.Register(projectName, item.Key, val);
                        resolved.Add(item.Key);
                        progress = true;
                    }
                }
            }

            if (!progress) break;
        }

        // Fallback pass: any remaining unresolved constants with clean text
        foreach (var item in pending)
        {
            if (resolved.Contains(item.Key)) continue;

            if (item.ValueNode.IsValid())
            {
                var text = item.ValueNode.Text.Trim();
                if (!text.Contains('\n') && text.Length is > 0 and < 300)
                {
                    var unquoted = AstValueResolver.Unquote(text);
                    if (!string.IsNullOrWhiteSpace(unquoted))
                    {
                        ConstantRegistry.Register(projectName, item.Key, unquoted);
                        resolved.Add(item.Key);
                    }
                }
            }
        }
    }
}
