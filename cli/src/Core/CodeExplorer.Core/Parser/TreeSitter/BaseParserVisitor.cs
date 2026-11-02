using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

public abstract partial class BaseParserVisitor : TreeSitterAstVisitor
{
    protected readonly List<ISemanticExtension> SemanticExtensions;
    protected List<ISemanticExtension> LibraryParsers => SemanticExtensions;
    protected readonly LanguageSyntaxProfile Profile;

    public string RelativePath { get; }
    public string AbsoluteWorkspacePath { get; }
    public IFileParser FileParser { get; }
    public SemanticExtensionRegistry ExtensionRegistry { get; }
    public SemanticExtensionRegistry LibraryRegistry => ExtensionRegistry;
    public string? ProjectName { get; set; }
    public ParsingContext? Context { get; set; }
    public bool IsTestFile { get; set; }

    public void ResolveAndInjectExtension(string importPath)
    {
        var matches = ExtensionRegistry.MatchAll(importPath);
        foreach (var match in matches)
        {
            if (match.IsImplemented && !SemanticExtensions.Contains(match))
            {
                SemanticExtensions.Add(match);
            }
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
        var type = node.Type;

        if (Profile.StringLiterals.Contains(type) && !WorkspaceConventions.IsFrontendFilePath(RelativePath))
        {
            if (NestedSqlParser.TryParseSql(CleanStringLiteral(node.Text), out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        if (Profile.StructDeclarations.Contains(type))
        {
            return Profile.DefaultStructKind;
        }

        if (Profile.EnumDeclarations.Contains(type))
        {
            return Profile.DefaultEnumKind;
        }

        if (Profile.ClassDeclarations.Contains(type))
        {
            return "Class";
        }

        if (Profile.InterfaceDeclarations.Contains(type))
        {
            return "Interface";
        }

        if (Profile.MethodDeclarations.Contains(type) || Profile.FunctionDeclarations.Contains(type))
        {
            return OntologyConstants.NodeLabels.Function;
        }

        return null;
    }

    protected string? ExtractProfileIdentifier(Node node)
    {
        if (Profile.StringLiterals.Contains(node.Type) && !WorkspaceConventions.IsFrontendFilePath(RelativePath))
        {
            if (NestedSqlParser.TryParseSql(CleanStringLiteral(node.Text), out var firstWord, out _))
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

        foreach (var child in node.Children)
        {
            if (child.IsValid() && Profile.IdentifierChildTypes.Contains(child.Type))
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
        var type = callNode.Type;

        if (Profile.ObjectCreationNodeTypes.Contains(type))
        {
            var typeNode = callNode.GetField(SyntaxFields.Type);
            if (typeNode.IsValid())
            {
                return typeNode.Text;
            }

            foreach (var child in callNode.Children)
            {
                if (child.IsValid() && Profile.IdentifierChildTypes.Contains(child.Type))
                {
                    return child.Text;
                }
            }
            return null;
        }

        if (Profile.ConstructorSelfCallNodeTypes.Contains(type))
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
                        return $"{recNode.Text}{Profile.CallMemberSeparator}{methodName}";
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
            if (Profile.IdentifierChildTypes.Contains(funcNode.Type))
            {
                return NormalizeIdentifierText(funcNode.Text);
            }

            if (Profile.MemberAccessExpressions.Contains(funcNode.Type))
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

                if (!string.IsNullOrEmpty(memberPart))
                {
                    if (Profile.IncludeReceiverInCallName)
                    {
                        foreach (var rf in Profile.CallReceiverFields)
                        {
                            var rChild = funcNode.GetField(rf);
                            if (rChild.IsValid() && (rChild.EndIndex - rChild.StartIndex) <= 128)
                            {
                                return $"{rChild.Text}{Profile.CallMemberSeparator}{memberPart}";
                            }
                        }
                    }
                    return memberPart;
                }
            }
        }

        foreach (var child in callNode.Children)
        {
            if (child.IsValid() && Profile.IdentifierChildTypes.Contains(child.Type))
            {
                return NormalizeIdentifierText(child.Text);
            }
        }

        return null;
    }

    protected virtual void CollectProfileSymbolMetadata(Node node, SyntacticSymbol symbolNode)
    {
        // 1. If EntryPoint from an annotation/attribute, connect to the enclosing method/class
        if (symbolNode.Kind == OntologyConstants.NodeLabels.EntryPoint && Profile.AnnotationNodeTypes.Contains(node.Type))
        {
            var enclosing = FindEnclosingDeclaration(node);
            if (enclosing.IsValid())
            {
                var methodName = ExtractProfileIdentifier(enclosing);
                if (!string.IsNullOrEmpty(methodName) &&
                    !symbolNode.References.Any(r => r.Kind == "CALLS" && r.TargetName == methodName))
                {
                    symbolNode.References.Add(new Reference("", methodName, "CALLS",
                        node.StartPosition.Row + 1,
                        node.StartPosition.Column + 1));
                }
            }
        }

        // 2. Class/Interface/Struct inheritance, implemented interfaces, and annotations
        if (symbolNode.Kind is "Class" or "Interface" or "Struct")
        {
            if (Profile.SuperclassClauseNodeTypes.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    if (child.IsValid() && Profile.SuperclassClauseNodeTypes.Contains(child.Type))
                    {
                        foreach (var typeChild in child.Children)
                        {
                            if (typeChild.IsValid() && Profile.IdentifierChildTypes.Contains(typeChild.Type))
                            {
                                symbolNode.References.Add(new Reference("", typeChild.Text, "INHERITS"));
                                break;
                            }
                        }
                    }
                }
            }

            if (Profile.InterfaceClauseNodeTypes.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    if (child.IsValid() && Profile.InterfaceClauseNodeTypes.Contains(child.Type))
                    {
                        foreach (var typeChild in child.Children)
                        {
                            if (typeChild.IsValid() && (Profile.IdentifierChildTypes.Contains(typeChild.Type) || typeChild.Type == "scoped_identifier"))
                            {
                                symbolNode.References.Add(new Reference("", typeChild.Text, "IMPLEMENTS"));
                            }
                        }
                    }
                }
            }

            ExtractProfileAnnotations(node, symbolNode);
        }

        // 3. Function/Method annotations
        if (symbolNode.Kind == OntologyConstants.NodeLabels.Function)
        {
            ExtractProfileAnnotations(node, symbolNode);
        }
    }

    protected Node? FindEnclosingDeclaration(Node node)
    {
        var current = node.Parent;
        while (current.IsValid())
        {
            var t = current.Type;
            if (Profile.MethodDeclarations.Contains(t) ||
                Profile.FunctionDeclarations.Contains(t) ||
                Profile.ClassDeclarations.Contains(t) ||
                Profile.InterfaceDeclarations.Contains(t) ||
                Profile.StructDeclarations.Contains(t))
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

            if (Profile.AnnotationNodeTypes.Contains(child.Type))
            {
                AttachAnnotationReference(child, symbolNode);
            }
            else if (Profile.AnnotationContainerNodeTypes.Contains(child.Type))
            {
                foreach (var grandChild in child.Children)
                {
                    if (grandChild.IsValid() && Profile.AnnotationNodeTypes.Contains(grandChild.Type))
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
        foreach (var child in annotationNode.Children)
        {
            if (child.IsValid() && (Profile.IdentifierChildTypes.Contains(child.Type) || child.Type is "scoped_identifier" or "qualified_name"))
            {
                return child.Text;
            }
        }
        var nameField = annotationNode.GetField(SyntaxFields.Name);
        return nameField.IsValid() ? nameField.Text : null;
    }

    protected override void VisitNode(Node node, int depth)
    {
        if (node is TokenSyntax) return;

        if (Profile.PackageDeclarationNodeTypes.Contains(node.Type))
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

            if (currentScope.Kind != "file")
            {
                if (node is IdentifierExprSyntax || node.IsAny("identifier", "type_identifier"))
                {
                    currentScope.References.Add(new Reference("", node.Text,
                        OntologyConstants.Relationships.PotentialType,
                        node.StartPosition.Row + 1,
                        node.StartPosition.Column + 1));
                }
            }


            if (LibraryParsers.Count > 0 && MayContainSymbolsOrReferences(node))
            {
                foreach (var libParser in LibraryParsers)
                {
                    libParser.CollectReferences(node, "", currentScope.References, Context!);
                    if (currentScope.Kind == OntologyConstants.NodeLabels.Query)
                    {
                        var enclosingFunc = SymbolStack.FirstOrDefault(s => s.Kind == OntologyConstants.NodeLabels.Function);
                        if (enclosingFunc != null)
                        {
                            libParser.CollectReferences(node, "", enclosingFunc.References, Context!);
                        }
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
        var type = node.Type;

        if (Profile.StringLiterals.Contains(type)
            || (type.Contains("string") && !Profile.ExcludedStringInterpolations.Contains(type)))
        {
            VisitStringLiteral(node, depth);
            return;
        }

        if (Profile.ClassDeclarations.Contains(type) || Profile.EnumDeclarations.Contains(type) || Profile.StructDeclarations.Contains(type))
        {
            VisitClassDeclaration(node, depth);
            return;
        }

        if (Profile.InterfaceDeclarations.Contains(type))
        {
            VisitInterfaceDeclaration(node, depth);
            return;
        }

        if (Profile.MethodDeclarations.Contains(type))
        {
            VisitMethodDeclaration(node, depth);
            return;
        }

        if (Profile.FunctionDeclarations.Contains(type))
        {
            VisitFunctionDeclaration(node, depth);
            return;
        }

        if (Profile.VariableDeclarations.Contains(type))
        {
            VisitVariableDeclaration(node, depth);
            return;
        }

        if (Profile.Parameters.Contains(type))
        {
            VisitParameter(node, depth);
            return;
        }

        if (Profile.Imports.Contains(type))
        {
            VisitImportStatement(node, depth);
            return;
        }

        if (Profile.Calls.Contains(type))
        {
            VisitCallExpression(node, depth);
            return;
        }

        if (Profile.Inheritance.Contains(type))
        {
            VisitInheritanceClause(node, depth);
            return;
        }

        if (Profile.CatchClauses.Contains(type))
        {
            VisitCatchClause(node, depth);
            return;
        }

        VisitDefault(node, depth);
    }

    protected string? MapNodeTypeUsingLibraries(Node node)
    {
        foreach (var lp in LibraryParsers)
        {
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
        foreach (var lp in LibraryParsers)
        {
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

                VisitSymbolNode(node, baseKind, baseName, depth, baseVisit, extraEntryPointName);
                return;
            }
        }

        var kind = libKind ?? baseKind;
        string? name = null;

        if (kind != null)
        {
            name = ExtractIdentifierUsingLibraries(node, kind) ?? ExtractIdentifier(node);
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

    protected void VisitSymbolNode(Node node, string kind, string name, int depth, Action? baseVisit = null, string? extraEntryPointName = null)
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
            // External services represent target endpoints/hosts (leaf nodes).
            // Any inner calls or expressions within the call arguments belong to the enclosing function/scope (parent).
            if (baseVisit != null) baseVisit(); else VisitChildren(node, depth);
            return;
        }

        SymbolStack.Push(syntacticNode);
        PushedNodeIds.Push(node.Id);

        // Collect references for the symbol node itself (library references & SQL dependencies)
        foreach (var libParser in LibraryParsers)
        {
            libParser.CollectReferences(node, "", syntacticNode.References, Context!);
            libParser.EnrichSymbol(node, syntacticNode, Context!);
        }

        if (!string.IsNullOrEmpty(extraEntryPointName) && extraEntryPointName != name)
        {
            var epChild = new SyntacticSymbol(OntologyConstants.NodeLabels.EntryPoint, extraEntryPointName, node);
            foreach (var libParser in LibraryParsers)
            {
                libParser.CollectReferences(node, "", epChild.References, Context!);
                libParser.EnrichSymbol(node, epChild, Context!);
            }
            syntacticNode.Children.Add(epChild);
        }

        if (kind == OntologyConstants.NodeLabels.Topic &&
            !syntacticNode.Properties.ContainsKey(OntologyConstants.PropertyNames.MessagingDirection))
        {
            var subscribes = syntacticNode.References.Any(r =>
                r.Kind is OntologyConstants.Relationships.SubscribesTo or OntologyConstants.Relationships.Triggers);
            var publishes = syntacticNode.References.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo);
            if (subscribes && !publishes)
            {
                syntacticNode.Properties[OntologyConstants.PropertyNames.MessagingDirection] = OntologyConstants.EvidenceKinds.Subscribe;
            }
        }

        if (kind == OntologyConstants.NodeLabels.Query)
        {
            NestedSqlParser.TryDetectSqlDependencies(syntacticNode.Text ?? node.Text, "", syntacticNode.References);
            if (parent.Kind == OntologyConstants.NodeLabels.Function)
            {
                foreach (var r in syntacticNode.References)
                {
                    if (r.Kind == OntologyConstants.Relationships.DependsOn && !parent.References.Contains(r))
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
