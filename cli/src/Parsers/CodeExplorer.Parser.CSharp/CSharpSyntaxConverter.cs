using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.CSharp;

/// <summary>
/// Converts native Tree-sitter C# CST nodes into the Universal AST (<see cref="SyntaxDocument"/>).
/// </summary>
public sealed class CSharpSyntaxConverter : TreeSitterSyntaxConverterBase
{
    public static readonly CSharpSyntaxConverter Instance = new();

    protected override SyntaxNode CreateSyntaxNode(string rawKind, TextSpan span, string parentRawKind) =>
        rawKind switch
        {
            "namespace_declaration" or "file_scoped_namespace_declaration" =>
                new NamespaceDeclSyntax { Span = span },
            "using_directive" => new ImportDeclSyntax { Span = span },

            "class_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Class },
            "interface_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Interface },
            "struct_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Struct },
            "record_declaration" or "record_struct_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Record },
            "enum_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Enum },
            "delegate_declaration" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.TypeAlias },

            "method_declaration" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Method },
            "local_function_statement" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Function },
            "constructor_declaration" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Constructor },
            "destructor_declaration" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Destructor },
            "accessor_declaration" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Accessor },
            "operator_declaration" or "conversion_operator_declaration" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Operator },

            "parameter" => new ParameterDeclSyntax { Span = span },

            "field_declaration" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Field },
            "property_declaration" or "indexer_declaration" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Property },
            "event_declaration" or "event_field_declaration" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Event },
            "enum_member_declaration" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.EnumMember },

            "variable_declarator" =>
                new VariableDeclSyntax { Span = span, IsDeclarator = true },
            "variable_declaration" or "local_declaration_statement" =>
                new VariableDeclSyntax { Span = span },

            "attribute" => new AnnotationSyntax { Span = span },

            "invocation_expression" => new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Method },
            "object_creation_expression" or "implicit_object_creation_expression" =>
                new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Constructor },

            "argument" or "attribute_argument" or "expression_element" or "spread_element" =>
                new ArgumentSyntax { Span = span, IsSpread = rawKind == "spread_element" },

            "interpolated_string_expression" or "interpolated_verbatim_string_expression" or "interpolated_raw_string_expression" =>
                new InterpolatedStringExprSyntax { Span = span },
            "interpolation" =>
                new ParenthesizedExprSyntax { Span = span, IsInterpolation = true },
            "parenthesized_expression" =>
                new ParenthesizedExprSyntax { Span = span },
            "cast_expression" =>
                new TypeCastExprSyntax { Span = span },
            "base_list" or "primary_constructor_base_type" or "simple_base_type" =>
                new BaseTypeClauseSyntax { Span = span },
            "equals_value_clause" or "arrow_expression_clause" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.Initializer },
            "declaration_pattern" or "var_pattern" or "constant_pattern" or "recursive_pattern" or "type_pattern" or "list_pattern" =>
                new PatternSyntax { Span = span, Kind = PatternKind.Binding },

            "string_literal" or "string_content" or "escape_sequence" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.String },
            "verbatim_string_literal" or "raw_string_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.RawString },
            "character_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Char },
            "integer_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Integer },
            "real_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Float },
            "boolean_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Boolean },
            "null_literal" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Null },

            "array_creation_expression" or "implicit_array_creation_expression" or "collection_expression" =>
                new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Array },
            "initializer_expression" =>
                new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Initializer },
            "tuple_expression" =>
                new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Tuple },

            "identifier" => new IdentifierExprSyntax { Span = span },
            "this" or "base" => new IdentifierExprSyntax { Span = span, Role = IdentifierRole.SelfOrSuper },
            "predefined_type" when parentRawKind is "member_access_expression" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Type },

            "member_access_expression" => new MemberAccessExprSyntax { Span = span },
            "qualified_name" => new MemberAccessExprSyntax { Span = span, Operator = "::" },
            "element_access_expression" => new IndexExprSyntax { Span = span },
            "binary_expression" or "is_expression" or "as_expression" => new BinaryExprSyntax { Span = span },
            "prefix_unary_expression" or "postfix_unary_expression" or "typeof_expression" or "sizeof_expression" =>
                new UnaryExprSyntax { Span = span },
            "assignment_expression" => new AssignmentExprSyntax { Span = span },
            "conditional_expression" => new ConditionalExprSyntax { Span = span },
            "lambda_expression" => new LambdaExprSyntax { Span = span },
            "await_expression" => new UnaryExprSyntax { Span = span, Operator = "await", IsAwait = true },

            "block" or "unsafe_statement" or "checked_statement" =>
                new BlockStmtSyntax { Span = span, IsUnsafe = rawKind == "unsafe_statement" },
            "if_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.If },
            "switch_statement" or "switch_expression" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Switch },
            "for_statement" or "foreach_statement" or "while_statement" or "do_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Loop },
            "try_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Try },
            "catch_clause" => new CatchClauseSyntax { Span = span },
            "using_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Using },
            "return_statement" or "yield_statement" => new ReturnStmtSyntax { Span = span },
            "throw_statement" or "throw_expression" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Throw },
            "break_statement" or "continue_statement" or "goto_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Branch },
            "lock_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Lock },
            "empty_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.NoOp },
            "global_statement" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.TopLevelStatement },

            "generic_name" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Generic },
            "nullable_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Nullable },
            "array_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Array },
            "pointer_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Pointer },
            "tuple_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Tuple },
            "predefined_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Predefined },

            "parameter_list" or "bracketed_parameter_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Parameters },
            "argument_list" or "bracketed_argument_list" or "attribute_argument_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Arguments },
            "type_parameter_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeParameters },
            "type_argument_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeArguments },
            "declaration_list" or "enum_member_declaration_list" or "accessor_list" or "switch_body" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.Declarations },
            "attribute_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Attributes },
            "catch_declaration" or "finally_clause" => new SyntaxList { Span = span, Kind = SyntaxListKind.Clauses },

            "expression_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Expression },

            _ => new SyntaxList { Span = span, Kind = SyntaxListKind.General }
        };

    protected override void PopulateSyntaxNode(SyntaxNode node, SyntaxDocument doc)
    {
        base.PopulateSyntaxNode(node, doc);

        switch (node)
        {
            case CallExprSyntax callExpr:
                var fnNode = callExpr.GetChildForField("function") ?? callExpr.GetChildForField("type");
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
                var objNode = memExpr.GetChildForField("expression") ?? memExpr.GetChildForField("qualifier");
                var propNode = memExpr.GetChildForField("name");
                if (objNode is ExprSyntax objExpr)
                {
                    memExpr.Object = objExpr;
                }
                else if (objNode != null)
                {
                    memExpr.Object = new IdentifierExprSyntax
                    {
                        Span = objNode.Span,
                        Document = objNode.Document,
                        Parent = memExpr
                    };
                }
                if (propNode != null) memExpr.MemberNameSlice = new SourceSlice(propNode.Span.StartByte, propNode.Span.Length);
                break;
        }
    }
}

