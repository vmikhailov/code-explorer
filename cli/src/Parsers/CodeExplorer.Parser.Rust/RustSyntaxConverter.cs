using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Converts native Tree-sitter Rust CST nodes into the Universal AST (<see cref="SyntaxDocument"/>).
/// </summary>
public sealed class RustSyntaxConverter : TreeSitterSyntaxConverterBase
{
    public static readonly RustSyntaxConverter Instance = new();

    protected override SyntaxNode CreateSyntaxNode(string rawKind, TextSpan span, string parentRawKind) =>
        rawKind switch
        {
            "mod_item" => new NamespaceDeclSyntax { Span = span },
            "use_declaration" or "extern_crate_declaration" => new ImportDeclSyntax { Span = span },

            "struct_item" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Struct },
            "enum_item" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Enum },
            "trait_item" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Trait },
            "type_item" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.TypeAlias },
            "union_item" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Union },

            "impl_item" => new ImplDeclSyntax { Span = span },

            "function_item" or "function_signature_item" =>
                new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Function },
            "macro_definition" =>
                new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Macro },

            "parameter" or "self_parameter" =>
                new ParameterDeclSyntax { Span = span, PassingMode = rawKind == "self_parameter" ? ParameterPassingMode.Receiver : ParameterPassingMode.Value },
            "variadic_parameter" =>
                new ParameterDeclSyntax { Span = span, PassingMode = ParameterPassingMode.VariadicArgs },

            "field_declaration" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Field },
            "enum_variant" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.EnumMember },
            "const_item" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Constant },
            "static_item" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Field },

            "let_declaration" => new VariableDeclSyntax { Span = span },

            "attribute_item" or "inner_attribute_item" => new AnnotationSyntax { Span = span },
            "field_initializer" => new KeyValueExprSyntax { Span = span },
            "parenthesized_expression" => new ParenthesizedExprSyntax { Span = span },
            "type_cast_expression" or "generic_function" =>
                new TypeCastExprSyntax { Span = span, IsGenericInstantiation = rawKind == "generic_function" },
            "trait_bounds" => new BaseTypeClauseSyntax { Span = span },
            "tuple_struct_pattern" or "struct_pattern" or "tuple_pattern" or "slice_pattern" or "or_pattern" or "ref_pattern" or "mut_pattern" =>
                new PatternSyntax { Span = span, Kind = PatternKind.Binding },

            "call_expression" => new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Method },
            "macro_invocation" => new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Macro },

            "string_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.String },
            "raw_string_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.RawString },
            "char_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Char },
            "integer_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Integer },
            "float_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Float },
            "boolean_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Boolean },

            "array_expression" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Array },
            "struct_expression" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Object },
            "tuple_expression" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Tuple },

            "identifier" =>
                new IdentifierExprSyntax { Span = span },
            "type_identifier" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Type },
            "field_identifier" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Member },
            "self" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Self },
            "super" or "crate" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Super },

            "field_expression" => new MemberAccessExprSyntax { Span = span },
            "scoped_identifier" => new MemberAccessExprSyntax { Span = span, Operator = "::" },
            "index_expression" => new IndexExprSyntax { Span = span },
            "binary_expression" => new BinaryExprSyntax { Span = span },
            "unary_expression" or "reference_expression" or "pointer_expression" =>
                new UnaryExprSyntax { Span = span },
            "assignment_expression" or "compound_assignment_expr" =>
                new AssignmentExprSyntax { Span = span },
            "closure_expression" => new LambdaExprSyntax { Span = span },
            "await_expression" => new UnaryExprSyntax { Span = span, Operator = "await", IsAwait = true },
            "try_expression" => new UnaryExprSyntax { Span = span, Operator = "?", IsTryPropagate = true },

            "block" or "unsafe_block" =>
                new BlockStmtSyntax { Span = span, IsUnsafe = rawKind == "unsafe_block" },
            "if_expression" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.If },
            "match_expression" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Switch },
            "loop_expression" or "while_expression" or "for_expression" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Loop },
            "return_expression" => new ReturnStmtSyntax { Span = span },
            "break_expression" or "continue_expression" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Branch },
            "empty_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.NoOp },

            "generic_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Generic },
            "reference_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Reference },
            "pointer_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Pointer },
            "tuple_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Tuple },
            "array_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Array },
            "function_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Function },
            "scoped_type_identifier" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Named },
            "primitive_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Predefined },

            "parameters" or "closure_parameters" => new SyntaxList { Span = span, Kind = SyntaxListKind.Parameters },
            "arguments" or "token_tree" => new SyntaxList { Span = span, Kind = SyntaxListKind.Arguments },
            "type_parameters" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeParameters },
            "type_arguments" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeArguments },
            "declaration_list" or "field_declaration_list" or "enum_variant_list" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.Declarations },
            "match_block" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.SwitchArms },
            "use_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Imports },
            "visibility_modifier" => new SyntaxList { Span = span, Kind = SyntaxListKind.Attributes },

            "expression_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Expression },

            _ => new SyntaxList { Span = span, Kind = SyntaxListKind.General }
        };

    protected override void PopulateSyntaxNode(SyntaxNode node, SyntaxDocument doc)
    {
        base.PopulateSyntaxNode(node, doc);

        switch (node)
        {
            case CallExprSyntax callExpr:
                var fnNode = callExpr.GetChildForField("function") ?? callExpr.GetChildForField("macro");
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
                var objNode = memExpr.GetChildForField("value") ?? memExpr.GetChildForField("path");
                var propNode = memExpr.GetChildForField("field") ?? memExpr.GetChildForField("name");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberNameSlice = new SourceSlice(propNode.Span.StartByte, propNode.Span.Length);
                break;
        }
    }
}

