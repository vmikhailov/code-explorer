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
            "module" or "internal_module" => new NamespaceDeclSyntax { Span = span, RawGrammarKind = rawKind },
            "import_statement" => new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind, IsReExport = false },
            "export_statement" => new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind, IsReExport = true },

            "class_declaration" or "abstract_class_declaration" =>
                new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Class },
            "interface_declaration" =>
                new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Interface },
            "enum_declaration" =>
                new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Enum },
            "type_alias_declaration" =>
                new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.TypeAlias },

            "function_declaration" or "generator_function_declaration" =>
                new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Function },
            "method_definition" or "method_signature" =>
                new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Method },

            "required_parameter" or "optional_parameter" or "rest_pattern" =>
                new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "public_field_definition" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Field },
            "property_signature" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Property },
            "enum_assignment" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.EnumMember },

            "variable_declarator" or "variable_declaration" or "lexical_declaration" =>
                new VariableDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "decorator" => new AnnotationSyntax { Span = span, RawGrammarKind = rawKind },

            "call_expression" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Method },
            "new_expression" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Constructor },

            "template_string" => new InterpolatedStringExprSyntax { Span = span, RawGrammarKind = rawKind },
            "string" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.String },
            "number" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Integer },
            "true" or "false" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Boolean },
            "null" or "undefined" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Null },

            "identifier" or "type_identifier" or "property_identifier" or "shorthand_property_identifier" or "this" or "super" =>
                new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind },

            "member_expression" => new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind },
            "subscript_expression" => new IndexOrSliceExprSyntax { Span = span, RawGrammarKind = rawKind },
            "binary_expression" => new BinaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "unary_expression" or "update_expression" => new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "assignment_expression" or "augmented_assignment_expression" =>
                new AssignmentExprSyntax { Span = span, RawGrammarKind = rawKind },
            "ternary_expression" => new ConditionalExprSyntax { Span = span, RawGrammarKind = rawKind },
            "arrow_function" or "function_expression" => new LambdaExprSyntax { Span = span, RawGrammarKind = rawKind },
            "await_expression" => new AwaitOrTryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "array" or "object" => new CollectionExprSyntax { Span = span, RawGrammarKind = rawKind },
            "jsx_element" or "jsx_self_closing_element" or "jsx_fragment" =>
                new JsxElementExprSyntax { Span = span, RawGrammarKind = rawKind },

            "statement_block" => new BlockStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "if_statement" => new IfStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "switch_statement" => new SwitchOrMatchStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "for_statement" or "for_in_statement" or "while_statement" or "do_statement" =>
                new LoopStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "try_statement" => new TryStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "catch_clause" => new CatchClauseSyntax { Span = span, RawGrammarKind = rawKind },
            "with_statement" => new UsingOrWithStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "return_statement" => new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind, IsYield = false },
            "yield_expression" => new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind, IsYield = true },
            "throw_statement" => new ThrowStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "break_statement" or "continue_statement" =>
                new BranchStmtSyntax { Span = span, RawGrammarKind = rawKind, BranchKind = rawKind },

            "generic_type" or "array_type" or "tuple_type" or "union_type" or "intersection_type" or "function_type" or "predefined_type" =>
                new TypeRefSyntax { Span = span, RawGrammarKind = rawKind },

            "expression_statement" => new ExprStmtSyntax { Span = span, RawGrammarKind = rawKind },

            _ => new UnmappedSyntax { Span = span, RawGrammarKind = rawKind }
        };

    protected override bool HasInterpolationChild(LiteralExprSyntax litExpr)
    {
        for (int i = 0; i < litExpr.Children.Count; i++)
        {
            if (litExpr.Children[i].RawGrammarKind == "template_substitution")
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
                var fnNode = callExpr.GetChildForField("function") ?? callExpr.GetChildForField("constructor");
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
                var objNode = memExpr.GetChildForField("object");
                var propNode = memExpr.GetChildForField("property");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberName = propNode.Text;
                break;
        }
    }
}

