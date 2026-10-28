using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

public abstract class BaseParserVisitor : TreeSitterAstVisitor
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
                    if (recNode.IsValid())
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
                            if (rChild.IsValid())
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
                    symbolNode.References.Add(new Reference("", methodName, "CALLS"));
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
        var syntacticNode = new SyntacticSymbol(kind, name, node) { Text = node.Text };

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
            var epChild = new SyntacticSymbol(OntologyConstants.NodeLabels.EntryPoint, extraEntryPointName, node) { Text = node.Text };
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

    protected override void VisitClassDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitInterfaceDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitMethodDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitFunctionDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        if (Profile.VariableDeclaratorNodeTypes.Count > 0)
        {
            CollectProfileVariables(node);
        }
        VisitSymbolOrBase(node, depth);
    }

    protected virtual void CollectProfileVariables(Node node)
    {
        var typeNode = node.GetField(SyntaxFields.Type);
        if (!typeNode.IsValid())
        {
            foreach (var child in node.Children)
            {
                if (child.IsValid() && Profile.IdentifierChildTypes.Contains(child.Type))
                {
                    typeNode = child;
                    break;
                }
            }
        }

        var typeName = typeNode.IsValid() ? typeNode.Text : "var";
        var isConstant = Profile.ConstantDeclarations.Contains(node.Type) || HasProfileConstantModifier(node);
        var scope = SymbolStack.Peek().Kind switch
        {
            "Class" or "Interface" or "Struct" => "class",
            _ => "local"
        };

        foreach (var declarator in node.Children)
        {
            if (declarator.IsValid() && Profile.VariableDeclaratorNodeTypes.Contains(declarator.Type))
            {
                var varName = ExtractProfileIdentifier(declarator);
                if (!string.IsNullOrEmpty(varName))
                {
                    var valNode = declarator.GetField(SyntaxFields.Value);
                    var initText = valNode.IsValid() ? valNode.Text : "";

                    RawVariables.Add(new RawVariable(
                        varName,
                        initText,
                        scope,
                        isConstant,
                        "",
                        declarator.StartPosition.Row + 1,
                        declarator.EndPosition.Row + 1,
                        declarator.StartPosition.Column + 1,
                        declarator.EndPosition.Column + 1
                    ));

                    RawTypeBindings.Add(new RawTypeBinding(varName, typeName, "", SymbolStack.Peek().Name));
                }
            }
        }
    }

    protected bool HasProfileConstantModifier(Node node)
    {
        if (Profile.ConstantModifierKeywords.Count == 0) return false;
        foreach (var child in node.Children)
        {
            if (!child.IsValid()) continue;
            if (Profile.ConstantModifierKeywords.Contains(child.Text)) return true;
            if (Profile.AnnotationContainerNodeTypes.Contains(child.Type))
            {
                foreach (var mod in child.Children)
                {
                    if (mod.IsValid() && Profile.ConstantModifierKeywords.Contains(mod.Text))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    protected override void VisitParameter(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        if (Profile.ImportIdentifierChildTypes.Count > 0 && !Profile.PackageDeclarationNodeTypes.Contains(node.Type))
        {
            CollectProfileImport(node);
        }
        VisitSymbolOrBase(node, depth);
    }

    protected virtual void CollectProfileImport(Node node)
    {
        string? importText = null;
        foreach (var pf in Profile.ImportPathFields)
        {
            var fNode = node.GetField(pf);
            if (fNode.IsValid())
            {
                importText = CleanStringLiteral(fNode.Text);
                break;
            }
        }

        if (string.IsNullOrEmpty(importText))
        {
            foreach (var child in node.Children)
            {
                if (child.IsValid() && Profile.ImportIdentifierChildTypes.Contains(child.Type))
                {
                    importText = CleanStringLiteral(child.Text);
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(importText))
        {
            var importType = FileParser.ResolveImportType(importText, RelativePath, AbsoluteWorkspacePath);
            RawImports.Add(new RawImport(importText, "", importType));
            ResolveAndInjectLibraryParser(importText);
        }
    }

    protected override void VisitInheritanceClause(Node node, int depth)
    {
        var currentScope = SymbolStack.Peek();
        if (currentScope.Kind != "file")
        {
            foreach (var child in node.Children)
            {
                if (child.IsValid() && (child.Type.Contains("identifier", StringComparison.Ordinal) || child.Type.Contains("name", StringComparison.Ordinal)))
                {
                    var baseName = child.Text;
                    var refKind = baseName.StartsWith('I') && baseName.Length > 1 && char.IsUpper(baseName[1])
                        ? "IMPLEMENTS"
                        : "INHERITS";
                    currentScope.References.Add(new Reference("", baseName, refKind));
                }
            }
        }
        base.VisitInheritanceClause(node, depth);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool MayContainSymbolsOrReferences(Node node)
    {
        var type = node.Type;
        if (type is "identifier" or "type_identifier" or "predefined_type" or "generic_name" or "qualified_name"
            or "block" or "statement_block" or "argument_list" or "parameter_list" or "type_argument_list"
            or "equals_value_clause" or "variable_declarator" or "return_statement" or "if_statement"
            or "for_statement" or "foreach_statement" or "while_statement" or "binary_expression"
            or "parenthesized_expression" or "assignment_expression" or "member_access_expression"
            or "cast_expression" or "conditional_expression" or "empty_statement")
        {
            return false;
        }
        return true;
    }

    protected override void VisitDefault(Node node, int depth)
    {
        if (!MayContainSymbolsOrReferences(node))
        {
            VisitChildren(node, depth);
            return;
        }

        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitStringLiteral(Node node, int depth)
    {
        var currentScope = SymbolStack.Peek();
        if (currentScope.Kind == OntologyConstants.NodeLabels.Query)
        {
            var cleanedLiteral = CleanStringLiteral(node.Text);
            if ((string.IsNullOrEmpty(currentScope.Text) || !NestedSqlParser.TryParseSql(currentScope.Text, out _, out _)) &&
                NestedSqlParser.TryParseSql(cleanedLiteral, out _, out _))
            {
                currentScope.Text = cleanedLiteral;
            }
            if (!WorkspaceConventions.IsFrontendFilePath(RelativePath))
            {
                NestedSqlParser.TryDetectSqlDependencies(cleanedLiteral, "", currentScope.References);
                var enclosingFunc = SymbolStack.FirstOrDefault(s => s.Kind == OntologyConstants.NodeLabels.Function);
                if (enclosingFunc != null)
                {
                    foreach (var r in currentScope.References)
                    {
                        if (r.Kind == OntologyConstants.Relationships.DependsOn && !enclosingFunc.References.Contains(r))
                        {
                            enclosingFunc.References.Add(r);
                        }
                    }
                }
            }
            base.VisitStringLiteral(node, depth);
            return;
        }

        if (!WorkspaceConventions.IsFrontendFilePath(RelativePath) && NestedSqlParser.TryParseSql(node.Text, out _, out _))
        {
            var name = "Query";

            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                name = $"{firstWord} Query";
            }

            VisitSymbolNode(node, OntologyConstants.NodeLabels.Query, name, depth,
                () => base.VisitStringLiteral(node, depth));
        }
        else
        {
            if (currentScope.Kind != "file" && !WorkspaceConventions.IsFrontendFilePath(RelativePath))
            {
                NestedSqlParser.TryDetectSqlDependencies(node.Text, "", currentScope.References);
            }

            base.VisitStringLiteral(node, depth);
        }
    }

    protected override void VisitCallExpression(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () =>
        {
            var currentScope = SymbolStack.Peek();

            foreach (var libParser in LibraryParsers)
            {
                libParser.CollectReferences(node, "", currentScope.References, Context!);
            }

            var callName = FindCallName(node);

            if (!string.IsNullOrEmpty(callName) && currentScope.Kind != "file")
            {
                currentScope.References.Add(new Reference("", callName, "CALLS",
                    node.StartPosition.Row + 1,
                    node.StartPosition.Column + 1));
            }


            CollectCallLiteralArguments(node, callName, currentScope);

            base.VisitCallExpression(node, depth);
        });
    }

    protected virtual void CollectCallLiteralArguments(Node callNode, string? callName, SyntacticSymbol currentScope)
    {
        if (IsTestFile)
        {
            return;
        }

        callName ??= FindCallName(callNode);
        var (role, isDecorator) = CallSemanticClassifier.Classify(callNode, callName);

        var argsNode = callNode.GetField(SyntaxFields.Arguments) ??
                       callNode.FindChildOfType("argument_list") ??
                       callNode.FindChildOfType("arguments") ??
                       callNode.FindChildOfType("attribute_argument_list");

        if (!argsNode.IsValid())
        {
            argsNode = callNode.Children.FirstOrDefault(c => c.Type.Contains("argument"));
        }

        if (!argsNode.IsValid()) return;

        var argIndex = 0;
        foreach (var child in argsNode.Children)
        {
            if (child.Type is "(" or ")" or "," or ";" or "{" or "}" or "[" or "]")
            {
                continue;
            }

            var exprNode = child;
            if (child.Type is "argument" or "attribute_argument")
            {
                var valueCandidate = child.Children.LastOrDefault(c => c.Type is not "name_colon" and not ":" and not ",");
                if (valueCandidate.IsValid())
                {
                    exprNode = valueCandidate;
                }
            }

            if (IsStringLiteralNode(exprNode))
            {
                var cleaned = CleanStringLiteral(exprNode.Text);

                if (!string.IsNullOrWhiteSpace(cleaned) &&
                    !NestedSqlParser.TryParseSql(cleaned, out _, out _))
                {
                    var line = exprNode.StartPosition.Row + 1;
                    var col = exprNode.StartPosition.Column + 1;
                    var displayName = cleaned.Length > 80 ? cleaned[..77] + "..." : cleaned;

                    var literalSymbol = new SyntacticSymbol(OntologyConstants.NodeLabels.CallLiteral, displayName, exprNode)
                    {
                        Text = cleaned
                    };
                    literalSymbol.Properties["value"] = cleaned;
                    literalSymbol.Properties["callee"] = callName ?? "";
                    literalSymbol.Properties["arg_index"] = argIndex.ToString();
                    literalSymbol.Properties["file_path"] = RelativePath;
                    literalSymbol.Properties["line"] = line.ToString();
                    literalSymbol.Properties["column"] = col.ToString();
                    literalSymbol.Properties["role"] = role;
                    literalSymbol.Properties["is_decorator"] = isDecorator ? "true" : "false";

                    currentScope.Children.Add(literalSymbol);
                }
            }

            argIndex++;
        }
    }

    protected override void VisitAttribute(Node node, int depth)
    {
        var attrName = node.Children.FirstOrDefault(c => c.Type.Contains("identifier"))?.Text ?? node.Text;
        if (!string.IsNullOrEmpty(attrName))
        {
            var currentScope = SymbolStack.Peek();
            CollectCallLiteralArguments(node, attrName, currentScope);
        }
        base.VisitAttribute(node, depth);
    }

    protected override void VisitCatchClause(Node node, int depth)
    {
        if (IsTestFile)
        {
            VisitChildren(node, depth);
            return;
        }

        string? exType = null;
        Node? bodyBlock = null;

        foreach (var child in node.Children)
        {
            var cType = child.Type;
            if (cType is "catch_declaration" or "catch_formal_parameter")
            {
                foreach (var grandChild in child.Children)
                {
                    if (grandChild.Type is "type_identifier" or "identifier" or "qualified_name" or "generic_name")
                    {
                        exType = grandChild.Text;
                        break;
                    }
                }
            }
            else if (cType is "block" or "statement_block")
            {
                bodyBlock = child;
            }
            else if (cType is "identifier" && exType == null)
            {
                exType = child.Text;
            }
            else if (cType is "as_pattern")
            {
                var first = child.Children.FirstOrDefault();
                if (first != null && first.IsValid()) exType = first.Text;
            }
        }

        var statementCount = 0;
        var hasComment = false;

        if (bodyBlock != null && bodyBlock.IsValid())
        {
            var text = bodyBlock.Text;
            if (text.Contains("//") || text.Contains("/*") || text.Contains("#"))
            {
                hasComment = true;
            }

            foreach (var child in bodyBlock.Children)
            {
                var cType = child.Type;
                if (cType is "{" or "}" or ":" or ";")
                {
                    continue;
                }

                if (cType.Contains("comment"))
                {
                    hasComment = true;
                    continue;
                }

                // In Python, pass_statement is the syntactical placeholder for an empty block
                if (cType == "pass_statement")
                {
                    continue;
                }

                statementCount++;
            }
        }

        var isEmpty = statementCount == 0;
        var currentScope = SymbolStack.Peek();
        var catchSymbol = new SyntacticSymbol(OntologyConstants.NodeLabels.CatchClause, exType ?? "catch", node);
        catchSymbol.Properties["exception_type"] = exType ?? "";
        catchSymbol.Properties["is_empty"] = isEmpty.ToString().ToLowerInvariant();
        catchSymbol.Properties["has_comment"] = hasComment.ToString().ToLowerInvariant();
        catchSymbol.Properties["statement_count"] = statementCount.ToString();
        catchSymbol.Properties["line"] = (node.StartPosition.Row + 1).ToString();
        catchSymbol.Properties["column"] = (node.StartPosition.Column + 1).ToString();

        currentScope.Children.Add(catchSymbol);

        VisitChildren(node, depth);
    }

    protected bool IsStringLiteralNode(Node node)
    {
        if (!node.IsValid()) return false;
        var type = node.Type;

        if (Profile.StringLiterals.Contains(type)) return true;

        if (type is "string_literal" or "verbatim_string_literal" or "raw_string_literal" or "string" or "character_literal")
        {
            return true;
        }

        return false;
    }

    protected static string CleanStringLiteral(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var s = text.Trim();

        if (s.StartsWith("r#\"", StringComparison.Ordinal) && s.EndsWith("\"#", StringComparison.Ordinal) && s.Length >= 5)
        {
            return s[3..^2];
        }
        if (s.StartsWith("r##\"", StringComparison.Ordinal) && s.EndsWith("\"##", StringComparison.Ordinal) && s.Length >= 7)
        {
            return s[4..^3];
        }
        if (s.StartsWith("\"\"\"") && s.EndsWith("\"\"\"") && s.Length >= 6)
        {
            return s[3..^3].Trim('\r', '\n');
        }
        if (s.StartsWith("@\"") && s.EndsWith("\"") && s.Length >= 3)
        {
            return s[2..^1].Replace("\"\"", "\"");
        }
        if (s.StartsWith("$\"") && s.EndsWith("\"") && s.Length >= 3)
        {
            return s[2..^1];
        }
        if (s.StartsWith('"') && s.EndsWith('"') && s.Length >= 2)
        {
            return s[1..^1];
        }
        if (s.StartsWith('\'') && s.EndsWith('\'') && s.Length >= 2)
        {
            return s[1..^1];
        }
        if (s.StartsWith('`') && s.EndsWith('`') && s.Length >= 2)
        {
            return s[1..^1];
        }

        return s;
    }
}

