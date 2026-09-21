using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp;

public class CSharpFileVisitor : BaseParserVisitor
{
    private readonly CSharpParser _parser;

    public CSharpFileVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        CSharpParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
        : base(rootNode, activeLibraryParsers, relativePath, absoluteWorkspacePath, fileParser, libraryRegistry)
    {
        _parser = parser;
    }

    protected override string? MapNodeType(Node node)
    {
        if (node.Is(TreeSitterSyntax.CSharp.Attribute))
        {
            var nameNode = node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
            if (nameNode.IsValid() && (nameNode.Text is "Route" or "RoutePrefix" || nameNode.Text.StartsWith("Http") || nameNode.Text is "Get" or "Post" or "Put" or "Delete" or "Patch" or "Head" or "Options"))
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

        if (IsHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (IsInsideInterpolatedString(node))
        {
            return null;
        }

        if (node.Type.Contains("string") || IsInterpolatedStringExpression(node))
        {
            var sqlCandidate = ExtractFullStringText(node);
            if (NestedSqlParser.TryParseSql(sqlCandidate, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
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
            return Libraries.AspNetCoreLibraryParser.ExtractRoute(node) ?? ExtractCSharpAttributeRoute(node);
        }

        if (IsHttpClientCall(node))
        {
            return ExtractHttpClientTarget(node);
        }

        if (IsInsideInterpolatedString(node))
        {
            return null;
        }

        if (node.Type.Contains("string") || IsInterpolatedStringExpression(node))
        {
            var sqlCandidate = ExtractFullStringText(node);
            if (NestedSqlParser.TryParseSql(sqlCandidate, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        return ExtractCsIdentifier(node);
    }

    private static bool IsHttpClientCall(Node node) => Libraries.HttpClientLibraryParser.IsHttpClientCall(node);

    private static string? ExtractHttpClientTarget(Node node) => Libraries.HttpClientLibraryParser.ExtractTarget(node);

    private static string? ExtractCSharpAttributeRoute(Node attributeNode) => Libraries.AspNetCoreLibraryParser.ExtractRoute(attributeNode);

    protected override void CollectCustomReferencesForSymbol(
        Node node,
        SyntacticSymbol symbolNode,
        SyntacticSymbol parentNode)
    {
        if (symbolNode.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var curr = node.Parent;
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

                    if (!symbolNode.References.Any(r => r.TargetName == targetName && r.Kind == OntologyConstants.Relationships.Triggers))
                    {
                        symbolNode.References.Add(new Reference("", targetName, OntologyConstants.Relationships.Triggers));
                    }
                }
            }
        }
    }

    private static bool IsInterpolatedStringExpression(Node node) =>
        node.IsAny(TreeSitterSyntax.CSharp.InterpolatedStringExpression,
                   TreeSitterSyntax.CSharp.InterpolatedVerbatimStringExpression,
                   TreeSitterSyntax.CSharp.InterpolatedRawStringExpression);

    private static bool IsInsideInterpolatedString(Node node) =>
        node.Parent.IsValid() && IsInterpolatedStringExpression(node.Parent);

    public static string ExtractFullStringText(Node node)
    {
        if (IsInterpolatedStringExpression(node))
        {
            var sb = new System.Text.StringBuilder();
            foreach (var child in node.Children)
            {
                if (child.Type == "interpolated_string_text")
                {
                    sb.Append(child.Text);
                }
                else if (child.Type == "interpolation")
                {
                    var expr = child.Children.FirstOrDefault(c => c.Text != "{" && c.Text != "}");
                    if (expr.IsValid())
                    {
                        var exprText = expr.Text;
                        if (exprText.Contains('.'))
                        {
                            exprText = exprText[(exprText.LastIndexOf('.') + 1)..];
                        }
                        sb.Append(exprText.Trim('"'));
                    }
                }
            }
            return sb.ToString();
        }
        return node.Text;
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
                        var nameNode = declarator.GetField(TreeSitterSyntax.Fields.Name);

                        if (!nameNode.IsValid())
                        {
                            continue;
                        }

                        var varName = nameNode.Text;
                        var resolvedTypeName = typeName;

                        if (typeName == "var")
                        {
                            var valueNode = declarator.GetField(TreeSitterSyntax.Fields.Value) ?? declarator
                                .FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause)?.Children
                                .ElementAtOrDefault(1);

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
                        }

                        if (resolvedTypeName != "var")
                        {
                            var scopeName = GetContainingScopeName(node);
                            RawTypeBindings.Add(new RawTypeBinding(varName, resolvedTypeName, "", scopeName));
                            RawTypeBindings.Add(new RawTypeBinding("this." + varName, resolvedTypeName, "", scopeName));
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
            var scopeName = GetContainingScopeName(node);
            RawTypeBindings.Add(new RawTypeBinding(nameNode.Text, typeNode.Text, "", scopeName));
            RawTypeBindings.Add(new RawTypeBinding("this." + nameNode.Text, typeNode.Text, "", scopeName));
        }
        VisitChildren(node, depth);
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
        var expr = callNode.GetFunctionNode();
        if (!expr.IsValid()) return null;

        if (expr.Is(TreeSitterSyntax.Common.Identifier))
        {
            return expr.Text;
        }
        if (expr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var nameChild = expr.GetField(TreeSitterSyntax.Fields.Name);
            var expressionChild = expr.GetField(TreeSitterSyntax.Fields.Expression);
            if (nameChild.IsValid())
            {
                if (expressionChild.IsValid())
                {
                    return $"{expressionChild.Text}.{nameChild.Text}";
                }
                return nameChild.Text;
            }
        }
        return null;
    }

    protected override void VisitInheritanceClause(Node node, int depth)
    {
        var currentScope = SymbolStack.Peek();
        if (currentScope.Kind != "file")
        {
            foreach (var child in node.Children)
            {
                if (child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    var baseName = child.Text;
                    var refKind = baseName.StartsWith('I') && baseName.Length > 1 && char.IsUpper(baseName[1])
                        ? "IMPLEMENTS"
                        : "INHERITS_FROM";
                    currentScope.References.Add(new Reference("", baseName, refKind));
                }
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
            if (!valueNode.IsValid())
            {
                var eqClause = node.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                if (eqClause.IsValid() && eqClause.Children.Count > 1)
                {
                    valueNode = eqClause.Children[1];
                }
            }
            var initializerText = valueNode.IsValid() ? valueNode.Text : "";
            var isConstant = IsCSharpConstant(node);
            var scope = DetermineCSharpScope(node);

            RawVariables.Add(new RawVariable(
                name,
                initializerText,
                scope,
                isConstant,
                "",
                node.StartPosition.Row,
                node.EndPosition.Row,
                node.StartPosition.Column,
                node.EndPosition.Column
            ));

            if (node.Is(TreeSitterSyntax.CSharp.PropertyDeclaration))
            {
                var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
                if (typeNode.IsValid())
                {
                    var scopeName = GetContainingScopeName(node);
                    RawTypeBindings.Add(new RawTypeBinding(name, typeNode.Text, "", scopeName));
                    RawTypeBindings.Add(new RawTypeBinding("this." + name, typeNode.Text, "", scopeName));
                }
            }
        }
    }

    private static bool IsCSharpConstant(Node node)
    {
        var curr = node;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.FieldDeclaration, TreeSitterSyntax.CSharp.LocalDeclarationStatement))
            {
                foreach (var child in curr.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.CSharp.Const, TreeSitterSyntax.CSharp.Readonly) || child.Text is "const" or "readonly")
                        return true;
                }
            }
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
