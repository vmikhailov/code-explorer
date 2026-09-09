using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace CodeExplorer.Cypher.Parser;

public static class CypherTokenizer
{
    public static Tokenizer<CypherToken> Instance { get; } =
        new TokenizerBuilder<CypherToken>()
            // Whitespace and comments
            .Ignore(Span.WhiteSpace)
            .Ignore(Span.Regex(@"//.*"))
            .Ignore(Comment.CStyle)

            // Multi-char symbols and operators first (longest match)
            .Match(Span.EqualTo("->"), CypherToken.ArrowRight)
            .Match(Span.EqualTo("<-"), CypherToken.ArrowLeft)
            .Match(Span.EqualTo("<="), CypherToken.LessOrEqual)
            .Match(Span.EqualTo(">="), CypherToken.GreaterOrEqual)
            .Match(Span.EqualTo("<>"), CypherToken.NotEqual)
            .Match(Span.EqualTo("!="), CypherToken.NotEqual)
            .Match(Span.EqualTo(".."), CypherToken.DotDot)

            // Single-char symbols
            .Match(Character.EqualTo('('), CypherToken.LParen)
            .Match(Character.EqualTo(')'), CypherToken.RParen)
            .Match(Character.EqualTo('['), CypherToken.LBracket)
            .Match(Character.EqualTo(']'), CypherToken.RBracket)
            .Match(Character.EqualTo('{'), CypherToken.LBrace)
            .Match(Character.EqualTo('}'), CypherToken.RBrace)
            .Match(Character.EqualTo(':'), CypherToken.Colon)
            .Match(Character.EqualTo(','), CypherToken.Comma)
            .Match(Character.EqualTo('.'), CypherToken.Dot)
            .Match(Character.EqualTo('*'), CypherToken.Asterisk)
            .Match(Character.EqualTo('|'), CypherToken.Pipe)
            .Match(Character.EqualTo('-'), CypherToken.Dash)
            .Match(Character.EqualTo('='), CypherToken.Equal)
            .Match(Character.EqualTo('<'), CypherToken.LessThan)
            .Match(Character.EqualTo('>'), CypherToken.GreaterThan)
            .Match(Character.EqualTo('+'), CypherToken.Plus)

            // Cypher parameter ($param) must be matched before single Dollar
            .Match(Span.Regex(@"\$[a-zA-Z_][a-zA-Z0-9_]*"), CypherToken.Parameter)
            .Match(Character.EqualTo('$'), CypherToken.Dollar)

            // Keywords (Case-insensitive with word boundary)
            .Match(Span.Regex("(?i)\\bOPTIONAL\\b"), CypherToken.Optional)
            .Match(Span.Regex("(?i)\\bMATCH\\b"), CypherToken.Match)
            .Match(Span.Regex("(?i)\\bWHERE\\b"), CypherToken.Where)
            .Match(Span.Regex("(?i)\\bRETURN\\b"), CypherToken.Return)
            .Match(Span.Regex("(?i)\\bDISTINCT\\b"), CypherToken.Distinct)
            .Match(Span.Regex("(?i)\\bAS\\b"), CypherToken.As)
            .Match(Span.Regex("(?i)\\bORDER\\b"), CypherToken.Order)
            .Match(Span.Regex("(?i)\\bBY\\b"), CypherToken.By)
            .Match(Span.Regex("(?i)\\bASC\\b"), CypherToken.Asc)
            .Match(Span.Regex("(?i)\\bDESC\\b"), CypherToken.Desc)
            .Match(Span.Regex("(?i)\\bSKIP\\b"), CypherToken.Skip)
            .Match(Span.Regex("(?i)\\bLIMIT\\b"), CypherToken.Limit)
            .Match(Span.Regex("(?i)\\bAND\\b"), CypherToken.And)
            .Match(Span.Regex("(?i)\\bOR\\b"), CypherToken.Or)
            .Match(Span.Regex("(?i)\\bNOT\\b"), CypherToken.Not)
            .Match(Span.Regex("(?i)\\bSTARTS\\b"), CypherToken.Starts)
            .Match(Span.Regex("(?i)\\bENDS\\b"), CypherToken.Ends)
            .Match(Span.Regex("(?i)\\bCONTAINS\\b"), CypherToken.Contains)
            .Match(Span.Regex("(?i)\\bIN\\b"), CypherToken.In)
            .Match(Span.Regex("(?i)\\bIS\\b"), CypherToken.Is)
            .Match(Span.Regex("(?i)\\bNULL\\b"), CypherToken.Null)
            .Match(Span.Regex("(?i)\\bTRUE\\b"), CypherToken.True)
            .Match(Span.Regex("(?i)\\bFALSE\\b"), CypherToken.False)
            .Match(Span.Regex("(?i)\\bCASE\\b"), CypherToken.Case)
            .Match(Span.Regex("(?i)\\bWHEN\\b"), CypherToken.When)
            .Match(Span.Regex("(?i)\\bTHEN\\b"), CypherToken.Then)
            .Match(Span.Regex("(?i)\\bELSE\\b"), CypherToken.Else)
            .Match(Span.Regex("(?i)\\bEND\\b"), CypherToken.End)
            .Match(Span.Regex("(?i)\\bWITH\\b"), CypherToken.With)
            .Match(Span.Regex("(?i)\\bUNWIND\\b"), CypherToken.Unwind)

            // Backtick-quoted identifiers (e.g. `some-prop`)
            .Match(Span.Regex(@"`[^`]+`"), CypherToken.Identifier)

            // Identifiers
            .Match(Identifier.CStyle, CypherToken.Identifier)

            // String literals: single and double quotes with escaping
            .Match(Span.Regex(@"'([^'\\]|\\.)*'"), CypherToken.StringLiteral)
            .Match(Span.Regex(@"""([^""\\]|\\.)*"""), CypherToken.StringLiteral)

            // Numbers: integer or decimal
            .Match(Span.Regex(@"[0-9]+(\.[0-9]+)?"), CypherToken.Number)

            .Build();
}
