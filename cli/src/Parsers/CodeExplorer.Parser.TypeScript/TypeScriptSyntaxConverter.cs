using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.TypeScript;

/// <summary>
/// Converts native Tree-sitter TypeScript/JavaScript CST nodes into the Universal AST (<see cref="SyntaxDocument"/>).
/// </summary>
public sealed class TypeScriptSyntaxConverter : TreeSitterSyntaxConverterBase
{
    public static readonly TypeScriptSyntaxConverter Instance = new();

    protected override SyntaxNode CreateSyntaxNode(string rawKind, TextSpan span, string parentRawKind) =>
        rawKind switch
        {
            "module" or "internal_module" => new NamespaceDeclSyntax { Span = span },
            "import_statement" => new ImportDeclSyntax { Span = span, IsReExport = false },
            "export_statement" => new ImportDeclSyntax { Span = span, IsReExport = true },

            "class_declaration" or "abstract_class_declaration" or "class" =>
                new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Class },
            "interface_declaration" =>
                new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Interface },
            "enum_declaration" =>
                new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Enum },
            "type_alias_declaration" =>
                new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.TypeAlias },

            "function_declaration" or "generator_function_declaration" =>
                new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Function },
            "method_definition" or "method_signature" =>
                new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Method },

            "required_parameter" or "optional_parameter" =>
                new ParameterDeclSyntax { Span = span },
            "rest_pattern" =>
                new ParameterDeclSyntax { Span = span, PassingMode = ParameterPassingMode.VariadicArgs },

            "public_field_definition" or "property_definition" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Field },
            "property_signature" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.Property },
            "enum_assignment" => new MemberDeclSyntax { Span = span, Kind = MemberSyntaxKind.EnumMember },

            "variable_declarator" =>
                new VariableDeclSyntax { Span = span, IsDeclarator = true },
            "variable_declaration" or "lexical_declaration" =>
                new VariableDeclSyntax { Span = span },

            "decorator" => new AnnotationSyntax { Span = span },

            "call_expression" or "tagged_template_expression" => new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Method },
            "new_expression" => new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Constructor },
            "spread_element" => new ArgumentSyntax { Span = span, IsSpread = true },

            "template_string" => new InterpolatedStringExprSyntax { Span = span },
            "template_substitution" or "jsx_expression" =>
                new ParenthesizedExprSyntax { Span = span, IsInterpolation = true },
            "parenthesized_expression" =>
                new ParenthesizedExprSyntax { Span = span },
            "as_expression" or "satisfies_expression" or "type_assertion" or "instantiation_expression" or "expression_with_type_arguments" =>
                new TypeCastExprSyntax { Span = span, IsGenericInstantiation = rawKind is "instantiation_expression" or "expression_with_type_arguments" },
            "pair" or "jsx_attribute" or "import_specifier" or "export_specifier" or "namespace_import" or "import_require_clause" =>
                new KeyValueExprSyntax { Span = span },
            "jsx_element" or "jsx_self_closing_element" or "jsx_opening_element" or "jsx_closing_element" or "jsx_fragment" =>
                new UiElementSyntax
                {
                    Span = span,
                    IsSelfClosing = rawKind == "jsx_self_closing_element",
                    IsClosing = rawKind == "jsx_closing_element",
                    IsFragment = rawKind == "jsx_fragment"
                },
            "class_heritage" or "extends_clause" or "extends_type_clause" or "implements_clause" =>
                new BaseTypeClauseSyntax { Span = span, IsImplements = rawKind == "implements_clause" },
            "object_pattern" =>
                new PatternSyntax { Span = span, Kind = PatternKind.Object },
            "pair_pattern" =>
                new PatternSyntax { Span = span, Kind = PatternKind.Pair },
            "array_pattern" =>
                new PatternSyntax { Span = span, Kind = PatternKind.Array },

            "string" or "string_fragment" or "escape_sequence" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.String },
            "number" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Integer },
            "true" or "false" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Boolean },
            "null" or "undefined" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Null },
            "regex" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Regex },

            "array" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Array },
            "object" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Object },

            "identifier" => new IdentifierExprSyntax { Span = span },
            "type_identifier" => new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Type },
            "property_identifier" or "private_property_identifier" or "shorthand_property_identifier" or "shorthand_property_identifier_pattern" =>
                new IdentifierExprSyntax { Span = span, Role = IdentifierRole.Member },
            "this" or "super" => new IdentifierExprSyntax { Span = span, Role = IdentifierRole.SelfOrSuper },

            "member_expression" => new MemberAccessExprSyntax { Span = span },
            "subscript_expression" => new IndexExprSyntax { Span = span },
            "binary_expression" => new BinaryExprSyntax { Span = span },
            "unary_expression" or "update_expression" => new UnaryExprSyntax { Span = span },
            "assignment_expression" or "augmented_assignment_expression" =>
                new AssignmentExprSyntax { Span = span },
            "ternary_expression" => new ConditionalExprSyntax { Span = span },
            "arrow_function" or "function_expression" or "function" or "generator_function" => new LambdaExprSyntax { Span = span },
            "await_expression" => new UnaryExprSyntax { Span = span, Operator = "await", IsAwait = true },

            "statement_block" => new BlockStmtSyntax { Span = span },
            "if_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.If },
            "switch_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Switch },
            "for_statement" or "for_in_statement" or "while_statement" or "do_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Loop },
            "try_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Try },
            "catch_clause" => new CatchClauseSyntax { Span = span },
            "with_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Using },
            "return_statement" or "yield_expression" => new ReturnStmtSyntax { Span = span },
            "throw_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Throw },
            "break_statement" or "continue_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Branch },
            "empty_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.NoOp },

            "type_annotation" or "opting_type_annotation" or "omitting_type_annotation" =>
                new TypeRefSyntax { Span = span, Kind = TypeRefKind.Annotation },
            "predefined_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Predefined },
            "generic_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Generic },
            "array_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Array },
            "tuple_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Tuple },
            "union_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Union },
            "intersection_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Intersection },
            "function_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Function },

            "formal_parameters" => new SyntaxList { Span = span, Kind = SyntaxListKind.Parameters },
            "arguments" => new SyntaxList { Span = span, Kind = SyntaxListKind.Arguments },
            "type_parameters" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeParameters },
            "type_arguments" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeArguments },
            "class_body" or "interface_body" or "object_type" or "enum_body" or "switch_body" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.Declarations },
            "named_imports" or "import_clause" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.Imports },
            "export_clause" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.Exports },
            "finally_clause" =>
                new SyntaxList { Span = span, Kind = SyntaxListKind.Clauses },

            "expression_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Expression },

            _ => new SyntaxList { Span = span, Kind = SyntaxListKind.General }
        };

    protected override void PopulateSyntaxNode(SyntaxNode node, SyntaxDocument doc)
    {
        base.PopulateSyntaxNode(node, doc);

        switch (node)
        {
            case CallExprSyntax callExpr:
                var fnNode = callExpr.GetChildForField("function") ?? callExpr.GetChildForField("constructor");
                if (fnNode is ExprSyntax fnExpr) callExpr.Callee = fnExpr;
                if (fnNode is MemberAccessExprSyntax memberCallee)
                {
                    callExpr.Receiver = memberCallee.Object;
                    callExpr.MethodNameSlice = memberCallee.MemberNameSlice;
                    if (memberCallee.HasExplicitMemberName) callExpr.MethodName = memberCallee.MemberName;
                }
                else if (fnNode != null && (fnNode.Span.EndByte - fnNode.Span.StartByte) <= 128)
                {
                    callExpr.MethodNameSlice = new SourceSlice(fnNode.Span.StartByte, fnNode.Span.Length);
                }
                break;

            case MemberAccessExprSyntax memExpr:
                var objNode = memExpr.GetChildForField("object");
                var propNode = memExpr.GetChildForField("property");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberNameSlice = new SourceSlice(propNode.Span.StartByte, propNode.Span.Length);
                break;
        }
    }
}

