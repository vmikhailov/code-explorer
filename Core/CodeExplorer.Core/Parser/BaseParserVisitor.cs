using System;
using System.Collections.Generic;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public abstract class BaseParserVisitor : TreeSitterAstVisitor
{
    protected readonly List<ILibraryParser> LibraryParsers;

    public List<RawImport> RawImports { get; } = new();
    public List<RawVariable> RawVariables { get; } = new();
    public List<RawTypeBinding> RawTypeBindings { get; } = new();

    public SyntacticSymbol RootSymbol { get; }
    protected readonly Stack<SyntacticSymbol> SymbolStack = new();
    protected readonly Stack<IntPtr> PushedNodeIds = new();

    protected BaseParserVisitor(Node rootNode, List<ILibraryParser> libraryParsers)
    {
        LibraryParsers = libraryParsers;
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
        // 1. General reference collection
        var currentScope = SymbolStack.Peek();

        if (currentScope.Kind != "file")
        {
            if (node.Type is "identifier" or "type_identifier")
            {
                currentScope.References.Add(new Reference("", node.Text,
                    OntologyConstants.Relationships.PotentialType));
            }
        }

        foreach (var libParser in LibraryParsers)
        {
            libParser.CollectReferences(node, "", currentScope.References, null!);
        }

        // 2. Dispatch to specific typed visit methods
        Dispatch(node, depth);
    }

    protected virtual void Dispatch(Node node, int depth)
    {
        // Union of all string/literal node types
        if (node.Type is "string" 
                or "template_string" 
                or "string_literal" 
                or "interpreted_string_literal"
                or "raw_string_literal" 
            || (node.Type.Contains("string") 
                && node.Type != "interpolated_string_expression" 
                && node.Type != "interpolated_verbatim_string_expression" 
                && node.Type != "interpolated_raw_string_expression"))
        {
            VisitStringLiteral(node, depth);
            return;
        }

        switch (node.Type)
        {
            // Class Declarations
            case "class_declaration":
            case "class_expression":
            case "enum_declaration":
            case "struct_declaration":
            case "record_declaration":
            case "class_definition":
                VisitClassDeclaration(node, depth);
                break;

            // Interface Declarations
            case "interface_declaration":
            case "type_alias_declaration":
                VisitInterfaceDeclaration(node, depth);
                break;

            // Method Declarations
            case "method_definition":
            case "method_declaration":
            case "constructor_declaration":
            case "local_function_statement":
                VisitMethodDeclaration(node, depth);
                break;

            // Function Declarations
            case "function_declaration":
            case "function_expression":
            case "arrow_function":
            case "function_definition":
                VisitFunctionDeclaration(node, depth);
                break;

            // Variable/Field Declarations
            case "variable_declarator":
            case "public_field_definition":
            case "property_definition":
            case "property_declaration":
            case "variable_declaration":
            case "const_spec":
            case "var_spec":
            case "field_declaration":
            case "short_var_declaration":
            case "assignment":
            case "parameters":
            case "pattern":
                VisitVariableDeclaration(node, depth);
                break;

            // Parameters
            case "parameter":
            case "parameter_declaration":
            case "required_parameter":
            case "optional_parameter":
            case "parameter_property":
                VisitParameter(node, depth);
                break;

            // Imports
            case "import_statement":
            case "import_from_statement":
            case "using_directive":
            case "import_spec":
                VisitImportStatement(node, depth);
                break;

            // Call Expressions
            case "call_expression":
            case "invocation_expression":
            case "call":
                VisitCallExpression(node, depth);
                break;

            // Inheritance Clauses
            case "extends_clause":
            case "implements_clause":
            case "base_list":
                VisitInheritanceClause(node, depth);
                break;

            default:
                VisitDefault(node, depth);
                break;
        }
    }

    protected string? MapNodeTypeUsingLibraries(Node node)
    {
        foreach (var lp in LibraryParsers)
        {
            var kind = lp.MapNodeType(node, null!);
            if (kind != null) return kind;
        }

        return null;
    }

    protected string? ExtractIdentifierUsingLibraries(Node node, string kind)
    {
        foreach (var lp in LibraryParsers)
        {
            if (lp.MapNodeType(node, null!) == kind)
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
        if (kind == OntologyConstants.NodeLabels.Variable)
        {
            // Skip variable nodes in the graph as they are too deep level
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
