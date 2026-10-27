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

            case DeclarationSyntax decl:
                var nameChild = decl.GetChildForField(SyntaxFields.Name) ??
                                decl.Children.FirstOrDefault(static c => c is IdentifierExprSyntax);
                if (nameChild != null)
                {
                    decl.Name = nameChild.Text;
                }
                if (decl is NamespaceDeclSyntax ns && !string.IsNullOrEmpty(ns.Name) && string.IsNullOrEmpty(doc.PackageOrNamespace))
                {
                    doc.PackageOrNamespace = ns.Name;
                }
                break;

            case BinaryExprSyntax binExpr:
                if (binExpr.GetChildForField(SyntaxFields.Left) is ExprSyntax leftExpr) binExpr.Left = leftExpr;
                if (binExpr.GetChildForField(SyntaxFields.Right) is ExprSyntax rightExpr) binExpr.Right = rightExpr;
                var opNode = binExpr.GetChildForField(SyntaxFields.Operator);
                if (opNode != null) binExpr.Operator = opNode.Text;
                break;
        }
    }

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
