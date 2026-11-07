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
            TreeSitterSyntaxPopulator.PopulateDeclarationHeader(decl, doc);
        }

        switch (node)
        {
            case IdentifierExprSyntax:
                break;

            case ImportDeclSyntax imp:
                TreeSitterSyntaxPopulator.PopulateImport(imp);
                break;

            case TypeRefSyntax typeRef:
                TreeSitterSyntaxPopulator.PopulateTypeRef(typeRef);
                break;

            case ParameterDeclSyntax param:
                TreeSitterSyntaxPopulator.PopulateParameter(param);
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
                TreeSitterSyntaxPopulator.PopulateFunction(fn);
                break;

            case LambdaExprSyntax lambda:
                TreeSitterSyntaxPopulator.PopulateLambda(lambda);
                break;

            case CallExprSyntax call:
                call.Arguments = TreeSitterSyntaxPopulator.ExtractArguments(call);
                break;

            case AnnotationSyntax ann:
                TreeSitterSyntaxPopulator.PopulateAnnotation(ann);
                break;

            case ArgumentSyntax argSyntax:
                TreeSitterSyntaxPopulator.PopulateArgument(argSyntax);
                break;

            case IndexExprSyntax idx:
                TreeSitterSyntaxPopulator.PopulateIndex(idx);
                break;

            case AssignmentExprSyntax assign:
                assign.Left = assign.GetChildForField(SyntaxFields.Left) ?? (assign.Children.Count > 0 ? assign.Children[0] : null);
                assign.Right = assign.GetChildForField(SyntaxFields.Right) as ExprSyntax ?? assign.LastChildOfType<ExprSyntax>();
                var assignOp = assign.GetChildForField(SyntaxFields.Operator);
                if (assignOp != null) assign.Operator = InternOperator(assignOp.AsSpan());
                break;

            case ConditionalExprSyntax cond:
                TreeSitterSyntaxPopulator.PopulateConditional(cond, doc);
                break;

            case CollectionExprSyntax col:
                TreeSitterSyntaxPopulator.PopulateCollection(col);
                break;

            case UnaryExprSyntax unary:
                unary.Operand = unary.GetChildForField(SyntaxFields.Operand) as ExprSyntax ??
                                unary.GetChildForField(SyntaxFields.Argument) as ExprSyntax ??
                                unary.FirstChildOfType<ExprSyntax>();
                var unaryOp = unary.GetChildForField(SyntaxFields.Operator);
                if (unaryOp != null) unary.Operator = InternOperator(unaryOp.AsSpan());
                break;

            case ControlFlowStmtSyntax cf:
                TreeSitterSyntaxPopulator.PopulateControlFlow(cf);
                break;

            case CatchClauseSyntax catchClause:
                TreeSitterSyntaxPopulator.PopulateCatchClause(catchClause);
                break;

            case ReturnStmtSyntax ret:
                ret.Value = ret.FirstChildOfType<ExprSyntax>();
                break;

            case VariableDeclSyntax varDecl:
                TreeSitterSyntaxPopulator.PopulateVariable(varDecl);
                break;

            case ImplDeclSyntax implDecl:
                TreeSitterSyntaxPopulator.PopulateImpl(implDecl);
                break;

            case MemberDeclSyntax memDecl:
                TreeSitterSyntaxPopulator.PopulateMember(memDecl);
                break;

            case KeyValueExprSyntax kv:
                TreeSitterSyntaxPopulator.PopulateKeyValue(kv);
                break;

            case ParenthesizedExprSyntax paren:
                TreeSitterSyntaxPopulator.PopulateParenthesized(paren);
                break;

            case TypeCastExprSyntax castExpr:
                TreeSitterSyntaxPopulator.PopulateTypeCast(castExpr);
                break;
        }
    }

    protected virtual bool HasInterpolationChild(LiteralExprSyntax litExpr)
    {
        for (int i = 0; i < litExpr.Children.Count; i++)
        {
            if (litExpr.Children[i] is ParenthesizedExprSyntax { IsInterpolation: true })
                return true;
        }
        return false;
    }

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

        SyntaxNode ConvertNode(TreeSitter.TreeCursor cur, SyntaxNode parent, string? fieldRole, string parentRawKind)
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
            if (rawKind == "ERROR" && parentRawKind != "ERROR" && diagnostics.Count < 10)
            {
                var errLen = Math.Min(Math.Max(0, span.EndByte - span.StartByte), 80);
                var rawSpan = span.StartByte >= 0 && span.StartByte + errLen <= sourceText.Length
                    ? sourceText.AsSpan(span.StartByte, errLen).Trim()
                    : ReadOnlySpan<char>.Empty;

                var snippet = rawSpan.ToString().Replace('\r', ' ').Replace('\n', ' ').Trim();
                diagnostics.Add(new SyntaxDiagnostic(
                    CodeExplorer.Cypher.Common.SyntaxDiagnosticKind.SyntaxError,
                    string.IsNullOrEmpty(snippet)
                        ? $"Syntax error in {parent.SyntaxKind}"
                        : $"Syntax error in {parent.SyntaxKind} near '{snippet}'",
                    span));
            }

            SyntaxNode syntaxNode = (rawKind == "string" && languageName == "python" &&
                                     span.StartByte >= 0 && span.StartByte + 2 <= sourceText.Length &&
                                     (sourceText[span.StartByte] is 'f' or 'F' ||
                                      (span.StartByte + 3 <= sourceText.Length && sourceText[span.StartByte] is 'r' or 'R' && sourceText[span.StartByte + 1] is 'f' or 'F')))
                ? new InterpolatedStringExprSyntax { Span = span }
                : CreateSyntaxNode(rawKind, span, parentRawKind);

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
                    else if (childType is "comment" or "line_comment" or "block_comment" or "interpolation_brace" or "interpolation_start" or "interpolation_quote" or "string_start" or "string_end")
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

                    var convertedChild = ConvertNode(cur, syntaxNode, childField, rawKind);
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
                const string rootDocumentRawKind = "document";
                var childSyntax = ConvertNode(cursor, doc, childField, rootDocumentRawKind);
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

    private static void ApplyNamedModifierSpan(SyntaxNode parent, ReadOnlySpan<char> modSpan)
    {
        if (parent is LambdaExprSyntax lam && modSpan is "async")
        {
            lam.IsAsync = true;
            return;
        }
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
            case "mut":
                decl.Modifiers |= SyntaxModifiers.Mutable;
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
            case LambdaExprSyntax lam when tokenKind == "async":
                lam.IsAsync = true;
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
                    case "mut":
                        decl.Modifiers |= SyntaxModifiers.Mutable;
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
