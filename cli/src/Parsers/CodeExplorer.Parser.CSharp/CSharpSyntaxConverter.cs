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
                new NamespaceDeclSyntax { Span = span, RawGrammarKind = rawKind },
            "using_directive" => new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "class_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Class },
            "interface_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Interface },
            "struct_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Struct },
            "record_declaration" or "record_struct_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Record },
            "enum_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Enum },
            "delegate_declaration" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.TypeAlias },

            "method_declaration" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Method },
            "local_function_statement" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Function },
            "constructor_declaration" or "primary_constructor" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Constructor },
            "destructor_declaration" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Destructor },
            "accessor_declaration" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Accessor },
            "operator_declaration" or "conversion_operator_declaration" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Operator },

            "parameter" => new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "field_declaration" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Field },
            "property_declaration" or "indexer_declaration" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Property },
            "event_declaration" or "event_field_declaration" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.Event },
            "enum_member_declaration" => new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = MemberSyntaxKind.EnumMember },

            "variable_declarator" or "variable_declaration" or "local_declaration_statement" =>
                new VariableDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "attribute" => new AnnotationSyntax { Span = span, RawGrammarKind = rawKind },

            "invocation_expression" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Method },
            "object_creation_expression" or "implicit_object_creation_expression" =>
                new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Constructor },

            "argument" or "attribute_argument" => new ArgumentSyntax { Span = span, RawGrammarKind = rawKind },

            "interpolated_string_expression" or "interpolated_verbatim_string_expression" or "interpolated_raw_string_expression" =>
                new InterpolatedStringExprSyntax { Span = span, RawGrammarKind = rawKind },

            "string_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.String },
            "verbatim_string_literal" or "raw_string_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.RawString },
            "character_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Char },
            "integer_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Integer },
            "real_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Float },
            "boolean_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Boolean },
            "null_literal" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Null },

            "identifier" or "this" or "base" => new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind },
            "predefined_type" when parentRawKind is "member_access_expression" =>
                new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind },

            "member_access_expression" or "qualified_name" => new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind },
            "element_access_expression" => new IndexExprSyntax { Span = span, RawGrammarKind = rawKind },
            "binary_expression" or "is_expression" or "as_expression" => new BinaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "prefix_unary_expression" or "postfix_unary_expression" or "typeof_expression" or "sizeof_expression" or "nameof_expression" =>
                new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "assignment_expression" => new AssignmentExprSyntax { Span = span, RawGrammarKind = rawKind },
            "conditional_expression" => new ConditionalExprSyntax { Span = span, RawGrammarKind = rawKind },
            "lambda_expression" => new LambdaExprSyntax { Span = span, RawGrammarKind = rawKind },
            "await_expression" => new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind, Operator = "await" },

            "block" or "unsafe_statement" or "checked_statement" =>
                new BlockStmtSyntax { Span = span, RawGrammarKind = rawKind, IsUnsafe = rawKind == "unsafe_statement" },
            "if_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.If },
            "switch_statement" or "switch_expression" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Switch },
            "for_statement" or "for_each_statement" or "while_statement" or "do_statement" =>
                new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Loop },
            "try_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Try },
            "catch_clause" => new CatchClauseSyntax { Span = span, RawGrammarKind = rawKind },
            "using_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Using },
            "return_statement" or "yield_statement" => new ReturnStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "throw_statement" or "throw_expression" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Throw },
            "break_statement" or "continue_statement" or "goto_statement" =>
                new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Branch },
            "lock_statement" => new ControlFlowStmtSyntax { Span = span, RawGrammarKind = rawKind, Kind = ControlFlowKind.Lock },

            "generic_name" or "nullable_type" or "array_type" or "pointer_type" or "tuple_type" or "predefined_type" =>
                new TypeRefSyntax { Span = span, RawGrammarKind = rawKind },

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
                        Parent = memExpr,
                        RawGrammarKind = objNode.RawGrammarKind
                    };
                }
                if (propNode != null) memExpr.MemberNameSlice = new SourceSlice(propNode.Span.StartByte, propNode.Span.Length);
                break;
        }
    }
}

