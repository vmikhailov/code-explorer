using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Python;

/// <summary>
/// Converts native Tree-sitter Python CST nodes into the Universal AST (<see cref="SyntaxDocument"/>).
/// </summary>
public sealed class PythonSyntaxConverter : TreeSitterSyntaxConverterBase
{
    public static readonly PythonSyntaxConverter Instance = new();

    protected override SyntaxNode CreateSyntaxNode(string rawKind, TextSpan span, string parentRawKind) =>
        rawKind switch
        {
            "module" => new NamespaceDeclSyntax { Span = span },
            "import_statement" or "import_from_statement" => new ImportDeclSyntax { Span = span },

            "class_definition" => new TypeDeclSyntax { Span = span, Kind = TypeSyntaxKind.Class },
            "function_definition" => new FunctionDeclSyntax { Span = span, Kind = FunctionSyntaxKind.Function },

            "identifier" when parentRawKind is "parameters" => new ParameterDeclSyntax { Span = span },
            "default_parameter" or "typed_parameter" or "typed_default_parameter" =>
                new ParameterDeclSyntax { Span = span },
            "list_splat_pattern" =>
                new ParameterDeclSyntax { Span = span, PassingMode = ParameterPassingMode.VariadicArgs },
            "dictionary_splat_pattern" =>
                new ParameterDeclSyntax { Span = span, PassingMode = ParameterPassingMode.VariadicKwargs },

            "decorator" => new AnnotationSyntax { Span = span },

            "call" => new CallExprSyntax { Span = span, CallKind = CallSyntaxKind.Method },
            "keyword_argument" => new ArgumentSyntax { Span = span },

            "format_string" or "concatenated_string" => new InterpolatedStringExprSyntax { Span = span },
            "interpolation" => new ParenthesizedExprSyntax { Span = span, IsInterpolation = true },
            "parenthesized_expression" => new ParenthesizedExprSyntax { Span = span },
            "pair" or "aliased_import" => new KeyValueExprSyntax { Span = span },
            "pattern_list" or "tuple_pattern" or "list_pattern" => new PatternSyntax { Span = span, Kind = PatternKind.Array },
            "dict_pattern" => new PatternSyntax { Span = span, Kind = PatternKind.Object },
            "as_pattern" => new PatternSyntax { Span = span, Kind = PatternKind.As },

            "string" or "string_content" or "escape_sequence" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.String },
            "integer" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Integer },
            "float" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Float },
            "true" or "false" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Boolean },
            "none" => new LiteralExprSyntax { Span = span, LiteralKind = LiteralSyntaxKind.Null },

            "list" or "list_comprehension" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Array },
            "dictionary" or "dictionary_comprehension" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Dictionary },
            "tuple" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Tuple },
            "set" or "set_comprehension" => new CollectionExprSyntax { Span = span, Kind = CollectionSyntaxKind.Set },

            "identifier" => new IdentifierExprSyntax { Span = span },

            "attribute" or "dotted_name" => new MemberAccessExprSyntax { Span = span },
            "subscript" => new IndexExprSyntax { Span = span },
            "binary_operator" or "boolean_operator" or "comparison_operator" =>
                new BinaryExprSyntax { Span = span },
            "unary_operator" or "not_operator" => new UnaryExprSyntax { Span = span },
            "assignment" or "augmented_assignment" => new AssignmentExprSyntax { Span = span },
            "conditional_expression" => new ConditionalExprSyntax { Span = span },
            "lambda" => new LambdaExprSyntax { Span = span },
            "await" => new UnaryExprSyntax { Span = span, Operator = "await", IsAwait = true },

            "block" => new BlockStmtSyntax { Span = span },
            "if_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.If },
            "match_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Switch },
            "for_statement" or "while_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Loop },
            "try_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Try },
            "except_clause" => new CatchClauseSyntax { Span = span },
            "with_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Using },
            "return_statement" or "yield" => new ReturnStmtSyntax { Span = span },
            "raise_statement" => new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Throw },
            "break_statement" or "continue_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.Branch },
            "pass_statement" =>
                new ControlFlowStmtSyntax { Span = span, Kind = ControlFlowKind.NoOp },

            "type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Annotation },
            "generic_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Generic },
            "union_type" => new TypeRefSyntax { Span = span, Kind = TypeRefKind.Union },

            "parameters" or "lambda_parameters" => new SyntaxList { Span = span, Kind = SyntaxListKind.Parameters },
            "argument_list" => new SyntaxList { Span = span, Kind = SyntaxListKind.Arguments },
            "type_parameter" => new SyntaxList { Span = span, Kind = SyntaxListKind.TypeParameters },
            "decorated_definition" => new SyntaxList { Span = span, Kind = SyntaxListKind.Declarations },
            "finally_clause" or "else_clause" or "elif_clause" => new SyntaxList { Span = span, Kind = SyntaxListKind.Clauses },

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
                var objNode = memExpr.GetChildForField("object");
                var propNode = memExpr.GetChildForField("attribute");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberNameSlice = new SourceSlice(propNode.Span.StartByte, propNode.Span.Length);
                break;
        }
    }
}

