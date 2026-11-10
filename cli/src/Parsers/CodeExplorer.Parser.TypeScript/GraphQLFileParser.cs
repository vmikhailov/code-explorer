using System.Text;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Cypher.Common;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript;

public sealed record GraphQLParsedOperation(
    OperationType OperationType,
    string? OperationName,
    IReadOnlyList<string> RootFields,
    int StartLine,
    int EndLine)
{
    public GraphQLParsedOperation(
        string OperationType,
        string? OperationName,
        IReadOnlyList<string> RootFields,
        int StartLine,
        int EndLine)
        : this(TaxonomyEnumExtendedExtensions.ParseOperationType(OperationType), OperationName, RootFields, StartLine, EndLine)
    {
    }
}

public sealed record GraphQLParsedSdlField(
    OperationType OperationType,
    string FieldName,
    string? ReturnType,
    int Line)
{
    public GraphQLParsedSdlField(
        string OperationType,
        string FieldName,
        string? ReturnType,
        int Line)
        : this(TaxonomyEnumExtendedExtensions.ParseOperationType(OperationType), FieldName, ReturnType, Line)
    {
    }
}

/// <summary>
/// Dedicated file parser for GraphQL documents (.graphql, .gql).
/// Extracts executable operations (query, mutation, subscription) with their top-level
/// root field selections (including aliased and multi-root-field operations) as well as
/// SDL root type definitions (type Query, type Mutation, type Subscription).
/// </summary>
public class GraphQLFileParser : IFileParser
{
    private const string GraphqlLanguage = "graphql";
    private const string GraphqlProtocol = "GraphQL";

    public string LanguageName => GraphqlLanguage;
    public bool UsesTreeSitter => false;
    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } = [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".graphql", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".gql", StringComparison.OrdinalIgnoreCase);
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath) =>
        ImportType.Internal;

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("GraphQLFileParser does not use TreeSitter visitors.");
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileId, fileName, relativePath, filePath);

        var content = await PhysicalFileStore.ConsumeFileTextAsync(filePath);
        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
        }

        // Test fixtures (e.g. __tests__/**/fixture/schema.graphql) describe no real API surface or client calls.
        if (WorkspaceConventions.IsTestFilePath(relativePath))
        {
            return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
        }

        var (operations, sdlFields) = GraphQLDocumentExtractor.ParseDocument(content);

        foreach (var op in operations)
        {
            if (op.RootFields.Count == 0) continue;

            var opTypeStr = op.OperationType.ToCanonicalString();
            var opName = !string.IsNullOrEmpty(op.OperationName)
                ? op.OperationName
                : $"Anonymous{opTypeStr}_{op.StartLine}";

            var pascalOp = ToPascalCase(opName);
            var primaryHookName = !string.IsNullOrEmpty(op.OperationName)
                ? $"use{pascalOp}{opTypeStr}"
                : opName;

            var symbolId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:fn:{primaryHookName}";
            var funcNode = new FunctionNode(
                symbolId,
                primaryHookName,
                symbolId,
                relativePath,
                relativePath,
                op.StartLine,
                op.EndLine,
                1,
                1);

            funcNode.GraphqlOperation = opName;
            funcNode.OperationType = opTypeStr;
            funcNode.GraphqlFields = string.Join(",", op.RootFields);
            if (op.RootFields.Count == 1)
            {
                funcNode.Signals().GraphqlField = op.RootFields[0];
            }

            if (!string.IsNullOrEmpty(op.OperationName))
            {
                var aliases = BuildCodegenAliases(op.OperationName, opTypeStr);
                funcNode.Signals().GraphqlAliases = string.Join(",", aliases);
            }

            foreach (var rootField in op.RootFields)
            {
                var gqlPath = "/" + rootField.TrimStart('/');
                var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:graphql:{opTypeStr}:{rootField}:{symbolId}";
                var extNode = new ExternalServiceNode(extId, rootField, GraphqlLanguage, GraphqlLanguage, gqlPath)
                {
                    FilePath = relativePath,
                    StartLine = op.StartLine,
                    StartCol = 1,
                    GraphqlField = rootField,
                    OperationType = opTypeStr,
                    CallerSymbolId = symbolId,
                    IsInternal = true,
                    Url = gqlPath
                };
                funcNode.Children.Add(extNode);
            }

            fileNode.Children.Add(funcNode);
        }

        var resolvedProj = ConstantRegistry.ExtractProjectName(relativePath) ?? "default";
        var projPart = (!string.IsNullOrEmpty(resolvedProj) && resolvedProj != "default") ? $"{resolvedProj}:" : "";

        foreach (var sdl in sdlFields)
        {
            var method = sdl.OperationType.ToCanonicalString().ToUpperInvariant();
            var epName = $"{method}:{sdl.FieldName}";
            var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{projPart}sdl:{method}:{sdl.FieldName}";
            var epNode = new EndpointNode(
                epId,
                epName,
                relativePath,
                method,
                sdl.FieldName,
                GraphqlProtocol,
                false,
                null,
                null,
                null,
                sdl.ReturnType,
                sdl.OperationType)
            {
                GraphqlField = sdl.FieldName,
                IsGraphqlSdl = true
            };
            fileNode.Children.Add(epNode);
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }

    internal static List<string> BuildCodegenAliases(string opName, string opType)
    {
        var pascal = ToPascalCase(opName);
        var aliases = new List<string>
        {
            opName,
            pascal,
            $"{pascal}Document",
            $"{opName}Document",
            $"use{pascal}{opType}",
            $"{pascal}{opType}"
        };

        if (opType == "Query")
        {
            aliases.Add($"use{pascal}LazyQuery");
            aliases.Add($"use{pascal}SuspenseQuery");
        }

        return [.. aliases.Distinct(StringComparer.Ordinal)];
    }

    internal static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (char.IsUpper(name[0]) && !name.Contains('_') && !name.Contains('-'))
        {
            return name;
        }

        var parts = name.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return char.ToUpperInvariant(name[0]) + name[1..];
        }

        var sb = new StringBuilder(name.Length);
        foreach (var part in parts)
        {
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part[1..]);
        }
        return sb.ToString();
    }
}

