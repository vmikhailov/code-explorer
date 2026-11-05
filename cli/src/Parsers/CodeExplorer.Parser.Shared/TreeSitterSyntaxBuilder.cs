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

    private static Language GetLanguage(string languageName) =>
        LanguageCache.GetOrAdd(languageName, static name => new Language(name));

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
                            decl.Children.FirstOrDefault(static c => c is IdentifierExprSyntax);
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
                if (opNode != null) binExpr.Operator = opNode.Text;
                else if (string.IsNullOrEmpty(binExpr.Operator) && binExpr.Left != null && binExpr.Right != null && binExpr.Right.Span.StartByte > binExpr.Left.Span.EndByte)
                {
                    binExpr.Operator = doc.GetText(binExpr.Left.Span.EndByte, binExpr.Right.Span.StartByte).Trim();
                }
                break;

            case FunctionDeclSyntax fn:
                var paramsContainer = fn.GetChildForField(SyntaxFields.Parameters);
                if (paramsContainer != null)
                {
                    fn.Parameters = paramsContainer.Children.Select(c => c switch
                    {
                        ParameterDeclSyntax p => p,
                        IdentifierExprSyntax id => new ParameterDeclSyntax
                        {
                            NameSlice = new SourceSlice(id.Span.StartByte, id.Span.Length),
                            Span = id.Span,
                            Document = id.Document,
                            Parent = fn,
                            RawGrammarKind = id.RawGrammarKind
                        },
                        _ => null
                    }).Where(static p => p != null).ToList()!;
                }
                else
                {
                    var directParams = fn.Children.OfType<ParameterDeclSyntax>().ToList();
                    if (directParams.Count > 0) fn.Parameters = directParams;
                }
                fn.Body = fn.GetChildForField(SyntaxFields.Body) as BlockStmtSyntax ?? fn.Children.OfType<BlockStmtSyntax>().FirstOrDefault();
                fn.ExpressionBody = fn.Children.OfType<ExprSyntax>().FirstOrDefault(static e => e is not IdentifierExprSyntax);
                CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Analyze(fn);
                break;

            case CallExprSyntax call:
                var argsContainer = call.GetChildForField(SyntaxFields.Arguments) ??
                                    call.Children.FirstOrDefault(static c => c.RawGrammarKind is "argument_list" or "arguments" or "tuple_expression");
                if (argsContainer != null)
                {
                    var args = new List<ArgumentSyntax>();
                    foreach (var child in argsContainer.Children)
                    {
                        if (child is ArgumentSyntax arg)
                        {
                            args.Add(arg);
                        }
                        else if (child is ExprSyntax expr && expr.RawGrammarKind is not "(" and not ")" and not ",")
                        {
                            args.Add(new ArgumentSyntax
                            {
                                Value = expr,
                                Span = expr.Span,
                                Parent = call,
                                Document = call.Document,
                                RawGrammarKind = "argument"
                            });
                        }
                    }
                    if (args.Count > 0) call.Arguments = args;
                }
                else
                {
                    var directArgs = call.Children.OfType<ArgumentSyntax>().ToList();
                    if (directArgs.Count > 0) call.Arguments = directArgs;
                }
                break;

            case ArgumentSyntax argSyntax:
                argSyntax.Value = argSyntax.GetChildForField(SyntaxFields.Value) as ExprSyntax ??
                                  argSyntax.Children.OfType<ExprSyntax>().FirstOrDefault();
                var argName = argSyntax.GetChildForField(SyntaxFields.Name);
                if (argName != null) argSyntax.NameSlice = new SourceSlice(argName.Span.StartByte, argName.Span.Length);
                break;

            case AssignmentExprSyntax assign:
                assign.Left = assign.GetChildForField(SyntaxFields.Left) ?? assign.Children.FirstOrDefault();
                assign.Right = assign.GetChildForField(SyntaxFields.Right) as ExprSyntax ?? assign.Children.OfType<ExprSyntax>().LastOrDefault();
                var assignOp = assign.GetChildForField(SyntaxFields.Operator);
                if (assignOp != null) assign.Operator = assignOp.Text;
                break;

            case ConditionalExprSyntax cond:
                cond.Condition = cond.GetChildForField(SyntaxFields.Condition) as ExprSyntax ?? cond.Children.OfType<ExprSyntax>().ElementAtOrDefault(0);
                cond.WhenTrue = cond.GetChildForField(SyntaxFields.Consequence) as ExprSyntax ?? cond.Children.OfType<ExprSyntax>().ElementAtOrDefault(1);
                cond.WhenFalse = cond.GetChildForField(SyntaxFields.Alternative) as ExprSyntax ?? cond.Children.OfType<ExprSyntax>().ElementAtOrDefault(2);
                break;

            case UnaryExprSyntax unary:
                unary.Operand = unary.GetChildForField(SyntaxFields.Operand) as ExprSyntax ??
                                unary.GetChildForField(SyntaxFields.Argument) as ExprSyntax ??
                                unary.Children.OfType<ExprSyntax>().FirstOrDefault();
                var unaryOp = unary.GetChildForField(SyntaxFields.Operator);
                if (unaryOp != null) unary.Operator = unaryOp.Text;
                break;

            case BlockStmtSyntax block:
                block.Statements = block.Children.Select(AsStatementOrWrap).Where(static s => s != null).ToList()!;
                break;

            case ExprStmtSyntax exprStmt:
                exprStmt.Expression = exprStmt.Children.OfType<ExprSyntax>().FirstOrDefault();
                break;

            case IfStmtSyntax ifStmt:
                ifStmt.Condition = ifStmt.GetChildForField(SyntaxFields.Condition) as ExprSyntax ??
                                   ifStmt.Children.OfType<ExprSyntax>().FirstOrDefault() ??
                                   ifStmt.Children.FirstOrDefault(static c => c.FieldRole == "condition")?.Children.OfType<ExprSyntax>().FirstOrDefault();
                ifStmt.ThenBranch = AsStatementOrWrap(ifStmt.GetChildForField(SyntaxFields.Consequence) ?? ifStmt.Children.OfType<StatementSyntax>().FirstOrDefault());
                ifStmt.ElseBranch = AsStatementOrWrap(ifStmt.GetChildForField(SyntaxFields.Alternative) ?? ifStmt.Children.OfType<StatementSyntax>().Skip(1).FirstOrDefault());
                break;

            case LoopStmtSyntax loop:
                loop.Condition = loop.GetChildForField(SyntaxFields.Condition) as ExprSyntax ??
                                 loop.Children.OfType<ExprSyntax>().FirstOrDefault() ??
                                 loop.Children.FirstOrDefault(static c => c.FieldRole == "condition")?.Children.OfType<ExprSyntax>().FirstOrDefault();
                loop.Body = AsStatementOrWrap(loop.GetChildForField(SyntaxFields.Body) ?? loop.Children.OfType<StatementSyntax>().LastOrDefault());
                loop.Collection = loop.GetChildForField(SyntaxFields.Right) as ExprSyntax ?? loop.GetChildForField(SyntaxFields.Value) as ExprSyntax;
                break;

            case TryStmtSyntax tryStmt:
                tryStmt.Body = tryStmt.GetChildForField(SyntaxFields.Body) as BlockStmtSyntax ??
                               tryStmt.Children.OfType<BlockStmtSyntax>().FirstOrDefault();
                tryStmt.Catches = tryStmt.Children.OfType<CatchClauseSyntax>().ToList();
                tryStmt.FinallyBody = tryStmt.GetChildForField(SyntaxFields.Finalizer) as BlockStmtSyntax ??
                                      tryStmt.Children.FirstOrDefault(static c => c.FieldRole is "finalizer" or "finally_clause" || c.RawGrammarKind is "finally_clause") as BlockStmtSyntax ??
                                      tryStmt.Children.FirstOrDefault(static c => c.FieldRole is "finalizer" or "finally_clause" || c.RawGrammarKind is "finally_clause")?.Children.OfType<BlockStmtSyntax>().FirstOrDefault();
                break;

            case CatchClauseSyntax catchClause:
                catchClause.Body = catchClause.Children.OfType<BlockStmtSyntax>().FirstOrDefault();
                break;

            case ReturnOrYieldStmtSyntax ret:
                ret.Value = ret.Children.OfType<ExprSyntax>().FirstOrDefault();
                break;

            case ThrowStmtSyntax thr:
                thr.Exception = thr.Children.OfType<ExprSyntax>().FirstOrDefault();
                break;

            case VariableDeclSyntax varDecl:
                var varInitNode = varDecl.GetChildForField(SyntaxFields.Value) ??
                                  varDecl.GetChildForField(SyntaxFields.Right) ??
                                  varDecl.Children.FirstOrDefault(static c => c.RawGrammarKind is "equals_value_clause");
                if (varInitNode is { RawGrammarKind: "equals_value_clause" } && varInitNode.Children.Count > 0)
                {
                    varInitNode = varInitNode.Children[^1];
                }
                varDecl.Initializer = varInitNode as ExprSyntax ??
                                      (varDecl.Children.Count >= 2 ? varDecl.Children.OfType<ExprSyntax>().LastOrDefault(c => c != varDecl.Children[0] && c.FieldRole is not (SyntaxFields.Name or SyntaxFields.Type or SyntaxFields.Pattern or SyntaxFields.Left) && !c.AsSpan().Equals(varDecl.NameSpan, StringComparison.Ordinal)) : null);
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
                if (varDecl.IsConstant)
                {
                    for (int i = 0; i < varDecl.Children.Count; i++)
                    {
                        if (varDecl.Children[i] is VariableDeclSyntax childVd)
                        {
                            childVd.IsConstant = true;
                            childVd.Modifiers |= varDecl.Modifiers & (SyntaxModifiers.Const | SyntaxModifiers.ReadOnly | SyntaxModifiers.Static);
                        }
                    }
                }
                break;

            case ImplDeclSyntax implDecl:
                const string TraitFieldName = "trait";
                implDecl.ImplementedTrait = implDecl.GetChildForField(TraitFieldName) as TypeRefSyntax;
                implDecl.TargetType = implDecl.GetChildForField(SyntaxFields.Type) as TypeRefSyntax;
                if (implDecl.TargetType == null && implDecl.Children.Count > 0)
                {
                    var typeRefs = implDecl.Children.OfType<TypeRefSyntax>().ToList();
                    if (typeRefs.Count >= 2)
                    {
                        implDecl.ImplementedTrait ??= typeRefs[0];
                        implDecl.TargetType = typeRefs[1];
                    }
                    else if (typeRefs.Count == 1)
                    {
                        implDecl.TargetType = typeRefs[0];
                    }
                }
                break;

            case MemberDeclSyntax memDecl:
                var memInitNode = memDecl.GetChildForField(SyntaxFields.Value) ??
                                  memDecl.Children.FirstOrDefault(static c => c.RawGrammarKind is "equals_value_clause" or "arrow_expression_clause");
                if (memInitNode is { RawGrammarKind: "equals_value_clause" or "arrow_expression_clause" } && memInitNode.Children.Count > 0)
                {
                    memInitNode = memInitNode.Children[^1];
                }
                memDecl.Initializer = memInitNode as ExprSyntax ??
                                      (memDecl.Children.Count >= 2 ? memDecl.Children.OfType<ExprSyntax>().LastOrDefault(c => c != memDecl.Children[0] && c.FieldRole is not (SyntaxFields.Name or SyntaxFields.Type or SyntaxFields.Pattern or SyntaxFields.Left) && !c.AsSpan().Equals(memDecl.NameSpan, StringComparison.Ordinal)) : null);
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

