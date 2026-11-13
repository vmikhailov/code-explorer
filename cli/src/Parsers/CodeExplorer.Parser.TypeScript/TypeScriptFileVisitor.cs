using CodeExplorer.Parser.TypeScript.Libraries;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptFileVisitor : BaseParserVisitor
{
    public TypeScriptFileVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry) :
        base(rootNode, activeExtensions, relativePath, absoluteWorkspacePath, fileParser, extensionRegistry)
    {
    }

    protected override string? MapNodeType(Node node)
    {
        if (TryMapSqlQueryNodeType(node) is { } sqlKind)
        {
            return sqlKind;
        }

        return node switch
        {
            CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Class or CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Enum } => "Class",

            CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Interface or CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.TypeAlias } => "Interface",

            CodeExplorer.Core.Parser.Syntax.FunctionDeclSyntax or CodeExplorer.Core.Parser.Syntax.LambdaExprSyntax =>
                OntologyConstants.NodeLabels.Function,

            _ => null
        };
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (TryExtractSqlQueryIdentifier(node) is { } sqlId)
        {
            return sqlId;
        }

        return ExtractTsIdentifier(node);
    }

    protected override void CollectCustomReferencesForSymbol(
        Node node,
        SyntacticSymbol symbolNode,
        SyntacticSymbol parentNode)
    {
        // If this is a decorator (EntryPoint) on a method, link it to the next method sibling via TRIGGERS
        if (node.Is(TreeSitterSyntax.TypeScript.Decorator) && symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint)
        {
            var nextNode = GetNextNamedSibling(node);

            if (nextNode.IsValid() && nextNode.IsAny(TreeSitterSyntax.TypeScript.MethodDefinition, TreeSitterSyntax.TypeScript.ClassDeclaration))
            {
                var targetName = ExtractTsIdentifier(nextNode);

                if (!string.IsNullOrEmpty(targetName))
                {
                    var line = (int)node.StartPosition.Row + 1;
                    var col = (int)node.StartPosition.Column + 1;
                    symbolNode.References.Add(new Reference(string.Empty, targetName, OntologyConstants.Relationships.Triggers, line, col, argFlow: RelativePath));
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.CallExpression) && symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint)
        {
            ExtractExpressRouteReferences(node, symbolNode, parentNode);
        }
        else if (symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function &&
                 parentNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function &&
                 !string.IsNullOrEmpty(symbolNode.Name) &&
                 !parentNode.References.Any(r => r.TargetName == symbolNode.Name && r.Kind == RelationshipKind.Calls))
        {
            var line = (int)node.StartPosition.Row + 1;
            var col = (int)node.StartPosition.Column + 1;
            parentNode.References.Add(new Reference("", symbolNode.Name, OntologyConstants.Relationships.Calls, line, col, argFlow: RelativePath));
        }
    }

    private static void ExtractExpressRouteReferences(Node callNode, SyntacticSymbol symbolNode, SyntacticSymbol parentNode)
    {
        var args = AstHelper.GetCallArguments(callNode);
        if (args.Count == 0) return;

        var startIndex = 0;
        if (AstHelper.IsStringLiteralNode(args[0]) || args[0].Type.Contains("string") || args[0].Type.Contains("regex"))
        {
            startIndex = 1;
        }

        for (var i = startIndex; i < args.Count; i++)
        {
            ExtractHandlerReferencesFromArg(args[i], symbolNode, parentNode);
        }
    }

    private static void ExtractHandlerReferencesFromArg(Node arg, SyntacticSymbol symbolNode, SyntacticSymbol parentNode)
    {
        if (!arg.IsValid()) return;

        if (arg.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var name = arg.Text;
            if (!string.IsNullOrEmpty(name) && !IsNoiseHandlerName(name))
            {
                var line = (int)arg.StartPosition.Row + 1;
                var col = (int)arg.StartPosition.Column + 1;
                if (!parentNode.References.Any(r => r.TargetName == name && r.Kind == RelationshipKind.Calls))
                {
                    parentNode.References.Add(new Reference("", name, OntologyConstants.Relationships.Calls, line, col));
                }
                if (!symbolNode.References.Any(r => r.TargetName == name && r.Kind == RelationshipKind.Triggers))
                {
                    symbolNode.References.Add(new Reference(symbolNode.Name, name, OntologyConstants.Relationships.Triggers, line, col));
                }
            }
        }
        else if (arg.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var name = arg.Text;
            if (!string.IsNullOrEmpty(name))
            {
                var line = (int)arg.StartPosition.Row + 1;
                var col = (int)arg.StartPosition.Column + 1;
                if (!parentNode.References.Any(r => r.TargetName == name && r.Kind == RelationshipKind.Calls))
                {
                    parentNode.References.Add(new Reference("", name, OntologyConstants.Relationships.Calls, line, col));
                }
                if (!symbolNode.References.Any(r => r.TargetName == name && r.Kind == RelationshipKind.Triggers))
                {
                    symbolNode.References.Add(new Reference(symbolNode.Name, name, OntologyConstants.Relationships.Triggers, line, col));
                }
            }
        }
        else if (arg is CodeExplorer.Core.Parser.Syntax.CollectionExprSyntax { Kind: CodeExplorer.Core.Parser.Syntax.CollectionSyntaxKind.Array })
        {
            foreach (var child in arg.Children)
            {
                if (child.IsValid() && (char.IsLetter(child.Type[0]) || child.Type[0] == '_'))
                {
                    ExtractHandlerReferencesFromArg(child, symbolNode, parentNode);
                }
            }
        }
    }

    private static bool IsNoiseHandlerName(string name) =>
        name is "req" or "res" or "next" or "err" or "error" or "undefined" or "null" or "true" or "false";

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
        if (node.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
        {
            VisitSymbolOrBase(node, depth);
        }
        else
        {
            VisitChildren(node, depth);
        }
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
                    var directReqCall = TypeScriptImportAndCallbackHelper.TryExtractDirectRequireMemberCall(unwrappedObj, propChild.Text);
                    if (!string.IsNullOrEmpty(directReqCall))
                    {
                        return directReqCall;
                    }

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

                    var recvPath = objChild.GetCanonicalMemberPath();
                    return string.IsNullOrEmpty(recvPath) ? propChild.Text : $"{recvPath}.{propChild.Text}";
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

        if (scopeSymbol.Kind != CodeExplorer.Cypher.Common.SyntacticSymbolKind.File)
        {
            var kind = node.Is(TreeSitterSyntax.TypeScript.ImplementsClause) ? "IMPLEMENTS" : "INHERITS_FROM";

            foreach (var child in node.Children)
            {
                if (child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    SymbolStack.Peek().References.Add(new Reference("", child.Text, kind));
                }
                else if (child is CodeExplorer.Core.Parser.Syntax.CallExprSyntax callChild)
                {
                    var calleeName = callChild.Callee is CodeExplorer.Core.Parser.Syntax.IdentifierExprSyntax id
                        ? id.Text
                        : (callChild.Callee is CodeExplorer.Core.Parser.Syntax.MemberAccessExprSyntax mem
                            ? mem.MemberName
                            : callChild.MethodName);
                    if (!string.IsNullOrEmpty(calleeName))
                    {
                        SymbolStack.Peek().References.Add(new Reference("", calleeName, kind));
                    }
                }
                else if (child is CodeExplorer.Core.Parser.Syntax.TypeRefSyntax { Kind: CodeExplorer.Core.Parser.Syntax.TypeRefKind.Generic } genericChild)
                {
                    var firstId = genericChild.FirstChildOfType<CodeExplorer.Core.Parser.Syntax.IdentifierExprSyntax>();
                    if (firstId != null && !string.IsNullOrEmpty(firstId.Text))
                    {
                        SymbolStack.Peek().References.Add(new Reference("", firstId.Text, kind));
                    }
                }
            }
        }

        VisitChildren(node, depth);
    }

    protected override void VisitCallExpression(Node node, int depth)
    {
        if (node is CodeExplorer.Core.Parser.Syntax.CallExprSyntax { MethodName: "require" } ||
            (node.GetField(TreeSitterSyntax.Fields.Function) is { } fn && fn.IsValid() && fn.AsSpan() is "require"))
        {
            TypeScriptImportAndCallbackHelper.ExtractCommonJsRequireImport(
                node,
                RawImports,
                ResolveAndInjectLibraryParser);
        }

        base.VisitCallExpression(node, depth);
        if (SymbolStack.Count > 0)
        {
            TypeScriptImportAndCallbackHelper.CollectCallbackArgumentReferences(node, SymbolStack.Peek());
        }
    }

    protected override void VisitDefault(Node node, int depth)
    {
        if (node is CodeExplorer.Core.Parser.Syntax.UiElementSyntax { IsClosing: false, IsFragment: false })
        {
            ExtractJsxComponentCall(node);
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.AssignmentExpression))
        {
            TypeScriptImportAndCallbackHelper.CollectCommonJsExportAliases(node, RawImports);
        }

        base.VisitDefault(node, depth);
    }

    private void ExtractJsxComponentCall(Node node)
    {
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid())
        {
            nameNode = node.Children.FirstOrDefault(c =>
                c is CodeExplorer.Core.Parser.Syntax.IdentifierExprSyntax or CodeExplorer.Core.Parser.Syntax.MemberAccessExprSyntax);
        }

        if (!nameNode.IsValid()) return;

        var tagName = nameNode.Text;
        if (string.IsNullOrEmpty(tagName)) return;

        // Custom React/Vue components start with uppercase letter
        // (DOM elements like <div>, <span>, <button> start with lowercase)
        if (!char.IsUpper(tagName[0]) || tagName is "Fragment" or "StrictMode" or "React.Fragment" or "React.StrictMode")
        {
            return;
        }

        var line = (int)nameNode.StartPosition.Row + 1;
        var col = (int)nameNode.StartPosition.Column + 1;
        var currentScope = SymbolStack.Peek();

        if (currentScope.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint)
        {
            if (!currentScope.References.Any(r => r.TargetName == tagName && r.Kind == RelationshipKind.Triggers))
            {
                currentScope.References.Add(new Reference(currentScope.Name, tagName, OntologyConstants.Relationships.Triggers, line, col));
            }
            if (!currentScope.References.Any(r => r.TargetName == tagName && r.Kind == RelationshipKind.Calls))
            {
                currentScope.References.Add(new Reference("", tagName, OntologyConstants.Relationships.Calls, line, col));
            }

            // Also attach to RootSymbol so the entry file (e.g. index.tsx) has a CALLS edge to the component
            if (!RootSymbol.References.Any(r => r.TargetName == tagName && r.Kind == RelationshipKind.Calls))
            {
                RootSymbol.References.Add(new Reference("", tagName, OntologyConstants.Relationships.Calls, line, col));
            }
        }
        else
        {
            if (!currentScope.References.Any(r => r.TargetName == tagName && r.Kind == RelationshipKind.Calls))
            {
                currentScope.References.Add(new Reference("", tagName, OntologyConstants.Relationships.Calls, line, col));
            }
        }
    }

    private void CollectImport(Node node) =>
        TypeScriptImportAndCallbackHelper.CollectImportOrExportStatement(
            node,
            RawImports,
            ResolveAndInjectLibraryParser);

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
                var initializerText = valueNode.IsValid() ? valueNode.GetBoundedInitializerText() : "";
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
            var name = nameNode?.Text ?? (node as CodeExplorer.Core.Parser.Syntax.MemberDeclSyntax)?.Name;

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetField(TreeSitterSyntax.Fields.Value);
                var initializerText = valueNode.IsValid() ? valueNode.GetBoundedInitializerText() : "";
                var isConstant = false;
                var scope = "class";

                var typeAnnotation = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TypeAnnotation));
                string? fieldTypeName = null;

                if (typeAnnotation != null)
                {
                    var typeNode = typeAnnotation.Children.FirstOrDefault(c => !c.Is(TreeSitterSyntax.Symbols.Colon));
                    if (typeNode != null) fieldTypeName = typeNode.Text;
                }
                else if (node is CodeExplorer.Core.Parser.Syntax.MemberDeclSyntax { MemberType: { } mType } && mType.IsValid())
                {
                    fieldTypeName = mType.Text.TrimStart(':').Trim();
                }
                else if (valueNode.IsValid())
                {
                    fieldTypeName = TryInferTypeFromInitializer(valueNode);
                }

                RawVariables.Add(new RawVariable(
                    name,
                    initializerText,
                    scope,
                    isConstant,
                    "",
                    node.StartPosition.Row + 1,
                    node.EndPosition.Row + 1,
                    node.StartPosition.Column + 1,
                    node.EndPosition.Column + 1,
                    fieldTypeName,
                    CodeExplorer.Cypher.Common.MemberKind.Property));

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
                return curr.AsSpan().StartsWith("const", StringComparison.Ordinal);
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
}