public enum GqlTokenKind
{
    Name,
    Spread,
    LBrace,
    RBrace,
    LParen,
    RParen,
    LBracket,
    RBracket,
    Colon,
    At,
    Bang,
    Equal,
    Dollar,
    Pipe,
    Amp
}

/// <summary>
/// Declarative Superpower-based GraphQL selection-set and SDL parser.
/// Extracts root fields from executable operations (supporting aliases, arguments, directives,
/// inline fragments, and multi-root-field queries) and SDL root types, as well as TypedDocumentNode JSON ASTs.
/// </summary>
public static class GraphQLDocumentExtractor
{
    private const char CommaChar = ',';
    private const string SpreadText = "...";
    private const string TemplateInterpolationPattern = @"\$\{[^{}]*\}";
    private const string BlockStringPattern = @"(?s)""""""[\s\S]*?""""""";
    private const string StringLiteralPattern = @"(?s)""(?:\\.|[^""\\])*""";
    private const string NamePattern = @"[A-Za-z0-9_-]+";
    private const string StrayCharPattern = @"[^\sA-Za-z0-9_\-\.\{\}\(\)\[\]:@!=\$\|&"",#]+";
    private const string IntrospectionPrefix = "__";
    private const string QueryKeyword = "query";
    private const string MutationKeyword = "mutation";
    private const string SubscriptionKeyword = "subscription";
    private const string TypeKeyword = "type";
    private const string ExtendKeyword = "extend";
    private const string OnKeyword = "on";
    private const string RootQueryType = "Query";
    private const string RootMutationType = "Mutation";
    private const string RootSubscriptionType = "Subscription";

    private static readonly (char Char, GqlTokenKind Kind)[] Punctuators =
    [
        ('{', GqlTokenKind.LBrace),
        ('}', GqlTokenKind.RBrace),
        ('(', GqlTokenKind.LParen),
        (')', GqlTokenKind.RParen),
        ('[', GqlTokenKind.LBracket),
        (']', GqlTokenKind.RBracket),
        (':', GqlTokenKind.Colon),
        ('@', GqlTokenKind.At),
        ('!', GqlTokenKind.Bang),
        ('=', GqlTokenKind.Equal),
        ('$', GqlTokenKind.Dollar),
        ('|', GqlTokenKind.Pipe),
        ('&', GqlTokenKind.Amp)
    ];

    public static Tokenizer<GqlTokenKind> TokenizerInstance { get; } = BuildTokenizer();

