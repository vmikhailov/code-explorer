using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// Declarative Superpower combinator parser for HCL2 (Terraform, OpenTofu, and Terragrunt).
/// </summary>
public static class HclParser
{
    private const string TrueLiteral = "true";
    private const string FalseLiteral = "false";
    private const string NullLiteral = "null";
    private const string ForKeyword = "for";
    private const string InKeyword = "in";
    private const string IfKeyword = "if";
    private const char DotSeparator = '.';

    private static readonly TokenListParser<HclToken, Token<HclToken>> IdentOrStringKey =
        Token.EqualTo(HclToken.Identifier)
            .Or(Token.EqualTo(HclToken.StringLiteral));

    private static readonly TokenListParser<HclToken, string> BlockLabelParser =
        Token.EqualTo(HclToken.StringLiteral).Select(t => HclTokenizer.UnquoteString(t.ToStringValue()))
            .Or(Token.EqualTo(HclToken.Identifier).Select(t => t.ToStringValue()));

    public static HclFile Parse(string source, string filePath = "")
    {
        var tokenized = HclTokenizer.Instance.TryTokenize(source);
        if (!tokenized.HasValue)
        {
            return new HclFile(filePath, [], []);
        }

        return ParseTokenList(tokenized.Value, filePath);
    }

    public static bool TryParse(string source, string filePath, out HclFile file)
    {
        var tokenized = HclTokenizer.Instance.TryTokenize(source);
        if (!tokenized.HasValue)
        {
            file = new HclFile(filePath, [], []);
            return false;
        }

        file = ParseTokenList(tokenized.Value, filePath);
        return true;
    }

    private static HclFile ParseTokenList(TokenList<HclToken> tokens, string filePath)
    {
        var blocks = new List<HclBlock>();
        var attributes = new List<HclAttribute>();
        var remainder = tokens;

        while (!remainder.IsAtEnd)
        {
            var bodyItem = ParseBodyItem(remainder);
            if (!bodyItem.HasValue)
            {
                remainder = SkipSingleTokenOrBalancedBraces(remainder);
                continue;
            }

            if (bodyItem.Value.Block != null)
            {
                blocks.Add(bodyItem.Value.Block);
            }
            else if (bodyItem.Value.Attribute != null)
            {
                attributes.Add(bodyItem.Value.Attribute);
            }
            remainder = bodyItem.Remainder;
        }

        return new HclFile(filePath, blocks, attributes);
    }

    private readonly record struct BodyItemResult(HclBlock? Block, HclAttribute? Attribute);

    private static TokenListParserResult<HclToken, BodyItemResult> ParseBodyItem(TokenList<HclToken> input)
    {
        var first = input.ConsumeToken();
        if (!first.HasValue)
        {
            return TokenListParserResult.Empty<HclToken, BodyItemResult>(input);
        }

        if (first.Value.Kind is HclToken.Identifier or HclToken.StringLiteral)
        {
            var afterKey = first.Remainder;
            var second = afterKey.ConsumeToken();
            if (second.HasValue && second.Value.Kind is HclToken.Equals or HclToken.Colon)
            {
                var keyName = first.Value.Kind == HclToken.StringLiteral
                    ? HclTokenizer.UnquoteString(first.Value.ToStringValue())
                    : first.Value.ToStringValue();
                var keyLine = first.Value.Position.Line;
                var exprResult = ParseExpression(second.Remainder);
                if (exprResult.HasValue)
                {
                    var optionalComma = exprResult.Remainder.ConsumeToken();
                    var finalRem = optionalComma.HasValue && optionalComma.Value.Kind == HclToken.Comma
                        ? optionalComma.Remainder
                        : exprResult.Remainder;
                    var attr = new HclAttribute(keyName, exprResult.Value, keyLine);
                    return TokenListParserResult.Value(new BodyItemResult(null, attr), input, finalRem);
                }
            }

            if (first.Value.Kind == HclToken.Identifier)
            {
                var blockRes = ParseBlockFromHeader(first.Value, afterKey, input);
                if (blockRes.HasValue)
                {
                    return TokenListParserResult.Value(new BodyItemResult(blockRes.Value, null), input, blockRes.Remainder);
                }
            }
        }

        return TokenListParserResult.Empty<HclToken, BodyItemResult>(input);
    }

