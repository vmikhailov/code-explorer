using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace CodeExplorer.Cypher.Parser;

public static class CypherTokenizer
{
    public static IReadOnlyList<CypherToken> QueryKeywords { get; } =
    [
        CypherToken.Optional,
        CypherToken.Match,
        CypherToken.Where,
        CypherToken.Return,
        CypherToken.Distinct,
        CypherToken.As,
        CypherToken.Order,
        CypherToken.By,
        CypherToken.Asc,
        CypherToken.Desc,
        CypherToken.Skip,
        CypherToken.Limit,
        CypherToken.And,
        CypherToken.Or,
        CypherToken.Not,
        CypherToken.Starts,
        CypherToken.Ends,
        CypherToken.Contains,
        CypherToken.In,
        CypherToken.Is,
        CypherToken.Case,
        CypherToken.When,
        CypherToken.Then,
        CypherToken.Else,
        CypherToken.End,
        CypherToken.With,
        CypherToken.Unwind,
        CypherToken.Xor,
        CypherToken.Union,
        CypherToken.Call,
        CypherToken.Yield
    ];

    public static IReadOnlyList<CypherToken> MutatingKeywords { get; } =
    [
        CypherToken.Create,
        CypherToken.Delete,
        CypherToken.Set,
        CypherToken.Merge,
        CypherToken.Remove,
        CypherToken.Drop,
        CypherToken.Detach,
        CypherToken.Alter,
        CypherToken.Truncate
    ];

    public static IReadOnlyList<CypherToken> LiteralKeywords { get; } =
    [
        CypherToken.Null,
        CypherToken.True,
        CypherToken.False
    ];

    public static IReadOnlyList<CypherToken> IdentifierCompatibleKeywords { get; } =
        [.. QueryKeywords, .. MutatingKeywords];

    private static readonly (string Text, CypherToken Token)[] MultiCharSymbols =
    [
        ("->", CypherToken.ArrowRight),
        ("<-", CypherToken.ArrowLeft),
        ("<=", CypherToken.LessOrEqual),
        (">=", CypherToken.GreaterOrEqual),
        ("<>", CypherToken.NotEqual),
        ("!=", CypherToken.NotEqual),
        ("=~", CypherToken.RegexMatch),
        ("..", CypherToken.DotDot)
    ];

    private static readonly (char Char, CypherToken Token)[] SingleCharSymbols =
    [
        ('(', CypherToken.LParen),
        (')', CypherToken.RParen),
        ('[', CypherToken.LBracket),
        (']', CypherToken.RBracket),
        ('{', CypherToken.LBrace),
        ('}', CypherToken.RBrace),
        (':', CypherToken.Colon),
        (',', CypherToken.Comma),
        ('.', CypherToken.Dot),
        ('*', CypherToken.Asterisk),
        ('|', CypherToken.Pipe),
        ('-', CypherToken.Dash),
        ('=', CypherToken.Equal),
        ('<', CypherToken.LessThan),
        ('>', CypherToken.GreaterThan),
        ('+', CypherToken.Plus),
        ('/', CypherToken.Slash),
        ('%', CypherToken.Percent),
        ('^', CypherToken.Caret)
    ];

    private const string SingleLineCommentPattern = @"//.*";
    private const string ParameterPattern = @"\$[a-zA-Z_][a-zA-Z0-9_]*";
    private const char DollarChar = '$';
    private const string BacktickIdentPattern = @"`[^`]+`";
    private const string SingleQuotedStringPattern = @"(?s)'(?:[^'\\]|\\.|'')*'";
    private const string DoubleQuotedStringPattern = @"(?s)""(?:[^""\\]|\\.|"""")*""";
    private const string HexNumberPattern = @"0x[0-9a-fA-F]+";
    private const string DecimalNumberPattern = @"[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?";

    public static Tokenizer<CypherToken> Instance { get; } = BuildTokenizer();

    private static Tokenizer<CypherToken> BuildTokenizer()
    {
        var builder = new TokenizerBuilder<CypherToken>()
            .Ignore(Span.WhiteSpace)
            .Ignore(Span.Regex(SingleLineCommentPattern))
            .Ignore(Comment.CStyle);

        foreach (var (text, token) in MultiCharSymbols)
        {
            builder = builder.Match(Span.EqualTo(text), token);
        }

        foreach (var (ch, token) in SingleCharSymbols)
        {
            builder = builder.Match(Character.EqualTo(ch), token);
        }

        // Cypher parameter ($param) must be matched before single Dollar
        builder = builder
            .Match(Span.Regex(ParameterPattern), CypherToken.Parameter)
            .Match(Character.EqualTo(DollarChar), CypherToken.Dollar);

        // Keywords (case-insensitive with word boundary)
        foreach (var keyword in QueryKeywords.Concat(LiteralKeywords).Concat(MutatingKeywords))
        {
            builder = builder.Match(Span.Regex($"(?i)\\b{keyword}\\b"), keyword);
        }

        return builder
            // Backtick-quoted identifiers (e.g. `some-prop`)
            .Match(Span.Regex(BacktickIdentPattern), CypherToken.Identifier)
            // Standard identifiers
            .Match(Identifier.CStyle, CypherToken.Identifier)
            // String literals: single and double quotes with escaping (multiline, escaped quotes \' and '')
            .Match(Span.Regex(SingleQuotedStringPattern), CypherToken.StringLiteral)
            .Match(Span.Regex(DoubleQuotedStringPattern), CypherToken.StringLiteral)
            // Numbers: hexadecimal, exponential, decimal, or integer
            .Match(Span.Regex(HexNumberPattern), CypherToken.Number)
            .Match(Span.Regex(DecimalNumberPattern), CypherToken.Number)
            .Build();
    }
}
