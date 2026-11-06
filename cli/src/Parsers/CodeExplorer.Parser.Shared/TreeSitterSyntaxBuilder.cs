using System.Buffers;
using System.Collections.Concurrent;
using TreeSitter;

namespace CodeExplorer.Core.Parser.Syntax;

/// <summary>
/// Language-agnostic base class for converting a native <see cref="TreeSitter.Tree"/> / <see cref="TreeSitter.Node"/>
/// into the Universal AST (<see cref="SyntaxDocument"/>) and immediately disposing native Tree-sitter handles.
/// Each concrete language parser implements <see cref="CreateSyntaxNode"/> and <see cref="PopulateSyntaxNode"/>
/// for its own proprietary grammar.
/// </summary>
public abstract class TreeSitterSyntaxConverterBase
{
    private static readonly ConcurrentDictionary<string, Language> LanguageCache = new(StringComparer.OrdinalIgnoreCase);

    [ThreadStatic]
    private static Dictionary<string, TreeSitter.Parser>? _threadParsers;

    private static Language GetLanguage(string languageName) =>
        LanguageCache.GetOrAdd(languageName, static name => new Language(name));

    private static TreeSitter.Parser GetThreadParser(string languageName)
    {
        var dict = _threadParsers ??= new Dictionary<string, TreeSitter.Parser>(StringComparer.OrdinalIgnoreCase);
        if (!dict.TryGetValue(languageName, out var parser))
        {
            parser = new TreeSitter.Parser(GetLanguage(languageName));
            dict[languageName] = parser;
        }
        return parser;
    }

    protected static string InternOperator(ReadOnlySpan<char> span)
    {
        span = span.Trim();
        return span switch
        {
            "" => string.Empty,
            "=" => "=",
            "+" => "+",
            "-" => "-",
            "*" => "*",
            "/" => "/",
            "%" => "%",
            "==" => "==",
            "!=" => "!=",
            "===" => "===",
            "!==" => "!==",
            "<" => "<",
            ">" => ">",
            "<=" => "<=",
            ">=" => ">=",
            "&&" => "&&",
            "||" => "||",
            "!" => "!",
            "&" => "&",
            "|" => "|",
            "^" => "^",
            "~" => "~",
            "<<" => "<<",
            ">>" => ">>",
            "??" => "??",
            "??=" => "??=",
            "+=" => "+=",
            "-=" => "-=",
            "*=" => "*=",
            "/=" => "/=",
            ":=" => ":=",
            "=>" => "=>",
            "->" => "->",
            "." => ".",
            "?." => "?.",
            "::" => "::",
            "++" => "++",
            "--" => "--",
            "and" => "and",
            "or" => "or",
            "not" => "not",
            "in" => "in",
            "is" => "is",
            "as" => "as",
            _ => SyntaxStringPool.Intern(span)
        };
    }

    /// <summary>
    /// Maps a named native grammar node kind to a strongly-typed <see cref="SyntaxNode"/> in the Universal AST.
    /// </summary>
    protected abstract SyntaxNode CreateSyntaxNode(string rawKind, TextSpan span, string parentRawKind);