    private static Tokenizer<GqlTokenKind> BuildTokenizer()
    {
        var builder = new TokenizerBuilder<GqlTokenKind>()
            .Ignore(Span.WhiteSpace)
            .Ignore(Character.EqualTo(CommaChar))
            .Ignore(Comment.ShellStyle)
            .Ignore(Span.Regex(TemplateInterpolationPattern))
            .Ignore(Span.Regex(BlockStringPattern))
            .Ignore(Span.Regex(StringLiteralPattern))
            .Match(Span.EqualTo(SpreadText), GqlTokenKind.Spread);

        foreach (var (ch, kind) in Punctuators)
        {
            builder = builder.Match(Character.EqualTo(ch), kind);
        }

        return builder
            .Match(Span.Regex(NamePattern), GqlTokenKind.Name)
            .Ignore(Span.Regex(StrayCharPattern))
            .Build();
    }

    private static TokenListParser<GqlTokenKind, Token<GqlTokenKind>> NameToken { get; } =
        Token.EqualTo(GqlTokenKind.Name);

    private static TokenListParser<GqlTokenKind, Token<GqlTokenKind>> Keyword(string text) =>
        Token.EqualToValue(GqlTokenKind.Name, text);

    private static TokenListParser<GqlTokenKind, Token<GqlTokenKind>> Balanced(
        GqlTokenKind openKind,
        GqlTokenKind closeKind) =>
        input =>
        {
            var first = input.ConsumeToken();
            if (!first.HasValue || first.Value.Kind != openKind)
            {
                return TokenListParserResult.Empty<GqlTokenKind, Token<GqlTokenKind>>(input);
            }

            var depth = 1;
            var rem = first.Remainder;
            var lastTok = first.Value;

            while (!rem.IsAtEnd)
            {
                var next = rem.ConsumeToken();
                if (!next.HasValue) break;
                lastTok = next.Value;
                rem = next.Remainder;

                if (lastTok.Kind == openKind) depth++;
                else if (lastTok.Kind == closeKind && --depth == 0)
                {
                    return TokenListParserResult.Value(lastTok, input, rem);
                }
            }

            return TokenListParserResult.Value(lastTok, input, rem);
        };

    private static TokenListParser<GqlTokenKind, Token<GqlTokenKind>> Parens { get; } =
        Balanced(GqlTokenKind.LParen, GqlTokenKind.RParen);

    private static TokenListParser<GqlTokenKind, Token<GqlTokenKind>> Braces { get; } =
        Balanced(GqlTokenKind.LBrace, GqlTokenKind.RBrace);

    private static TokenListParser<GqlTokenKind, Unit> Directive { get; } =
        from at in Token.EqualTo(GqlTokenKind.At)
        from name in NameToken.Optional()
        from args in Parens.Optional()
        select Unit.Value;

    private static TokenListParser<GqlTokenKind, Unit[]> Directives { get; } =
        Directive.Many();

    private static TokenListParser<GqlTokenKind, IReadOnlyList<string>> InlineFragmentSelection { get; } =
        (from spread in Token.EqualTo(GqlTokenKind.Spread)
         from onClause in (
             from onKw in Keyword(OnKeyword)
             from typeName in NameToken.Optional()
             select Unit.Value
         ).Optional()
         from dirs in Directives
         from nested in Parse.Ref(() => RootSelectionSet!)
         select nested.Fields).Try();

    private static TokenListParser<GqlTokenKind, IReadOnlyList<string>> FragmentSpreadSelection { get; } =
        from spread in Token.EqualTo(GqlTokenKind.Spread)
        from fragName in NameToken.Optional()
        from dirs in Directives
        select (IReadOnlyList<string>)Array.Empty<string>();

    private static TokenListParser<GqlTokenKind, IReadOnlyList<string>> FieldSelection { get; } =
        from first in NameToken
        from aliased in (
            from colon in Token.EqualTo(GqlTokenKind.Colon)
            from actual in NameToken
            select actual
        ).Optional()
        from args in Parens.Optional()
        from dirs in Directives
        from subSelection in Braces.Optional()
        let fieldName = (aliased.HasValue ? aliased.Value : first).ToStringValue()
        select (IReadOnlyList<string>)(fieldName.StartsWith(IntrospectionPrefix, StringComparison.Ordinal)
            ? Array.Empty<string>()
            : [fieldName]);

