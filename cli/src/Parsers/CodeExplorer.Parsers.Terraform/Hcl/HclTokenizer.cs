using System.Text;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace CodeExplorer.Parsers.Terraform.Hcl;

/// <summary>
/// Superpower-based HCL2 tokenizer supporting line/block comments, heredocs (&lt;&lt;EOF / &lt;&lt;-EOF),
/// quoted strings with nested "${...}" and "%{...}" interpolations, numbers, identifiers, and operators.
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

    private const char HashChar = '#';
    private const char CrChar = '\r';
    private const char LfChar = '\n';
    private const char TabChar = '\t';
    private const char DotChar = '.';
    private const char ExpLowerChar = 'e';
    private const char ExpUpperChar = 'E';
    private const char PlusChar = '+';
    private const char MinusChar = '-';
    private const char UnderscoreChar = '_';
    private const char StarChar = '*';
    private const char SlashChar = '/';
    private const char PercentChar = '%';
    private const char DollarChar = '$';
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
    private const char QuoteChar = '"';
    private const char BackslashChar = '\\';

    private static readonly TextParser<TextSpan> HashLineComment =
        from hash in Character.EqualTo(HashChar)
        from rest in Character.ExceptIn(CrChar, LfChar).Many()
        select TextSpan.Empty;

    private static readonly TextParser<TextSpan> SlashLineComment =
        (from slashes in Span.EqualTo(LineCommentSlash)
         from rest in Character.ExceptIn(CrChar, LfChar).Many()
         select TextSpan.Empty).Try();

    private static readonly TextParser<TextSpan> CBlockComment = input =>
    {
        var source = input.Source;
        if (source == null || input.Length < 2)
        {
            return Result.Empty<TextSpan>(input);
        }

        var startOffset = input.Position.Absolute;
        if (source[startOffset] != SlashChar || source[startOffset + 1] != StarChar)
        {
            return Result.Empty<TextSpan>(input);
        }

        var endIdx = source.IndexOf(BlockCommentEnd, startOffset + 2, StringComparison.Ordinal);
        var consumed = endIdx >= 0
            ? (endIdx + 2 - startOffset)
            : input.Length;
        return Result.Value(TextSpan.Empty, input, input.Skip(consumed));
    };

    private static readonly TextParser<Unit> QuotedStringToken = input =>
    {
        var next = input.ConsumeChar();
        if (!next.HasValue || next.Value != QuoteChar)
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

            if (ch == BackslashChar)
            {
                if (!remainder.IsAtEnd)
                {
                    remainder = remainder.ConsumeChar().Remainder;
                }
                continue;
            }

            if ((ch == DollarChar || ch == PercentChar) && !remainder.IsAtEnd)
            {
                var peek = remainder.ConsumeChar();
                if (peek.Value == ch)
                {
                    remainder = peek.Remainder;
                    continue;
                }
                if (peek.Value == LBraceChar)
                {
                    interpBraceDepth++;
                    remainder = peek.Remainder;
                    continue;
                }
            }

            if (interpBraceDepth > 0)
            {
                if (ch == LBraceChar)
                {
                    interpBraceDepth++;
                }
                else if (ch == RBraceChar)
                {
                    interpBraceDepth--;
                }
                else if (ch == QuoteChar)
                {
                    remainder = ConsumeInnerQuotedString(remainder);
                }
                continue;
            }

            if (ch == QuoteChar)
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
            select Unit.Value).Try().OptionalOrDefault()
        from exp in (
            from e in Character.In(ExpLowerChar, ExpUpperChar)
            from sign in Character.In(PlusChar, MinusChar).OptionalOrDefault()
            from expDigits in Character.Digit.AtLeastOnce()
            select Unit.Value).Try().OptionalOrDefault()
        select Unit.Value;

    private static readonly TextParser<Unit> HclIdentifierToken =
        from first in Character.Letter.Or(Character.EqualTo(UnderscoreChar))
        from rest in Character.LetterOrDigit.Or(Character.In(UnderscoreChar, MinusChar)).Many()
        select Unit.Value;

    private static readonly TextParser<Unit> MultiCharOperatorToken =
        Span.EqualTo(OpEqText).Try().Value(Unit.Value)
            .Or(Span.EqualTo(OpNeqText).Try().Value(Unit.Value))
            .Or(Span.EqualTo(OpLteText).Try().Value(Unit.Value))
            .Or(Span.EqualTo(OpGteText).Try().Value(Unit.Value))
            .Or(Span.EqualTo(OpAndText).Try().Value(Unit.Value))
            .Or(Span.EqualTo(OpOrText).Try().Value(Unit.Value))
            .Or(Character.In(PlusChar, MinusChar, StarChar, SlashChar, PercentChar, BangChar, LtChar, GtChar).Value(Unit.Value));

    public static readonly Tokenizer<HclToken> Instance = new TokenizerBuilder<HclToken>()
        .Ignore(Span.WhiteSpace)
        .Ignore(HashLineComment)
        .Ignore(SlashLineComment)
        .Ignore(CBlockComment)
        .Match(HeredocToken, HclToken.HeredocLiteral)
        .Match(QuotedStringToken, HclToken.StringLiteral)
        .Match(Span.EqualTo(FatArrowText).Try(), HclToken.FatArrow)
        .Match(Span.EqualTo(EllipsisText).Try(), HclToken.Ellipsis)
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
        .Match(Character.AnyChar, HclToken.Unknown)
        .Build();

    public static string UnquoteString(string rawToken)
    {
        if (rawToken.Length < 2 || rawToken[0] != QuoteChar)
        {
            return rawToken;
        }

        var inner = rawToken[^1] == QuoteChar
            ? rawToken.AsSpan(1, rawToken.Length - 2)
            : rawToken.AsSpan(1);

        if (!inner.Contains(BackslashChar))
        {
            return inner.ToString();
        }

        var sb = new StringBuilder(inner.Length);
        var i = 0;
        while (i < inner.Length)
        {
            var ch = inner[i++];
            if (ch != BackslashChar || i >= inner.Length)
            {
                sb.Append(ch);
                continue;
            }

            var esc = inner[i++];
            switch (esc)
            {
                case QuoteChar:
                    sb.Append(QuoteChar);
                    break;
                case BackslashChar:
                    sb.Append(BackslashChar);
                    break;
                case 'n':
                    sb.Append(LfChar);
                    break;
                case 'r':
                    sb.Append(CrChar);
                    break;
                case 't':
                    sb.Append(TabChar);
                    break;
                case 'u' when i + 4 <= inner.Length &&
                              ushort.TryParse(inner.Slice(i, 4), System.Globalization.NumberStyles.HexNumber, null, out var codeUnit):
                    sb.Append((char)codeUnit);
                    i += 4;
                    break;
                default:
                    sb.Append(esc);
                    break;
            }
        }
        return sb.ToString();
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