    /// <summary>
    /// Populates strongly-typed Universal AST properties on <paramref name="node"/> after its children are attached.
    /// Concrete language converters override this to map language-specific grammar fields.
    /// </summary>
    protected virtual void PopulateSyntaxNode(SyntaxNode node, SyntaxDocument doc)
    {
        if (node is DeclarationSyntax decl)
        {
            var nameChild = decl.GetChildForField(SyntaxFields.Name) ??
                            decl.FirstChildOfType<IdentifierExprSyntax>();
            if (nameChild != null)
            {
                decl.NameSlice = new SourceSlice(nameChild.Span.StartByte, nameChild.Span.Length);
            }
            else if (!decl.HasName && decl.Children.Count == 0 && !decl.Span.IsEmpty)
            {
                decl.NameSlice = new SourceSlice(decl.Span.StartByte, decl.Span.Length);
            }
            if (decl is NamespaceDeclSyntax ns && ns.HasName && string.IsNullOrEmpty(doc.PackageOrNamespace))
            {
                doc.PackageOrNamespace = ns.Name;
            }
        }

        switch (node)
        {
            case IdentifierExprSyntax:
                break;

            case LiteralExprSyntax litExpr:
                if (litExpr.LiteralKind is LiteralSyntaxKind.String or LiteralSyntaxKind.RawString or LiteralSyntaxKind.Char)
                {
                    litExpr.ValueSlice = AstValueResolver.ComputeLiteralValueSlice(
                        doc.Slice(litExpr.Span),
                        litExpr.Span.StartByte,
                        out var hasEscapes,
                        out var hasObfuscatedEscapes);
                    litExpr.HasEscapeSequences = hasEscapes;
                    if (hasObfuscatedEscapes && !HasInterpolationChild(litExpr))
                    {
                        litExpr.IsDecoded = true;
                    }
                }
                else
                {
                    litExpr.ValueSlice = new SourceSlice(litExpr.Span.StartByte, litExpr.Span.Length);
                }
                break;

            case BinaryExprSyntax binExpr:
                binExpr.Left = binExpr.GetChildForField(SyntaxFields.Left) as ExprSyntax ?? (binExpr.Children.Count > 0 ? binExpr.Children[0] as ExprSyntax : null);
                binExpr.Right = binExpr.GetChildForField(SyntaxFields.Right) as ExprSyntax ?? (binExpr.Children.Count > 1 ? binExpr.Children[^1] as ExprSyntax : null);
                var opNode = binExpr.GetChildForField(SyntaxFields.Operator);
                if (opNode != null) binExpr.Operator = InternOperator(opNode.AsSpan());
                else if (string.IsNullOrEmpty(binExpr.Operator) && binExpr.Left != null && binExpr.Right != null && binExpr.Right.Span.StartByte > binExpr.Left.Span.EndByte)
                {
                    var opStart = binExpr.Left.Span.EndByte;
                    var opLen = binExpr.Right.Span.StartByte - opStart;
                    if (opStart >= 0 && opStart + opLen <= doc.SourceText.Length)
                    {
                        binExpr.Operator = InternOperator(doc.SourceText.AsSpan(opStart, opLen));
                    }
                }
                break;

            case FunctionDeclSyntax fn:
                var paramsContainer = fn.GetChildForField(SyntaxFields.Parameters);
                if (paramsContainer != null)
                {
                    List<ParameterDeclSyntax>? paramList = null;
                    for (int i = 0; i < paramsContainer.Children.Count; i++)
                    {
                        var c = paramsContainer.Children[i];
                        if (c is ParameterDeclSyntax p)
                        {
                            (paramList ??= new List<ParameterDeclSyntax>(paramsContainer.Children.Count)).Add(p);
                        }
                        else if (c is IdentifierExprSyntax id)
                        {
                            (paramList ??= new List<ParameterDeclSyntax>(paramsContainer.Children.Count)).Add(new ParameterDeclSyntax
                            {
                                NameSlice = new SourceSlice(id.Span.StartByte, id.Span.Length),
                                Span = id.Span,
                                Document = id.Document,
                                Parent = fn,
                                RawGrammarKind = id.RawGrammarKind
                            });
                        }
                    }
                    if (paramList != null) fn.Parameters = paramList;
                }
                else
                {
                    List<ParameterDeclSyntax>? directParams = null;
                    for (int i = 0; i < fn.Children.Count; i++)
                    {
                        if (fn.Children[i] is ParameterDeclSyntax dp)
                        {
                            (directParams ??= new List<ParameterDeclSyntax>(4)).Add(dp);
                        }
                    }
                    if (directParams != null) fn.Parameters = directParams;
                }
                fn.Body = fn.GetChildForField(SyntaxFields.Body) as BlockStmtSyntax ?? fn.FirstChildOfType<BlockStmtSyntax>();
                for (int i = 0; i < fn.Children.Count; i++)
                {
                    if (fn.Children[i] is ExprSyntax exprChild && exprChild is not IdentifierExprSyntax)
                    {
                        fn.ExpressionBody = exprChild;
                        break;
                    }
                }
                CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Analyze(fn);
                break;

            case CallExprSyntax call:
                var argsContainer = call.GetChildForField(SyntaxFields.Arguments);
                if (argsContainer == null)
                {
                    for (int i = 0; i < call.Children.Count; i++)
                    {
                        if (call.Children[i].RawGrammarKind is "argument_list" or "arguments" or "tuple_expression")
                        {
                            argsContainer = call.Children[i];
                            break;
                        }
                    }
                }
                if (argsContainer != null)
                {
                    List<ArgumentSyntax>? args = null;
                    for (int i = 0; i < argsContainer.Children.Count; i++)
                    {
                        var child = argsContainer.Children[i];
                        if (child is ArgumentSyntax arg)
                        {
                            (args ??= new List<ArgumentSyntax>(argsContainer.Children.Count)).Add(arg);
                        }
                        else if (child is ExprSyntax expr && expr.RawGrammarKind is not "(" and not ")" and not ",")
                        {
                            (args ??= new List<ArgumentSyntax>(argsContainer.Children.Count)).Add(new ArgumentSyntax
                            {
                                Value = expr,
                                Span = expr.Span,
                                Parent = call,
                                Document = call.Document,
                                RawGrammarKind = "argument"
                            });
                        }
                    }
                    if (args != null) call.Arguments = args;
                }
                else
                {
                    List<ArgumentSyntax>? directArgs = null;
                    for (int i = 0; i < call.Children.Count; i++)
                    {
                        if (call.Children[i] is ArgumentSyntax da)
                        {
                            (directArgs ??= new List<ArgumentSyntax>(4)).Add(da);
                        }
                    }
                    if (directArgs != null) call.Arguments = directArgs;
                }
                break;

            case ArgumentSyntax argSyntax:
                argSyntax.Value = argSyntax.GetChildForField(SyntaxFields.Value) as ExprSyntax ??
                                  argSyntax.FirstChildOfType<ExprSyntax>();
                var argName = argSyntax.GetChildForField(SyntaxFields.Name);
                if (argName != null) argSyntax.NameSlice = new SourceSlice(argName.Span.StartByte, argName.Span.Length);
                break;

            case AssignmentExprSyntax assign:
                assign.Left = assign.GetChildForField(SyntaxFields.Left) ?? (assign.Children.Count > 0 ? assign.Children[0] : null);
                assign.Right = assign.GetChildForField(SyntaxFields.Right) as ExprSyntax ?? assign.LastChildOfType<ExprSyntax>();
                var assignOp = assign.GetChildForField(SyntaxFields.Operator);
                if (assignOp != null) assign.Operator = InternOperator(assignOp.AsSpan());
                break;

            case ConditionalExprSyntax cond:
                cond.Condition = cond.GetChildForField(SyntaxFields.Condition) as ExprSyntax ?? cond.NthChildOfType<ExprSyntax>(0);
                break;

            case UnaryExprSyntax unary:
                unary.Operand = unary.GetChildForField(SyntaxFields.Operand) as ExprSyntax ??
                                unary.GetChildForField(SyntaxFields.Argument) as ExprSyntax ??
                                unary.FirstChildOfType<ExprSyntax>();
                var unaryOp = unary.GetChildForField(SyntaxFields.Operator);
                if (unaryOp != null) unary.Operator = InternOperator(unaryOp.AsSpan());
                break;

            case ControlFlowStmtSyntax cf:
                switch (cf.Kind)
                {
                    case ControlFlowKind.If:
                        cf.Condition = cf.GetChildForField(SyntaxFields.Condition) as ExprSyntax ??
                                       cf.FirstChildOfType<ExprSyntax>() ??
                                       cf.GetChildForField(SyntaxFields.Condition)?.FirstChildOfType<ExprSyntax>();
                        cf.Body = cf.GetChildForField(SyntaxFields.Consequence) ?? cf.FirstChildOfType<StatementSyntax>();
                        cf.Alternative = cf.GetChildForField(SyntaxFields.Alternative) ?? cf.NthChildOfType<StatementSyntax>(1);
                        break;

                    case ControlFlowKind.Loop:
                        cf.Condition = cf.GetChildForField(SyntaxFields.Condition) as ExprSyntax ??
                                       cf.FirstChildOfType<ExprSyntax>() ??
                                       cf.GetChildForField(SyntaxFields.Condition)?.FirstChildOfType<ExprSyntax>();
                        cf.Body = cf.GetChildForField(SyntaxFields.Body) ?? cf.LastChildOfType<StatementSyntax>();
                        break;

                    case ControlFlowKind.Try:
                        cf.Body = cf.GetChildForField(SyntaxFields.Body) as BlockStmtSyntax ??
                                  cf.FirstChildOfType<BlockStmtSyntax>();
                        SyntaxNode? finallyNode = cf.GetChildForField(SyntaxFields.Finalizer);
                        if (finallyNode == null)
                        {
                            for (int i = 0; i < cf.Children.Count; i++)
                            {
                                var c = cf.Children[i];
                                if (c.FieldRole is "finalizer" or "finally_clause" || c.RawGrammarKind is "finally_clause")
                                {
                                    finallyNode = c;
                                    break;
                                }
                            }
                        }
                        cf.Alternative = finallyNode as BlockStmtSyntax ?? finallyNode?.FirstChildOfType<BlockStmtSyntax>();
                        break;

                    case ControlFlowKind.Lock:
                        cf.Body = cf.FirstChildOfType<BlockStmtSyntax>();
                        break;

                    case ControlFlowKind.Using:
                        cf.Body = cf.LastChildOfType<StatementSyntax>();
                        break;
                }
                break;

            case CatchClauseSyntax catchClause:
                catchClause.Body = catchClause.FirstChildOfType<BlockStmtSyntax>();
                break;

            case ReturnStmtSyntax ret:
                ret.Value = ret.FirstChildOfType<ExprSyntax>();
                break;

            case VariableDeclSyntax varDecl:
                var varInitNode = varDecl.GetChildForField(SyntaxFields.Value) ??
                                  varDecl.GetChildForField(SyntaxFields.Right);
                if (varInitNode == null)
                {
                    for (int i = 0; i < varDecl.Children.Count; i++)
                    {
                        if (varDecl.Children[i].RawGrammarKind == "equals_value_clause")
                        {
                            varInitNode = varDecl.Children[i];
                            break;
                        }
                    }
                }
                if (varInitNode is { RawGrammarKind: "equals_value_clause" } && varInitNode.Children.Count > 0)
                {
                    varInitNode = varInitNode.Children[^1];
                }
                if (varInitNode is ExprSyntax directVarInit)
                {
                    varDecl.Initializer = directVarInit;
                }
                else if (varDecl.Children.Count >= 2)
                {
                    var vdNameSpan = varDecl.NameSpan;
                    for (int i = varDecl.Children.Count - 1; i >= 1; i--)
                    {
                        if (varDecl.Children[i] is ExprSyntax exprCandidate &&
                            exprCandidate.FieldRole is not (SyntaxFields.Name or SyntaxFields.Type or SyntaxFields.Pattern or SyntaxFields.Left) &&
                            !exprCandidate.AsSpan().Equals(vdNameSpan, StringComparison.Ordinal))
                        {
                            varDecl.Initializer = exprCandidate;
                            break;
                        }
                    }
                }
                if (varDecl.Modifiers.HasFlag(SyntaxModifiers.Const) || varDecl.Modifiers.HasFlag(SyntaxModifiers.ReadOnly))
                {
                    varDecl.IsConstant = true;
                }
                for (int i = 0; i < varDecl.Children.Count; i++)
                {
                    if (varDecl.Children[i].RawGrammarKind == "modifier" && varDecl.Children[i].AsSpan() is "const" or "readonly")
                    {
                        varDecl.IsConstant = true;
                        varDecl.Modifiers |= SyntaxModifiers.Const;
                    }
                }
                for (int i = 0; i < varDecl.Children.Count; i++)
                {
                    if (varDecl.Children[i] is VariableDeclSyntax childVd)
                    {
                        PropagateVariableDeclMetadata(varDecl, childVd);
                    }
                }
                break;

            case ImplDeclSyntax implDecl:
                implDecl.ImplementedTrait = implDecl.GetChildForField(SyntaxFields.Trait) as TypeRefSyntax;
                implDecl.TargetType = implDecl.GetChildForField(SyntaxFields.Type) as TypeRefSyntax;
                if (implDecl.TargetType == null && implDecl.Children.Count > 0)
                {
                    var firstType = implDecl.NthChildOfType<TypeRefSyntax>(0);
                    var secondType = implDecl.NthChildOfType<TypeRefSyntax>(1);
                    if (secondType != null)
                    {
                        implDecl.ImplementedTrait ??= firstType;
                        implDecl.TargetType = secondType;
                    }
                    else if (firstType != null)
                    {
                        implDecl.TargetType = firstType;
                    }
                }
                break;

            case MemberDeclSyntax memDecl:
                var memInitNode = memDecl.GetChildForField(SyntaxFields.Value);
                if (memInitNode == null)
                {
                    for (int i = 0; i < memDecl.Children.Count; i++)
                    {
                        if (memDecl.Children[i].RawGrammarKind is "equals_value_clause" or "arrow_expression_clause")
                        {
                            memInitNode = memDecl.Children[i];
                            break;
                        }
                    }
                }
                if (memInitNode is { RawGrammarKind: "equals_value_clause" or "arrow_expression_clause" } && memInitNode.Children.Count > 0)
                {
                    memInitNode = memInitNode.Children[^1];
                }
                if (memInitNode is ExprSyntax directMemInit)
                {
                    memDecl.Initializer = directMemInit;
                }
                else if (memDecl.Children.Count >= 2)
                {
                    var mdNameSpan = memDecl.NameSpan;
                    for (int i = memDecl.Children.Count - 1; i >= 1; i--)
                    {
                        if (memDecl.Children[i] is ExprSyntax exprCandidate &&
                            exprCandidate.FieldRole is not (SyntaxFields.Name or SyntaxFields.Type or SyntaxFields.Pattern or SyntaxFields.Left) &&
                            !exprCandidate.AsSpan().Equals(mdNameSpan, StringComparison.Ordinal))
                        {
                            memDecl.Initializer = exprCandidate;
                            break;
                        }
                    }
                }
                for (int i = 0; i < memDecl.Children.Count; i++)
                {
                    if (memDecl.Children[i].RawGrammarKind == "modifier")
                    {
                        var modSpan = memDecl.Children[i].AsSpan();
                        if (modSpan is "const") memDecl.Modifiers |= SyntaxModifiers.Const;
                        else if (modSpan is "readonly") memDecl.Modifiers |= SyntaxModifiers.ReadOnly;
                        else if (modSpan is "static") memDecl.Modifiers |= SyntaxModifiers.Static;
                    }
                }
                if (memDecl.Modifiers.HasFlag(SyntaxModifiers.Const) || memDecl.Modifiers.HasFlag(SyntaxModifiers.ReadOnly))
                {
                    for (int i = 0; i < memDecl.Children.Count; i++)
                    {
                        if (memDecl.Children[i] is VariableDeclSyntax childVd)
                        {
                            childVd.IsConstant = true;
                            childVd.Modifiers |= memDecl.Modifiers & (SyntaxModifiers.Const | SyntaxModifiers.ReadOnly | SyntaxModifiers.Static);
                            for (int j = 0; j < childVd.Children.Count; j++)
                            {
                                if (childVd.Children[j] is VariableDeclSyntax subVd)
                                {
                                    subVd.IsConstant = true;
                                    subVd.Modifiers |= memDecl.Modifiers & (SyntaxModifiers.Const | SyntaxModifiers.ReadOnly | SyntaxModifiers.Static);
                                }
                            }
                        }
                    }
                }
                break;
        }
    }