    private static TokenListParserResult<HclToken, HclBlock> ParseBlockFromHeader(
        Token<HclToken> typeToken,
        TokenList<HclToken> remainder,
        TokenList<HclToken> originalInput)
    {
        var labels = new List<string>();
        var cur = remainder;

        while (!cur.IsAtEnd)
        {
            var next = cur.ConsumeToken();
            if (!next.HasValue)
            {
                return TokenListParserResult.Empty<HclToken, HclBlock>(originalInput);
            }

            if (next.Value.Kind == HclToken.LBrace)
            {
                cur = next.Remainder;
                break;
            }

            var labelRes = BlockLabelParser(cur);
            if (labelRes.HasValue)
            {
                labels.Add(labelRes.Value);
                cur = labelRes.Remainder;
                continue;
            }

            return TokenListParserResult.Empty<HclToken, HclBlock>(originalInput);
        }

        var nestedBlocks = new List<HclBlock>();
        var attrs = new List<HclAttribute>();
        var endLine = typeToken.Position.Line;

        while (!cur.IsAtEnd)
        {
            var peek = cur.ConsumeToken();
            if (peek.HasValue && peek.Value.Kind == HclToken.RBrace)
            {
                endLine = peek.Value.Position.Line;
                cur = peek.Remainder;
                break;
            }

            var item = ParseBodyItem(cur);
            if (item.HasValue)
            {
                if (item.Value.Block != null)
                {
                    nestedBlocks.Add(item.Value.Block);
                }
                else if (item.Value.Attribute != null)
                {
                    attrs.Add(item.Value.Attribute);
                }
                cur = item.Remainder;
            }
            else
            {
                cur = SkipSingleTokenOrBalancedBraces(cur);
            }
        }

        var block = new HclBlock(typeToken.ToStringValue(), labels, attrs, nestedBlocks, typeToken.Position.Line, endLine);
        return TokenListParserResult.Value(block, originalInput, cur);
    }

    internal static TokenListParserResult<HclToken, HclExpr> ParseExpression(TokenList<HclToken> input)
    {
        var primary = ParsePrimaryExpression(input);
        if (!primary.HasValue)
        {
            return TokenListParserResult.Empty<HclToken, HclExpr>(input);
        }

        var cur = primary.Remainder;
        var next = cur.ConsumeToken();
        if (!next.HasValue || !IsExpressionContinuation(next.Value, primary.Value.Line))
        {
            return primary;
        }

        var subExprs = new List<HclExpr> { primary.Value };
        var line = primary.Value.Line;

        while (!cur.IsAtEnd)
        {
            var tok = cur.ConsumeToken();
            if (!tok.HasValue || !IsExpressionContinuation(tok.Value, line))
            {
                break;
            }

            if (tok.Value.Kind is HclToken.Operator or HclToken.Question or HclToken.Colon or HclToken.FatArrow or HclToken.Ellipsis)
            {
                cur = tok.Remainder;
                var rhs = ParsePrimaryExpression(cur);
                if (rhs.HasValue)
                {
                    subExprs.Add(rhs.Value);
                    line = Math.Max(line, rhs.Value.Line);
                    cur = rhs.Remainder;
                }
                continue;
            }

            break;
        }

        var refs = new List<HclReference>();
        foreach (var sub in subExprs)
        {
            HclReferenceCollector.CollectExprReferences(sub, HclReferenceCollector.DefaultVia, refs);
        }

        return TokenListParserResult.Value<HclToken, HclExpr>(
            new HclExpr.Complex(string.Empty, subExprs, refs, primary.Value.Line),
            input,
            cur);
    }

    private static bool IsExpressionContinuation(Token<HclToken> token, int currentLine)
    {
        if (token.Kind is HclToken.Operator or HclToken.Question or HclToken.Colon or HclToken.FatArrow or HclToken.Ellipsis)
        {
            return token.Position.Line == currentLine || token.Kind is HclToken.Question or HclToken.Colon;
        }
        return false;
    }

