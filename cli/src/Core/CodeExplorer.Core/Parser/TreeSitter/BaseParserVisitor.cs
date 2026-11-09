using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

public abstract partial class BaseParserVisitor : TreeSitterAstVisitor
{
    protected readonly List<ISemanticExtension> SemanticExtensions;
    protected List<ISemanticExtension> LibraryParsers => SemanticExtensions;
    private readonly List<DeclarativeSemanticExtension> _declarativeExtensions = [];
    private readonly List<ISemanticExtension> _imperativeExtensions = [];
    private DeclarativeSemanticExtension? _fusedDeclarative;
    protected readonly LanguageSyntaxProfile Profile;

    public string RelativePath { get; }
    public string AbsoluteWorkspacePath { get; }
    public IFileParser FileParser { get; }
    public SemanticExtensionRegistry ExtensionRegistry { get; }
    public SemanticExtensionRegistry LibraryRegistry => ExtensionRegistry;
    public string? ProjectName { get; set; }
    public ParsingContext? Context { get; set; }
    public bool IsTestFile { get; set; }

    protected void RegisterActiveExtension(ISemanticExtension ext)
    {
        if (!ext.IsImplemented || SemanticExtensions.Contains(ext)) return;
        SemanticExtensions.Add(ext);
        if (ext is DeclarativeSemanticExtension dec)
        {
            _declarativeExtensions.Add(dec);
            _fusedDeclarative = CompiledDeclarativeRuleIndex.GetOrCreateFused(_declarativeExtensions);
        }
        else
        {
            _imperativeExtensions.Add(ext);
        }
    }

    public void ResolveAndInjectExtension(string importPath)
    {
        var matches = ExtensionRegistry.MatchAll(importPath);
        var decChanged = false;
        foreach (var match in matches)
        {
            if (match.IsImplemented && !SemanticExtensions.Contains(match))
            {
                SemanticExtensions.Add(match);
                if (match is DeclarativeSemanticExtension dec)
                {
                    _declarativeExtensions.Add(dec);
                    decChanged = true;
                }
                else
                {
                    _imperativeExtensions.Add(match);
                }
            }
        }
        if (decChanged)
        {
            _fusedDeclarative = CompiledDeclarativeRuleIndex.GetOrCreateFused(_declarativeExtensions);
        }
    }

    public void ResolveAndInjectLibraryParser(string importPath) => ResolveAndInjectExtension(importPath);

    public List<RawImport> RawImports { get; } = [];
    public List<RawVariable> RawVariables { get; } = [];
    public List<RawTypeBinding> RawTypeBindings { get; } = [];

    public SyntacticSymbol RootSymbol { get; }
    protected readonly Stack<SyntacticSymbol> SymbolStack = new();
    protected readonly Stack<IntPtr> PushedNodeIds = new();

    public SequenceDetector<Node> SequenceDetector { get; } = new();

    protected BaseParserVisitor(
        Node rootNode,
        List<ISemanticExtension> semanticExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        SemanticExtensions = semanticExtensions;
        for (var i = 0; i < semanticExtensions.Count; i++)
        {
            if (semanticExtensions[i] is DeclarativeSemanticExtension dec)
            {
                _declarativeExtensions.Add(dec);
            }
            else
            {
                _imperativeExtensions.Add(semanticExtensions[i]);
            }
        }
        _fusedDeclarative = CompiledDeclarativeRuleIndex.GetOrCreateFused(_declarativeExtensions);
        RelativePath = relativePath;
        AbsoluteWorkspacePath = absoluteWorkspacePath;
        FileParser = fileParser;
        ExtensionRegistry = extensionRegistry;
        Profile = fileParser.SyntaxProfile;
        RootSymbol = new SyntacticSymbol("file", "root", rootNode);
        SymbolStack.Push(RootSymbol);
    }

    public string? PackageName { get; protected set; }

