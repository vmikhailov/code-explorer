using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public abstract class BaseParserVisitor : TreeSitterAstVisitor
{
    protected readonly List<ILibraryParser> LibraryParsers;
    protected readonly LanguageSyntaxProfile Profile;

    public string RelativePath { get; }
    public string AbsoluteWorkspacePath { get; }
    public IFileParser FileParser { get; }
    public LibraryTrieRegistry LibraryRegistry { get; }

    public void ResolveAndInjectLibraryParser(string importPath)
    {
        var type = FileParser.ResolveImportType(importPath, RelativePath, AbsoluteWorkspacePath);
        if (type == ImportType.External)
        {
            var match = LibraryRegistry.Match(importPath);
            if (match != null)
            {
                if (match.IsImplemented)
                {
                    if (!LibraryParsers.Contains(match))
                    {
                        LibraryParsers.Add(match);
                    }
                }
                else
                {
                    // Library detected but parser is not implemented yet.
                }

                // Library detected but parser is not implemented yet.

            }
        }
    }

    public List<RawImport> RawImports { get; } = [];
    public List<RawVariable> RawVariables { get; } = [];
    public List<RawTypeBinding> RawTypeBindings { get; } = [];

    public SyntacticSymbol RootSymbol { get; }
    protected readonly Stack<SyntacticSymbol> SymbolStack = new();
    protected readonly Stack<IntPtr> PushedNodeIds = new();

    public SequenceDetector<Node> SequenceDetector { get; } = new();

    protected BaseParserVisitor(
        Node rootNode,
        List<ILibraryParser> libraryParsers,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
    {
        LibraryParsers = libraryParsers;
        RelativePath = relativePath;
        AbsoluteWorkspacePath = absoluteWorkspacePath;
        FileParser = fileParser;
        LibraryRegistry = libraryRegistry;
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
                var name = lp.ExtractIdentifier(node, null!);
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
        SymbolStack.Push(syntacticNode);
        PushedNodeIds.Push(node.Id);

        // Collect references for the symbol node itself (library references & SQL dependencies)
        foreach (var libParser in LibraryParsers)
        {
            libParser.CollectReferences(node, "", syntacticNode.References, null!);
            libParser.EnrichSymbol(node, syntacticNode, null!);
        }

        if (kind == OntologyConstants.NodeLabels.Query)
        {
            NestedSqlParser.TryDetectSqlDependencies(node.Text, "", syntacticNode.References);
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
        if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
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

            if (currentScope.Kind != "file")
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

            if (currentScope.Kind != "file")
            {
                var callName = FindCallName(node);

                if (!string.IsNullOrEmpty(callName))
                {
                    currentScope.References.Add(new Reference("", callName, "CALLS"));
                }
            }

            base.VisitCallExpression(node, depth);
        });
    }
}
