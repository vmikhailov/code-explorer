using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// Superpower-based HCL2 tokenizer supporting line/block comments, heredocs (&lt;&lt;EOF / &lt;&lt;-EOF),
/// quoted strings with nested "${...}" interpolations, numbers, identifiers, and operators.
/// </summary>
public static class HclTokenizer
{
    private const string FatArrowText = "=>";
    private const string EllipsisText = "...";
    private const string HeredocIndentedPrefix = "<<-";
    private const string HeredocStandardPrefix = "<<";
    private const string LineCommentSlash = "//";
    private const string BlockCommentStart = "/*";
    private const string BlockCommentEnd = "*/";
    private const string OpEqText = "==";
    private const string OpNeqText = "!=";
    private const string OpLteText = "<=";
    private const string OpGteText = ">=";
    private const string OpAndText = "&&";
    private const string OpOrText = "||";
    private const string EscapedQuote = "\\\"";
    private const string QuoteStr = "\"";
    private const string EscapedBackslash = "\\\\";
    private const string BackslashStr = "\\";

    private const char HashChar = '#';
    private const char CrChar = '\r';
    private const char LfChar = '\n';
    private const char DotChar = '.';
    private const char ExpLowerChar = 'e';
    private const char ExpUpperChar = 'E';
    private const char PlusChar = '+';
    private const char MinusChar = '-';
    private const char UnderscoreChar = '_';
    private const char StarChar = '*';
    private const char SlashChar = '/';
    private const char PercentChar = '%';
    private const char BangChar = '!';
    private const char LtChar = '<';
    private const char GtChar = '>';
    private const char LBraceChar = '{';
    private const char RBraceChar = '}';
    private const char LBracketChar = '[';
    private const char RBracketChar = ']';
    private const char LParenChar = '(';
    private const char RParenChar = ')';
    private const char EqualsChar = '=';
    private const char CommaChar = ',';
    private const char ColonChar = ':';
    private const char QuestionChar = '?';

    private static readonly TextParser<TextSpan> HashLineComment =
        from hash in Character.EqualTo(HashChar)
        from rest in Character.ExceptIn(CrChar, LfChar).Many()
        select TextSpan.Empty;

    private static readonly TextParser<TextSpan> SlashLineComment =
        from slashes in Span.EqualTo(LineCommentSlash)
        from rest in Character.ExceptIn(CrChar, LfChar).Many()
        select TextSpan.Empty;

    private static readonly TextParser<TextSpan> CBlockComment =
        Comment.CStyle;

    private static readonly TextParser<Unit> QuotedStringToken = input =>
    {
        var next = input.ConsumeChar();
        if (!next.HasValue || next.Value != '"')
        {
            return Result.Empty<Unit>(input);
        }

        var remainder = next.Remainder;
        var interpBraceDepth = 0;

        while (!remainder.IsAtEnd)
        {
            var step = remainder.ConsumeChar();
            var ch = step.Value;
            remainder = step.Remainder;

            if (ch == '\\')
            {
                if (!remainder.IsAtEnd)
                {
                    remainder = remainder.ConsumeChar().Remainder;
                }
                continue;
            }

            if (ch == '$' && !remainder.IsAtEnd)
            {
                var peek = remainder.ConsumeChar();
                if (peek.Value == '{')
                {
                    interpBraceDepth++;
                    remainder = peek.Remainder;
                    continue;
                }
            }

            if (interpBraceDepth > 0)
            {
                if (ch == '{')
                {
                    interpBraceDepth++;
                }
                else if (ch == '}')
                {
                    interpBraceDepth--;
                }
                else if (ch == '"')
                {
                    remainder = ConsumeInnerQuotedString(remainder);
                }
                continue;
            }

            if (ch == '"')
            {
                return Result.Value(Unit.Value, input, remainder);
            }
        }

        return Result.Value(Unit.Value, input, remainder);
    };

    private static TextSpan ConsumeInnerQuotedString(TextSpan remainder)
    {
        while (!remainder.IsAtEnd)
        {
            var step = remainder.ConsumeChar();
            remainder = step.Remainder;
            if (step.Value == '\\' && !remainder.IsAtEnd)
            {
                remainder = remainder.ConsumeChar().Remainder;
            }
            else if (step.Value == '"')
            {
                break;
            }
        }
        return remainder;
    }

    private static readonly TextParser<Unit> HeredocToken = input =>
    {
        var source = input.Source;
        if (source == null)
        {
            return Result.Empty<Unit>(input);
        }

        var startOffset = input.Position.Absolute;
        var remainingLen = input.Length;
        if (remainingLen < 3 || source[startOffset] != '<' || source[startOffset + 1] != '<')
        {
            return Result.Empty<Unit>(input);
        }

        var cursor = startOffset + 2;
        if (cursor < startOffset + remainingLen && source[cursor] == '-')
        {
            cursor++;
        }

        var markerStart = cursor;
        while (cursor < startOffset + remainingLen &&
               (char.IsLetterOrDigit(source[cursor]) || source[cursor] is '_' or '-'))
        {
            cursor++;
        }

        if (cursor == markerStart)
        {
            return Result.Empty<Unit>(input);
        }

        var marker = source.AsSpan(markerStart, cursor - markerStart);
        while (cursor < startOffset + remainingLen && source[cursor] != '\n')
        {
            cursor++;
        }
        if (cursor < startOffset + remainingLen && source[cursor] == '\n')
        {
            cursor++;
        }

        while (cursor < startOffset + remainingLen)
        {
            var lineStart = cursor;
            while (cursor < startOffset + remainingLen && source[cursor] != '\n')
            {
                cursor++;
            }
            var lineEnd = cursor;
            if (cursor < startOffset + remainingLen && source[cursor] == '\n')
            {
                cursor++;
            }

            var lineSpan = source.AsSpan(lineStart, lineEnd - lineStart).Trim();
            if (lineSpan.SequenceEqual(marker))
            {
                break;
            }
        }

        var consumed = cursor - startOffset;
        var remainder = input.Skip(consumed);
        return Result.Value(Unit.Value, input, remainder);
    };

