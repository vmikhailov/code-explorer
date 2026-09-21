using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java;

public class JavaFileVisitor : BaseParserVisitor
{
    private readonly JavaParser _parser;
    public string? PackageName { get; private set; }

    public JavaFileVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        JavaParser parser,
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
        if (Libraries.SpringMvcLibraryParser.IsSpringRouteAnnotation(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (Libraries.HttpClientJavaLibraryParser.IsHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (node.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        if (node.IsAny(TreeSitterSyntax.Java.ClassDeclaration,
                       TreeSitterSyntax.Java.RecordDeclaration,
                       TreeSitterSyntax.Java.EnumDeclaration,
                       TreeSitterSyntax.Java.AnnotationTypeDeclaration))
        {
            return "Class";
        }

        if (node.Is(TreeSitterSyntax.Java.InterfaceDeclaration))
        {
            // If interface has @FeignClient, it's an external service interface
            if (HasAnnotation(node, "FeignClient"))
            {
                return OntologyConstants.NodeLabels.ExternalService;
            }
            return "Interface";
        }

        if (node.IsAny(TreeSitterSyntax.Java.MethodDeclaration,
                       TreeSitterSyntax.Java.ConstructorDeclaration,
                       TreeSitterSyntax.Java.CompactConstructorDeclaration))
        {
            return OntologyConstants.NodeLabels.Function;
        }

        return null;
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (Libraries.SpringMvcLibraryParser.IsSpringRouteAnnotation(node))
        {
            return Libraries.SpringMvcLibraryParser.ExtractRoute(node);
        }

        if (Libraries.HttpClientJavaLibraryParser.IsHttpClientCall(node))
        {
            return Libraries.HttpClientJavaLibraryParser.ExtractTarget(node);
        }

        if (node.Is(TreeSitterSyntax.Java.InterfaceDeclaration) && HasAnnotation(node, "FeignClient"))
        {
            var feignAnn = GetAnnotation(node, "FeignClient");
            if (feignAnn.IsValid())
            {
                return Libraries.HttpClientJavaLibraryParser.ExtractTarget(feignAnn);
            }
        }

        if (node.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        return ExtractJavaIdentifier(node);
    }

    protected override string? FindCallName(Node callNode)
    {
        if (callNode.Is(TreeSitterSyntax.Java.MethodInvocation))
        {
            var name = callNode.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            var expr = callNode.GetField(TreeSitterSyntax.Fields.Object);
            if (!string.IsNullOrEmpty(name))
            {
                if (expr.IsValid())
                {
                    return $"{expr.Text}.{name}";
                }
                return name;
            }

            foreach (var child in callNode.Children)
            {
                if (child.Is(TreeSitterSyntax.Java.Identifier))
                {
                    return child.Text;
                }
            }
        }

        if (callNode.Is(TreeSitterSyntax.Java.ObjectCreationExpression))
        {
            var typeNode = callNode.GetField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid())
            {
                return typeNode.Text;
            }
            var typeId = callNode.FindChildOfType(TreeSitterSyntax.Java.TypeIdentifier);
            if (typeId.IsValid())
            {
                return typeId.Text;
            }
        }

        if (callNode.IsAny(TreeSitterSyntax.Java.ExplicitConstructorInvocation, TreeSitterSyntax.Java.SuperConstructorInvocation))
        {
            return "super";
        }

        return null;
    }

    protected override void CollectCustomReferencesForSymbol(Node node, SyntacticSymbol symbolNode, SyntacticSymbol parentNode)
    {
        // 1. If EntryPoint from Spring annotation, connect to the enclosing method
        if (symbolNode.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var methodNode = FindEnclosingMethodOrClass(node);
            if (methodNode.IsValid())
            {
                var methodName = ExtractJavaIdentifier(methodNode);
                if (!string.IsNullOrEmpty(methodName))
                {
                    symbolNode.References.Add(new Reference("", methodName, "CALLS"));
                }
            }
        }

        // 2. Class inheritance & interface implementations
        if (symbolNode.Kind is "Class" or "Interface")
        {
            // Superclass e.g. extends BaseService
            var superclass = node.FindChildOfType(TreeSitterSyntax.Java.Superclass);
            if (superclass.IsValid())
            {
                var typeId = superclass.FindChildOfType(TreeSitterSyntax.Java.TypeIdentifier)
                             ?? superclass.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                if (typeId.IsValid())
                {
                    symbolNode.References.Add(new Reference("", typeId.Text, "INHERITS"));
                }
            }

            // Super interfaces e.g. implements IUserService, Serializable
            var interfaces = node.FindChildOfType(TreeSitterSyntax.Java.SuperInterfaces)
                             ?? node.FindChildOfType(TreeSitterSyntax.Java.ExtendsInterfaces);
            if (interfaces.IsValid())
            {
                foreach (var child in interfaces.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.Java.TypeIdentifier, TreeSitterSyntax.Java.Identifier, TreeSitterSyntax.Java.ScopedIdentifier))
                    {
                        symbolNode.References.Add(new Reference("", child.Text, "IMPLEMENTS"));
                    }
                }
            }

            // Class-level annotations
            ExtractAndAttachAnnotations(node, symbolNode);
        }

        // 3. Method references & annotations
        if (symbolNode.Kind == OntologyConstants.NodeLabels.Function)
        {
            ExtractAndAttachAnnotations(node, symbolNode);
        }
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        var scopedId = node.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier)
                       ?? node.FindChildOfType(TreeSitterSyntax.Java.Identifier);
        if (scopedId.IsValid())
        {
            var importText = scopedId.Text;
            var importType = FileParser.ResolveImportType(importText, RelativePath, AbsoluteWorkspacePath);
            RawImports.Add(new RawImport(importText, "", importType));
            ResolveAndInjectLibraryParser(importText);
        }
        VisitChildren(node, depth);
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
                        : "INHERITS";
                    currentScope.References.Add(new Reference("", baseName, refKind));
                }
            }
        }
        VisitChildren(node, depth);
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        CollectJavaVariables(node);
        VisitChildren(node, depth);
    }

    private void CollectJavaVariables(Node node)
    {
        var typeNode = node.GetField(TreeSitterSyntax.Fields.Type)
                       ?? node.FindChildOfType(TreeSitterSyntax.Java.TypeIdentifier)
                       ?? node.FindChildOfType(TreeSitterSyntax.Java.Identifier);

        var typeName = typeNode.IsValid() ? typeNode.Text : "var";
        var isConstant = HasModifier(node, "final") || node.Is(TreeSitterSyntax.Java.ConstantDeclaration);
        var scope = SymbolStack.Peek().Kind switch
        {
            "Class" or "Interface" => "class",
            _ => "local"
        };

        foreach (var declarator in node.Children)
        {
            if (declarator.Is(TreeSitterSyntax.Java.VariableDeclarator))
            {
                var varNameNode = declarator.GetField(TreeSitterSyntax.Fields.Name)
                                  ?? declarator.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                if (varNameNode.IsValid())
                {
                    var varName = varNameNode.Text;
                    var valNode = declarator.GetField(TreeSitterSyntax.Fields.Value);
                    var initText = valNode.IsValid() ? valNode.Text : "";

                    RawVariables.Add(new RawVariable(
                        varName,
                        initText,
                        scope,
                        isConstant,
                        "",
                        declarator.StartPosition.Row,
                        declarator.EndPosition.Row,
                        declarator.StartPosition.Column,
                        declarator.EndPosition.Column
                    ));

                    RawTypeBindings.Add(new RawTypeBinding(varName, typeName, "", SymbolStack.Peek().Name));
                }
            }
        }
    }

    protected override void VisitNode(Node node, int depth)
    {
        // Handle package declaration
        if (node.Is(TreeSitterSyntax.Java.PackageDeclaration))
        {
            var scopedId = node.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier)
                           ?? node.FindChildOfType(TreeSitterSyntax.Java.Identifier);
            if (scopedId.IsValid())
            {
                PackageName = scopedId.Text;
            }
        }

        base.VisitNode(node, depth);
    }

    private static Node? FindEnclosingMethodOrClass(Node node)
    {
        var current = node.Parent;
        while (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.Java.MethodDeclaration,
                              TreeSitterSyntax.Java.ConstructorDeclaration,
                              TreeSitterSyntax.Java.ClassDeclaration,
                              TreeSitterSyntax.Java.InterfaceDeclaration))
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    private static bool HasAnnotation(Node node, string annotationName)
    {
        return GetAnnotation(node, annotationName).IsValid();
    }

    private static Node? GetAnnotation(Node node, string annotationName)
    {
        var modifiers = node.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (!modifiers.IsValid()) return null;

        foreach (var child in modifiers.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
            {
                var id = child.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                         ?? child.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);
                if (id.IsValid() && id.Text.EndsWith(annotationName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }
        return null;
    }

    private static bool HasModifier(Node node, string modifierText)
    {
        var modifiers = node.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (!modifiers.IsValid()) return false;

        foreach (var child in modifiers.Children)
        {
            if (child.Text.Equals(modifierText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static void ExtractAndAttachAnnotations(Node node, SyntacticSymbol symbolNode)
    {
        var modifiers = node.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (!modifiers.IsValid()) return;

        foreach (var child in modifiers.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
            {
                var id = child.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                         ?? child.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);
                if (id.IsValid())
                {
                    symbolNode.References.Add(new Reference("", id.Text, "ANNOTATED_WITH"));
                }
            }
        }
    }

    private string? ExtractJavaIdentifier(Node node)
    {
        var nameField = node.GetField(TreeSitterSyntax.Fields.Name);
        if (nameField.IsValid()) return nameField.Text;

        foreach (var child in node.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Java.Identifier, TreeSitterSyntax.Java.TypeIdentifier))
            {
                return child.Text;
            }
        }

        return null;
    }
}
