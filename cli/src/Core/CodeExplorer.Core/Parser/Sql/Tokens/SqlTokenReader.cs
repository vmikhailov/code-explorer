using Superpower.Model;

namespace CodeExplorer.Core.Parser.Sql.Tokens;

public sealed class SqlTokenReader
{
    private readonly TokenList<SqlToken> _tokens;
    private int _position;

    public SqlTokenReader(TokenList<SqlToken> tokens)
    {
        _tokens = tokens;
        _position = 0;
    }

    public bool IsAtEnd => _position >= _tokens.Count();

    public Token<SqlToken> Current => !IsAtEnd ? _tokens.ElementAt(_position) : default;

    public void Advance()
    {
        if (!IsAtEnd) _position++;
    }

    public void Backtrack()
    {
        if (_position > 0) _position--;
    }

    public bool Match(SqlToken expected)
    {
        if (!IsAtEnd && Current.Kind == expected)
        {
            Advance();
            return true;
        }
        return false;
    }

    public bool MatchAny(params SqlToken[] expected)
    {
        if (!IsAtEnd && expected.Contains(Current.Kind))
        {
            Advance();
            return true;
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

        return string.Join(".", parts);
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