    private static readonly TextParser<Unit> HclNumberToken =
        from digits in Character.Digit.AtLeastOnce()
        from frac in (
            from dot in Character.EqualTo(DotChar)
            from dec in Character.Digit.AtLeastOnce()
            select Unit.Value).OptionalOrDefault()
        from exp in (
            from e in Character.In(ExpLowerChar, ExpUpperChar)
            from sign in Character.In(PlusChar, MinusChar).OptionalOrDefault()
            from expDigits in Character.Digit.AtLeastOnce()
            select Unit.Value).OptionalOrDefault()
        select Unit.Value;

    private static readonly TextParser<Unit> HclIdentifierToken =
        from first in Character.Letter.Or(Character.EqualTo(UnderscoreChar))
        from rest in Character.LetterOrDigit.Or(Character.In(UnderscoreChar, MinusChar)).Many()
        select Unit.Value;

    private static readonly TextParser<Unit> MultiCharOperatorToken =
        Span.EqualTo(OpEqText).Value(Unit.Value)
            .Or(Span.EqualTo(OpNeqText).Value(Unit.Value))
            .Or(Span.EqualTo(OpLteText).Value(Unit.Value))
            .Or(Span.EqualTo(OpGteText).Value(Unit.Value))
            .Or(Span.EqualTo(OpAndText).Value(Unit.Value))
            .Or(Span.EqualTo(OpOrText).Value(Unit.Value))
            .Or(Character.In(PlusChar, MinusChar, StarChar, SlashChar, PercentChar, BangChar, LtChar, GtChar).Value(Unit.Value));

    public static readonly Tokenizer<HclToken> Instance = new TokenizerBuilder<HclToken>()
        .Ignore(Span.WhiteSpace)
        .Ignore(HashLineComment)
        .Ignore(SlashLineComment)
        .Ignore(CBlockComment)
        .Match(HeredocToken, HclToken.HeredocLiteral)
        .Match(QuotedStringToken, HclToken.StringLiteral)
        .Match(Span.EqualTo(FatArrowText), HclToken.FatArrow)
        .Match(Span.EqualTo(EllipsisText), HclToken.Ellipsis)
        .Match(MultiCharOperatorToken, HclToken.Operator)
        .Match(Character.EqualTo(LBraceChar), HclToken.LBrace)
        .Match(Character.EqualTo(RBraceChar), HclToken.RBrace)
        .Match(Character.EqualTo(LBracketChar), HclToken.LBracket)
        .Match(Character.EqualTo(RBracketChar), HclToken.RBracket)
        .Match(Character.EqualTo(LParenChar), HclToken.LParen)
        .Match(Character.EqualTo(RParenChar), HclToken.RParen)
        .Match(Character.EqualTo(EqualsChar), HclToken.Equals)
        .Match(Character.EqualTo(CommaChar), HclToken.Comma)
        .Match(Character.EqualTo(DotChar), HclToken.Dot)
        .Match(Character.EqualTo(ColonChar), HclToken.Colon)
        .Match(Character.EqualTo(QuestionChar), HclToken.Question)
        .Match(HclNumberToken, HclToken.Number)
        .Match(HclIdentifierToken, HclToken.Identifier)
        .Build();

    public static string UnquoteString(string rawToken)
    {
        if (rawToken.Length >= 2 && rawToken[0] == '"' && rawToken[^1] == '"')
        {
            return rawToken[1..^1].Replace(EscapedQuote, QuoteStr).Replace(EscapedBackslash, BackslashStr);
        }
        return rawToken;
    }

    public static string ExtractHeredocBody(string rawToken)
    {
        if (!rawToken.StartsWith(HeredocStandardPrefix, StringComparison.Ordinal))
        {
            return rawToken;
        }

        var firstNewline = rawToken.IndexOf('\n');
        if (firstNewline < 0)
        {
            return string.Empty;
        }

        var header = rawToken[..firstNewline].Trim();
        var marker = header.StartsWith(HeredocIndentedPrefix, StringComparison.Ordinal)
            ? header[3..].Trim()
            : header[2..].Trim();

        var bodyWithFooter = rawToken[(firstNewline + 1)..];
        var lastNewline = bodyWithFooter.TrimEnd('\r', '\n').LastIndexOf('\n');
        if (lastNewline >= 0)
        {
            var lastLine = bodyWithFooter[(lastNewline + 1)..].Trim();
            if (string.Equals(lastLine, marker, StringComparison.Ordinal))
            {
                return bodyWithFooter[..lastNewline].TrimEnd('\r');
            }
        }

        return bodyWithFooter.TrimEnd('\r', '\n');
    }
}