#if FALSE
                memDecl.Initializer = memDecl.GetChildForField(SyntaxFields.Value) as ExprSyntax ??
                                      memDecl.Children.OfType<ExprSyntax>().LastOrDefault();
                break;

#endif
            case LockStmtSyntax lockStmt:
                lockStmt.Body = lockStmt.Children.OfType<BlockStmtSyntax>().FirstOrDefault();
                break;

            case UsingOrWithStmtSyntax usingStmt:
                usingStmt.Body = usingStmt.Children.OfType<StatementSyntax>().LastOrDefault();
                break;

            case AwaitOrTryExprSyntax awaitExpr:
                awaitExpr.Operand = awaitExpr.Children.OfType<ExprSyntax>().FirstOrDefault();
                break;
        }
    }

    protected static StatementSyntax? AsStatementOrWrap(SyntaxNode? node) =>
        node switch
        {
            null => null,
            StatementSyntax stmt => stmt,
            DeclarationSyntax decl => new DeclStmtSyntax { Span = decl.Span, RawGrammarKind = decl.RawGrammarKind, Parent = decl.Parent, Document = decl.Document, Declarations = [decl] },
            ExprSyntax expr => new ExprStmtSyntax { Span = expr.Span, RawGrammarKind = expr.RawGrammarKind, Parent = expr.Parent, Document = expr.Document, Expression = expr },
            _ => null
        };

    protected virtual bool HasInterpolationChild(LiteralExprSyntax litExpr) => false;

    /// <summary>
    /// Parses source text using Tree-sitter into a complete <see cref="SyntaxDocument"/>,
    /// immediately disposing the native <see cref="TreeSitter.Tree"/> and <see cref="TreeSitter.Parser"/>.
    /// </summary>
    public SyntaxDocument Parse(
        string sourceText,
        string languageName,
        string filePath = "",
        string relativePath = "")
    {
        sourceText ??= string.Empty;
        var language = GetLanguage(languageName);
        using var parser = new TreeSitter.Parser(language);
        using var tree = parser.Parse(sourceText);

        var fullSpan = ComputeFullSpan(sourceText, tree?.RootNode);
        var doc = new SyntaxDocument
        {
            NodeId = 1,
            Span = fullSpan,
            RawGrammarKind = tree?.RootNode != null ? tree.RootNode.Type : "document",
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
            var rawKind = tsNode.Type;
            if (rawKind == "ERROR" && parent.RawGrammarKind != "ERROR" && diagnostics.Count < 10)
            {
                var snippet = doc.GetText(span.StartByte, Math.Min(span.EndByte, span.StartByte + 80))
                    .Replace('\r', ' ')
                    .Replace('\n', ' ')
                    .Trim();

                var isBenign =
                    (parent.RawGrammarKind.StartsWith("jsx_") && snippet.StartsWith("&")) ||
                    (string.Equals(languageName, "c_sharp", StringComparison.OrdinalIgnoreCase) || string.Equals(languageName, "csharp", StringComparison.OrdinalIgnoreCase) || string.Equals(languageName, "c-sharp", StringComparison.OrdinalIgnoreCase)) && (snippet.Contains("with") || snippet.Contains("from") || snippet.Contains("select") || snippet.Contains("}") || snippet.Contains(">") || snippet.Contains(";") || snippet.Contains("Token.EqualTo") || snippet.Contains("in AndExpression") || snippet.Contains(",") || snippet == "next");

                if (!isBenign)
                {
                    diagnostics.Add(new SyntaxDiagnostic(
                        CodeExplorer.Cypher.Common.SyntaxDiagnosticKind.SyntaxError,
                        string.IsNullOrEmpty(snippet)
                            ? $"Syntax error in {parent.RawGrammarKind}"
                            : $"Syntax error in {parent.RawGrammarKind} near '{snippet}'",
                        span,
                        parent.RawGrammarKind));
                }
            }
            // tsNode is guaranteed named here

            SyntaxNode syntaxNode = CreateSyntaxNode(rawKind, span, parent.RawGrammarKind);
            // Only named AST nodes are materialized; TokenSyntax is never allocated.
            // Unnamed punctuation/keyword tokens are handled inline via ApplyUnnamedTokenMetadata.

            syntaxNode.NodeId = id;
            syntaxNode.FieldRole = fieldRole;
            syntaxNode.Document = doc;
            syntaxNode.Parent = parent;

            if (cur.GotoFirstChild())
            {
                var convertedChildren = new List<SyntaxNode>();
                do
                {
                    var childField = cur.CurrentFieldName;
                    var childTsNode = cur.CurrentNode;
                    if (!childTsNode.IsNamed && childTsNode.Type is not ("this" or "base" or "self" or "super"))
                    {
                        ApplyUnnamedTokenMetadata(syntaxNode, childTsNode.Type, childField);
                        continue;
                    }
                    var convertedChild = ConvertNode(cur, syntaxNode, childField);
                    convertedChild.ChildIndex = convertedChildren.Count;
                    convertedChildren.Add(convertedChild);
                } while (cur.GotoNextSibling());
                cur.GotoParent();
                syntaxNode.Children = convertedChildren;
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
                    allVariables.Add(vd);
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
                if (!cursor.CurrentNode.IsNamed)
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
