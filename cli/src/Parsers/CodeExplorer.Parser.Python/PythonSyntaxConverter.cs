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
            "module" => new NamespaceDeclSyntax { Span = span, RawGrammarKind = rawKind },
            "import_statement" or "import_from_statement" => new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "class_definition" => new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Class },
            "function_definition" => new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = FunctionSyntaxKind.Function },

            "default_parameter" or "typed_parameter" or "typed_default_parameter" or "list_splat_pattern" or "dictionary_splat_pattern" =>
                new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind },

            "decorator" => new AnnotationSyntax { Span = span, RawGrammarKind = rawKind },

            "call" => new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = CallSyntaxKind.Method },
            "keyword_argument" => new ArgumentSyntax { Span = span, RawGrammarKind = rawKind },

            "format_string" or "concatenated_string" => new InterpolatedStringExprSyntax { Span = span, RawGrammarKind = rawKind },
            "string" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.String },
            "integer" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Integer },
            "float" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Float },
            "true" or "false" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Boolean },
            "none" => new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = LiteralSyntaxKind.Null },

            "identifier" => new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind },

            "attribute" => new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind },
            "subscript" => new IndexOrSliceExprSyntax { Span = span, RawGrammarKind = rawKind },
            "binary_operator" or "boolean_operator" or "comparison_operator" =>
                new BinaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "unary_operator" or "not_operator" => new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "assignment" or "augmented_assignment" => new AssignmentExprSyntax { Span = span, RawGrammarKind = rawKind },
            "conditional_expression" => new ConditionalExprSyntax { Span = span, RawGrammarKind = rawKind },
            "lambda" => new LambdaExprSyntax { Span = span, RawGrammarKind = rawKind },
            "await" => new AwaitOrTryExprSyntax { Span = span, RawGrammarKind = rawKind },
            "list" or "dictionary" or "set" or "tuple" => new CollectionExprSyntax { Span = span, RawGrammarKind = rawKind },
            "list_comprehension" or "dictionary_comprehension" or "set_comprehension" or "generator_expression" =>
                new QueryOrComprehensionExprSyntax { Span = span, RawGrammarKind = rawKind, IsLinq = false },

            "block" => new BlockStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "if_statement" => new IfStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "match_statement" => new SwitchOrMatchStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "for_statement" or "while_statement" => new LoopStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "try_statement" => new TryStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "except_clause" => new CatchClauseSyntax { Span = span, RawGrammarKind = rawKind },
            "with_statement" => new UsingOrWithStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "return_statement" => new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind, IsYield = false },
            "yield" => new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind, IsYield = true },
            "raise_statement" => new ThrowStmtSyntax { Span = span, RawGrammarKind = rawKind },
            "break_statement" or "continue_statement" =>
                new BranchStmtSyntax { Span = span, RawGrammarKind = rawKind, BranchKind = rawKind },

            "generic_type" or "union_type" => new TypeRefSyntax { Span = span, RawGrammarKind = rawKind },

            "expression_statement" => new ExprStmtSyntax { Span = span, RawGrammarKind = rawKind },

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
                var fnNode = callExpr.GetChildForField("function");
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
                var propNode = memExpr.GetChildForField("attribute");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberName = propNode.Text;
                break;
        }
    }
}