    protected virtual string? MapNodeType(Node node) => MapProfileNodeType(node);

    protected virtual string? ExtractIdentifier(Node node) => ExtractProfileIdentifier(node);

    protected virtual void CollectCustomReferencesForSymbol(
        Node node,
        SyntacticSymbol symbolNode,
        SyntacticSymbol parentNode)
    {
    }

    protected virtual string? FindCallName(Node callNode) => ExtractProfileCallName(callNode);

    protected string? MapProfileNodeType(Node node)
    {
        if ((node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString } ||
             node is InterpolatedStringExprSyntax ||
             Profile.StringLiterals.Contains(node.Type)) &&
            !WorkspaceConventions.IsFrontendFilePath(RelativePath))
        {
            if (NestedSqlParser.FastCheckSqlCandidate(node.AsSpan()) &&
                NestedSqlParser.TryParseSql(CleanStringLiteral(node.Text), out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        if (node is TypeDeclSyntax td)
        {
            return td.Kind switch
            {
                TypeSyntaxKind.Struct => CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ToCanonicalString(Profile.DefaultStructKind),
                TypeSyntaxKind.Enum => CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ToCanonicalString(Profile.DefaultEnumKind),
                TypeSyntaxKind.Class or TypeSyntaxKind.Record => "Class",
                TypeSyntaxKind.Interface or TypeSyntaxKind.Trait => "Interface",
                TypeSyntaxKind.TypeAlias or TypeSyntaxKind.Union when Profile.StructDeclarations.Count > 0 && Profile.DefaultStructKind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Struct =>
                    CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ToCanonicalString(Profile.DefaultStructKind),
                _ => null
            };
        }

        if (node is FunctionDeclSyntax fd)
        {
            if (fd.Kind is FunctionSyntaxKind.Function or FunctionSyntaxKind.Method)
            {
                return OntologyConstants.NodeLabels.Function;
            }
            if (fd.Kind == FunctionSyntaxKind.Constructor &&
                (Profile.MethodDeclarations.Contains("constructor_declaration") ||
                 Profile.MethodDeclarations.Contains("compact_constructor_declaration")))
            {
                return OntologyConstants.NodeLabels.Function;
            }
        }

        return null;
    }

    protected string? ExtractProfileIdentifier(Node node)
    {
        if ((node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString } ||
             node is InterpolatedStringExprSyntax ||
             Profile.StringLiterals.Contains(node.Type)) &&
            !WorkspaceConventions.IsFrontendFilePath(RelativePath))
        {
            if (NestedSqlParser.FastCheckSqlCandidate(node.AsSpan()) &&
                NestedSqlParser.TryParseSql(CleanStringLiteral(node.Text), out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        foreach (var fieldName in Profile.IdentifierFieldNames)
        {
            var fieldNode = node.GetField(fieldName);
            if (fieldNode.IsValid())
            {
                return NormalizeIdentifierText(fieldNode.Text);
            }
        }

        if (node is DeclarationSyntax { HasName: true } decl)
        {
            return NormalizeIdentifierText(decl.Name);
        }

        foreach (var child in node.Children)
        {
            if (child.IsValid() && (child is IdentifierExprSyntax || Profile.IdentifierChildTypes.Contains(child.Type)))
            {
                return NormalizeIdentifierText(child.Text);
            }
        }

        foreach (var child in node.Children)
        {
            if (child.IsValid() && child.Type.Contains("name", StringComparison.Ordinal))
            {
                return NormalizeIdentifierText(child.Text);
            }
        }

        return null;
    }

    protected string NormalizeIdentifierText(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return rawText;
        return Profile.IdentifierTrimChars.Length > 0
            ? rawText.TrimStart(Profile.IdentifierTrimChars)
            : rawText;
    }

    protected string? ExtractProfileCallName(Node callNode)
    {
        if (callNode is CallExprSyntax { CallKind: CallSyntaxKind.Constructor } || Profile.ObjectCreationNodeTypes.Contains(callNode.Type))
        {
            var typeNode = callNode.GetField(SyntaxFields.Type);
            if (typeNode.IsValid())
            {
                return typeNode.Text;
            }

            foreach (var child in callNode.Children)
            {
                if (child.IsValid() && (child is IdentifierExprSyntax or TypeRefSyntax || Profile.IdentifierChildTypes.Contains(child.Type)))
                {
                    return child.Text;
                }
            }
            return null;
        }

        if (callNode is CallExprSyntax { CallKind: CallSyntaxKind.SuperConstructor or CallSyntaxKind.ThisConstructor } || Profile.ConstructorSelfCallNodeTypes.Contains(callNode.Type))
        {
            return "super";
        }

        // 1. Check direct CallMethodFields on callNode (e.g. Java method_invocation: name + object, PHP member_call_expression: name + object/scope)
        string? methodName = null;
        foreach (var methodField in Profile.CallMethodFields)
        {
            var mNode = callNode.GetField(methodField);
            if (mNode.IsValid())
            {
                methodName = NormalizeIdentifierText(mNode.Text);
                break;
            }
        }

        if (!string.IsNullOrEmpty(methodName))
        {
            if (Profile.IncludeReceiverInCallName)
            {
                foreach (var recField in Profile.CallReceiverFields)
                {
                    var recNode = callNode.GetField(recField);
                    if (recNode.IsValid() && (recNode.EndIndex - recNode.StartIndex) <= 128)
                    {
                        var recPath = recNode.GetCanonicalMemberPath(Profile.CallMemberSeparator);
                        return string.IsNullOrEmpty(recPath) ? methodName : $"{recPath}{Profile.CallMemberSeparator}{methodName}";
                    }
                }
            }
            return methodName;
        }

        // 2. Check 'function' field or first child
        var funcNode = callNode.GetField(SyntaxFields.Function);
        if (!funcNode.IsValid() && callNode.Children.Count > 0)
        {
            funcNode = callNode.Children[0];
        }

        if (funcNode.IsValid())
        {
            if (funcNode is IdentifierExprSyntax || Profile.IdentifierChildTypes.Contains(funcNode.Type))
            {
                return NormalizeIdentifierText(funcNode.Text);
            }

            if (funcNode is MemberAccessExprSyntax || Profile.MemberAccessExpressions.Contains(funcNode.Type))
            {
                string? memberPart = null;
                foreach (var mf in Profile.CallMethodFields)
                {
                    var fChild = funcNode.GetField(mf);
                    if (fChild.IsValid())
                    {
                        memberPart = NormalizeIdentifierText(fChild.Text);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(memberPart) && funcNode is MemberAccessExprSyntax { HasExplicitMemberName: true } memSyntax)
                {
                    memberPart = NormalizeIdentifierText(memSyntax.MemberName);
                }

                if (!string.IsNullOrEmpty(memberPart))
                {
                    if (Profile.IncludeReceiverInCallName)
                    {
                        foreach (var rf in Profile.CallReceiverFields)
                        {
                            var rChild = funcNode.GetField(rf);
                            if (rChild.IsValid() && (rChild.EndIndex - rChild.StartIndex) <= 128)
                            {
                                var recPath = rChild.GetCanonicalMemberPath(Profile.CallMemberSeparator);
                                return string.IsNullOrEmpty(recPath) ? memberPart : $"{recPath}{Profile.CallMemberSeparator}{memberPart}";
                            }
                        }
                    }
                    return memberPart;
                }
            }
        }

        foreach (var child in callNode.Children)
        {
            if (child.IsValid() && (child is IdentifierExprSyntax || Profile.IdentifierChildTypes.Contains(child.Type)))
            {
                return NormalizeIdentifierText(child.Text);
            }
        }

        return null;
    }

    protected virtual void CollectProfileSymbolMetadata(Node node, SyntacticSymbol symbolNode)
    {
        // 1. If EntryPoint from an annotation/attribute, connect to the enclosing method/class
        if (symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint &&
            (node is AnnotationSyntax || Profile.AnnotationNodeTypes.Contains(node.Type)))
        {
            var enclosing = FindEnclosingDeclaration(node);
            if (enclosing.IsValid())
            {
                var methodName = ExtractProfileIdentifier(enclosing);
                if (!string.IsNullOrEmpty(methodName) &&
                    !symbolNode.References.Any(r => r.RelationshipKind == CodeExplorer.Cypher.Common.RelationshipKind.Calls && r.TargetName == methodName))
                {
                    symbolNode.References.Add(new Reference("", methodName, CodeExplorer.Cypher.Common.RelationshipKind.Calls,
                        node.StartPosition.Row + 1,
                        node.StartPosition.Column + 1));
                }
            }
        }

        // 2. Class/Interface/Struct inheritance, implemented interfaces, and annotations
        if (symbolNode.Kind is CodeExplorer.Cypher.Common.SyntacticSymbolKind.Class or CodeExplorer.Cypher.Common.SyntacticSymbolKind.Interface or CodeExplorer.Cypher.Common.SyntacticSymbolKind.Struct)
        {
            foreach (var child in node.Children)
            {
                if (!child.IsValid()) continue;
                if (child is BaseTypeClauseSyntax { IsImplements: false } && Profile.SuperclassClauseNodeTypes.Count > 0)
                {
                    foreach (var typeChild in child.Children)
                    {
                        if (typeChild.IsValid() && (typeChild is IdentifierExprSyntax or TypeRefSyntax || Profile.IdentifierChildTypes.Contains(typeChild.Type)))
                        {
                            symbolNode.References.Add(new Reference("", typeChild.Text, CodeExplorer.Cypher.Common.RelationshipKind.InheritsFrom));
                            break;
                        }
                    }
                }
                else if (child is BaseTypeClauseSyntax { IsImplements: true } && Profile.InterfaceClauseNodeTypes.Count > 0)
                {
                    foreach (var typeChild in child.Children)
                    {
                        if (typeChild.IsValid() && (typeChild is IdentifierExprSyntax or TypeRefSyntax or MemberAccessExprSyntax || Profile.IdentifierChildTypes.Contains(typeChild.Type)))
                        {
                            symbolNode.References.Add(new Reference("", typeChild.Text, CodeExplorer.Cypher.Common.RelationshipKind.Implements));
                        }
                    }
                }
            }

            ExtractProfileAnnotations(node, symbolNode);
        }

        // 3. Function/Method annotations
        if (symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function)
        {
            ExtractProfileAnnotations(node, symbolNode);
        }
    }

    protected Node? FindEnclosingDeclaration(Node node)
    {
        var current = node.Parent;
        while (current.IsValid())
        {
            if (current is FunctionDeclSyntax or TypeDeclSyntax)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    protected void ExtractProfileAnnotations(Node node, SyntacticSymbol symbolNode)
    {
        if (Profile.AnnotationNodeTypes.Count == 0) return;

        foreach (var child in node.Children)
        {
            if (!child.IsValid()) continue;

            if (child is AnnotationSyntax || Profile.AnnotationNodeTypes.Contains(child.Type))
            {
                AttachAnnotationReference(child, symbolNode);
            }
            else if (child is SyntaxList { Kind: SyntaxListKind.Attributes } || Profile.AnnotationContainerNodeTypes.Contains(child.Type))
            {
                foreach (var grandChild in child.Children)
                {
                    if (grandChild.IsValid() && (grandChild is AnnotationSyntax || Profile.AnnotationNodeTypes.Contains(grandChild.Type)))
                    {
                        AttachAnnotationReference(grandChild, symbolNode);
                    }
                }
            }
        }
    }

    private void AttachAnnotationReference(Node annotationNode, SyntacticSymbol symbolNode)
    {
        var id = ExtractAnnotationIdentifier(annotationNode);
        if (!string.IsNullOrEmpty(id))
        {
            symbolNode.References.Add(new Reference("", id, "ANNOTATED_WITH"));
        }
    }

    protected string? ExtractAnnotationIdentifier(Node annotationNode)
    {
        if (annotationNode is AnnotationSyntax { HasName: true } annotSyntax)
        {
            return annotSyntax.Name;
        }
        foreach (var child in annotationNode.Children)
        {
            if (child.IsValid() && (child is IdentifierExprSyntax or MemberAccessExprSyntax || Profile.IdentifierChildTypes.Contains(child.Type)))
            {
                return child.Text;
            }
        }
        var nameField = annotationNode.GetField(SyntaxFields.Name);
        return nameField.IsValid() ? nameField.Text : null;
    }

    protected override void VisitNode(Node node, int depth)
    {
        // TokenSyntax removed: all visited nodes are named AST nodes

        if ((node is NamespaceDeclSyntax && Profile.PackageDeclarationNodeTypes.Count > 0) ||
            Profile.PackageDeclarationNodeTypes.Contains(node.Type))
        {
            var pkgId = ExtractAnnotationIdentifier(node) ?? ExtractProfileIdentifier(node);
            if (!string.IsNullOrEmpty(pkgId))
            {
                PackageName = pkgId;
            }
        }

        if (SequenceDetector.HasRules)
        {
            SequenceDetector.Push(node);
        }

        try
        {
            // 1. General reference collection
            var currentScope = SymbolStack.Peek();

            if (LibraryParsers.Count > 0 && MayContainSymbolsOrReferences(node))
            {
                var enclosingFunc = currentScope.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Query
                    ? SymbolStack.FirstOrDefault(s => s.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function)
                    : null;

                if (_fusedDeclarative != null)
                {
                    _fusedDeclarative.CollectReferences(node, "", currentScope.References, Context!);
                    if (enclosingFunc != null)
                    {
                        _fusedDeclarative.CollectReferences(node, "", enclosingFunc.References, Context!);
                    }
                }

                for (var i = 0; i < _imperativeExtensions.Count; i++)
                {
                    var libParser = _imperativeExtensions[i];
                    libParser.CollectReferences(node, "", currentScope.References, Context!);
                    if (enclosingFunc != null)
                    {
                        libParser.CollectReferences(node, "", enclosingFunc.References, Context!);
                    }
                }
            }

            // 2. Dispatch to specific typed visit methods
            Dispatch(node, depth);
        }
        finally
        {
            if (SequenceDetector.HasRules)
            {
                SequenceDetector.Pop();
            }
        }
    }

    protected virtual void Dispatch(Node node, int depth)
    {
        switch (node)
        {
            case LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString }:
            case InterpolatedStringExprSyntax when Profile.StringLiterals.Contains("template_string") || Profile.StringLiterals.Contains("interpolated_string_expression"):
                VisitStringLiteral(node, depth);
                return;

            case TypeDeclSyntax { Kind: TypeSyntaxKind.Class or TypeSyntaxKind.Struct or TypeSyntaxKind.Record or TypeSyntaxKind.Enum or TypeSyntaxKind.Union }:
                VisitClassDeclaration(node, depth);
                return;

            case TypeDeclSyntax { Kind: TypeSyntaxKind.Interface or TypeSyntaxKind.Trait }:
            case TypeDeclSyntax { Kind: TypeSyntaxKind.TypeAlias } when Profile.InterfaceDeclarations.Contains("type_alias_declaration"):
                VisitInterfaceDeclaration(node, depth);
                return;

            case FunctionDeclSyntax { Kind: FunctionSyntaxKind.Method or FunctionSyntaxKind.Constructor }:
                VisitMethodDeclaration(node, depth);
                return;

            case FunctionDeclSyntax { Kind: FunctionSyntaxKind.Function }:
            case LambdaExprSyntax when Profile.FunctionDeclarations.Contains("arrow_function"):
                VisitFunctionDeclaration(node, depth);
                return;

            case VariableDeclSyntax { IsDeclarator: false }:
            case VariableDeclSyntax { IsDeclarator: true } when Profile.VariableDeclarations.Contains("variable_declarator") || Profile.VariableDeclarations.Contains("var_spec"):
            case MemberDeclSyntax { Kind: MemberSyntaxKind.Field or MemberSyntaxKind.Constant or MemberSyntaxKind.Property } when Profile.VariableDeclarations.Count > 0:
            case AssignmentExprSyntax when Profile.VariableDeclarations.Contains("assignment"):
                VisitVariableDeclaration(node, depth);
                return;

            case ParameterDeclSyntax:
                VisitParameter(node, depth);
                return;

            case ImportDeclSyntax { IsReExport: false }:
            case ImportDeclSyntax { IsReExport: true } when Profile.Imports.Contains("export_statement"):
            case NamespaceDeclSyntax when Profile.Imports.Contains("package_declaration"):
                VisitImportStatement(node, depth);
                return;

            case CallExprSyntax { CallKind: CallSyntaxKind.Method or CallSyntaxKind.Function or CallSyntaxKind.Macro }:
            case CallExprSyntax { CallKind: CallSyntaxKind.Constructor, MethodNameSlice.Length: > 0 }
                when Profile.Calls.Contains("new_expression") || Profile.Calls.Contains("object_creation_expression"):
            case CallExprSyntax { CallKind: CallSyntaxKind.Constructor, Callee: not null }
                when Profile.Calls.Contains("new_expression") || Profile.Calls.Contains("object_creation_expression"):
            case CallExprSyntax { CallKind: CallSyntaxKind.SuperConstructor or CallSyntaxKind.ThisConstructor }
                when Profile.Calls.Contains("new_expression") || Profile.Calls.Contains("object_creation_expression"):
                VisitCallExpression(node, depth);
                return;

            case BaseTypeClauseSyntax when Profile.Inheritance.Count > 0:
                VisitInheritanceClause(node, depth);
                return;

            case CatchClauseSyntax:
                VisitCatchClause(node, depth);
                return;

            default:
                VisitDefault(node, depth);
                return;
        }
    }

    protected string? MapNodeTypeUsingLibraries(Node node)
    {
        if (_fusedDeclarative != null)
        {
            var decKind = _fusedDeclarative.MapNodeType(node, Context!);
            if (decKind != null) return decKind;
        }

        for (var i = 0; i < _imperativeExtensions.Count; i++)
        {
            var lp = _imperativeExtensions[i];
            foreach (var kvp in lp.Selectors)
            {
                if (kvp.Value.Matches(node))
                {
                    return kvp.Key;
                }
            }

            var kind = lp.MapNodeType(node, Context!);
            if (kind != null) return kind;
        }

        return null;
    }

    protected string? ExtractIdentifierUsingLibraries(Node node, string kind)
    {
        if (_fusedDeclarative != null)
        {
            var decName = _fusedDeclarative.ExtractIdentifier(node, Context!, ProjectName, RelativePath);
            if (decName != null) return decName;
        }

        for (var i = 0; i < _imperativeExtensions.Count; i++)
        {
            var lp = _imperativeExtensions[i];
            var isMatch = false;
            if (lp.Selectors.TryGetValue(kind, out var sel))
            {
                isMatch = sel.Matches(node);
            }
            else
            {
                isMatch = lp.MapNodeType(node, Context!) == kind;
            }

            if (isMatch)
            {
                var name = lp.ExtractIdentifier(node, Context!, ProjectName, RelativePath);
                if (name != null) return name;
            }
        }

        return null;
    }

    protected void VisitSymbolOrBase(Node node, int depth, Action? baseVisit = null)
    {
        var baseKind = MapNodeType(node);
        var libKind = MapNodeTypeUsingLibraries(node);

        if (baseKind is "Class" or "Interface" or "Struct" or "Enum" || baseKind == OntologyConstants.NodeLabels.Type)
        {
            var baseName = ExtractIdentifier(node);
            if (!string.IsNullOrEmpty(baseName))
            {
                string? extraEntryPointName = null;
                if (libKind == OntologyConstants.NodeLabels.EntryPoint)
                {
                    extraEntryPointName = ExtractIdentifierUsingLibraries(node, libKind);
                }

                string? extraExternalServiceName = null;
                if (libKind == OntologyConstants.NodeLabels.ExternalService)
                {
                    extraExternalServiceName = ExtractIdentifierUsingLibraries(node, libKind);
                }

                string? extraTableName = null;
                if (libKind == OntologyConstants.NodeLabels.Table)
                {
                    extraTableName = ExtractIdentifierUsingLibraries(node, libKind);
                }

                VisitSymbolNode(node, baseKind, baseName, depth, baseVisit, extraEntryPointName, extraExternalServiceName, extraTableName);
                return;
            }
        }

        var kind = libKind ?? baseKind;
        string? name = null;

        if (kind != null)
        {
            name = (libKind != null ? ExtractIdentifierUsingLibraries(node, kind) : null) ?? ExtractIdentifier(node);
        }

        var isSymbol = kind != null && !string.IsNullOrEmpty(name);

        if (isSymbol)
        {
            if (kind != null && name != null) VisitSymbolNode(node, kind, name, depth, baseVisit);
        }
        else
        {
            if (baseVisit != null) baseVisit(); else VisitChildren(node, depth);
        }
    }

    protected void VisitSymbolNode(Node node, string kind, string name, int depth, Action? baseVisit = null, string? extraEntryPointName = null, string? extraExternalServiceName = null, string? extraTableName = null)
    {
        if (kind == "Variable" || kind == OntologyConstants.NodeLabels.Member)
        {
            // Skip variable/member nodes in the graph as they are too deep level
            if (baseVisit != null) baseVisit(); else VisitChildren(node, depth);
            return;
        }

        var parent = SymbolStack.Peek();
        var syntacticNode = new SyntacticSymbol(kind, name, node);

        parent.Children.Add(syntacticNode);

        if (kind == OntologyConstants.NodeLabels.ExternalService)
        {
            _fusedDeclarative?.EnrichSymbol(node, syntacticNode, Context!);
            for (var i = 0; i < _imperativeExtensions.Count; i++)
            {
                _imperativeExtensions[i].EnrichSymbol(node, syntacticNode, Context!);
            }
            // External services represent target endpoints/hosts (leaf nodes).
            // Any inner calls or expressions within the call arguments belong to the enclosing function/scope (parent).
            if (baseVisit != null) baseVisit(); else VisitChildren(node, depth);
            return;
        }

        SymbolStack.Push(syntacticNode);
        PushedNodeIds.Push(node.Id);

        // Collect references for the symbol node itself (library references & SQL dependencies)
        if (_fusedDeclarative != null)
        {
            _fusedDeclarative.CollectReferences(node, "", syntacticNode.References, Context!);
            _fusedDeclarative.EnrichSymbol(node, syntacticNode, Context!);
        }
        for (var i = 0; i < _imperativeExtensions.Count; i++)
        {
            var libParser = _imperativeExtensions[i];
            libParser.CollectReferences(node, "", syntacticNode.References, Context!);
            libParser.EnrichSymbol(node, syntacticNode, Context!);
        }

        if (!string.IsNullOrEmpty(extraEntryPointName) && extraEntryPointName != name)
        {
            var epChild = new SyntacticSymbol(OntologyConstants.NodeLabels.EntryPoint, extraEntryPointName, node);
            if (_fusedDeclarative != null)
            {
                _fusedDeclarative.CollectReferences(node, "", epChild.References, Context!);
                _fusedDeclarative.EnrichSymbol(node, epChild, Context!);
            }
            for (var i = 0; i < _imperativeExtensions.Count; i++)
            {
                var libParser = _imperativeExtensions[i];
                libParser.CollectReferences(node, "", epChild.References, Context!);
                libParser.EnrichSymbol(node, epChild, Context!);
            }
            syntacticNode.Children.Add(epChild);
        }

        if (!string.IsNullOrEmpty(extraExternalServiceName) && extraExternalServiceName != name)
        {
            var extChild = new SyntacticSymbol(OntologyConstants.NodeLabels.ExternalService, extraExternalServiceName, node);
            if (_fusedDeclarative != null)
            {
                _fusedDeclarative.CollectReferences(node, "", extChild.References, Context!);
                _fusedDeclarative.EnrichSymbol(node, extChild, Context!);
            }
            for (var i = 0; i < _imperativeExtensions.Count; i++)
            {
                var libParser = _imperativeExtensions[i];
                libParser.CollectReferences(node, "", extChild.References, Context!);
                libParser.EnrichSymbol(node, extChild, Context!);
            }
            syntacticNode.Children.Add(extChild);
        }

        if (!string.IsNullOrEmpty(extraTableName))
        {
            var tblChild = new SyntacticSymbol(OntologyConstants.NodeLabels.Table, extraTableName, node);
            if (_fusedDeclarative != null)
            {
                _fusedDeclarative.CollectReferences(node, "", tblChild.References, Context!);
                _fusedDeclarative.EnrichSymbol(node, tblChild, Context!);
            }
            for (var i = 0; i < _imperativeExtensions.Count; i++)
            {
                var libParser = _imperativeExtensions[i];
                libParser.CollectReferences(node, "", tblChild.References, Context!);
                libParser.EnrichSymbol(node, tblChild, Context!);
            }
            syntacticNode.Children.Add(tblChild);
        }

        if (kind == OntologyConstants.NodeLabels.Topic &&
            !syntacticNode.Properties.ContainsKey(OntologyConstants.PropertyNames.MessagingDirection))
        {
            var subscribes = syntacticNode.References.Any(r =>
                r.Kind is CodeExplorer.Cypher.Common.RelationshipKind.SubscribesTo or CodeExplorer.Cypher.Common.RelationshipKind.Triggers);
            var publishes = syntacticNode.References.Any(r => r.Kind == CodeExplorer.Cypher.Common.RelationshipKind.PublishesTo);
            if (subscribes && !publishes)
            {
                syntacticNode.Properties[OntologyConstants.PropertyNames.MessagingDirection] = OntologyConstants.EvidenceKinds.Subscribe;
            }
        }

        if (kind == OntologyConstants.NodeLabels.Query)
        {
            NestedSqlParser.TryDetectSqlDependencies(syntacticNode.Text ?? node.Text, "", syntacticNode.References);
            if (parent.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function)
            {
                foreach (var r in syntacticNode.References)
                {
                    if (r.Kind is CodeExplorer.Cypher.Common.RelationshipKind.DependsOn
                            or CodeExplorer.Cypher.Common.RelationshipKind.ReadsFrom
                            or CodeExplorer.Cypher.Common.RelationshipKind.WritesTo &&
                        !parent.References.Contains(r))
                    {
                        parent.References.Add(r);
                    }
                }
            }
        }

        CollectProfileSymbolMetadata(node, syntacticNode);
        CollectCustomReferencesForSymbol(node, syntacticNode, parent);

        if (baseVisit != null) baseVisit(); else VisitChildren(node, depth);

        if (PushedNodeIds.Count <= 0 || PushedNodeIds.Peek() != node.Id)
        {
            return;
        }

        PushedNodeIds.Pop();
        SymbolStack.Pop();
    }
}
