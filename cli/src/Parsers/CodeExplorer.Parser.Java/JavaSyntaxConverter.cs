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
            "package_declaration" => new NamespaceDeclSyntax { Span = span, RawGrammarKind = rawKind },
            "import_declaration" => new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "class_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Class },
            "interface_declaration" or "annotation_type_declaration" =>
                new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Interface },
            "record_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Record },
            "enum_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Enum },

            "method_declaration" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Method },
            "constructor_declaration" or "compact_constructor_declaration" =>
                new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Constructor },

            "formal_parameter" or "spread_parameter" or "receiver_parameter" =>
                new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "field_declaration" or "annotation_type_element_declaration" =>
                new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Field },
            "constant_declaration" =>
                new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Constant },
            "enum_constant" =>
                new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.EnumMember },

            "variable_declarator" or "local_variable_declaration" =>
                new VariableDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "annotation" or "marker_annotation" => new AnnotationSyntax { Span = span, RawGrammarKind = rawKind },

            "method_invocation" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Method },
            "object_creation_expression" or "explicit_constructor_invocation" or "super_constructor_invocation" =>
                new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Constructor },

            "string_literal" or "text_block" =>
                new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.String },
            "character_literal" =>
                new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Char },
            "decimal_integer_literal" or "hex_integer_literal" or "octal_integer_literal" or "binary_integer_literal" =>
                new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Integer },
            "decimal_floating_point_literal" or "hex_floating_point_literal" =>
                new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Float },
            "true" or "false" =>
                new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Boolean },
            "null_literal" =>
                new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Null },

            "identifier" or "type_identifier" or "this" or "super" =>
                new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind },

            "field_access" or "scoped_identifier" => new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind },
            "array_access" => new IndexOrSliceExprSyntax { Span = span, RawGrammarKind = rawKind },
            "binary_expression" or "instanceof_expression" => new BinaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "unary_expression" or "update_expression" => new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "assignment_expression" => new AssignmentExprSyntax { Span = span, RawGrammarKind = rawKind },
            "ternary_expression" => new ConditionalExprSyntax { Span = span, RawGrammarKind = rawKind },
            "lambda_expression" => new LambdaExprSyntax { Span = span, RawGrammarKind = rawKind },
            "array_creation_expression" or "array_initializer" => new CollectionExprSyntax { Span = span, RawGrammarKind = rawKind },

            "block" => new BlockStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "if_statement" => new IfStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "switch_expression" or "switch_statement" => new SwitchOrMatchStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "for_statement" or "enhanced_for_statement" or "while_statement" or "do_statement" =>
                new LoopStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "try_statement" or "try_with_resources_statement" => new TryStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "catch_clause" => new CatchClauseSyntax { Span = span, RawGrammarKind = rawKind },
            "return_statement" => new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind, IsYield = false },
            "yield_statement" => new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind, IsYield = true },
            "throw_statement" => new ThrowStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "break_statement" or "continue_statement" =>
                new BranchStmtSyntax { Span = span, RawGrammarKind = rawKind, BranchKind = rawKind },
            "synchronized_statement" => new LockStmtSyntax { Span = span, RawGrammarKind = rawKind },

            "generic_type" or "array_type" or "scoped_type_identifier" or "integral_type" or "floating_point_type" or "boolean_type" =>
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
                var objNode = callExpr.GetChildForField("object");
                var fnNode = callExpr.GetChildForField("name") ?? callExpr.GetChildForField("type");
                if (objNode is ExprSyntax recvExpr) callExpr.Receiver = recvExpr;
                if (fnNode is ExprSyntax fnExpr) callExpr.Callee = fnExpr;
                if (fnNode != null) callExpr.MethodName = fnNode.Text;
                break;

            case MemberAccessExprSyntax memExpr:
                var targetNode = memExpr.GetChildForField("object") ?? memExpr.GetChildForField("scope");
                var fieldNode = memExpr.GetChildForField("field") ?? memExpr.GetChildForField("name");
                if (targetNode is ExprSyntax targetExpr) memExpr.Object = targetExpr;
                if (fieldNode != null) memExpr.MemberName = fieldNode.Text;
                break;
        }
    }
}