    private static TokenListParserResult<HclToken, HclExpr> ParsePrimaryExpression(TokenList<HclToken> input)
    {
        var first = input.ConsumeToken();
        if (!first.HasValue)
        {
            return TokenListParserResult.Empty<HclToken, HclExpr>(input);
        }

        var tok = first.Value;
        if (tok.Kind == HclToken.Operator)
        {
            var operand = ParsePrimaryExpression(first.Remainder);
            if (operand.HasValue)
            {
                return TokenListParserResult.Value(operand.Value, input, operand.Remainder);
            }
            return TokenListParserResult.Empty<HclToken, HclExpr>(input);
        }

        if (tok.Kind == HclToken.StringLiteral)
        {
            var expr = HclReferenceCollector.ParseStringOrTemplate(tok);
            return ParsePostfixContinuations(expr, first.Remainder, input);
        }

        if (tok.Kind == HclToken.HeredocLiteral)
        {
            var expr = HclReferenceCollector.ParseHeredocOrTemplate(tok);
            return TokenListParserResult.Value(expr, input, first.Remainder);
        }

        if (tok.Kind == HclToken.Number)
        {
            var lit = new HclExpr.Literal(HclLiteralKind.Number, tok.ToStringValue(), tok.Position.Line);
            return TokenListParserResult.Value<HclToken, HclExpr>(lit, input, first.Remainder);
        }

        if (tok.Kind == HclToken.LBracket)
        {
            return ParseTupleOrForList(first.Remainder, tok.Position.Line, input);
        }

        if (tok.Kind == HclToken.LBrace)
        {
            return ParseObjectOrForMap(first.Remainder, tok.Position.Line, input);
        }

        if (tok.Kind == HclToken.LParen)
        {
            return ParseParenthesizedExpr(first.Remainder, tok.Position.Line, input);
        }

        if (tok.Kind == HclToken.Identifier)
        {
            return ParseIdentifierLedExpr(tok, first.Remainder, input);
        }

        return TokenListParserResult.Empty<HclToken, HclExpr>(input);
    }

    private static TokenListParserResult<HclToken, HclExpr> ParseIdentifierLedExpr(
        Token<HclToken> firstIdent,
        TokenList<HclToken> remainder,
        TokenList<HclToken> originalInput)
    {
        var text = firstIdent.ToStringValue();
        var line = firstIdent.Position.Line;

        if (string.Equals(text, TrueLiteral, StringComparison.Ordinal) ||
            string.Equals(text, FalseLiteral, StringComparison.Ordinal))
        {
            var boolLit = new HclExpr.Literal(HclLiteralKind.Bool, text, line);
            return TokenListParserResult.Value<HclToken, HclExpr>(boolLit, originalInput, remainder);
        }

        if (string.Equals(text, NullLiteral, StringComparison.Ordinal))
        {
            var nullLit = new HclExpr.Literal(HclLiteralKind.Null, text, line);
            return TokenListParserResult.Value<HclToken, HclExpr>(nullLit, originalInput, remainder);
        }

        var next = remainder.ConsumeToken();
        if (next.HasValue && next.Value.Kind == HclToken.LParen)
        {
            var callRes = ParseCallExpr(text, line, next.Remainder, originalInput);
            if (callRes.HasValue)
            {
                return ParsePostfixContinuations(callRes.Value, callRes.Remainder, originalInput);
            }
        }

        if (next.HasValue && next.Value.Kind == HclToken.Colon)
        {
            var secondColon = next.Remainder.ConsumeToken();
            if (secondColon.HasValue && secondColon.Value.Kind == HclToken.Colon)
            {
                var fnNameTok = secondColon.Remainder.ConsumeToken();
                if (fnNameTok.HasValue && fnNameTok.Value.Kind == HclToken.Identifier)
                {
                    var lparenTok = fnNameTok.Remainder.ConsumeToken();
                    if (lparenTok.HasValue && lparenTok.Value.Kind == HclToken.LParen)
                    {
                        var qualifiedName = $"{text}::{fnNameTok.Value.ToStringValue()}";
                        var callRes = ParseCallExpr(qualifiedName, line, lparenTok.Remainder, originalInput);
                        if (callRes.HasValue)
                        {
                            return ParsePostfixContinuations(callRes.Value, callRes.Remainder, originalInput);
                        }
                    }
                }
            }
        }

        var parts = new List<string> { text };
        var extraSubExprs = new List<HclExpr>();
        var cur = remainder;

        while (!cur.IsAtEnd)
        {
            var step = cur.ConsumeToken();
            if (!step.HasValue) break;

            if (step.Value.Kind == HclToken.Dot)
            {
                var memberTok = step.Remainder.ConsumeToken();
                if (memberTok.HasValue && memberTok.Value.Kind is HclToken.Identifier or HclToken.Number)
                {
                    parts.Add(memberTok.Value.ToStringValue());
                    cur = memberTok.Remainder;
                    continue;
                }
                if (memberTok.HasValue && memberTok.Value.Kind == HclToken.Operator && memberTok.Value.ToStringValue() == "*")
                {
                    cur = memberTok.Remainder;
                    continue;
                }
                break;
            }

            if (step.Value.Kind == HclToken.LBracket)
            {
                cur = ConsumeBracketSubExpressions(step.Remainder, extraSubExprs);
                continue;
            }

            break;
        }

        HclExpr resultExpr = new HclExpr.Traversal(parts, line);
        if (extraSubExprs.Count > 0)
        {
            var allSubs = new List<HclExpr>(1 + extraSubExprs.Count) { resultExpr };
            allSubs.AddRange(extraSubExprs);
            var refs = new List<HclReference>();
            foreach (var sub in allSubs)
            {
                HclReferenceCollector.CollectExprReferences(sub, HclReferenceCollector.DefaultVia, refs);
            }
            resultExpr = new HclExpr.Complex(string.Join(DotSeparator, parts), allSubs, refs, line);
        }

        return TokenListParserResult.Value(resultExpr, originalInput, cur);
    }

