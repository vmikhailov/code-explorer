using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp;

public class CSharpFileVisitor : BaseParserVisitor
{
    public CSharpFileVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
        : base(rootNode, activeExtensions, relativePath, absoluteWorkspacePath, fileParser, extensionRegistry)
    {
        if (rootNode.IsValid() && !WorkspaceConventions.IsTestFilePath(relativePath))
        {
            var firstGlobal = rootNode.Children.FirstOrDefault(c =>
                c is CodeExplorer.Core.Parser.Syntax.SyntaxList { Kind: CodeExplorer.Core.Parser.Syntax.SyntaxListKind.TopLevelStatement });
            if (firstGlobal.IsValid())
            {
                var fileName = Path.GetFileName(relativePath);
                RootSymbol.Children.Add(new SyntacticSymbol(OntologyConstants.NodeLabels.EntryPoint, $"top-level:{fileName}", firstGlobal) { Text = firstGlobal.Text });
            }
        }
    }

    protected override string? MapNodeType(Node node)
    {
        if (node.Is(TreeSitterSyntax.CSharp.Attribute))
        {
            var nameNode = node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
            if (nameNode.IsValid())
            {
                var nameSpan = nameNode.AsSpan();
                if (nameSpan is "Route" or "RoutePrefix" or "Get" or "Post" or "Put" or "Delete" or "Patch" or "Head" or "Options" ||
                    nameSpan.StartsWith("Http", StringComparison.Ordinal))
                {
                    var parentDecl = node.Parent?.Parent;
                    if (parentDecl.IsValid() && parentDecl.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
                    {
                        return null;
                    }
                    var current = parentDecl?.Parent;
                    while (current.IsValid())
                    {
                        if (current.Is(TreeSitterSyntax.CSharp.InterfaceDeclaration))
                        {
                            return OntologyConstants.NodeLabels.ExternalService;
                        }
                        if (current.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
                            break;
                        current = current.Parent;
                    }
                    return OntologyConstants.NodeLabels.EntryPoint;
                }
            }
        }

        if (IsHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (IsGrpcClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (IsInsideInterpolatedString(node))
        {
            return null;
        }

        if (TryMapSqlQueryNodeType(node) is { } sqlKind)
        {
            return sqlKind;
        }

        if (node.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            return "Class";
        if (node.Is(TreeSitterSyntax.CSharp.InterfaceDeclaration))
            return "Interface";
        if (node.IsAny(TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.Common.FunctionDeclaration, TreeSitterSyntax.CSharp.ConstructorDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement))
            return OntologyConstants.NodeLabels.Function;

        return null;
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (node.Is(TreeSitterSyntax.CSharp.Attribute))
        {
            var parentDecl = node.Parent?.Parent;
            if (parentDecl.IsValid() && parentDecl.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            {
                return null;
            }
            if (Libraries.RefitLibraryParser.IsRefitMethodAttribute(node))
            {
                var refitTarget = Libraries.RefitLibraryParser.ExtractRefitTarget(node);
                if (!string.IsNullOrEmpty(refitTarget))
                {
                    return refitTarget;
                }
            }
            return Libraries.AspNetCoreLibraryParser.ExtractRoute(node) ?? ExtractCSharpAttributeRoute(node);
        }

        if (IsHttpClientCall(node))
        {
            return ExtractHttpClientTarget(node);
        }

        if (IsGrpcClientCall(node))
        {
            return ExtractGrpcClientTarget(node);
        }

        if (IsInsideInterpolatedString(node))
        {
            return null;
        }

        if (TryExtractSqlQueryIdentifier(node) is { } sqlId)
        {
            return sqlId;
        }

        return ExtractCsIdentifier(node);
    }

    private static bool IsHttpClientCall(Node node) => Libraries.HttpClientLibraryParser.IsHttpClientCall(node);

    private static string? ExtractHttpClientTarget(Node node) => Libraries.HttpClientLibraryParser.ExtractTarget(node);

    private static bool IsGrpcClientCall(Node node) => Libraries.GrpcCSharpLibraryParser.IsGrpcClientCall(node);

    private static string? ExtractGrpcClientTarget(Node node) => Libraries.GrpcCSharpLibraryParser.ExtractClientTarget(node);

    private static string? ExtractCSharpAttributeRoute(Node attributeNode) => Libraries.AspNetCoreLibraryParser.ExtractRoute(attributeNode);

    protected override void CollectCustomReferencesForSymbol(
        Node node,
        SyntacticSymbol symbolNode,
        SyntacticSymbol parentNode)
    {
        if (symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function &&
            symbolNode.Name == "Main" &&
            !WorkspaceConventions.IsTestFilePath(RelativePath) &&
            ((node is CodeExplorer.Core.Parser.Syntax.DeclarationSyntax declMain && declMain.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.Static)) ||
             node.Children.Any(static c => c.AsSpan() is "static")))
        {
            symbolNode.Children.Add(new SyntacticSymbol(CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint, "main-method:Main", node) { Text = node.Text });
        }

        if (symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint)
        {
            var curr = node.IsAny(
                TreeSitterSyntax.CSharp.MethodDeclaration,
                TreeSitterSyntax.CSharp.LocalFunctionStatement,
                TreeSitterSyntax.CSharp.ConstructorDeclaration,
                TreeSitterSyntax.Common.FunctionDeclaration)
                ? node
                : node.Parent;

            while (curr.IsValid() && !curr.IsAny(
                TreeSitterSyntax.CSharp.MethodDeclaration,
                TreeSitterSyntax.CSharp.LocalFunctionStatement,
                TreeSitterSyntax.CSharp.ConstructorDeclaration,
                TreeSitterSyntax.Common.FunctionDeclaration))
            {
                curr = curr.Parent;
            }

            if (curr.IsValid())
            {
                var methodName = ExtractCsIdentifier(curr);
                if (!string.IsNullOrEmpty(methodName))
                {
                    var classCurr = curr.Parent;
                    while (classCurr.IsValid() && !classCurr.IsAny(
                        TreeSitterSyntax.CSharp.ClassDeclaration,
                        TreeSitterSyntax.CSharp.StructDeclaration,
                        TreeSitterSyntax.CSharp.RecordDeclaration,
                        TreeSitterSyntax.CSharp.InterfaceDeclaration))
                    {
                        classCurr = classCurr.Parent;
                    }

                    var className = classCurr.IsValid() ? ExtractCsIdentifier(classCurr) : null;
                    var targetName = !string.IsNullOrEmpty(className) ? $"{className}.{methodName}" : methodName;

                    if (!symbolNode.References.Any(r => r.TargetName == targetName && r.Kind == RelationshipKind.Triggers))
                    {
                        symbolNode.References.Add(new Reference("", targetName, OntologyConstants.Relationships.Triggers));
                    }
                }
            }
        }
    }

    private string? ExtractCsIdentifier(Node node)
    {
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (nameNode.IsValid())
        {
            return nameNode.Text;
        }

        foreach (var child in node.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.VariableName))
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
        VisitSymbolOrBase(node, depth, () =>
        {
            if (!node.Is(TreeSitterSyntax.CSharp.VariableDeclaration))
            {
                CollectVariable(node);
                VisitChildren(node, depth);
            }
            else
            {
                var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);

                if (typeNode.IsValid())
                {
                    var typeName = typeNode.Text;

                    foreach (var declarator in node.FindChildrenOfType(TreeSitterSyntax.CSharp.VariableDeclarator))
                    {
                        var nameNode = declarator.GetField(TreeSitterSyntax.Fields.Name) ??
                                       declarator.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                        if (!nameNode.IsValid())
                        {
                            continue;
                        }

                        var varName = nameNode.Text;
                        var resolvedTypeName = typeName;

                        if (typeName == "var")
                        {
                            var valueNode = (declarator as CodeExplorer.Core.Parser.Syntax.VariableDeclSyntax)?.Initializer
                                            ?? declarator.GetField(TreeSitterSyntax.Fields.Value)
                                            ?? declarator.FindChildOfType(TreeSitterSyntax.CSharp.ObjectCreationExpression);

                            if (valueNode.IsValid() && valueNode is CodeExplorer.Core.Parser.Syntax.SyntaxList { Kind: CodeExplorer.Core.Parser.Syntax.SyntaxListKind.Initializer, Children.Count: > 0 } initClause)
                            {
                                valueNode = initClause.Children[^1];
                            }
                            if (valueNode.IsValid() && valueNode is CodeExplorer.Core.Parser.Syntax.UnaryExprSyntax { IsAwait: true, Operand: not null } awaitExpr)
                            {
                                valueNode = awaitExpr.Operand;
                            }

                            if (valueNode.IsValid() && valueNode.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
                            {
                                var objectTypeNode = valueNode.GetField(TreeSitterSyntax.Fields.Type) ??
                                                     valueNode.Children.FirstOrDefault(c =>
                                                         c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier,
                                                             TreeSitterSyntax.Common.Identifier));

                                if (objectTypeNode.IsValid())
                                {
                                    resolvedTypeName = objectTypeNode.Text;
                                }
                            }
                            else if (valueNode.IsValid() && valueNode is CodeExplorer.Core.Parser.Syntax.TypeCastExprSyntax { TargetType: not null } castExpr)
                            {
                                resolvedTypeName = castExpr.TargetType.Text;
                            }
                            else if (valueNode.IsValid() && valueNode is CodeExplorer.Core.Parser.Syntax.BinaryExprSyntax { Operator: "as", Right: not null } asExpr)
                            {
                                resolvedTypeName = asExpr.Right.Text;
                            }
                            else if (valueNode.IsValid() && valueNode is CodeExplorer.Core.Parser.Syntax.CallExprSyntax callInit)
                            {
                                var targetTypeNode = callInit.GetFunctionNode()?.FindDescendantOfType(TreeSitterSyntax.CSharp.TypeArgumentList)
                                                     ?? callInit.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                                if (targetTypeNode.IsValid() && targetTypeNode.Children.Count > 0)
                                {
                                    var firstTypeArg = targetTypeNode.Children[0];
                                    if (firstTypeArg.IsValid())
                                    {
                                        resolvedTypeName = firstTypeArg.Text;
                                    }
                                }
                            }
                        }

                        if (resolvedTypeName != "var")
                        {
                            resolvedTypeName = resolvedTypeName.TrimEnd('?');
                            var scopeName = GetContainingScopeName(node);
                            RawTypeBindings.Add(new RawTypeBinding(varName, resolvedTypeName, "", scopeName));
                        }
                    }
                }

                VisitChildren(node, depth);
            }
        });
    }

    protected override void VisitParameter(Node node, int depth)
    {
        var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ?? node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
        if (typeNode.IsValid() && nameNode.IsValid())
        {
            var cleanType = typeNode.Text.TrimEnd('?');
            var scopeName = GetContainingScopeName(node);
            RawTypeBindings.Add(new RawTypeBinding(nameNode.Text, cleanType, "", scopeName));

            var parent = node.Parent;
            if (parent.IsValid() && parent.Is(TreeSitterSyntax.CSharp.ParameterList))
            {
                if (node.ChildIndex == 0 && node.AsSpan().TrimStart().StartsWith("this ", StringComparison.Ordinal))
                {
                    var extType = cleanType;
                    var ltIdx = extType.IndexOf('<');
                    if (ltIdx > 0) extType = extType[..ltIdx].Trim();
                    var currentFn = SymbolStack.FirstOrDefault(s => s.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function);
                    if (currentFn != null && !string.IsNullOrEmpty(extType))
                    {
                        currentFn.Properties["ext_receiver_type"] = extType;
                    }
                }

                var recordNode = parent.Parent;
                if (recordNode.IsValid() && recordNode.IsAny(
                    TreeSitterSyntax.CSharp.RecordDeclaration,
                    TreeSitterSyntax.CSharp.ClassDeclaration,
                    TreeSitterSyntax.CSharp.StructDeclaration))
                {
                    var scope = DetermineCSharpScope(recordNode);
                    RawVariables.Add(new RawVariable(
                        nameNode.Text,
                        string.Empty,
                        scope,
                        false,
                        string.Empty,
                        node.StartPosition.Row + 1,
                        node.EndPosition.Row + 1,
                        node.StartPosition.Column + 1,
                        node.EndPosition.Column + 1,
                        typeNode.Text,
                        CodeExplorer.Cypher.Common.MemberKind.Property
                    ));
                }
            }
        }
        VisitChildren(node, depth);
    }

    protected override void VisitMethodDeclaration(Node node, int depth)
    {
        if (node.Is(TreeSitterSyntax.CSharp.ConstructorDeclaration))
        {
            InspectConstructorAssignments(node);
        }
        base.VisitMethodDeclaration(node, depth);
    }

    protected override void VisitFunctionDeclaration(Node node, int depth)
    {
        if (node.Is(TreeSitterSyntax.CSharp.ConstructorDeclaration))
        {
            InspectConstructorAssignments(node);
        }
        base.VisitFunctionDeclaration(node, depth);
    }

    private void InspectConstructorAssignments(Node constructorNode)
    {
        var scopeName = GetContainingScopeName(constructorNode);
        var paramTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        var paramList = constructorNode.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (paramList.IsValid())
        {
            foreach (var param in paramList.FindChildrenOfType(TreeSitterSyntax.CSharp.Parameter))
            {
                var pType = param.GetField(TreeSitterSyntax.Fields.Type);
                var pName = param.GetField(TreeSitterSyntax.Fields.Name) ?? param.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                if (pType.IsValid() && pName.IsValid())
                {
                    paramTypes[pName.Text] = pType.Text.TrimEnd('?');
                }
            }
        }

        if (paramTypes.Count == 0) return;

        foreach (var assign in constructorNode.FindDescendantsOfType(TreeSitterSyntax.Common.AssignmentExpression))
        {
            var left = assign.GetField(TreeSitterSyntax.Fields.Left);
            var right = assign.GetField(TreeSitterSyntax.Fields.Right);
            if (left.IsValid() && right.IsValid())
            {
                var rightName = right.Text.Trim();
                if (paramTypes.TryGetValue(rightName, out var typeName))
                {
                    var leftName = left.GetCanonicalMemberPath();
                    if (!string.IsNullOrEmpty(leftName))
                    {
                        RawTypeBindings.Add(new RawTypeBinding(leftName, typeName, "", scopeName));
                    }
                }
            }
        }
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.Common.Identifier));
        if (nameNode.IsValid())
        {
            var importPath = nameNode.Text;
            RawImports.Add(new RawImport(importPath, "", ImportType.External));
            ResolveAndInjectLibraryParser(importPath);
        }
        VisitChildren(node, depth);
    }

    protected override string? FindCallName(Node callNode)
    {
        if (callNode.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var typeNode = callNode.GetField(TreeSitterSyntax.Fields.Type) ??
                           callNode.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
            if (typeNode.IsValid())
            {
                return $"new {typeNode.Text}";
            }
            return null;
        }

        var expr = callNode.GetFunctionNode();
        if (!expr.IsValid()) return null;

        if (expr.Is(TreeSitterSyntax.Common.Identifier) || expr is TypeRefSyntax)
        {
            var idText = expr.Text;
            var ltIdx = idText.IndexOf('<');
            return ltIdx > 0 ? idText[..ltIdx].Trim() : idText;
        }
        if (expr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var nameChild = expr.GetField(TreeSitterSyntax.Fields.Name);
            var expressionChild = expr.GetField(TreeSitterSyntax.Fields.Expression);
            if (nameChild.IsValid())
            {
                var rawMethod = nameChild.Text;
                var ltIdx = rawMethod.IndexOf('<');
                if (ltIdx > 0) rawMethod = rawMethod[..ltIdx].Trim();
                if (expressionChild.IsValid())
                {
                    if (expressionChild is CodeExplorer.Core.Parser.Syntax.CallExprSyntax innerCall)
                    {
                        var innerTypeArgs = innerCall.GetFunctionNode()?.FindDescendantOfType(TreeSitterSyntax.CSharp.TypeArgumentList)
                                            ?? innerCall.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                        if (innerTypeArgs.IsValid() && innerTypeArgs.Children.Count > 0 && innerTypeArgs.Children[0].IsValid())
                        {
                            var chainedType = innerTypeArgs.Children[0].Text.TrimEnd('?');
                            if (!string.IsNullOrEmpty(chainedType))
                            {
                                return $"{chainedType}.{rawMethod}";
                            }
                        }
                    }
                    var recvPath = expressionChild.GetCanonicalMemberPath();
                    return string.IsNullOrEmpty(recvPath) ? rawMethod : $"{recvPath}.{rawMethod}";
                }
                return rawMethod;
            }
        }
        return null;
    }

    protected override void VisitInheritanceClause(Node node, int depth)
    {
        var currentScope = SymbolStack.Peek();
        if (currentScope.Kind != CodeExplorer.Cypher.Common.SyntacticSymbolKind.File)
        {
            List<string>? extendsList = null;
            List<string>? implementsList = null;
            foreach (var child in node.Children)
            {
                if (child is IdentifierExprSyntax or TypeRefSyntax or MemberAccessExprSyntax ||
                    child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    var fullTypeText = child.Text.Trim();
                    var baseName = fullTypeText;
                    var ltIdx = baseName.IndexOf('<');
                    if (ltIdx > 0) baseName = baseName[..ltIdx].Trim();
                    var lastDot = baseName.LastIndexOf('.');
                    var shortName = lastDot >= 0 ? baseName[(lastDot + 1)..] : baseName;
                    var refKind = shortName.StartsWith('I') && shortName.Length > 1 && char.IsUpper(shortName[1])
                        ? CodeExplorer.Cypher.Common.RelationshipKind.Implements
                        : CodeExplorer.Cypher.Common.RelationshipKind.InheritsFrom;
                    currentScope.References.Add(new Reference("", baseName, refKind));
                    if (refKind == CodeExplorer.Cypher.Common.RelationshipKind.Implements)
                    {
                        implementsList ??= [];
                        implementsList.Add(fullTypeText);
                    }
                    else
                    {
                        extendsList ??= [];
                        extendsList.Add(fullTypeText);
                    }
                }
            }
            if (extendsList is { Count: > 0 })
            {
                currentScope.Properties[OntologyConstants.PropertyNames.Extends] = string.Join(", ", extendsList);
            }
            if (implementsList is { Count: > 0 })
            {
                currentScope.Properties[OntologyConstants.PropertyNames.Implements] = string.Join(", ", implementsList);
            }
        }
        VisitChildren(node, depth);
    }

    private void CollectVariable(Node node)
    {
        var name = node.GetField(TreeSitterSyntax.Fields.Name)?.Text;
        if (string.IsNullOrEmpty(name))
        {
            name = node.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text;
        }

        if (!string.IsNullOrEmpty(name))
        {
            var valueNode = node.GetField(TreeSitterSyntax.Fields.Value);
            var initializerText = valueNode.IsValid() ? valueNode.GetBoundedInitializerText() : "";
            var isConstant = IsCSharpConstant(node);
            var scope = DetermineCSharpScope(node);

            string? memberKind = null;
            string? typeName = null;

            if (node.Is(TreeSitterSyntax.CSharp.PropertyDeclaration))
            {
                memberKind = "property";
                var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
                if (typeNode.IsValid())
                {
                    typeName = typeNode.Text;
                }
            }
            else if (node.Is(TreeSitterSyntax.CSharp.VariableDeclarator))
            {
                var parent = node.Parent;
                if (parent.IsValid() && parent.Is(TreeSitterSyntax.CSharp.VariableDeclaration))
                {
                    var typeNode = parent.GetField(TreeSitterSyntax.Fields.Type);
                    if (typeNode.IsValid())
                    {
                        typeName = typeNode.Text;
                    }
                    if (parent.Parent.IsValid() && parent.Parent.Is(TreeSitterSyntax.CSharp.FieldDeclaration))
                    {
                        memberKind = "field";
                    }
                    else
                    {
                        memberKind = "variable";
                    }
                }
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
                typeName,
                memberKind
            ));

            if (node.Is(TreeSitterSyntax.CSharp.PropertyDeclaration))
            {
                var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
                if (typeNode.IsValid())
                {
                    var scopeName = GetContainingScopeName(node);
                    RawTypeBindings.Add(new RawTypeBinding(name, typeNode.Text, "", scopeName));
                }
            }
        }
    }

    private static bool IsCSharpConstant(Node node)
    {
        var curr = node;
        while (curr.IsValid())
        {
            if (curr is CodeExplorer.Core.Parser.Syntax.VariableDeclSyntax { IsConstant: true } ||
                (curr is CodeExplorer.Core.Parser.Syntax.DeclarationSyntax d && (d.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.Const) || d.Modifiers.HasFlag(CodeExplorer.Core.Parser.Syntax.SyntaxModifiers.ReadOnly))))
                return true;
            curr = curr.Parent;
        }
        return false;
    }

    private static string DetermineCSharpScope(Node node)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration, TreeSitterSyntax.CSharp.InterfaceDeclaration))
                return "class";
            if (curr.IsAny(TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement, TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.ConstructorDeclaration))
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
            if (curr.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.InterfaceDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            {
                var nameNode = curr.GetField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid()) return nameNode.Text;
            }
            else if (curr.IsAny(TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.Common.FunctionDeclaration, TreeSitterSyntax.CSharp.ConstructorDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement))
            {
                var nameNode = curr.GetField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid()) return nameNode.Text;
            }
            curr = curr.Parent;
        }
        return "global";
    }
}
