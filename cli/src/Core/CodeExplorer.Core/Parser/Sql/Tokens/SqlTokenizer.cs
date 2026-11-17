using System.Collections.Frozen;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace CodeExplorer.Core.Parser.Sql.Tokens;

public static class SqlTokenizer
{
    private const char QuestionMarkChar = '?';
    private const char SingleQuoteChar = '\'';
    private const char DoubleQuoteChar = '"';
    private const string DoubleDollarDelimiter = "$$";
    private const string BlockCommentEnd = "*/";

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

    private static readonly FrozenDictionary<string, SqlToken> KeywordMap =
        Keywords.ToFrozenDictionary(static x => x.Keyword, static x => x.Token, StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, SqlToken>.AlternateLookup<ReadOnlySpan<char>> KeywordSpanLookup =
        KeywordMap.GetAlternateLookup<ReadOnlySpan<char>>();

    public static Tokenizer<SqlToken> Instance { get; } = BuildTokenizer();

    public static bool TryTokenizeFast(string sqlText, out Token<SqlToken>[] tokens)
    {
        var span = sqlText.AsSpan();
        var length = span.Length;
        var list = new List<Token<SqlToken>>(Math.Max(16, length / 6));
        var pos = 0;

        while (pos < length)
        {
            var ch = span[pos];
            if (char.IsWhiteSpace(ch))
            {
                pos++;
                continue;
            }

            if (TryScanStringOrComment(span, ref pos, out var strOrCommentMatched, out var strToken, sqlText))
            {
                if (strOrCommentMatched)
                {
                    list.Add(strToken);
                }
                continue;
            }

            if (strOrCommentMatched)
            {
                tokens = [];
                return false;
            }

            if (TryScanNextToken(sqlText, span, ref pos, out var token))
            {
                list.Add(token);
                continue;
            }

            tokens = [];
            return false;
        }

        tokens = list.ToArray();
        return true;
    }

    private static Token<SqlToken> CreateToken(string source, int start, int tokenLength, SqlToken kind) =>
        new(kind, new TextSpan(source, new Position(start, 1, 1), tokenLength));

    private static bool TryScanStringOrComment(
        ReadOnlySpan<char> span,
        ref int pos,
        out bool hasTokenOrFailure,
        out Token<SqlToken> token,
        string source)
    {
        token = default;
        hasTokenOrFailure = false;
        var ch = span[pos];

        if (ch == '$' && TryScanDollarQuotedString(span, pos, out var dollarEnd))
        {
            token = CreateToken(source, pos, dollarEnd - pos, SqlToken.StringLiteral);
            pos = dollarEnd;
            hasTokenOrFailure = true;
            return true;
        }

        if (ch == SingleQuoteChar || ((ch == 'e' || ch == 'E') && pos + 1 < span.Length && span[pos + 1] == SingleQuoteChar))
        {
            var quoteStart = ch == SingleQuoteChar ? pos : pos + 1;
            if (!TryScanQuotedLiteral(span, quoteStart, SingleQuoteChar, out var strEnd))
            {
                hasTokenOrFailure = true;
                return false;
            }

            token = CreateToken(source, pos, strEnd - pos, SqlToken.StringLiteral);
            pos = strEnd;
            hasTokenOrFailure = true;
            return true;
        }

        if (ch == '-' && pos + 1 < span.Length && span[pos + 1] == '-')
        {
            pos += 2;
            while (pos < span.Length && span[pos] != '\r' && span[pos] != '\n') pos++;
            return true;
        }

        if (ch == '/' && pos + 1 < span.Length && span[pos + 1] == '*')
        {
            var closeIdx = span[(pos + 2)..].IndexOf(BlockCommentEnd.AsSpan(), StringComparison.Ordinal);
            if (closeIdx < 0)
            {
                hasTokenOrFailure = true;
                return false;
            }

            pos += 2 + closeIdx + 2;
            return true;
        }

        return false;
    }

    private static bool TryScanDollarQuotedString(ReadOnlySpan<char> span, int start, out int end)
    {
        end = start;
        if (start + 1 >= span.Length) return false;

        if (span[start + 1] == '$')
        {
            var closeIdx = span[(start + 2)..].IndexOf(DoubleDollarDelimiter.AsSpan(), StringComparison.Ordinal);
            if (closeIdx < 0) return false;
            end = start + 2 + closeIdx + 2;
            return true;
        }

        if (!IsAsciiIdentStart(span[start + 1])) return false;
        var tagCursor = start + 2;
        while (tagCursor < span.Length && IsAsciiIdentPart(span[tagCursor])) tagCursor++;
        if (tagCursor >= span.Length || span[tagCursor] != '$') return false;

        var tagSpan = span[start..(tagCursor + 1)];
        var tagCloseIdx = span[(tagCursor + 1)..].IndexOf(tagSpan, StringComparison.Ordinal);
        if (tagCloseIdx < 0) return false;
        end = tagCursor + 1 + tagCloseIdx + tagSpan.Length;
        return true;
    }

    private static bool TryScanQuotedLiteral(ReadOnlySpan<char> span, int quotePos, char quoteChar, out int end)
    {
        var i = quotePos + 1;
        while (i < span.Length)
        {
            var c = span[i];
            if (c == '\\')
            {
                if (i + 1 >= span.Length)
                {
                    end = i;
                    return false;
                }
                i += 2;
                continue;
            }

            if (c == quoteChar)
            {
                if (i + 1 < span.Length && span[i + 1] == quoteChar)
                {
                    i += 2;
                    continue;
                }

                end = i + 1;
                return true;
            }

            i++;
        }

        end = span.Length;
        return false;
    }

    private static bool TryScanNextToken(string source, ReadOnlySpan<char> span, ref int pos, out Token<SqlToken> token)
    {
        if (TryScanOperatorOrPlaceholder(source, span, ref pos, out token) ||
            TryScanQuotedOrSingleSymbol(source, span, ref pos, out token) ||
            TryScanIdentifierOrNumber(source, span, ref pos, out token))
        {
            return true;
        }

        token = default;
        return false;
    }

    private static bool TryScanOperatorOrPlaceholder(string source, ReadOnlySpan<char> span, ref int pos, out Token<SqlToken> token)
    {
        var ch = span[pos];
        if (pos + 1 < span.Length)
        {
            var next = span[pos + 1];
            var twoChar = (ch, next) switch
            {
                (':', ':') => SqlToken.DoubleColon,
                ('<', '=') => SqlToken.LessOrEqual,
                ('>', '=') => SqlToken.GreaterOrEqual,
                ('<', '>') => SqlToken.NotEquals,
                ('!', '=') => SqlToken.NotEquals,
                _ => SqlToken.None
            };
            if (twoChar != SqlToken.None)
            {
                token = CreateToken(source, pos, 2, twoChar);
                pos += 2;
                return true;
            }
        }

        if ((ch == '@' || ch == ':') && pos + 1 < span.Length && IsAsciiIdentPart(span[pos + 1]))
        {
            var end = pos + 2;
            while (end < span.Length && IsAsciiIdentPart(span[end])) end++;
            token = CreateToken(source, pos, end - pos, SqlToken.ParameterPlaceholder);
            pos = end;
            return true;
        }

        if (ch == '$' && pos + 1 < span.Length && char.IsAsciiDigit(span[pos + 1]))
        {
            var end = pos + 2;
            while (end < span.Length && char.IsAsciiDigit(span[end])) end++;
            token = CreateToken(source, pos, end - pos, SqlToken.ParameterPlaceholder);
            pos = end;
            return true;
        }

        if (ch == QuestionMarkChar)
        {
            token = CreateToken(source, pos, 1, SqlToken.ParameterPlaceholder);
            pos++;
            return true;
        }

        token = default;
        return false;
    }

    private static bool TryScanQuotedOrSingleSymbol(string source, ReadOnlySpan<char> span, ref int pos, out Token<SqlToken> token)
    {
        var ch = span[pos];
        if (ch == '[' && pos + 1 < span.Length && span[pos + 1] != ']')
        {
            var closeIdx = span[(pos + 1)..].IndexOf(']');
            if (closeIdx > 0)
            {
                var len = closeIdx + 2;
                token = CreateToken(source, pos, len, SqlToken.QuotedIdentifier);
                pos += len;
                return true;
            }
        }

        if (ch == '`' && pos + 1 < span.Length && span[pos + 1] != '`')
        {
            var closeIdx = span[(pos + 1)..].IndexOf('`');
            if (closeIdx > 0)
            {
                var len = closeIdx + 2;
                token = CreateToken(source, pos, len, SqlToken.QuotedIdentifier);
                pos += len;
                return true;
            }
        }

        if (ch == DoubleQuoteChar)
        {
            if (!TryScanQuotedLiteral(span, pos, DoubleQuoteChar, out var dblEnd))
            {
                token = default;
                return false;
            }

            token = CreateToken(source, pos, dblEnd - pos, SqlToken.QuotedIdentifier);
            pos = dblEnd;
            return true;
        }

        if ((ch == '$' || ch == '#' || ch == '{') && TryScanTemplatedIdentifier(span, pos, out var tplEnd))
        {
            token = CreateToken(source, pos, tplEnd - pos, SqlToken.QuotedIdentifier);
            pos = tplEnd;
            return true;
        }

        var single = MapSingleCharSymbol(ch);
        if (single != SqlToken.None)
        {
            token = CreateToken(source, pos, 1, single);
            pos++;
            return true;
        }

        token = default;
        return false;
    }

    private static bool TryScanTemplatedIdentifier(ReadOnlySpan<char> span, int start, out int end)
    {
        end = start;
        var cursor = start;
        if (span[cursor] is '$' or '#') cursor++;
        if (cursor >= span.Length || span[cursor] != '{') return false;

        var doubleBrace = cursor + 1 < span.Length && span[cursor + 1] == '{';
        cursor += doubleBrace ? 2 : 1;
        var bodyStart = cursor;
        while (cursor < span.Length && (IsAsciiIdentPart(span[cursor]) || span[cursor] == '.')) cursor++;
        if (cursor == bodyStart) return false;

        if (doubleBrace)
        {
            if (cursor + 1 >= span.Length || span[cursor] != '}' || span[cursor + 1] != '}') return false;
            end = cursor + 2;
            return true;
        }

        if (cursor >= span.Length || span[cursor] != '}') return false;
        end = cursor + 1;
        return true;
    }

    private static SqlToken MapSingleCharSymbol(char ch) => ch switch
    {
        '(' => SqlToken.LParen,
        ')' => SqlToken.RParen,
        '[' => SqlToken.LBracket,
        ']' => SqlToken.RBracket,
        '{' => SqlToken.LBrace,
        '}' => SqlToken.RBrace,
        ',' => SqlToken.Comma,
        ';' => SqlToken.Semicolon,
        '.' => SqlToken.Dot,
        ':' => SqlToken.Colon,
        '+' => SqlToken.Plus,
        '-' => SqlToken.Minus,
        '*' => SqlToken.Asterisk,
        '/' => SqlToken.Slash,
        '%' => SqlToken.Percent,
        '=' => SqlToken.Equals,
        '<' => SqlToken.LessThan,
        '>' => SqlToken.GreaterThan,
        _ => SqlToken.None
    };

    private static bool TryScanIdentifierOrNumber(string source, ReadOnlySpan<char> span, ref int pos, out Token<SqlToken> token)
    {
        var ch = span[pos];
        if (ch == '#')
        {
            var hashEnd = pos + 1 < span.Length && span[pos + 1] == '#' ? pos + 2 : pos + 1;
            if (hashEnd < span.Length && IsAsciiIdentStart(span[hashEnd]))
            {
                var end = hashEnd + 1;
                while (end < span.Length && IsAsciiIdentPart(span[end])) end++;
                token = CreateToken(source, pos, end - pos, SqlToken.Identifier);
                pos = end;
                return true;
            }
        }

        if (IsAsciiIdentStart(ch))
        {
            var end = pos + 1;
            while (end < span.Length && IsAsciiIdentPart(span[end])) end++;
            if (span[end - 1] == '_' && end < span.Length && span[end] == '*')
            {
                end++;
            }
            var word = span[pos..end];
            var kind = KeywordSpanLookup.TryGetValue(word, out var kw) ? kw : SqlToken.Identifier;
            token = CreateToken(source, pos, end - pos, kind);
            pos = end;
            return true;
        }

        if (char.IsAsciiDigit(ch))
        {
            var end = ScanNumber(span, pos);
            token = CreateToken(source, pos, end - pos, SqlToken.Number);
            pos = end;
            return true;
        }

        token = default;
        return false;
    }

    private static int ScanNumber(ReadOnlySpan<char> span, int start)
    {
        if (span[start] == '0' && start + 2 < span.Length &&
            (span[start + 1] == 'x' || span[start + 1] == 'X') &&
            Uri.IsHexDigit(span[start + 2]))
        {
            var hexEnd = start + 3;
            while (hexEnd < span.Length && Uri.IsHexDigit(span[hexEnd])) hexEnd++;
            return hexEnd;
        }

        var end = start + 1;
        while (end < span.Length && char.IsAsciiDigit(span[end])) end++;
        if (end + 1 < span.Length && span[end] == '.' && char.IsAsciiDigit(span[end + 1]))
        {
            end += 2;
            while (end < span.Length && char.IsAsciiDigit(span[end])) end++;
        }

        if (end < span.Length && (span[end] == 'e' || span[end] == 'E'))
        {
            var exp = end + 1;
            if (exp < span.Length && (span[exp] == '+' || span[exp] == '-')) exp++;
            if (exp < span.Length && char.IsAsciiDigit(span[exp]))
            {
                end = exp + 1;
                while (end < span.Length && char.IsAsciiDigit(span[end])) end++;
            }
        }

        return end;
    }

    private static bool IsAsciiIdentStart(char ch) => char.IsAsciiLetter(ch) || ch == '_';

    private static bool IsAsciiIdentPart(char ch) => char.IsAsciiLetterOrDigit(ch) || ch == '_';

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