    private static TokenListParserResult<HclToken, HclExpr> ParseCallExpr(
        string functionName,
        int line,
        TokenList<HclToken> afterLParen,
        TokenList<HclToken> originalInput)
    {
        var args = new List<HclExpr>();
        var cur = afterLParen;

        while (!cur.IsAtEnd)
        {
            var peek = cur.ConsumeToken();
            if (peek.HasValue && peek.Value.Kind == HclToken.RParen)
            {
                cur = peek.Remainder;
                break;
            }

            if (peek.HasValue && peek.Value.Kind is HclToken.Comma or HclToken.Ellipsis)
            {
                cur = peek.Remainder;
                continue;
            }

            var argRes = ParseExpression(cur);
            if (argRes.HasValue)
            {
                args.Add(argRes.Value);
                cur = argRes.Remainder;
            }
            else
            {
                cur = SkipSingleTokenOrBalancedParens(cur);
            }
        }

        return TokenListParserResult.Value<HclToken, HclExpr>(new HclExpr.Call(functionName, args, line), originalInput, cur);
    }

    private static TokenListParserResult<HclToken, HclExpr> ParsePostfixContinuations(
        HclExpr baseExpr,
        TokenList<HclToken> remainder,
        TokenList<HclToken> originalInput)
    {
        var cur = remainder;
        var extraExprs = new List<HclExpr>();

        while (!cur.IsAtEnd)
        {
            var peek = cur.ConsumeToken();
            if (!peek.HasValue) break;

            if (peek.Value.Kind == HclToken.Dot)
            {
                var memberTok = peek.Remainder.ConsumeToken();
                if (memberTok.HasValue && memberTok.Value.Kind is HclToken.Identifier or HclToken.Number)
                {
                    cur = memberTok.Remainder;
                    continue;
                }
                break;
            }

            if (peek.Value.Kind == HclToken.LBracket)
            {
                cur = ConsumeBracketSubExpressions(peek.Remainder, extraExprs);
                continue;
            }

            break;
        }

        if (extraExprs.Count == 0)
        {
            return TokenListParserResult.Value(baseExpr, originalInput, cur);
        }

        var allSubs = new List<HclExpr>(1 + extraExprs.Count) { baseExpr };
        allSubs.AddRange(extraExprs);
        var refs = new List<HclReference>();
        foreach (var sub in allSubs)
        {
            HclReferenceCollector.CollectExprReferences(sub, HclReferenceCollector.DefaultVia, refs);
        }
        return TokenListParserResult.Value<HclToken, HclExpr>(
            new HclExpr.Complex(string.Empty, allSubs, refs, baseExpr.Line),
            originalInput,
            cur);
    }

    private static TokenList<HclToken> ConsumeBracketSubExpressions(
        TokenList<HclToken> afterLBracket,
        List<HclExpr> sink)
    {
        var cur = afterLBracket;
        var depth = 1;
        while (!cur.IsAtEnd && depth > 0)
        {
            var peek = cur.ConsumeToken();
            if (!peek.HasValue) break;

            if (peek.Value.Kind == HclToken.RBracket)
            {
                depth--;
                cur = peek.Remainder;
                if (depth == 0) break;
                continue;
            }

            if (peek.Value.Kind == HclToken.LBracket)
            {
                depth++;
                cur = peek.Remainder;
                continue;
            }

            var sub = ParseExpression(cur);
            if (sub.HasValue)
            {
                sink.Add(sub.Value);
                cur = sub.Remainder;
            }
            else
            {
                cur = peek.Remainder;
            }
        }
        return cur;
    }

