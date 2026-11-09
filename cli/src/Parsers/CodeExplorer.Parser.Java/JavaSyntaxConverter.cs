using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Java;

/// <summary>
/// Converts native Tree-sitter Java CST nodes into the Universal AST (<see cref="SyntaxDocument"/>).
/// </summary>
public sealed class JavaSyntaxConverter : TreeSitterSyntaxConverterBase
{
    public static readonly JavaSyntaxConverter Instance = new();

    protected override SyntaxNode CreateSyntaxNode(string rawKind, TextSpan span, string parentRawKind) =>
        rawKind switch
        {
            "package_declaration" => new NamespaceDeclSyntax { Span = span },
            "import_declaration" => new ImportDeclSyntax { Span = span },

            "class_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Class },
            "interface_declaration" or "annotation_type_declaration" =>
                new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Interface },
            "record_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Record },
            "enum_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Enum },

            "method_declaration" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Method },
            "constructor_declaration" or "compact_constructor_declaration" =>
                new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Constructor },

            "formal_parameter" or "receiver_parameter" =>
                new ParameterDeclSyntax { Span = span },
            "spread_parameter" =>
                new ParameterDeclSyntax { Span = span, PassingMode = ParameterPassingMode.VariadicArgs },

            "field_declaration" or "annotation_type_element_declaration" =>
                new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Field },
            "constant_declaration" =>
                new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Constant },
            "enum_constant" =>
                new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.EnumMember },

            "variable_declarator" =>
                new VariableDeclSyntax { Span = span, IsDeclarator = true },
            "local_variable_declaration" =>
                new VariableDeclSyntax { Span = span },

            "annotation" or "marker_annotation" => new AnnotationSyntax { Span = span },
            "element_value_pair" => new KeyValueExprSyntax { Span = span },

            "method_invocation" => new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Method },
            "object_creation_expression" or "explicit_constructor_invocation" or "super_constructor_invocation" =>
                new CallExprSyntax
                {
                    Span = span,
                    CallKind = rawKind == "super_constructor_invocation"
                        ? CallSyntaxKind.SuperConstructor
                        : rawKind == "explicit_constructor_invocation"
                            ? CallSyntaxKind.ThisConstructor
                            : CallSyntaxKind.Constructor
                },

            "parenthesized_expression" => new ParenthesizedExprSyntax { Span = span },
            "cast_expression" => new TypeCastExprSyntax { Span = span },
            "superclass" or "super_interfaces" or "extends_interfaces" or "permits" =>
                new BaseTypeClauseSyntax { Span = span, IsImplements = rawKind is "super_interfaces" or "extends_interfaces" },

            "string_literal" or "text_block" =>
                new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.String },
            "character_literal" =>
                new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Char },
            "decimal_integer_literal" or "hex_integer_literal" or "octal_integer_literal" or "binary_integer_literal" =>
                new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Integer },
            "decimal_floating_point_literal" or "hex_floating_point_literal" =>
                new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Float },
            "true" or "false" =>
                new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Boolean },
            "null_literal" =>
                new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Null },

            "array_creation_expression" or "array_initializer" or "element_value_array_initializer" =>
                new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Array },

            "identifier" =>
                new IdentifierExprSyntax { Span = span },
            "type_identifier" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Type },
            "this" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Self },
            "super" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Super },

            "field_access" => new MemberAccessExprSyntax { Span = span },
            "scoped_identifier" => new MemberAccessExprSyntax { Span = span, Operator = "::" },
            "array_access" => new IndexExprSyntax { Span = span },
            "binary_expression" or "instanceof_expression" => new BinaryExprSyntax { Span = span },
            "unary_expression" or "update_expression" => new UnaryExprSyntax { Span = span },
            "assignment_expression" => new AssignmentExprSyntax { Span = span },
            "ternary_expression" => new ConditionalExprSyntax { Span = span },
            "lambda_expression" => new LambdaExprSyntax { Span = span },

            "block" => new BlockStmtSyntax { Span = span },
            "if_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.If },
            "switch_expression" or "switch_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Switch },
            "for_statement" or "enhanced_for_statement" or "while_statement" or "do_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Loop },
            "try_statement" or "try_with_resources_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Try },
            "catch_clause" => new CatchClauseSyntax { Span = span },
            "return_statement" or "yield_statement" => new ReturnStmtSyntax { Span = span },
            "throw_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Throw },
            "break_statement" or "continue_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Branch },
            "synchronized_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Lock },
            "empty_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.NoOp },

            "generic_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Generic },
            "array_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Array },
            "scoped_type_identifier" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Named },
            "integral_type" or "floating_point_type" or "boolean_type" or "void_type" =>
                new TypeRefSyntax { Span = span, Kind = TypeRefKind.Predefined },

            "formal_parameters" => new SyntaxList { Span = span, Kind = SyntaxListKind.Parameters },
            "argument_list" or "annotation_argument_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Arguments },
            "type_parameters" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeParameters },
            "type_arguments" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeArguments },
            "class_body" or "interface_body" or "enum_body" or "annotation_type_body" or "record_declaration_body" or "switch_block" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.Declarations },
            "modifiers" => new SyntaxList { Span = span, Kind = SyntaxListKind.Attributes },
            "catch_formal_parameter" or "finally_clause" => new SyntaxList { Span = span, Kind = SyntaxListKind.Clauses },

            "expression_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Expression },

            _ => new SyntaxList { Span = span, Kind = SyntaxListKind.General }
        };

    protected override void PopulateSyntaxNode(SyntaxNode node, SyntaxDocument doc)
    {
        base.PopulateSyntaxNode(node, doc);

        switch (node)
        {
            case CallExprSyntax callExpr:
                var objNode = callExpr.GetChildForField("object");
                var fnNode = callExpr.GetChildForField("name") ?? callExpr.GetChildForField("type");
                if (objNode is ExprSyntax recvExpr) callExpr.Receiver = recvExpr;
                if (fnNode is ExprSyntax fnExpr) callExpr.Callee = fnExpr;
                if (fnNode != null) callExpr.MethodNameSlice = new SourceSlice(fnNode.Span.StartByte, fnNode.Span.Length);
                break;

            case MemberAccessExprSyntax memExpr:
                var targetNode = memExpr.GetChildForField("object") ?? memExpr.GetChildForField("scope");
                var fieldNode = memExpr.GetChildForField("field") ?? memExpr.GetChildForField("name");
                if (targetNode is ExprSyntax targetExpr) memExpr.Object = targetExpr;
                if (fieldNode != null) memExpr.MemberNameSlice = new SourceSlice(fieldNode.Span.StartByte, fieldNode.Span.Length);
                break;
        }
    }
}

