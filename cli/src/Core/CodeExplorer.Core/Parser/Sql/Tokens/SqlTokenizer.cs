using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace CodeExplorer.Core.Parser.Sql.Tokens;

public static class SqlTokenizer
{
    private const char QuestionMarkChar = '?';

    private static readonly (string Text, SqlToken Token)[] MultiCharSymbols =
    [
        ("::", SqlToken.DoubleColon),
        ("<=", SqlToken.LessOrEqual),
        (">=", SqlToken.GreaterOrEqual),
        ("<>", SqlToken.NotEquals),
        ("!=", SqlToken.NotEquals)
    ];

    private static readonly (char Char, SqlToken Token)[] SingleCharSymbols =
    [
        ('(', SqlToken.LParen),
        (')', SqlToken.RParen),
        ('[', SqlToken.LBracket),
        (']', SqlToken.RBracket),
        ('{', SqlToken.LBrace),
        ('}', SqlToken.RBrace),
        (',', SqlToken.Comma),
        (';', SqlToken.Semicolon),
        ('.', SqlToken.Dot),
        (':', SqlToken.Colon),
        ('+', SqlToken.Plus),
        ('-', SqlToken.Minus),
        ('*', SqlToken.Asterisk),
        ('/', SqlToken.Slash),
        ('%', SqlToken.Percent),
        ('=', SqlToken.Equals),
        ('<', SqlToken.LessThan),
        ('>', SqlToken.GreaterThan)
    ];

    private static readonly (string Keyword, SqlToken Token)[] Keywords =
    [
        ("SELECT", SqlToken.Select),
        ("FROM", SqlToken.From),
        ("WHERE", SqlToken.Where),
        ("JOIN", SqlToken.Join),
        ("INNER", SqlToken.Inner),
        ("LEFT", SqlToken.Left),
        ("RIGHT", SqlToken.Right),
        ("FULL", SqlToken.Full),
        ("CROSS", SqlToken.Cross),
        ("OUTER", SqlToken.Outer),
        ("NATURAL", SqlToken.Natural),
        ("STRAIGHT_JOIN", SqlToken.StraightJoin),
        ("ON", SqlToken.On),
        ("USING", SqlToken.Using),
        ("AS", SqlToken.As),
        ("DISTINCT", SqlToken.Distinct),
        ("ALL", SqlToken.All),
        ("INSERT", SqlToken.Insert),
        ("INTO", SqlToken.Into),
        ("VALUES", SqlToken.Values),
        ("UPDATE", SqlToken.Update),
        ("SET", SqlToken.Set),
        ("DELETE", SqlToken.Delete),
        ("MERGE", SqlToken.Merge),
        ("MATCHED", SqlToken.Matched),
        ("RETURNING", SqlToken.Returning),
        ("CREATE", SqlToken.Create),
        ("ALTER", SqlToken.Alter),
        ("DROP", SqlToken.Drop),
        ("TRUNCATE", SqlToken.Truncate),
        ("REPLACE", SqlToken.Replace),
        ("TABLE", SqlToken.Table),
        ("VIEW", SqlToken.View),
        ("PROCEDURE", SqlToken.Procedure),
        ("PROC", SqlToken.Proc),
        ("FUNCTION", SqlToken.Function),
        ("DATABASE", SqlToken.Database),
        ("SCHEMA", SqlToken.Schema),
        ("INDEX", SqlToken.Index),
        ("TRIGGER", SqlToken.Trigger),
        ("TEMPORARY", SqlToken.Temporary),
        ("TEMP", SqlToken.Temp),
        ("EXTERNAL", SqlToken.External),
        ("MATERIALIZED", SqlToken.Materialized),
        ("IF", SqlToken.If),
        ("NOT", SqlToken.Not),
        ("EXISTS", SqlToken.Exists),
        ("ONLY", SqlToken.Only),
        ("FINAL", SqlToken.Final),
        ("SAMPLE", SqlToken.Sample),
        ("WITH", SqlToken.With),
        ("RECURSIVE", SqlToken.Recursive),
        ("EXEC", SqlToken.Exec),
        ("EXECUTE", SqlToken.Execute),
        ("CALL", SqlToken.Call),
        ("ORDER", SqlToken.Order),
        ("BY", SqlToken.By),
        ("GROUP", SqlToken.Group),
        ("HAVING", SqlToken.Having),
        ("LIMIT", SqlToken.Limit),
        ("OFFSET", SqlToken.Offset),
        ("UNION", SqlToken.Union),
        ("INTERSECT", SqlToken.Intersect),
        ("EXCEPT", SqlToken.Except),
        ("CASE", SqlToken.Case),
        ("WHEN", SqlToken.When),
        ("THEN", SqlToken.Then),
        ("ELSE", SqlToken.Else),
        ("END", SqlToken.End),
        ("AND", SqlToken.And),
        ("OR", SqlToken.Or),
        ("LIKE", SqlToken.Like),
        ("BETWEEN", SqlToken.Between),
        ("IN", SqlToken.In),
        ("IS", SqlToken.Is),
        ("NULL", SqlToken.Null),
        ("TRUE", SqlToken.True),
        ("FALSE", SqlToken.False),
        ("PRIMARY", SqlToken.Primary),
        ("KEY", SqlToken.Key),
        ("FOREIGN", SqlToken.Foreign),
        ("REFERENCES", SqlToken.References),
        ("CONSTRAINT", SqlToken.Constraint),
        ("DEFAULT", SqlToken.Default),
        ("CHECK", SqlToken.Check),
        ("UNIQUE", SqlToken.Unique)
    ];