    private static TokenListParser<GqlTokenKind, (IReadOnlyList<string> Fields, int StartLine, int EndLine)> RootSelectionSet { get; } =
        from open in Token.EqualTo(GqlTokenKind.LBrace)
        from items in InlineFragmentSelection
            .Or(FragmentSpreadSelection)
            .Or(FieldSelection)
            .Many()
        from close in Token.EqualTo(GqlTokenKind.RBrace)
        select (
            Fields: (IReadOnlyList<string>)items.SelectMany(x => x).Distinct(StringComparer.Ordinal).ToList(),
            StartLine: open.Position.Line,
            EndLine: close.Position.Line
        );

    private static TokenListParser<GqlTokenKind, GraphQLParsedOperation> ExecutableOperationDef { get; } =
        (from opTok in Keyword(QueryKeyword).Or(Keyword(MutationKeyword)).Or(Keyword(SubscriptionKeyword))
         from opNameTok in NameToken.Optional()
         from vars in Parens.Optional()
         from dirs in Directives
         from selSet in RootSelectionSet
         let opType = opTok.ToStringValue() switch
         {
             MutationKeyword => RootMutationType,
             SubscriptionKeyword => RootSubscriptionType,
             _ => RootQueryType
         }
         let opName = opNameTok.HasValue ? opNameTok.Value.ToStringValue() : null
         select new GraphQLParsedOperation(opType, opName, selSet.Fields, opTok.Position.Line, selSet.EndLine)).Try();

    private static TokenListParser<GqlTokenKind, GraphQLParsedOperation> ShorthandOperationDef { get; } =
        from selSet in RootSelectionSet
        select new GraphQLParsedOperation(RootQueryType, null, selSet.Fields, selSet.StartLine, selSet.EndLine);

    private static TokenListParser<GqlTokenKind, string> SdlTypeRef { get; } =
        from openBrackets in Token.EqualTo(GqlTokenKind.LBracket).Many()
        from baseType in NameToken
        from tail in Token.EqualTo(GqlTokenKind.Bang).Or(Token.EqualTo(GqlTokenKind.RBracket)).Many()
        select baseType.ToStringValue();

    private static TokenListParser<GqlTokenKind, (string FieldName, string? ReturnType, int Line)> SdlFieldDef { get; } =
        from fieldTok in NameToken
        from args in Parens.Optional()
        from returnType in (
            from colon in Token.EqualTo(GqlTokenKind.Colon)
            from typeName in SdlTypeRef
            select (string?)typeName
        ).OptionalOrDefault()
        from dirs in Directives
        select (fieldTok.ToStringValue(), returnType, fieldTok.Position.Line);

    private static TokenListParser<GqlTokenKind, IReadOnlyList<GraphQLParsedSdlField>> SdlRootTypeDef { get; } =
        (from ext in Keyword(ExtendKeyword).Optional()
         from typeKw in Keyword(TypeKeyword)
         from typeNameTok in NameToken
         where typeNameTok.ToStringValue() is RootQueryType or RootMutationType or RootSubscriptionType
         from header in Parens.Value(Unit.Value)
             .Or(NameToken.Value(Unit.Value))
             .Or(Token.EqualTo(GqlTokenKind.Amp).Value(Unit.Value))
             .Or(Directive)
             .Many()
         from open in Token.EqualTo(GqlTokenKind.LBrace)
         from fields in SdlFieldDef.Many()
         from close in Token.EqualTo(GqlTokenKind.RBrace)
         let opType = typeNameTok.ToStringValue()
         select (IReadOnlyList<GraphQLParsedSdlField>)fields
             .Where(f => !f.FieldName.StartsWith(IntrospectionPrefix, StringComparison.Ordinal))
             .Select(f => new GraphQLParsedSdlField(opType, f.FieldName, f.ReturnType, f.Line))
             .ToList()).Try();

    public static (List<GraphQLParsedOperation> Operations, List<GraphQLParsedSdlField> SdlFields) ParseDocument(string content)
    {
        var operations = new List<GraphQLParsedOperation>();
        var sdlFields = new List<GraphQLParsedSdlField>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return (operations, sdlFields);
        }

        var tokenized = TokenizerInstance.TryTokenize(content);
        if (!tokenized.HasValue)
        {
            return (operations, sdlFields);
        }

        var rem = tokenized.Value;
        var canStartShorthandQuery = true;

