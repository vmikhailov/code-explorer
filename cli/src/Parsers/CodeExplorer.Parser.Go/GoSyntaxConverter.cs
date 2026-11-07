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
            "package_clause" => new NamespaceDeclSyntax { Span = span },
            "import_declaration" or "import_spec" => new ImportDeclSyntax { Span = span },

            "type_spec" or "struct_type" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Struct },
            "interface_type" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Interface },

            "function_declaration" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Function },
            "method_declaration" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Method },

            "parameter_declaration" =>
                new ParameterDeclSyntax { Span = span },
            "variadic_parameter_declaration" =>
                new ParameterDeclSyntax { Span = span, PassingMode = ParameterPassingMode.VariadicArgs },

            "field_declaration" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Field },
            "const_spec" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Constant },

            "var_spec" =>
                new VariableDeclSyntax { Span = span, IsConstant = false, IsDeclarator = true },
            "var_declaration" or "short_var_declaration" =>
                new VariableDeclSyntax { Span = span, IsConstant = false },
            "const_declaration" =>
                new VariableDeclSyntax { Span = span, IsConstant = true },

            "call_expression" => new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Method },

            "interpreted_string_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.String },
            "raw_string_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.RawString },
            "rune_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Char },
            "int_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Integer },
            "float_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Float },
            "true" or "false" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Boolean },
            "nil" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Null },

            "composite_literal" or "literal_value" =>
                new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Initializer },
            "keyed_element" =>
                new KeyValueExprSyntax { Span = span },
            "parenthesized_expression" or "literal_element" =>
                new ParenthesizedExprSyntax { Span = span },
            "type_conversion_expression" or "type_assertion_expression" =>
                new TypeCastExprSyntax { Span = span },

            "identifier" or "package_identifier" =>
                new IdentifierExprSyntax { Span = span },
            "type_identifier" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Type },
            "field_identifier" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Member },

            "selector_expression" => new MemberAccessExprSyntax { Span = span },
            "index_expression" or "slice_expression" => new IndexExprSyntax { Span = span },
            "binary_expression" => new BinaryExprSyntax { Span = span },
            "unary_expression" => new UnaryExprSyntax { Span = span },
            "assignment_statement" => new AssignmentExprSyntax { Span = span },
            "func_literal" => new LambdaExprSyntax { Span = span },

            "block" or "statement_list" => new BlockStmtSyntax { Span = span },
            "if_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.If },
            "expression_switch_statement" or "type_switch_statement" or "select_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Switch },
            "for_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Loop },
            "defer_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Defer },
            "go_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Goroutine },
            "return_statement" => new ReturnStmtSyntax { Span = span },
            "break_statement" or "continue_statement" or "goto_statement" or "fallthrough_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Branch },
            "empty_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.NoOp },

            "qualified_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Named },
            "generic_type" or "map_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Generic },
            "pointer_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Pointer },
            "slice_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Slice },
            "array_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Array },
            "channel_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Channel },
            "function_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Function },

            "parameter_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Parameters },
            "argument_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Arguments },
            "type_parameter_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeParameters },
            "type_arguments" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeArguments },
            "field_declaration_list" or "interface_body" or "type_declaration" => new SyntaxList { Span = span, Kind = SyntaxListKind.Declarations },
            "import_spec_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Imports },
            "expression_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Expressions },

            "expression_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Expression },

            _ => new SyntaxList { Span = span, Kind = SyntaxListKind.General }
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
                    callExpr.MethodNameSlice = memberCallee.MemberNameSlice;
                    if (memberCallee.HasExplicitMemberName) callExpr.MethodName = memberCallee.MemberName;
                }
                else if (fnNode != null)
                {
                    callExpr.MethodNameSlice = new SourceSlice(fnNode.Span.StartByte, fnNode.Span.Length);
                }
                break;

            case MemberAccessExprSyntax memExpr:
                var objNode = memExpr.GetChildForField("operand");
                var propNode = memExpr.GetChildForField("field");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberNameSlice = new SourceSlice(propNode.Span.StartByte, propNode.Span.Length);
                break;
        }
    }
}

