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
        List<ISemanticExtension> activeExtensions,
        TypeScriptParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry) :
        base(rootNode, activeExtensions, relativePath, absoluteWorkspacePath, fileParser, extensionRegistry)
    {
        _parser = parser;

        if (relativePath.Contains("route", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("const", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("config", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("api", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("endpoint", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("env", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("url", StringComparison.OrdinalIgnoreCase))
        {
            RouteDictionaryRegistry.ScanAndRegister(rootNode.Text);
        }

        // CommonJS require statements are handled in VisitCallExpression for optimal performance

    }

    protected override string? MapNodeType(Node node)
    {
        if (!WorkspaceConventions.IsFrontendFilePath(RelativePath) &&
            node.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString))
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
        if (!WorkspaceConventions.IsFrontendFilePath(RelativePath) &&
            node.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString))
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

    /// <summary>
    /// Name of the nearest declaration that owns an object literal containing <paramref name="pair"/>:
    /// a class field, variable or outer object key. Stops at block/class/program boundaries.
    /// </summary>
    private static string? FindOwningDeclarationName(Node pair)
    {
        var current = pair.Parent;
        for (var hops = 0; current.IsValid() && hops < 12; hops++, current = current.Parent)
        {
            if (current.IsAny(TreeSitterSyntax.TypeScript.ClassBody, TreeSitterSyntax.TypeScript.StatementBlock,
                    TreeSitterSyntax.TypeScript.Program))
            {
                return null;
            }

            if (current.IsAny(TreeSitterSyntax.TypeScript.PublicFieldDefinition, TreeSitterSyntax.TypeScript.PropertyDefinition,
                    TreeSitterSyntax.TypeScript.VariableDeclarator))
            {
                return current.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            }

            if (current.Id != pair.Parent?.Id && current.Is(TreeSitterSyntax.TypeScript.Pair))
            {
                return current.GetChildFieldText(TreeSitterSyntax.Fields.Key)?.Trim('\'', '"', '`');
            }

            if (current.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var pothosField = TryExtractPothosFieldName(current);
                if (!string.IsNullOrEmpty(pothosField))
                {
                    return pothosField;
                }
            }
        }

        return null;
    }

    internal static string? TryExtractPothosFieldName(Node callNode)
    {
        var fnNode = callNode.GetFunctionNode();
        if (!fnNode.IsValid() || !fnNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            return null;
        }

        var propNode = fnNode.GetField(TreeSitterSyntax.Fields.Property);
        if (!propNode.IsValid()) return null;

        var method = propNode.Text;
        if (method is not ("queryField" or "mutationField" or "subscriptionField" or "objectField"))
        {
            return null;
        }

        var argsNode = callNode.GetField(TreeSitterSyntax.Fields.Arguments);
        if (!argsNode.IsValid()) return null;

        var stringArgs = new List<string>();
        foreach (var child in argsNode.Children)
        {
            if (child.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString))
            {
                var val = child.Text.Trim('\'', '"', '`');
                if (!string.IsNullOrEmpty(val))
                {
                    stringArgs.Add(val);
                }
            }
        }

        if (stringArgs.Count == 0) return null;
        return method == "objectField" && stringArgs.Count >= 2 ? stringArgs[1] : stringArgs[0];
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
                // Class field initialised with a function: `load = (id) => this.http.get(...)`.
                else if (parent.IsAny(TreeSitterSyntax.TypeScript.PublicFieldDefinition, TreeSitterSyntax.TypeScript.PropertyDefinition))
                {
                    var fieldName = parent.GetChildFieldText(TreeSitterSyntax.Fields.Name);
                    if (!string.IsNullOrEmpty(fieldName)) return fieldName;
                }
                // Function as an object-literal property value: `rxResource({ loader: () => http.get(...) })`,
                // NgRx/React Query option objects, handler maps. Qualified with the declaration that owns
                // the object literal (`plansResource.loader`) so sibling resources in one class stay distinct.
                else if (parent.Is(TreeSitterSyntax.TypeScript.Pair))
                {
                    var key = parent.GetChildFieldText(TreeSitterSyntax.Fields.Key)?.Trim('\'', '"', '`');
                    if (!string.IsNullOrEmpty(key))
                    {
                        var owner = FindOwningDeclarationName(parent);
                        return owner != null ? $"{owner}.{key}" : key;
                    }
                }
            }

            // An unnamed arrow (e.g. a `map`/`subscribe` callback) is not a declaration: it must not become a
            // Function named after its first parameter (`ids.map(id => ...)` used to yield `Function:id`).
            // Its calls stay attributed to the enclosing function/method.
            if (node.Is(TreeSitterSyntax.TypeScript.ArrowFunction))
            {
                return null;
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
        base.VisitVariableDeclaration(node, depth);
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
            var objChild = expr.GetField(TreeSitterSyntax.Fields.Object);
            if (propChild.IsValid())
            {
                if (objChild.IsValid())
                {
                    var unwrappedObj = UnwrapExpression(objChild);
                    if (unwrappedObj.IsAny(TreeSitterSyntax.TypeScript.CallExpression, TreeSitterSyntax.TypeScript.NewExpression))
                    {
                        var genericType = ExtractFirstGenericTypeArgument(unwrappedObj);
                        if (!string.IsNullOrEmpty(genericType))
                        {
                            return $"{genericType}.{propChild.Text}";
                        }

                        if (unwrappedObj.Is(TreeSitterSyntax.TypeScript.NewExpression))
                        {
                            var ctorNode = unwrappedObj.GetField(TreeSitterSyntax.Fields.Constructor);
                            if (ctorNode.IsValid() && !string.IsNullOrEmpty(ctorNode.Text))
                            {
                                return $"{ctorNode.Text}.{propChild.Text}";
                            }
                        }
                    }

                    return $"{objChild.Text}.{propChild.Text}";
                }
                return propChild.Text;
            }
        }

        return null;
    }

    private static Node UnwrapExpression(Node node)
    {
        var curr = node;
        for (var i = 0; i < 6 && curr.IsValid(); i++)
        {
            if (curr.IsAny("parenthesized_expression", "await_expression", "non_null_expression", "as_expression"))
            {
                var inner = curr.Children.FirstOrDefault(c =>
                    c.IsNamed &&
                    !c.Is("type_identifier") &&
                    !c.Is("predefined_type"));
                if (inner.IsValid())
                {
                    curr = inner;
                    continue;
                }
            }
            break;
        }
        return curr;
    }

    internal static string? ExtractFirstGenericTypeArgument(Node callOrNewNode)
    {
        var typeArgsNode = callOrNewNode.GetField("type_arguments");
        if (!typeArgsNode.IsValid())
        {
            typeArgsNode = callOrNewNode.FindChildOfType("type_arguments");
        }
        if (!typeArgsNode.IsValid()) return null;

        return ExtractPrimaryTypeNameFromTypeArguments(typeArgsNode);
    }

    private static string? ExtractPrimaryTypeNameFromTypeArguments(Node typeArgsNode)
    {
        foreach (var child in typeArgsNode.Children)
        {
            if (!child.IsNamed) continue;

            if (child.IsAny("type_identifier", TreeSitterSyntax.TypeScript.Identifier, "nested_type_identifier"))
            {
                return child.Text;
            }

            if (child.Is("generic_type"))
            {
                var nameNode = child.GetField(TreeSitterSyntax.Fields.Name) ??
                               child.Children.FirstOrDefault(c => c.IsAny("type_identifier", TreeSitterSyntax.TypeScript.Identifier));
                var outerName = nameNode.IsValid() ? nameNode.Text : null;
                if (outerName is "Promise" or "Readonly" or "NonNullable" or "Awaited")
                {
                    var innerArgs = child.FindChildOfType("type_arguments");
                    if (innerArgs.IsValid())
                    {
                        var unwrapped = ExtractPrimaryTypeNameFromTypeArguments(innerArgs);
                        if (!string.IsNullOrEmpty(unwrapped)) return unwrapped;
                    }
                }
                if (!string.IsNullOrEmpty(outerName))
                {
                    return outerName;
                }
            }
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
            var specifiers = ExtractImportSpecifiers(node);
            RawImports.Add(new RawImport(importPath, "", ImportType.External, specifiers));
            ResolveAndInjectLibraryParser(importPath);
        }
    }

    private static List<ImportSpecifier> ExtractImportSpecifiers(Node node)
    {
        var specifiers = new List<ImportSpecifier>();

        // 1. import_clause
        var clause = node.FindChildOfType("import_clause");
        if (clause.IsValid())
        {
            foreach (var child in clause.Children)
            {
                if (child.Is(TreeSitterSyntax.TypeScript.Identifier))
                {
                    specifiers.Add(new ImportSpecifier(child.Text, "default", IsDefault: true));
                }
                else if (child.Is("namespace_import"))
                {
                    var id = child.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);
                    if (id.IsValid())
                    {
                        specifiers.Add(new ImportSpecifier(id.Text, "*", IsNamespace: true));
                    }
                }
                else if (child.Is("named_imports"))
                {
                    foreach (var spec in child.FindChildrenOfType("import_specifier"))
                    {
                        var nameNode = spec.GetField(TreeSitterSyntax.Fields.Name);
                        var aliasNode = spec.GetField("alias");

                        if (nameNode.IsValid())
                        {
                            var impName = nameNode.Text;
                            var locName = aliasNode.IsValid() ? aliasNode.Text : impName;
                            specifiers.Add(new ImportSpecifier(locName, impName));
                        }
                        else
                        {
                            var idents = spec.FindChildrenOfType(TreeSitterSyntax.TypeScript.Identifier).ToList();
                            if (idents.Count == 1)
                            {
                                specifiers.Add(new ImportSpecifier(idents[0].Text, idents[0].Text));
                            }
                            else if (idents.Count >= 2)
                            {
                                specifiers.Add(new ImportSpecifier(idents[1].Text, idents[0].Text));
                            }
                        }
                    }
                }
            }
            return specifiers;
        }

        // 2. import_require_clause
        var reqClause = node.FindChildOfType("import_require_clause");
        if (reqClause.IsValid())
        {
            var id = reqClause.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);
            if (id.IsValid())
            {
                specifiers.Add(new ImportSpecifier(id.Text, "default", IsDefault: true));
            }
            return specifiers;
        }

        // 3. export_statement with source (e.g. export { a, b as c } from './b')
        var exportClause = node.FindChildOfType("export_clause");
        if (exportClause.IsValid())
        {
            foreach (var spec in exportClause.FindChildrenOfType("export_specifier"))
            {
                var nameNode = spec.GetField(TreeSitterSyntax.Fields.Name);
                var aliasNode = spec.GetField("alias");

                if (nameNode.IsValid())
                {
                    var impName = nameNode.Text;
                    var locName = aliasNode.IsValid() ? aliasNode.Text : impName;
                    specifiers.Add(new ImportSpecifier(locName, impName));
                }
                else
                {
                    var idents = spec.FindChildrenOfType(TreeSitterSyntax.TypeScript.Identifier).ToList();
                    if (idents.Count == 1)
                    {
                        specifiers.Add(new ImportSpecifier(idents[0].Text, idents[0].Text));
                    }
                    else if (idents.Count >= 2)
                    {
                        specifiers.Add(new ImportSpecifier(idents[1].Text, idents[0].Text));
                    }
                }
            }
            return specifiers;
        }

        // 4. export * from './b'
        if (node.Children.Any(c => c.Type == "*"))
        {
            specifiers.Add(new ImportSpecifier("*", "*", IsNamespace: true));
        }

        return specifiers;
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

                RawVariables.Add(new RawVariable(name, initializerText, scope, isConstant, "", node.StartPosition.Row + 1,
                    node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1));

                var typeAnnotation = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation)) ??
                                     nameNode?.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation));
                string? typeName = null;

                if (typeAnnotation != null)
                {
                    var typeNode = typeAnnotation.Children.FirstOrDefault(c => !c.Is(TreeSitterSyntax.Symbols.Colon));
                    if (typeNode != null) typeName = typeNode.Text;
                }
                else if (valueNode.IsValid())
                {
                    typeName = TryInferTypeFromInitializer(valueNode);
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

                RawVariables.Add(new RawVariable(name, initializerText, scope, isConstant, "", node.StartPosition.Row + 1,
                    node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1));

                var typeAnnotation = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation));
                string? fieldTypeName = null;

                if (typeAnnotation != null)
                {
                    var typeNode = typeAnnotation.Children.FirstOrDefault(c => !c.Is(TreeSitterSyntax.Symbols.Colon));
                    if (typeNode != null) fieldTypeName = typeNode.Text;
                }
                else if (valueNode.IsValid())
                {
                    fieldTypeName = TryInferTypeFromInitializer(valueNode);
                }

                if (!string.IsNullOrEmpty(fieldTypeName))
                {
                    var scopeName = GetContainingScopeName(node);
                    RawTypeBindings.Add(new RawTypeBinding(name, fieldTypeName, "", scopeName));
                }
            }
        }
    }

    private string? TryInferTypeFromInitializer(Node valueNode)
    {
        var unwrappedVal = UnwrapExpression(valueNode);
        if (!unwrappedVal.IsAny(TreeSitterSyntax.TypeScript.CallExpression, TreeSitterSyntax.TypeScript.NewExpression))
        {
            return null;
        }

        var typeName = ExtractFirstGenericTypeArgument(unwrappedVal);
        if (string.IsNullOrEmpty(typeName) && unwrappedVal.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var constructorNode = unwrappedVal.GetField(TreeSitterSyntax.Fields.Constructor);
            if (constructorNode.IsValid())
            {
                typeName = constructorNode.Text;
            }
        }

        if (string.IsNullOrEmpty(typeName) && unwrappedVal.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var callFn = unwrappedVal.GetField(TreeSitterSyntax.Fields.Function);
            if (callFn.IsValid() && callFn.Text == "inject")
            {
                var args = unwrappedVal.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
                if (args.IsValid())
                {
                    var firstArg = args.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));
                    if (firstArg.IsValid())
                    {
                        typeName = firstArg.Text;
                    }
                }
            }
        }

        return typeName;
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

    private string GetContainingScopeName(Node node)
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
            else if (curr.IsAny(TreeSitterSyntax.TypeScript.ArrowFunction, TreeSitterSyntax.TypeScript.FunctionExpression))
            {
                var arrowName = ExtractTsIdentifier(curr);
                if (!string.IsNullOrEmpty(arrowName))
                {
                    return arrowName;
                }
            }

            curr = curr.Parent;
        }

        return "global";
    }

    protected override void VisitCallExpression(Node node, int depth)
    {
        if (node.GetField(TreeSitterSyntax.Fields.Function)?.Text == "require")
        {
            HandleRequireCall(node);
        }
        base.VisitCallExpression(node, depth);
    }

    private void HandleRequireCall(Node callNode)
    {
        var argList = callNode.GetField(TreeSitterSyntax.Fields.Arguments);
        if (argList.IsValid() && argList.Children.Count > 1)
        {
            var firstArg = argList.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.String));
            if (firstArg.IsValid())
            {
                var importPath = firstArg.Text.Trim('\'', '"');
                var specifiers = new List<ImportSpecifier>();

                var declarator = callNode.Parent;
                if (declarator.IsValid() && declarator.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
                {
                    var nameField = declarator.GetField(TreeSitterSyntax.Fields.Name);
                    if (nameField.IsValid())
                    {
                        if (nameField.Is(TreeSitterSyntax.TypeScript.Identifier))
                        {
                            specifiers.Add(new ImportSpecifier(nameField.Text, "default", IsDefault: true));
                        }
                        else if (nameField.Type == "object_pattern")
                        {
                            foreach (var prop in nameField.FindChildrenOfType("shorthand_property_identifier_pattern"))
                            {
                                specifiers.Add(new ImportSpecifier(prop.Text, prop.Text));
                            }
                        }
                    }
                }

                RawImports.Add(new RawImport(importPath, "", ImportType.External, specifiers));
                ResolveAndInjectLibraryParser(importPath);
            }
        }
    }

}