        while (!rem.IsAtEnd)
        {
            var opRes = ExecutableOperationDef(rem);
            if (opRes.HasValue)
            {
                if (opRes.Value.RootFields.Count > 0)
                {
                    operations.Add(opRes.Value);
                }
                rem = opRes.Remainder;
                canStartShorthandQuery = true;
                continue;
            }

            var sdlRes = SdlRootTypeDef(rem);
            if (sdlRes.HasValue)
            {
                sdlFields.AddRange(sdlRes.Value);
                rem = sdlRes.Remainder;
                canStartShorthandQuery = true;
                continue;
            }

            if (canStartShorthandQuery)
            {
                var shortRes = ShorthandOperationDef(rem);
                if (shortRes.HasValue)
                {
                    if (shortRes.Value.RootFields.Count > 0)
                    {
                        operations.Add(shortRes.Value);
                    }
                    rem = shortRes.Remainder;
                    canStartShorthandQuery = true;
                    continue;
                }
            }

            // Skip any other SDL block `{ ... }` (e.g. input, enum, interface, schema, fragment, non-root type)
            var braceRes = Braces(rem);
            if (braceRes.HasValue)
            {
                rem = braceRes.Remainder;
                canStartShorthandQuery = true;
                continue;
            }

            var consumed = rem.ConsumeToken();
            if (!consumed.HasValue) break;
            rem = consumed.Remainder;
            canStartShorthandQuery = false;
        }

        return (operations, sdlFields);
    }

    private static readonly Regex TypedDocOperationRegex = new(
        @"[""']?kind[""']?\s*:\s*[""']OperationDefinition[""'][\s\S]*?[""']?operation[""']?\s*:\s*[""'](query|mutation|subscription)[""'](?:[\s\S]*?[""']?name[""']?\s*:\s*\{\s*(?:[""']?kind[""']?\s*:\s*[""']Name[""']\s*,\s*)?[""']?value[""']?\s*:\s*[""']([A-Za-z0-9_]+)[""'])?",
        RegexOptions.Compiled);

    private static readonly Regex TypedDocFieldRegex = new(
        @"[""']?kind[""']?\s*:\s*[""']Field[""'][\s\S]*?[""']?name[""']?\s*:\s*\{\s*(?:[""']?kind[""']?\s*:\s*[""']Name[""']\s*,\s*)?[""']?value[""']?\s*:\s*[""']([A-Za-z0-9_]+)[""']",
        RegexOptions.Compiled);

    /// <summary>
    /// Extracts operation metadata from a compiled TypedDocumentNode / DocumentNode JSON AST literal
    /// (e.g. `{ kind: "Document", definitions: [{ kind: "OperationDefinition", operation: "query", ... }] }`).
    /// </summary>
    public static GraphQLParsedOperation? TryParseTypedDocumentAst(string objectLiteralText, int startLine = 1, int endLine = 1)
    {
        if (string.IsNullOrWhiteSpace(objectLiteralText) ||
            !objectLiteralText.Contains("OperationDefinition", StringComparison.Ordinal))
        {
            return null;
        }

        var opMatch = TypedDocOperationRegex.Match(objectLiteralText);
        if (!opMatch.Success)
        {
            return null;
        }

        var rawOp = opMatch.Groups[1].Value.ToLowerInvariant();
        var opType = rawOp switch
        {
            "mutation" => "Mutation",
            "subscription" => "Subscription",
            _ => "Query"
        };
        var opName = opMatch.Groups[2].Success ? opMatch.Groups[2].Value : null;

        // Extract top-level Field selections from the first SelectionSet
        var rootFields = new List<string>();
        var selSetIdx = objectLiteralText.IndexOf("selectionSet", opMatch.Index, StringComparison.Ordinal);
        if (selSetIdx >= 0)
        {
            var fieldMatch = TypedDocFieldRegex.Match(objectLiteralText, selSetIdx);
            if (fieldMatch.Success)
            {
                var fieldName = fieldMatch.Groups[1].Value;
                if (!string.IsNullOrEmpty(fieldName) && !fieldName.StartsWith("__", StringComparison.Ordinal))
                {
                    rootFields.Add(fieldName);
                }
            }
        }

        if (rootFields.Count == 0) return null;
        return new GraphQLParsedOperation(opType, opName, rootFields, startLine, endLine);
    }
}