    private static TokenListParserResult<HclToken, HclExpr> ParseTupleOrForList(
        TokenList<HclToken> afterLBracket,
        int line,
        TokenList<HclToken> originalInput)
    {
        var elements = new List<HclExpr>();
        var cur = afterLBracket;

        var firstTok = cur.ConsumeToken();
        if (firstTok.HasValue && firstTok.Value.Kind == HclToken.Identifier &&
            string.Equals(firstTok.Value.ToStringValue(), ForKeyword, StringComparison.Ordinal))
        {
            return ParseComprehensionUntil(cur, HclToken.LBracket, HclToken.RBracket, line, originalInput);
        }

        while (!cur.IsAtEnd)
        {
            var peek = cur.ConsumeToken();
            if (peek.HasValue && peek.Value.Kind == HclToken.RBracket)
            {
                cur = peek.Remainder;
                break;
            }

            if (peek.HasValue && peek.Value.Kind == HclToken.Comma)
            {
                cur = peek.Remainder;
                continue;
            }

            var elem = ParseExpression(cur);
            if (elem.HasValue)
            {
                elements.Add(elem.Value);
                cur = elem.Remainder;
            }
            else
            {
                cur = peek.Remainder;
            }
        }

        return TokenListParserResult.Value<HclToken, HclExpr>(new HclExpr.Tuple(elements, line), originalInput, cur);
    }

    private static TokenListParserResult<HclToken, HclExpr> ParseObjectOrForMap(
        TokenList<HclToken> afterLBrace,
        int line,
        TokenList<HclToken> originalInput)
    {
        var cur = afterLBrace;
        var firstTok = cur.ConsumeToken();
        if (firstTok.HasValue && firstTok.Value.Kind == HclToken.Identifier &&
            string.Equals(firstTok.Value.ToStringValue(), ForKeyword, StringComparison.Ordinal))
        {
            return ParseComprehensionUntil(cur, HclToken.LBrace, HclToken.RBrace, line, originalInput);
        }

        var entries = new List<HclAttribute>();
        while (!cur.IsAtEnd)
        {
            var peek = cur.ConsumeToken();
            if (peek.HasValue && peek.Value.Kind == HclToken.RBrace)
            {
                cur = peek.Remainder;
                break;
            }

            if (peek.HasValue && peek.Value.Kind == HclToken.Comma)
            {
                cur = peek.Remainder;
                continue;
            }

            var keyTok = IdentOrStringKey(cur);
            if (keyTok.HasValue)
            {
                var sep = keyTok.Remainder.ConsumeToken();
                if (sep.HasValue && sep.Value.Kind is HclToken.Equals or HclToken.Colon)
                {
                    var valRes = ParseExpression(sep.Remainder);
                    if (valRes.HasValue)
                    {
                        var keyName = keyTok.Value.Kind == HclToken.StringLiteral
                            ? HclTokenizer.UnquoteString(keyTok.Value.ToStringValue())
                            : keyTok.Value.ToStringValue();
                        entries.Add(new HclAttribute(keyName, valRes.Value, keyTok.Value.Position.Line));
                        cur = valRes.Remainder;
                        continue;
                    }
                }
            }

            var fallbackExpr = ParseExpression(cur);
            if (fallbackExpr.HasValue)
            {
                entries.Add(new HclAttribute(string.Empty, fallbackExpr.Value, fallbackExpr.Value.Line));
                cur = fallbackExpr.Remainder;
            }
            else
            {
                cur = peek.Remainder;
            }
        }

        return TokenListParserResult.Value<HclToken, HclExpr>(new HclExpr.ObjectExpr(entries, line), originalInput, cur);
    }