    protected virtual bool HasInterpolationChild(LiteralExprSyntax litExpr) => false;

    /// <summary>
    /// Parses source text using Tree-sitter into a complete <see cref="SyntaxDocument"/>,
    /// immediately disposing the native <see cref="TreeSitter.Tree"/> and reusing the thread-local <see cref="TreeSitter.Parser"/>.
    /// </summary>
    public SyntaxDocument Parse(
        string sourceText,
        string languageName,
        string filePath = "",
        string relativePath = "")
    {
        sourceText ??= string.Empty;
        var parser = GetThreadParser(languageName);
        using var tree = parser.Parse(sourceText);

        var fullSpan = ComputeFullSpan(sourceText, tree?.RootNode);
        var doc = new SyntaxDocument
        {
            NodeId = 1,
            Span = fullSpan,
            RawGrammarKind = tree?.RootNode != null ? SyntaxStringPool.Intern(tree.RootNode.Type) : "document",
            FilePath = filePath,
            RelativePath = relativePath,
            LanguageName = languageName,
            SourceText = sourceText
        };
        doc.Document = doc;

        if (tree?.RootNode == null)
        {
            return doc;
        }

        var isCSharpLang = string.Equals(languageName, "c_sharp", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(languageName, "csharp", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(languageName, "c-sharp", StringComparison.OrdinalIgnoreCase);

        var nextNodeId = 2;
        var allTypes = new List<TypeDeclSyntax>();
        var allFunctions = new List<FunctionDeclSyntax>();
        var allMembers = new List<MemberDeclSyntax>();
        var allVariables = new List<VariableDeclSyntax>();
        var allCalls = new List<CallExprSyntax>();
        var imports = new List<ImportDeclSyntax>();
        var declarations = new List<DeclarationSyntax>();
        var topStatements = new List<StatementSyntax>();
        var diagnostics = new List<SyntaxDiagnostic>();

        using var cursor = tree.RootNode.Walk();

        SyntaxNode ConvertNode(TreeSitter.TreeCursor cur, SyntaxNode parent, string? fieldRole)
        {
            var id = nextNodeId++;
            var tsNode = cur.CurrentNode;
            var span = new TextSpan(
                tsNode.StartIndex,
                tsNode.EndIndex,
                tsNode.StartPosition.Row,
                tsNode.StartPosition.Column,
                tsNode.EndPosition.Row,
                tsNode.EndPosition.Column
            );
            var rawKind = SyntaxStringPool.Intern(tsNode.Type);
            if (rawKind == "ERROR" && parent.RawGrammarKind != "ERROR" && diagnostics.Count < 10)
            {
                var errLen = Math.Min(Math.Max(0, span.EndByte - span.StartByte), 80);
                var rawSpan = span.StartByte >= 0 && span.StartByte + errLen <= sourceText.Length
                    ? sourceText.AsSpan(span.StartByte, errLen).Trim()
                    : ReadOnlySpan<char>.Empty;

                var isBenign =
                    (parent.RawGrammarKind.StartsWith("jsx_", StringComparison.Ordinal) && rawSpan.StartsWith("&", StringComparison.Ordinal)) ||
                    (isCSharpLang && (
                        rawSpan.Contains("with", StringComparison.Ordinal) ||
                        rawSpan.Contains("from", StringComparison.Ordinal) ||
                        rawSpan.Contains("select", StringComparison.Ordinal) ||
                        rawSpan.IndexOfAny("}>;,".AsSpan()) >= 0 ||
                        rawSpan.Contains("Token.EqualTo", StringComparison.Ordinal) ||
                        rawSpan.Contains("in AndExpression", StringComparison.Ordinal) ||
                        rawSpan is "next"));

                if (!isBenign)
                {
                    var snippet = rawSpan.ToString().Replace('\r', ' ').Replace('\n', ' ').Trim();
                    diagnostics.Add(new SyntaxDiagnostic(
                        CodeExplorer.Cypher.Common.SyntaxDiagnosticKind.SyntaxError,
                        string.IsNullOrEmpty(snippet)
                            ? $"Syntax error in {parent.RawGrammarKind}"
                            : $"Syntax error in {parent.RawGrammarKind} near '{snippet}'",
                        span,
                        parent.RawGrammarKind));
                }
            }

            SyntaxNode syntaxNode = CreateSyntaxNode(rawKind, span, parent.RawGrammarKind);

            syntaxNode.NodeId = id;
            syntaxNode.FieldRole = fieldRole != null ? SyntaxStringPool.Intern(fieldRole) : null;
            syntaxNode.Document = doc;
            syntaxNode.Parent = parent;

            if (cur.GotoFirstChild())
            {
                SyntaxNode? firstChild = null;
                SyntaxNode? secondChild = null;
                SyntaxNode[]? rentedBuffer = null;
                var childCount = 0;

                do
                {
                    var childField = cur.CurrentFieldName;
                    var childTsNode = cur.CurrentNode;
                    var childType = childTsNode.Type;
                    if (!childTsNode.IsNamed)
                    {
                        if (childType is not ("this" or "base" or "self" or "super"))
                        {
                            ApplyUnnamedTokenMetadata(syntaxNode, childType, childField);
                            continue;
                        }
                    }
                    else if (childType is "comment" or "line_comment" or "block_comment" or "interpolation_brace")
                    {
                        continue;
                    }
                    else if (childType is "modifier" or "visibility_modifier" or "accessibility_modifier")
                    {
                        int modStart = childTsNode.StartIndex;
                        int modEnd = Math.Min(sourceText.Length, childTsNode.EndIndex);
                        if (modEnd > modStart)
                        {
                            ApplyNamedModifierSpan(syntaxNode, sourceText.AsSpan(modStart, modEnd - modStart).Trim());
                        }
                        continue;
                    }

                    var convertedChild = ConvertNode(cur, syntaxNode, childField);
                    convertedChild.ChildIndex = childCount;

                    if (childCount == 0)
                    {
                        firstChild = convertedChild;
                    }
                    else if (childCount == 1)
                    {
                        secondChild = convertedChild;
                    }
                    else
                    {
                        if (rentedBuffer == null)
                        {
                            rentedBuffer = ArrayPool<SyntaxNode>.Shared.Rent(8);
                            rentedBuffer[0] = firstChild!;
                            rentedBuffer[1] = secondChild!;
                        }
                        else if (childCount >= rentedBuffer.Length)
                        {
                            var grown = ArrayPool<SyntaxNode>.Shared.Rent(rentedBuffer.Length * 2);
                            Array.Copy(rentedBuffer, 0, grown, 0, childCount);
                            ArrayPool<SyntaxNode>.Shared.Return(rentedBuffer, clearArray: true);
                            rentedBuffer = grown;
                        }
                        rentedBuffer[childCount] = convertedChild;
                    }
                    childCount++;
                } while (cur.GotoNextSibling());

                cur.GotoParent();

                if (childCount == 1)
                {
                    syntaxNode.Children = [firstChild!];
                }
                else if (childCount == 2)
                {
                    syntaxNode.Children = [firstChild!, secondChild!];
                }
                else if (childCount > 2 && rentedBuffer != null)
                {
                    var exactChildren = new SyntaxNode[childCount];
                    Array.Copy(rentedBuffer, 0, exactChildren, 0, childCount);
                    ArrayPool<SyntaxNode>.Shared.Return(rentedBuffer, clearArray: true);
                    syntaxNode.Children = exactChildren;
                }
            }

            PopulateSyntaxNode(syntaxNode, doc);

            switch (syntaxNode)
            {
                case TypeDeclSyntax td:
                    allTypes.Add(td);
                    break;
                case FunctionDeclSyntax fd:
                    allFunctions.Add(fd);
                    break;
                case MemberDeclSyntax md:
                    allMembers.Add(md);
                    break;
                case VariableDeclSyntax vd:
                    if (!HasVariableDeclChild(vd))
                    {
                        allVariables.Add(vd);
                    }
                    break;
                case CallExprSyntax ce:
                    allCalls.Add(ce);
                    break;
                case ImportDeclSyntax imp:
                    imports.Add(imp);
                    break;
            }

            return syntaxNode;
        }

        if (cursor.GotoFirstChild())
        {
            var docChildren = new List<SyntaxNode>();
            do
            {
                var childField = cursor.CurrentFieldName;
                var topNode = cursor.CurrentNode;
                if (!topNode.IsNamed || topNode.Type is "comment" or "line_comment" or "block_comment")
                {
                    continue;
                }
                var childSyntax = ConvertNode(cursor, doc, childField);
                childSyntax.ChildIndex = docChildren.Count;
                docChildren.Add(childSyntax);

                if (childSyntax is DeclarationSyntax decl && decl is not ImportDeclSyntax)
                {
                    declarations.Add(decl);
                }
                else if (childSyntax is StatementSyntax stmt)
                {
                    topStatements.Add(stmt);
                }
            } while (cursor.GotoNextSibling());
            doc.Children = docChildren;
        }

        doc.Imports = imports;
        doc.Declarations = declarations;
        doc.TopLevelStatements = topStatements;
        doc.Diagnostics = diagnostics;
        doc.AllTypes = allTypes;
        doc.AllFunctions = allFunctions;
        doc.AllMembers = allMembers;
        doc.AllVariables = allVariables;
        doc.AllCalls = allCalls;

        return doc;
    }

    private static bool HasVariableDeclChild(VariableDeclSyntax vd)
    {
        for (int i = 0; i < vd.Children.Count; i++)
        {
            if (vd.Children[i] is VariableDeclSyntax)
                return true;
        }
        return false;
    }

    private static void PropagateVariableDeclMetadata(VariableDeclSyntax parent, VariableDeclSyntax child)
    {
        if (parent.IsConstant)
        {
            child.IsConstant = true;
        }
        child.Modifiers |= parent.Modifiers;
        child.VariableType ??= parent.VariableType;
        for (int i = 0; i < child.Children.Count; i++)
        {
            if (child.Children[i] is VariableDeclSyntax grandChild)
            {
                PropagateVariableDeclMetadata(child, grandChild);
            }
        }
    }

    private static void ApplyNamedModifierSpan(SyntaxNode parent, ReadOnlySpan<char> modSpan)
    {
        if (parent is not DeclarationSyntax decl) return;
        switch (modSpan)
        {
            case "const":
                decl.Modifiers |= SyntaxModifiers.Const;
                if (decl is VariableDeclSyntax vdConst) vdConst.IsConstant = true;
                break;
            case "readonly":
                decl.Modifiers |= SyntaxModifiers.ReadOnly;
                if (decl is VariableDeclSyntax vdRo) vdRo.IsConstant = true;
                break;
            case "static":
                decl.Modifiers |= SyntaxModifiers.Static;
                break;
            case "public" or "pub":
                decl.Modifiers |= SyntaxModifiers.Public;
                break;
            case var s when s.StartsWith("pub(", StringComparison.Ordinal):
                decl.Modifiers |= SyntaxModifiers.Internal;
                break;
            case "private":
                decl.Modifiers |= SyntaxModifiers.Private;
                break;
            case "protected":
                decl.Modifiers |= SyntaxModifiers.Protected;
                break;
            case "internal":
                decl.Modifiers |= SyntaxModifiers.Internal;
                break;
            case "async":
                decl.Modifiers |= SyntaxModifiers.Async;
                break;
            case "abstract":
                decl.Modifiers |= SyntaxModifiers.Abstract;
                break;
            case "virtual":
                decl.Modifiers |= SyntaxModifiers.Virtual;
                break;
            case "override":
                decl.Modifiers |= SyntaxModifiers.Override;
                break;
            case "sealed" or "final":
                decl.Modifiers |= SyntaxModifiers.Sealed;
                break;
            case "partial":
                decl.Modifiers |= SyntaxModifiers.Partial;
                break;
            case "extern":
                decl.Modifiers |= SyntaxModifiers.Extern;
                break;
            case "unsafe":
                decl.Modifiers |= SyntaxModifiers.Unsafe;
                break;
            case "export":
                decl.Modifiers |= SyntaxModifiers.Export;
                break;
            case "default":
                decl.Modifiers |= SyntaxModifiers.Default;
                break;
        }
    }

    private static void ApplyUnnamedTokenMetadata(SyntaxNode parent, string tokenKind, string? fieldRole)
    {
        switch (parent)
        {
            case BinaryExprSyntax bin when string.IsNullOrEmpty(bin.Operator) &&
                (fieldRole == SyntaxFields.Operator || tokenKind is not "(" and not ")" and not "," and not ";"):
                bin.Operator = tokenKind;
                break;
            case AssignmentExprSyntax assign when string.IsNullOrEmpty(assign.Operator) &&
                (fieldRole == SyntaxFields.Operator || tokenKind.Contains('=') || tokenKind is ":="):
                assign.Operator = tokenKind;
                break;
            case UnaryExprSyntax unary when string.IsNullOrEmpty(unary.Operator) &&
                (fieldRole == SyntaxFields.Operator || tokenKind is not "(" and not ")" and not "," and not ";"):
                unary.Operator = tokenKind;
                break;
            case ImportDeclSyntax imp when tokenKind == "*":
                imp.IsWildcard = true;
                break;
            case DeclarationSyntax decl:
                switch (tokenKind)
                {
                    case "const":
                        decl.Modifiers |= SyntaxModifiers.Const;
                        if (decl is VariableDeclSyntax vdConst) vdConst.IsConstant = true;
                        break;
                    case "readonly":
                        decl.Modifiers |= SyntaxModifiers.ReadOnly;
                        if (decl is VariableDeclSyntax vdRo) vdRo.IsConstant = true;
                        break;
                    case "static":
                        decl.Modifiers |= SyntaxModifiers.Static;
                        break;
                    case "public" or "pub":
                        decl.Modifiers |= SyntaxModifiers.Public;
                        break;
                    case "private":
                        decl.Modifiers |= SyntaxModifiers.Private;
                        break;
                    case "protected":
                        decl.Modifiers |= SyntaxModifiers.Protected;
                        break;
                    case "internal":
                        decl.Modifiers |= SyntaxModifiers.Internal;
                        break;
                    case "async":
                        decl.Modifiers |= SyntaxModifiers.Async;
                        break;
                    case "export":
                        decl.Modifiers |= SyntaxModifiers.Export;
                        break;
                    case "default":
                        decl.Modifiers |= SyntaxModifiers.Default;
                        break;
                }
                break;
        }
    }

    private static TextSpan ComputeFullSpan(string sourceText, Node? rootNode)
    {
        if (rootNode != null && rootNode.Id != IntPtr.Zero)
        {
            return new TextSpan(
                rootNode.StartIndex,
                rootNode.EndIndex,
                rootNode.StartPosition.Row,
                rootNode.StartPosition.Column,
                rootNode.EndPosition.Row,
                rootNode.EndPosition.Column
            );
        }

        int lines = 0;
        int lastLineStart = 0;
        for (int i = 0; i < sourceText.Length; i++)
        {
            if (sourceText[i] == '\n')
            {
                lines++;
                lastLineStart = i + 1;
            }
        }
        return new TextSpan(0, sourceText.Length, 0, 0, lines, sourceText.Length - lastLineStart);
    }
}
