using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using TreeSitter;

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
        var type = FileParser.ResolveImportType(importPath, RelativePath, AbsoluteWorkspacePath);
        if (type == ImportType.External || ExtensionRegistry.Match(importPath) != null)
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
        else
        {
            // For local imports referencing messaging/queue helpers (e.g. ./rabbit/rabbit, ../util/pubsub/GoogleCloudPublisher)
            if (importPath.Contains("rabbit", StringComparison.OrdinalIgnoreCase))
            {
                var matches = ExtensionRegistry.MatchAll("amqplib");
                if (matches.Count == 0) matches = ExtensionRegistry.MatchAll("github.com/rabbitmq/amqp091-go");
                foreach (var match in matches)
                {
                    if (match.IsImplemented && !SemanticExtensions.Contains(match))
                    {
                        SemanticExtensions.Add(match);
                    }
                }
            }
            if (importPath.Contains("pubsub", StringComparison.OrdinalIgnoreCase) ||
                importPath.Contains("pub-sub", StringComparison.OrdinalIgnoreCase))
            {
                var matches = ExtensionRegistry.MatchAll("@google-cloud/pubsub");
                if (matches.Count == 0) matches = ExtensionRegistry.MatchAll("cloud.google.com/go/pubsub");
                foreach (var match in matches)
                {
                    if (match.IsImplemented && !SemanticExtensions.Contains(match))
                    {
                        SemanticExtensions.Add(match);
                    }
                }
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
        Profile = fileParser.SyntaxProfile ?? LanguageSyntaxProfile.Empty;
        RootSymbol = new SyntacticSymbol("file", "root", rootNode);
        SymbolStack.Push(RootSymbol);
    }

    protected virtual string? MapNodeType(Node node) => null;

    protected virtual string? ExtractIdentifier(Node node) => null;

    protected virtual void CollectCustomReferencesForSymbol(
        Node node,
        SyntacticSymbol symbolNode,
        SyntacticSymbol parentNode)
    {
    }

    protected abstract string? FindCallName(Node callNode);

    protected override void VisitNode(Node node, int depth)
    {
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
                if (node.IsAny(TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.Common.TypeIdentifier))
                {
                    currentScope.References.Add(new Reference("", node.Text,
                        OntologyConstants.Relationships.PotentialType));
                }
            }

            if (LibraryParsers.Count > 0)
            {
                foreach (var libParser in LibraryParsers)
                {
                    libParser.CollectReferences(node, "", currentScope.References, null!);
                    if (currentScope.Kind == OntologyConstants.NodeLabels.Query)
                    {
                        var enclosingFunc = SymbolStack.FirstOrDefault(s => s.Kind == OntologyConstants.NodeLabels.Function);
                        if (enclosingFunc != null)
                        {
                            libParser.CollectReferences(node, "", enclosingFunc.References, null!);
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

        if (Profile.ClassDeclarations.Contains(type))
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

            var kind = lp.MapNodeType(node, null!);
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
                isMatch = lp.MapNodeType(node, null!) == kind;
            }

            if (isMatch)
            {
                var name = lp.ExtractIdentifier(node, Context!, ProjectName, RelativePath);
                if (name != null) return name;
            }
        }

        return null;
    }

    protected void VisitSymbolOrBase(Node node, int depth, Action baseVisit)
    {
        var kind = MapNodeTypeUsingLibraries(node) ?? MapNodeType(node);
        string? name = null;

        if (kind != null)
        {
            name = ExtractIdentifierUsingLibraries(node, kind) ?? ExtractIdentifier(node);
        }

        var isSymbol = kind != null && !string.IsNullOrEmpty(name);

        if (isSymbol)
        {
            VisitSymbolNode(node, kind!, name!, depth, baseVisit);
        }
        else
        {
            baseVisit();
        }
    }

    protected void VisitSymbolNode(Node node, string kind, string name, int depth, Action baseVisit)
    {
        if (kind == "Variable" || kind == OntologyConstants.NodeLabels.Member)
        {
            // Skip variable/member nodes in the graph as they are too deep level
            baseVisit();
            return;
        }

        var parent = SymbolStack.Peek();
        var syntacticNode = new SyntacticSymbol(kind, name, node) { Text = node.Text };

        parent.Children.Add(syntacticNode);

        if (kind == OntologyConstants.NodeLabels.ExternalService)
        {
            // External services represent target endpoints/hosts (leaf nodes).
            // Any inner calls or expressions within the call arguments belong to the enclosing function/scope (parent).
            baseVisit();
            return;
        }

        SymbolStack.Push(syntacticNode);
        PushedNodeIds.Push(node.Id);

        // Collect references for the symbol node itself (library references & SQL dependencies)
        foreach (var libParser in LibraryParsers)
        {
            libParser.CollectReferences(node, "", syntacticNode.References, null!);
            libParser.EnrichSymbol(node, syntacticNode, null!);
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
            NestedSqlParser.TryDetectSqlDependencies(node.Text, "", syntacticNode.References);
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

        CollectCustomReferencesForSymbol(node, syntacticNode, parent);

        baseVisit();

        if (PushedNodeIds.Count <= 0 || PushedNodeIds.Peek() != node.Id)
        {
            return;
        }

        PushedNodeIds.Pop();
        SymbolStack.Pop();
    }

    protected override void VisitClassDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () => base.VisitClassDeclaration(node, depth));
    }

    protected override void VisitInterfaceDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () => base.VisitInterfaceDeclaration(node, depth));
    }

    protected override void VisitMethodDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () => base.VisitMethodDeclaration(node, depth));
    }

    protected override void VisitFunctionDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () => base.VisitFunctionDeclaration(node, depth));
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () => base.VisitVariableDeclaration(node, depth));
    }

    protected override void VisitParameter(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () => base.VisitParameter(node, depth));
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () => base.VisitImportStatement(node, depth));
    }

    protected override void VisitDefault(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () => base.VisitDefault(node, depth));
    }

    protected override void VisitStringLiteral(Node node, int depth)
    {
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
            var currentScope = SymbolStack.Peek();

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
                libParser.CollectReferences(node, "", currentScope.References, null!);
            }

            var callName = FindCallName(node);

            if (!string.IsNullOrEmpty(callName) && currentScope.Kind != "file")
            {
                currentScope.References.Add(new Reference("", callName, "CALLS"));
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

        var argsNode = callNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList) ??
                       callNode.FindChildOfType(TreeSitterSyntax.Common.Arguments) ??
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

        return s;
    }
}
