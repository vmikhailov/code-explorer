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
            "mod_item" => new NamespaceDeclSyntax { Span = span, RawGrammarKind = rawKind },
            "use_declaration" or "extern_crate_declaration" => new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "struct_item" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Struct },
            "enum_item" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Enum },
            "trait_item" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Trait },
            "type_item" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.TypeAlias },
            "union_item" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Union },

            "impl_item" => new ImplDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "function_item" or "function_signature_item" =>
                new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Function },
            "macro_definition" =>
                new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Macro },

            "parameter" or "self_parameter" or "variadic_parameter" =>
                new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "field_declaration" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Field },
            "enum_variant" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.EnumMember },
            "const_item" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Constant },
            "static_item" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Field },

            "let_declaration" => new VariableDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "attribute_item" or "inner_attribute_item" => new AnnotationSyntax { Span = span, RawGrammarKind = rawKind },

            "call_expression" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Method },
            "macro_invocation" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Macro },

            "string_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.String },
            "raw_string_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.RawString },
            "char_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Char },
            "integer_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Integer },
            "float_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Float },
            "boolean_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Boolean },

            "identifier" or "type_identifier" or "field_identifier" or "self" or "super" =>
                new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind },

            "field_expression" or "scoped_identifier" => new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind },
            "index_expression" => new IndexOrSliceExprSyntax { Span = span, RawGrammarKind = rawKind },
            "binary_expression" => new BinaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "unary_expression" or "reference_expression" or "pointer_expression" =>
                new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "assignment_expression" or "compound_assignment_expr" =>
                new AssignmentExprSyntax { Span = span, RawGrammarKind = rawKind },
            "closure_expression" => new LambdaExprSyntax { Span = span, RawGrammarKind = rawKind },
            "await_expression" => new AwaitOrTryExprSyntax { Span = span, RawGrammarKind = rawKind, IsTryPropagate = false },
            "try_expression" => new AwaitOrTryExprSyntax { Span = span, RawGrammarKind = rawKind, IsTryPropagate = true },
            "array_expression" or "tuple_expression" or "struct_expression" =>
                new CollectionExprSyntax { Span = span, RawGrammarKind = rawKind },

            "block" or "unsafe_block" =>
                new BlockStmtSyntax { Span = span, RawGrammarKind = rawKind, IsUnsafe = rawKind == "unsafe_block" },
            "if_expression" => new IfStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "match_expression" => new SwitchOrMatchStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "loop_expression" or "while_expression" or "for_expression" =>
                new LoopStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "return_expression" => new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "break_expression" or "continue_expression" =>
                new BranchStmtSyntax { Span = span, RawGrammarKind = rawKind, BranchKind = rawKind },

            "generic_type" or "reference_type" or "pointer_type" or "tuple_type" or "array_type" or "function_type" or "scoped_type_identifier" or "primitive_type" =>
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
                var fnNode = callExpr.GetChildForField("function") ?? callExpr.GetChildForField("macro");
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
                var objNode = memExpr.GetChildForField("value") ?? memExpr.GetChildForField("path");
                var propNode = memExpr.GetChildForField("field") ?? memExpr.GetChildForField("name");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberName = propNode.Text;
                break;
        }
    }
}