    private static TokenListParserResult<HclToken, HclExpr> ParseParenthesizedExpr(
        TokenList<HclToken> afterLParen,
        int line,
        TokenList<HclToken> originalInput)
    {
        var subExprs = new List<HclExpr>();
        var cur = afterLParen;
        var depth = 1;

        while (!cur.IsAtEnd && depth > 0)
        {
            var peek = cur.ConsumeToken();
            if (!peek.HasValue) break;

            if (peek.Value.Kind == HclToken.RParen)
            {
                depth--;
                cur = peek.Remainder;
                if (depth == 0) break;
                continue;
            }

            if (peek.Value.Kind == HclToken.LParen)
            {
                depth++;
                cur = peek.Remainder;
                continue;
            }

            var sub = ParseExpression(cur);
            if (sub.HasValue)
            {
                subExprs.Add(sub.Value);
                cur = sub.Remainder;
            }
            else
            {
                cur = peek.Remainder;
            }
        }

        if (subExprs.Count == 1)
        {
            return ParsePostfixContinuations(subExprs[0], cur, originalInput);
        }

        var refs = new List<HclReference>();
        foreach (var sub in subExprs)
        {
            HclReferenceCollector.CollectExprReferences(sub, HclReferenceCollector.DefaultVia, refs);
        }
        return TokenListParserResult.Value<HclToken, HclExpr>(
            new HclExpr.Complex(string.Empty, subExprs, refs, line),
            originalInput,
            cur);
    }

    private static TokenListParserResult<HclToken, HclExpr> ParseComprehensionUntil(
        TokenList<HclToken> afterOpen,
        HclToken openKind,
        HclToken closeKind,
        int line,
        TokenList<HclToken> originalInput)
    {
        var subExprs = new List<HclExpr>();
        var cur = afterOpen;
        var depth = 1;
        var seenIn = false;

        while (!cur.IsAtEnd && depth > 0)
        {
            var step = cur.ConsumeToken();
            if (!step.HasValue) break;

            if (step.Value.Kind == openKind)
            {
                depth++;
                cur = step.Remainder;
                continue;
            }

            if (step.Value.Kind == closeKind)
            {
                depth--;
                cur = step.Remainder;
                if (depth == 0) break;
                continue;
            }

            if (!seenIn)
            {
                if (step.Value.Kind == HclToken.Identifier &&
                    string.Equals(step.Value.ToStringValue(), InKeyword, StringComparison.Ordinal))
                {
                    seenIn = true;
                }
                cur = step.Remainder;
                continue;
            }

            if (step.Value.Kind is HclToken.Colon or HclToken.FatArrow or HclToken.Comma or HclToken.Ellipsis ||
                (step.Value.Kind == HclToken.Identifier && string.Equals(step.Value.ToStringValue(), IfKeyword, StringComparison.Ordinal)))
            {
                cur = step.Remainder;
                continue;
            }

            var expr = ParsePrimaryExpression(cur);
            if (expr.HasValue)
            {
                subExprs.Add(expr.Value);
                cur = expr.Remainder;
            }
            else
            {
                cur = step.Remainder;
            }
        }

        var refs = new List<HclReference>();
        foreach (var sub in subExprs)
        {
            HclReferenceCollector.CollectExprReferences(sub, HclReferenceCollector.DefaultVia, refs);
        }

        return TokenListParserResult.Value<HclToken, HclExpr>(
            new HclExpr.Complex(ForKeyword, subExprs, refs, line),
            originalInput,
            cur);
    }

    private static TokenList<HclToken> SkipSingleTokenOrBalancedBraces(TokenList<HclToken> input)
    {
        var step = input.ConsumeToken();
        if (!step.HasValue) return input;
        if (step.Value.Kind == HclToken.LBrace)
        {
            var cur = step.Remainder;
            var depth = 1;
            while (!cur.IsAtEnd && depth > 0)
            {
                var tok = cur.ConsumeToken();
                if (!tok.HasValue) break;
                if (tok.Value.Kind == HclToken.LBrace) depth++;
                else if (tok.Value.Kind == HclToken.RBrace) depth--;
                cur = tok.Remainder;
            }
            return cur;
        }
        return step.Remainder;
    }

    private static TokenList<HclToken> SkipSingleTokenOrBalancedParens(TokenList<HclToken> input)
    {
        var step = input.ConsumeToken();
        if (!step.HasValue) return input;
        if (step.Value.Kind == HclToken.LParen)
        {
            var cur = step.Remainder;
            var depth = 1;
            while (!cur.IsAtEnd && depth > 0)
            {
                var tok = cur.ConsumeToken();
                if (!tok.HasValue) break;
                if (tok.Value.Kind == HclToken.LParen) depth++;
                else if (tok.Value.Kind == HclToken.RParen) depth--;
                cur = tok.Remainder;
            }
            return cur;
        }
        return step.Remainder;
    }
}