    public static Tokenizer<SqlToken> Instance { get; } = BuildTokenizer();

    private static Tokenizer<SqlToken> BuildTokenizer()
    {
        var builder = new TokenizerBuilder<SqlToken>()
            .Ignore(Span.WhiteSpace);

        // 1. Strings FIRST so comment markers (-- or /*) inside strings are NEVER matched as comments
        // PostgreSQL dollar-quoted strings: $tag$...$tag$ or $$...$$
        builder = builder.Match(Span.Regex(@"(?s)(?:\$\$[\s\S]*?\$\$|\$([a-zA-Z_][a-zA-Z0-9_]*)\$[\s\S]*?\$\1\$)"), SqlToken.StringLiteral);
        // Single-quoted strings (standard ANSI / PostgreSQL / MySQL)
        builder = builder.Match(Span.Regex(@"(?s)'(?:[^'\\]|\\.|'')*'"), SqlToken.StringLiteral);
        // E-strings (PostgreSQL E'...')
        builder = builder.Match(Span.Regex(@"(?s)[eE]'(?:[^'\\]|\\.|'')*'"), SqlToken.StringLiteral);

        // 2. Comments AFTER string literals
        builder = builder
            .Ignore(Span.Regex(@"--[^\r\n]*"))
            .Ignore(Comment.CStyle);

        // 3. Multi-character symbols
        foreach (var (text, token) in MultiCharSymbols)
        {
            builder = builder.Match(Span.EqualTo(text), token);
        }

        // 4. Placeholders (@param, :param, $1, ?)
        builder = builder
            .Match(Span.Regex(@"@[a-zA-Z0-9_]+"), SqlToken.ParameterPlaceholder)
            .Match(Span.Regex(@":[a-zA-Z0-9_]+"), SqlToken.ParameterPlaceholder)
            .Match(Span.Regex(@"\$[0-9]+"), SqlToken.ParameterPlaceholder)
            .Match(Character.EqualTo(QuestionMarkChar), SqlToken.ParameterPlaceholder);

        // 5. Quoted Identifiers (MUST be before SingleCharSymbols to prevent '[' or '{' capturing brackets)
        // T-SQL bracket quotes: [schema].[table]
        builder = builder.Match(Span.Regex(@"\[[^\]]+\]"), SqlToken.QuotedIdentifier);
        // MySQL / BigQuery backtick quotes: `table`
        builder = builder.Match(Span.Regex(@"`[^`]+`"), SqlToken.QuotedIdentifier);
        // Double-quoted ANSI identifiers: "table"
        builder = builder.Match(Span.Regex(@"(?s)""(?:[^""\\]|\\.|"""")*"""), SqlToken.QuotedIdentifier);
        // Templated identifiers: ${var}, {{var}}, {var}
        builder = builder.Match(Span.Regex(@"(?:\$|\#)?(?:\{\{[a-zA-Z0-9_\.]+\}\}|\{[a-zA-Z0-9_\.]+\})"), SqlToken.QuotedIdentifier);

        // 6. Single-character symbols (AFTER quoted identifiers)
        foreach (var (ch, token) in SingleCharSymbols)
        {
            builder = builder.Match(Character.EqualTo(ch), token);
        }

        // 7. Keywords
        foreach (var (kw, token) in Keywords)
        {
            builder = builder.Match(Span.Regex($"(?i)\\b{kw}\\b"), token);
        }

        // 8. General Identifiers (including T-SQL temp tables starting with # or ##)
        builder = builder
            .Match(Span.Regex(@"#{1,2}[a-zA-Z_][a-zA-Z0-9_]*"), SqlToken.Identifier)
            .Match(Span.Regex(@"[a-zA-Z_][a-zA-Z0-9_]*"), SqlToken.Identifier);

        // 9. Numbers
        builder = builder
            .Match(Span.Regex(@"0x[0-9a-fA-F]+"), SqlToken.Number)
            .Match(Span.Regex(@"[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?"), SqlToken.Number);

        return builder.Build();
    }
}
