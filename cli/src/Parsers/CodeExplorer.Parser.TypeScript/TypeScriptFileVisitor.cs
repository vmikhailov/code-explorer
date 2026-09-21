using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptFileVisitor : BaseParserVisitor
{
    private readonly TypeScriptParser _parser;

    public TypeScriptFileVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        TypeScriptParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry) :
        base(rootNode, activeLibraryParsers, relativePath, absoluteWorkspacePath, fileParser, libraryRegistry)
    {
        _parser = parser;

        if (relativePath.Contains("route", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("const", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("config", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("api", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("endpoint", StringComparison.OrdinalIgnoreCase))
        {
            RouteDictionaryRegistry.ScanAndRegister(rootNode.Text);
        }

        // Register a sequence detector rule for CommonJS 'require' statements
        SequenceDetector.Register([
            n => n.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
                 n.GetField(TreeSitterSyntax.Fields.Function)?.Text == "require"
        ], path =>
        {
            var callNode = path[^1];
            var argList = callNode.GetField(TreeSitterSyntax.Fields.Arguments);

            if (argList.IsValid() && argList.Children.Count > 1)
            {
                var firstArg = argList.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.String));

                if (firstArg.IsValid())
                {
                    var importPath = firstArg.Text.Trim('\'', '"');
                    RawImports.Add(new RawImport(importPath, ""));
                    ResolveAndInjectLibraryParser(importPath);
                }
            }
        });
    }

    protected override string? MapNodeType(Node node)
    {
        if (node.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        return node.Type switch
        {
            TreeSitterSyntax.TypeScript.ClassDeclaration or
            TreeSitterSyntax.TypeScript.ClassExpression or
            TreeSitterSyntax.TypeScript.EnumDeclaration => "Class",

            TreeSitterSyntax.TypeScript.InterfaceDeclaration or
            TreeSitterSyntax.TypeScript.TypeAliasDeclaration => "Interface",

            TreeSitterSyntax.TypeScript.MethodDefinition or
            TreeSitterSyntax.TypeScript.FunctionDeclaration or
            TreeSitterSyntax.TypeScript.FunctionExpression or
            TreeSitterSyntax.TypeScript.ArrowFunction =>
                OntologyConstants.NodeLabels.Function,

            _ => null
        };
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (node.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        return ExtractTsIdentifier(node);
    }

    protected override void CollectCustomReferencesForSymbol(
        Node node,
        SyntacticSymbol symbolNode,
        SyntacticSymbol parentNode)
    {
        // If this is a decorator (EntryPoint) on a method, link it to the next method sibling via TRIGGERS
        if (node.Is(TreeSitterSyntax.TypeScript.Decorator) && symbolNode.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var nextNode = GetNextNamedSibling(node);

            if (nextNode.IsValid() && nextNode.IsAny(TreeSitterSyntax.TypeScript.MethodDefinition, TreeSitterSyntax.TypeScript.ClassDeclaration))
            {
                var targetName = ExtractTsIdentifier(nextNode);

                if (!string.IsNullOrEmpty(targetName))
                {
                    symbolNode.References.Add(new Reference("", targetName, "TRIGGERS"));
                }
            }
        }
    }

    private static Node? GetNextNamedSibling(Node node)
    {
        var parent = node.Parent;
        if (!parent.IsValid()) return null;

        var children = parent.Children;
        var idx = -1;

        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].Id == node.Id)
            {
                idx = i;
                break;
            }
        }

        if (idx >= 0)
        {
            for (var i = idx + 1; i < children.Count; i++)
            {
                var sibling = children[i];

                if (sibling.IsAny(TreeSitterSyntax.TypeScript.MethodDefinition, TreeSitterSyntax.TypeScript.ClassDeclaration))
                {
                    return sibling;
                }
            }
        }

        return null;
    }

    private string? ExtractTsIdentifier(Node node)
    {
        if (node.IsAny(TreeSitterSyntax.TypeScript.ArrowFunction, TreeSitterSyntax.TypeScript.FunctionExpression))
        {
            var parent = node.Parent;

            if (parent.IsValid())
            {
                if (parent.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
                {
                    var parentName = parent.GetChildFieldText(TreeSitterSyntax.Fields.Name);
                    if (parentName != null) return parentName;

                    var firstIdent = parent.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));

                    if (firstIdent.IsValid())
                    {
                        return firstIdent.Text;
                    }
                }
                else if (parent.Is(TreeSitterSyntax.TypeScript.AssignmentExpression))
                {
                    var leftText = parent.GetChildFieldText(TreeSitterSyntax.Fields.Left);
                    if (leftText != null) return leftText;
                }
            }
        }

        var nameText = node.GetChildFieldText(TreeSitterSyntax.Fields.Name);
        if (nameText != null) return nameText;

        foreach (var child in node.Children)
        {
            if (child.IsAny(TreeSitterSyntax.TypeScript.Identifier, TreeSitterSyntax.TypeScript.VariableName))
            {
                return child.Text;
            }
        }

        foreach (var child in node.Children)
        {
            if (child.Type.Contains("name"))
            {
                return child.Text;
            }
        }

        return null;
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        CollectVariableAndTypeBindings(node);
        VisitChildren(node, depth);
    }

    protected override void VisitParameter(Node node, int depth)
    {
        var identNode = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));

        if (identNode == null && node.Is(TreeSitterSyntax.TypeScript.ParameterProperty))
        {
            var nestedParam = node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.RequiredParameter, TreeSitterSyntax.TypeScript.OptionalParameter));

            if (nestedParam != null)
            {
                identNode = nestedParam.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));
            }
        }

        var typeAnnotation = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation)) ?? node.Children
            .FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.RequiredParameter, TreeSitterSyntax.TypeScript.OptionalParameter))?.Children
            .FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation));

        if (identNode != null && typeAnnotation != null)
        {
            var typeNode = typeAnnotation.Children.FirstOrDefault(c => !c.Is(TreeSitterSyntax.Symbols.Colon));

            if (typeNode != null)
            {
                var varName = identNode.Text;
                var typeName = typeNode.Text;
                var scopeName = GetContainingScopeName(node);
                RawTypeBindings.Add(new RawTypeBinding(varName, typeName, "", scopeName));
                RawTypeBindings.Add(new RawTypeBinding("this." + varName, typeName, "", scopeName));
            }
        }

        VisitChildren(node, depth);
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        CollectImport(node);
        VisitChildren(node, depth);
    }

    protected override string? FindCallName(Node callNode)
    {
        var expr = callNode.GetFunctionNode();

        if (!expr.IsValid()) return null;

        if (expr.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            return expr.Text;
        }

        if (expr.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var propChild = expr.GetField(TreeSitterSyntax.Fields.Property);
            if (propChild.IsValid()) return propChild.Text;
        }

        return null;
    }

    protected override void VisitInheritanceClause(Node node, int depth)
    {
        var scopeSymbol = SymbolStack.Peek();

        if (scopeSymbol.Kind != "file")
        {
            var kind = node.Is(TreeSitterSyntax.TypeScript.ImplementsClause) ? "IMPLEMENTS" : "INHERITS_FROM";

            foreach (var child in node.Children)
            {
                if (child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    SymbolStack.Peek().References.Add(new Reference("", child.Text, kind));
                }
            }
        }

        VisitChildren(node, depth);
    }

    private void CollectImport(Node node)
    {
        var sourceNode = node.GetField(TreeSitterSyntax.Fields.Source);

        if (!sourceNode.IsValid())
        {
            sourceNode = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.String));
        }

        if (sourceNode.IsValid())
        {
            var importPath = sourceNode.Text.Trim('\'', '"');
            RawImports.Add(new RawImport(importPath, "", ImportType.External));
            ResolveAndInjectLibraryParser(importPath);
        }
    }

    private void CollectVariableAndTypeBindings(Node node)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));
            var name = nameNode?.Text;

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetField(TreeSitterSyntax.Fields.Value);
                var initializerText = valueNode.IsValid() ? valueNode.Text : "";
                var isConstant = IsTypeScriptConstant(node);
                var scope = DetermineTypeScriptScope(node);

                RawVariables.Add(new RawVariable(name, initializerText, scope, isConstant, "", node.StartPosition.Row,
                    node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column));

                var typeAnnotation = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation)) ??
                                     nameNode?.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation));
                string? typeName = null;

                if (typeAnnotation != null)
                {
                    var typeNode = typeAnnotation.Children.FirstOrDefault(c => !c.Is(TreeSitterSyntax.Symbols.Colon));
                    if (typeNode != null) typeName = typeNode.Text;
                }
                else if (valueNode.IsValid() && valueNode.Is(TreeSitterSyntax.TypeScript.NewExpression))
                {
                    var constructorNode = valueNode.GetField(TreeSitterSyntax.Fields.Constructor);

                    if (constructorNode.IsValid())
                    {
                        typeName = constructorNode.Text;
                    }
                }

                if (!string.IsNullOrEmpty(typeName))
                {
                    var scopeName = GetContainingScopeName(node);
                    RawTypeBindings.Add(new RawTypeBinding(name, typeName, "", scopeName));
                }
            }
        }
        else if (node.IsAny(TreeSitterSyntax.TypeScript.PublicFieldDefinition, TreeSitterSyntax.TypeScript.PropertyDefinition))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.PropertyIdentifier));
            var name = nameNode?.Text;

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetField(TreeSitterSyntax.Fields.Value);
                var initializerText = valueNode.IsValid() ? valueNode.Text : "";
                var isConstant = false;
                var scope = "class";

                RawVariables.Add(new RawVariable(name, initializerText, scope, isConstant, "", node.StartPosition.Row,
                    node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column));

                var typeAnnotation = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation));

                if (typeAnnotation != null)
                {
                    var typeNode = typeAnnotation.Children.FirstOrDefault(c => !c.Is(TreeSitterSyntax.Symbols.Colon));

                    if (typeNode != null)
                    {
                        var scopeName = GetContainingScopeName(node);
                        RawTypeBindings.Add(new RawTypeBinding(name, typeNode.Text, "", scopeName));
                        RawTypeBindings.Add(new RawTypeBinding("this." + name, typeNode.Text, "", scopeName));
                    }
                }
            }
        }
    }

    private static bool IsTypeScriptConstant(Node node)
    {
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.LexicalDeclaration))
            {
                return curr.Text.StartsWith("const");
            }

            curr = curr.Parent;
        }

        return false;
    }

    private static string DetermineTypeScriptScope(Node node)
    {
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.InterfaceDeclaration))
                return "class";

            if (curr.IsAny(TreeSitterSyntax.TypeScript.FunctionDeclaration, TreeSitterSyntax.TypeScript.ArrowFunction,
                           TreeSitterSyntax.TypeScript.MethodDefinition, TreeSitterSyntax.TypeScript.StatementBlock))
                return "local";

            curr = curr.Parent;
        }

        return "global";
    }

    private static string GetContainingScopeName(Node node)
    {
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.InterfaceDeclaration))
            {
                var nameText = curr.GetChildFieldText(TreeSitterSyntax.Fields.Name);
                if (nameText != null) return nameText;
            }
            else if (curr.IsAny(TreeSitterSyntax.TypeScript.FunctionDeclaration, TreeSitterSyntax.TypeScript.MethodDefinition))
            {
                var nameText = curr.GetChildFieldText(TreeSitterSyntax.Fields.Name);

                if (nameText != null)
                {
                    var nameTextStr = nameText;

                    if (nameTextStr == "constructor")
                    {
                        var classNode = curr.Parent;

                        while (classNode.IsValid())
                        {
                            if (classNode.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.InterfaceDeclaration))
                            {
                                var classNameText = classNode.GetChildFieldText(TreeSitterSyntax.Fields.Name);
                                if (classNameText != null) return classNameText;
                            }

                            classNode = classNode.Parent;
                        }
                    }

                    return nameTextStr;
                }
            }

            curr = curr.Parent;
        }

        return "global";
    }
}
