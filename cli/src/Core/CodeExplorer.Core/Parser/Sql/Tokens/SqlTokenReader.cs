using Superpower.Model;

namespace CodeExplorer.Core.Parser.Sql.Tokens;

public sealed class SqlTokenReader
{
    private const string DotSeparator = ".";

    private readonly Token<SqlToken>[] _tokens;
    private int _position;

    public SqlTokenReader(Token<SqlToken>[] tokens)
    {
        _tokens = tokens;
        _position = 0;
    }

    public SqlTokenReader(TokenList<SqlToken> tokens)
        : this(tokens.ToArray())
    {
    }

    public bool IsAtEnd => _position >= _tokens.Length;

    public int Position => _position;

    public Token<SqlToken> Current => _position < _tokens.Length ? _tokens[_position] : default;

    public bool CurrentEqualsIgnoreCase(string expected)
    {
        if (_position >= _tokens.Length) return false;
        var span = _tokens[_position].Span;
        return span.Source != null &&
               span.Source.AsSpan(span.Position.Absolute, span.Length).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    public void Advance()
    {
        if (_position < _tokens.Length) _position++;
    }

    public void Backtrack()
    {
        if (_position > 0) _position--;
    }

    public bool Match(SqlToken expected)
    {
        if (_position < _tokens.Length && _tokens[_position].Kind == expected)
        {
            _position++;
            return true;
        }
        return false;
    }

    public bool MatchAny(params ReadOnlySpan<SqlToken> expected)
    {
        if (_position < _tokens.Length)
        {
            var currentKind = _tokens[_position].Kind;
            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] == currentKind)
                {
                    _position++;
                    return true;
                }
            }
        }
        return false;
    }

    public string? ReadIdentifierOrString()
    {
        if (IsAtEnd) return null;

        var kind = Current.Kind;
        if (kind is SqlToken.Identifier or SqlToken.QuotedIdentifier or SqlToken.StringLiteral)
        {
            var val = Current.ToStringValue();
            Advance();
            return val;
        }

        // Keywords can also serve as identifiers in many dialects (e.g. "order", "group", "user")
        if (kind != SqlToken.Semicolon && kind != SqlToken.Comma && kind != SqlToken.Dot &&
            kind != SqlToken.LParen && kind != SqlToken.RParen)
        {
            var val = Current.ToStringValue();
            Advance();
            return val;
        }

        return null;
    }

    public string? ReadMultipartIdentifier()
    {
        var first = ReadIdentifierOrString();
        if (string.IsNullOrEmpty(first)) return null;

        var parts = new List<string> { first };

        while (Match(SqlToken.Dot))
        {
            var next = ReadIdentifierOrString();
            if (!string.IsNullOrEmpty(next))
            {
                parts.Add(next);
            }
            else
            {
                break;
            }
        }

        return string.Join(DotSeparator, parts);
    }

    public void SkipToMatchingRightParen()
    {
        var depth = 1;
        while (!IsAtEnd && depth > 0)
        {
            if (Current.Kind == SqlToken.LParen) depth++;
            else if (Current.Kind == SqlToken.RParen) depth--;
            Advance();
        }
    }

    public void SkipSemicolons()
    {
        while (!IsAtEnd && Current.Kind == SqlToken.Semicolon)
        {
            Advance();
        }
    }

    public void SkipToNextStatement()
    {
        while (!IsAtEnd && Current.Kind != SqlToken.Semicolon)
        {
            Advance();
        }
        SkipSemicolons();
    }
}
