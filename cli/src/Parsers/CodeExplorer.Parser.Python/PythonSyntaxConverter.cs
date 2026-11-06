using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Python;

/// <summary>
/// Converts native Tree-sitter Python CST nodes into the Universal AST (<see cref="SyntaxDocument"/>).
/// </summary>
public sealed class PythonSyntaxConverter : TreeSitterSyntaxConverterBase
{
    public static readonly PythonSyntaxConverter Instance = new();

    protected override SyntaxNode CreateSyntaxNode(string rawKind, TextSpan span, string parentRawKind) =>
        rawKind switch
        {
            "module" => new NamespaceDeclSyntax { Span = span, RawGrammarKind = rawKind },
            "import_statement" or "import_from_statement" => new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "class_definition" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Class },
            "function_definition" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Function },

            "identifier" when parentRawKind is "parameters" => new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind },
            "default_parameter" or "typed_parameter" or "typed_default_parameter" or "list_splat_pattern" or "dictionary_splat_pattern" =>
                new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "decorator" => new AnnotationSyntax { Span = span, RawGrammarKind = rawKind },

            "call" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Method },
            "keyword_argument" => new ArgumentSyntax { Span = span, RawGrammarKind = rawKind },

            "format_string" or "concatenated_string" => new InterpolatedStringExprSyntax { Span = span, RawGrammarKind = rawKind },
            "string" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.String },
            "integer" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Integer },
            "float" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Float },
            "true" or "false" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Boolean },
            "none" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Null },

            "identifier" => new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind },

            "attribute" => new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind },
            "subscript" => new IndexExprSyntax { Span = span, RawGrammarKind = rawKind },
            "binary_operator" or "boolean_operator" or "comparison_operator" =>
                new BinaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "unary_operator" or "not_operator" => new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "assignment" or "augmented_assignment" => new AssignmentExprSyntax { Span = span, RawGrammarKind = rawKind },
            "conditional_expression" => new ConditionalExprSyntax { Span = span, RawGrammarKind = rawKind },
            "lambda" => new LambdaExprSyntax { Span = span, RawGrammarKind = rawKind },
            "await" => new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind, Operator = "await" },

            "block" => new BlockStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "if_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.If },
            "match_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Switch },
            "for_statement" or "while_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Loop },
            "try_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Try },
            "except_clause" => new CatchClauseSyntax { Span = span, RawGrammarKind = rawKind },
            "with_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Using },
            "return_statement" or "yield" => new ReturnStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "raise_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Throw },
            "break_statement" or "continue_statement" =>
                new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Branch },

            "generic_type" or "union_type" => new TypeRefSyntax { Span = span, RawGrammarKind = rawKind },

            "expression_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Expression },

            _ => new UnmappedSyntax { Span = span, RawGrammarKind = rawKind }
        };

    protected override bool HasInterpolationChild(LiteralExprSyntax litExpr)
    {
        for (int i = 0; i < litExpr.Children.Count; i++)
        {
            if (litExpr.Children[i].RawGrammarKind == "interpolation")
                return true;
        }
        return false;
    }

    protected override void PopulateSyntaxNode(SyntaxNode node, SyntaxDocument doc)
    {
        base.PopulateSyntaxNode(node, doc);

        switch (node)
        {
            case CallExprSyntax callExpr:
                var fnNode = callExpr.GetChildForField("function");
                if (fnNode is ExprSyntax fnExpr) callExpr.Callee = fnExpr;
                if (fnNode is MemberAccessExprSyntax memberCallee)
                {
                    callExpr.Receiver = memberCallee.Object;
                    callExpr.MethodNameSlice = memberCallee.MemberNameSlice;
                    if (memberCallee.HasExplicitMemberName) callExpr.MethodName = memberCallee.MemberName;
                }
                else if (fnNode != null)
                {
                    callExpr.MethodNameSlice = new SourceSlice(fnNode.Span.StartByte, fnNode.Span.Length);
                }
                break;

            case MemberAccessExprSyntax memExpr:
                var objNode = memExpr.GetChildForField("object");
                var propNode = memExpr.GetChildForField("attribute");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberNameSlice = new SourceSlice(propNode.Span.StartByte, propNode.Span.Length);
                break;
        }
    }
}

