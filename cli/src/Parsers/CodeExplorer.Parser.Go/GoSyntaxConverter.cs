using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Go;

/// <summary>
/// Converts native Tree-sitter Go CST nodes into the Universal AST (<see cref="SyntaxDocument"/>).
/// </summary>
public sealed class GoSyntaxConverter : TreeSitterSyntaxConverterBase
{
    public static readonly GoSyntaxConverter Instance = new();

    protected override SyntaxNode CreateSyntaxNode(string rawKind, TextSpan span, string parentRawKind) =>
        rawKind switch
        {
            "package_clause" => new NamespaceDeclSyntax { Span = span, RawGrammarKind = rawKind },
            "import_declaration" or "import_spec" => new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "type_spec" or "struct_type" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Struct },
            "interface_type" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Interface },

            "function_declaration" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Function },
            "method_declaration" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Method },

            "parameter_declaration" or "variadic_parameter_declaration" =>
                new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "field_declaration" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Field },
            "const_spec" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Constant },

            "var_spec" or "var_declaration" or "short_var_declaration" =>
                new VariableDeclSyntax { Span = span, RawGrammarKind = rawKind, IsConstant = false },
            "const_declaration" =>
                new VariableDeclSyntax { Span = span, RawGrammarKind = rawKind, IsConstant = true },

            "call_expression" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Method },

            "interpreted_string_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.String },
            "raw_string_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.RawString },
            "rune_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Char },
            "int_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Integer },
            "float_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Float },
            "true" or "false" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Boolean },
            "nil" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Null },

            "identifier" or "type_identifier" or "field_identifier" or "package_identifier" =>
                new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind },

            "selector_expression" => new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind },
            "index_expression" or "slice_expression" => new IndexOrSliceExprSyntax { Span = span, RawGrammarKind = rawKind },
            "binary_expression" => new BinaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "unary_expression" => new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "assignment_statement" => new AssignmentExprSyntax { Span = span, RawGrammarKind = rawKind },
            "func_literal" => new LambdaExprSyntax { Span = span, RawGrammarKind = rawKind },
            "composite_literal" => new CollectionExprSyntax { Span = span, RawGrammarKind = rawKind },

            "block" => new BlockStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "if_statement" => new IfStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "expression_switch_statement" or "type_switch_statement" =>
                new SwitchOrMatchStmtSyntax { Span = span, RawGrammarKind = rawKind, IsChannelSelect = false },
            "select_statement" =>
                new SwitchOrMatchStmtSyntax { Span = span, RawGrammarKind = rawKind, IsChannelSelect = true },
            "for_statement" => new LoopStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "defer_statement" => new DeferOrGoStmtSyntax { Span = span, RawGrammarKind = rawKind, IsGoroutine = false },
            "go_statement" => new DeferOrGoStmtSyntax { Span = span, RawGrammarKind = rawKind, IsGoroutine = true },
            "return_statement" => new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "break_statement" or "continue_statement" or "goto_statement" or "fallthrough_statement" =>
                new BranchStmtSyntax { Span = span, RawGrammarKind = rawKind, BranchKind = rawKind },

            "qualified_type" or "generic_type" or "pointer_type" or "slice_type" or "array_type" or "map_type" or "channel_type" or "function_type" =>
                new TypeRefSyntax { Span = span, RawGrammarKind = rawKind },

            "expression_statement" => new ExprStmtSyntax { Span = span, RawGrammarKind = rawKind },

            _ => new UnmappedSyntax { Span = span, RawGrammarKind = rawKind }
        };

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
                    callExpr.MethodName = memberCallee.MemberName;
                }
                else if (fnNode != null)
                {
                    callExpr.MethodName = fnNode.Text;
                }
                break;

            case MemberAccessExprSyntax memExpr:
                var objNode = memExpr.GetChildForField("operand");
                var propNode = memExpr.GetChildForField("field");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberName = propNode.Text;
                break;
        }
    }
}

