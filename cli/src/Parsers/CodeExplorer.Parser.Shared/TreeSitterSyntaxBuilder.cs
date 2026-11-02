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
                decl.Name = nameChild.Text;
            }
            else if (string.IsNullOrEmpty(decl.Name) && !string.IsNullOrEmpty(decl.Text) && decl.Children.Count == 0)
            {
                decl.Name = decl.Text;
            }
            if (decl is NamespaceDeclSyntax ns && !string.IsNullOrEmpty(ns.Name) && string.IsNullOrEmpty(doc.PackageOrNamespace))
            {
                doc.PackageOrNamespace = ns.Name;
            }
        }

        switch (node)
        {
            case IdentifierExprSyntax idExpr:
                idExpr.Name = idExpr.Text;
                break;

            case LiteralExprSyntax litExpr:
                litExpr.ValueText = litExpr.LiteralKind is LiteralSyntaxKind.String or LiteralSyntaxKind.RawString or LiteralSyntaxKind.Char
                    ? AstValueResolver.Unquote(litExpr.Text)
                    : litExpr.Text;
                if (!HasInterpolationChild(litExpr))
                {
                    litExpr.ResolvedValue = litExpr.ValueText;
                }
                break;

            case BinaryExprSyntax binExpr:
                if (binExpr.GetChildForField(SyntaxFields.Left) is ExprSyntax leftExpr) binExpr.Left = leftExpr;
                if (binExpr.GetChildForField(SyntaxFields.Right) is ExprSyntax rightExpr) binExpr.Right = rightExpr;
                var opNode = binExpr.GetChildForField(SyntaxFields.Operator);
                if (opNode != null) binExpr.Operator = opNode.Text;
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
                            Name = id.Name,
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
                if (argName != null) argSyntax.Name = argName.Text;
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
                varDecl.Initializer = varDecl.GetChildForField(SyntaxFields.Value) as ExprSyntax ??
                                      varDecl.Children.OfType<ExprSyntax>().LastOrDefault();
                break;

            case MemberDeclSyntax memDecl:
                memDecl.Initializer = memDecl.GetChildForField(SyntaxFields.Value) as ExprSyntax ??
                                      memDecl.Children.OfType<ExprSyntax>().LastOrDefault();
                break;

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

        SyntaxNode ConvertNode(Node tsNode, SyntaxNode parent, string? fieldRole)
        {
            var id = nextNodeId++;
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
                        "syntax_error",
                        string.IsNullOrEmpty(snippet)
                            ? $"Syntax error in {parent.RawGrammarKind}"
                            : $"Syntax error in {parent.RawGrammarKind} near '{snippet}'",
                        span,
                        parent.RawGrammarKind));
                }
            }
            var isNamed = tsNode.IsNamed;

            SyntaxNode syntaxNode = isNamed
                ? CreateSyntaxNode(rawKind, span, parent.RawGrammarKind)
                : new TokenSyntax { Span = span, RawGrammarKind = rawKind, IsNamed = false };

            syntaxNode.NodeId = id;
            syntaxNode.FieldRole = fieldRole;
            syntaxNode.Document = doc;
            syntaxNode.Parent = parent;

            var tsChildren = tsNode.Children;
            var childCount = tsChildren.Count;
            if (childCount > 0)
            {
                var convertedChildren = new List<SyntaxNode>(childCount);
                for (int i = 0; i < childCount; i++)
                {
                    var tsChild = tsChildren[i];
                    if (tsChild.Id == IntPtr.Zero) continue;
                    var childField = tsNode.GetFieldNameForChild(i);
                    var convertedChild = ConvertNode(tsChild, syntaxNode, childField);
                    convertedChild.ChildIndex = convertedChildren.Count;
                    convertedChildren.Add(convertedChild);
                }
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

        var rootTsNode = tree.RootNode;
        var rootChildren = rootTsNode.Children;
        if (rootChildren.Count > 0)
        {
            var docChildren = new List<SyntaxNode>(rootChildren.Count);
            for (int i = 0; i < rootChildren.Count; i++)
            {
                var tsChild = rootChildren[i];
                if (tsChild.Id == IntPtr.Zero) continue;
                var childField = rootTsNode.GetFieldNameForChild(i);
                var childSyntax = ConvertNode(tsChild, doc, childField);
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
            }
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
