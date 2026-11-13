using System.Globalization;
using System.Text;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// Superpower grammar for HCL native-syntax expressions, templates, and heredocs.
///
/// Token parsers skip the whitespace and comments <em>before</em> their token and consume nothing
/// after it, so the remainder of any parser is exactly the end of its last token (used to slice
/// attribute source text). A token parser that does not match consumes no input, so alternatives
/// combine with <c>Or</c> without <c>Try</c>.
///
/// Newlines are insignificant inside expressions, except that a call's <c>(</c> and an index's
/// <c>[</c> must start on the same line as what precedes them, so a value can never swallow the
/// next line of a body.
/// </summary>
internal static class HclGrammar
{
    private const string Ellipsis = "...";
    private const string Arrow = "=>";
    private const string DoubleColon = "::";
    private const string EqualOp = "==";
    private const string NotEqualOp = "!=";
    private const string LessOrEqualOp = "<=";
    private const string GreaterOrEqualOp = ">=";
    private const string LessOp = "<";
    private const string GreaterOp = ">";
    private const string AndOp = "&&";
    private const string OrOp = "||";
    private const string PlusOp = "+";
    private const string MinusOp = "-";
    private const string MultiplyOp = "*";
    private const string DivideOp = "/";
    private const string ModuloOp = "%";
    private const string NotOp = "!";
    private const string AssignOp = "=";
    private const string ColonOp = ":";
    private const string QuestionOp = "?";
    private const string DotOp = ".";
    private const string StripMarkerText = "~";

    // Characters that must not follow an operator for it to match (so `=` is not `==` or `=>`).
    private const string NotAfterAssign = "=>";
    private const string NotAfterNot = "=";
    private const string NotAfterLess = "<=";
    private const string NotAfterGreater = "=";

    private const char OpenBrace = '{';
    private const char CloseBrace = '}';
    private const char OpenBracket = '[';
    private const char CloseBracket = ']';
    private const char OpenParen = '(';
    private const char CloseParen = ')';
    private const char Comma = ',';
    private const char Star = '*';
    private const char DecimalPoint = '.';
    private static readonly char[] ExponentMarkers = ['e', 'E'];
    private static readonly char[] ExponentSigns = ['+', '-'];

    private const string InterpolationOpen = "${";
    private const string DirectiveOpen = "%{";
    private const string EscapedInterpolationOpen = "$${";
    private const string EscapedDirectiveOpen = "%%{";
    private const string HeredocOpen = "<<";

    private const string KeywordFor = "for";
    private const string KeywordIn = "in";
    private const string KeywordIf = "if";
    private const string KeywordElse = "else";
    private const string KeywordEndIf = "endif";
    private const string KeywordEndFor = "endfor";
    private const string KeywordTrue = "true";
    private const string KeywordFalse = "false";
    private const string KeywordNull = "null";

    private const string ExpectedIdentifier = "identifier";
    private const string ExpectedNumber = "number";
    private const string ExpectedDirective = "template directive";
    private const string ExpectedClosingQuote = "closing quote";
    private const string ExpectedHeredocDelimiter = "heredoc delimiter";
    private const string ExpectedHeredocNewline = "newline after heredoc delimiter";
    private const string ExpectedHeredocClose = "closing heredoc delimiter";

    // ---- Trivia ---------------------------------------------------------------------------------

    /// <summary>Whitespace and <c>#</c>, <c>//</c>, <c>/* */</c> comments. Always succeeds.</summary>
    public static TextParser<Unit> Trivia { get; } =
        Span.WhiteSpace.Value(Unit.Value)
            .Or(Comment.ShellStyle.Value(Unit.Value))
            .Or(Comment.CPlusPlusStyle.Try().Value(Unit.Value))
            .Or(Comment.CStyle.Try().Value(Unit.Value))
            .IgnoreMany();

    // Skips trivia, then runs the parser; a failed token consumes nothing.
    private static TextParser<T> Token<T>(TextParser<T> parser) => input =>
    {
        var result = parser(Trivia(input).Remainder);
        return result.HasValue ? result : Result.Empty<T>(input, result.Expectations ?? []);
    };

    // Skips trivia, then runs the parser. A failure at the token's first character consumes nothing;
    // a failure further in (e.g. an unterminated string) is reported where it happened.
    private static TextParser<T> Lead<T>(TextParser<T> parser) => input =>
    {
        var start = Trivia(input).Remainder;
        var result = parser(start);
        return result.HasValue || result.Remainder != start
            ? result
            : Result.Empty<T>(input, result.Expectations ?? []);
    };

    // Like Token, but only spaces and tabs may precede the token (no newline, no comment).
    private static TextParser<T> SameLine<T>(TextParser<T> parser) => input =>
    {
        var start = input;
        while (start.ConsumeChar() is { HasValue: true } c && (c.Value == ' ' || c.Value == '\t'))
        {
            start = c.Remainder;
        }
        var result = parser(start);
        return result.HasValue ? result : Result.Empty<T>(input, result.Expectations ?? []);
    };

    // ---- Tokens ---------------------------------------------------------------------------------

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    // HCL identifiers may contain '-' (var.my-flag); subtraction needs surrounding spaces.
    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

    private static TextParser<TextSpan> IdentifierSpan { get; } =
        Span.MatchedBy(
            Character.Matching(IsIdentifierStart, ExpectedIdentifier)
                .IgnoreThen(Character.Matching(IsIdentifierPart, ExpectedIdentifier).IgnoreMany()));

    public static TextParser<TextSpan> IdentifierToken { get; } = Token(IdentifierSpan);

    private static TextParser<string> Identifier { get; } = IdentifierToken.Select(s => s.ToStringValue());

    private static TextParser<string> Keyword(string keyword) =>
        Token(IdentifierSpan.Where(s => s.EqualsValue(keyword), keyword)).Select(s => s.ToStringValue());

    private static TextParser<Unit> Digits { get; } =
        Character.Digit.AtLeastOnce().Value(Unit.Value);

    private static TextParser<TextSpan> NumberSpan { get; } =
        Span.MatchedBy(
            Digits
                .IgnoreThen(Character.EqualTo(DecimalPoint).IgnoreThen(Digits).Try().Optional())
                .IgnoreThen(Character.In(ExponentMarkers)
                    .IgnoreThen(Character.In(ExponentSigns).Optional())
                    .IgnoreThen(Digits).Try().Optional()));

    // Matches `text` not immediately followed by any character in `notFollowedBy`.
    private static TextParser<TextSpan> Op(string text, string notFollowedBy = "") => Token<TextSpan>(input =>
    {
        var matched = Span.EqualTo(text)(input);
        if (!matched.HasValue)
        {
            return Result.Empty<TextSpan>(input, text);
        }
        var next = matched.Remainder.ConsumeChar();
        return next.HasValue && notFollowedBy.Contains(next.Value)
            ? Result.Empty<TextSpan>(input, text)
            : matched;
    });

    public static TextParser<TextSpan> Symbol(char c) => Token(Span.EqualTo(c));

    /// <summary>The <c>{</c> opening a block body.</summary>
    public static TextParser<TextSpan> BlockOpenToken { get; } = Symbol(OpenBrace);

    /// <summary><c>=</c> introducing an attribute value (not <c>==</c> or <c>=&gt;</c>).</summary>
    public static TextParser<TextSpan> AssignToken { get; } = Op(AssignOp, NotAfterAssign);

    private static TextParser<TextSpan?> StripMarker { get; } = Op(StripMarkerText).Optional();

    private static TextParser<TextSpan> TemplateClose { get; } =
        from strip in StripMarker
        from close in Symbol(CloseBrace)
        select close;

    // ---- Templates ------------------------------------------------------------------------------

    private static bool StartsWith(TextSpan span, string value) =>
        span.Length >= value.Length
        && string.CompareOrdinal(span.Source, span.Position.Absolute, value, 0, value.Length) == 0;

    private static TextParser<HclExpr> Interpolation { get; } =
        from strip in StripMarker
        from expr in Parse.Ref(() => Expr!)
        from close in TemplateClose
        select expr;

    private static TextParser<IReadOnlyList<string>> ForVariables { get; } =
        from first in Identifier
        from second in (from comma in Symbol(Comma) from name in Identifier select name).OptionalOrDefault()
        select second == null ? (IReadOnlyList<string>)[first] : [first, second];

    private static TextParser<HclTemplateDirective> Directive { get; } =
        from strip in StripMarker
        from keyword in Identifier
        from directive in DirectiveBody(keyword)
        from close in TemplateClose
        select directive;

    private static TextParser<HclTemplateDirective> DirectiveBody(string keyword) => keyword switch
    {
        KeywordIf => Parse.Ref(() => Expr!).Select(e => new HclTemplateDirective(HclDirectiveKind.If, e, [])),
        KeywordElse => Parse.Return(new HclTemplateDirective(HclDirectiveKind.Else, null, [])),
        KeywordEndIf => Parse.Return(new HclTemplateDirective(HclDirectiveKind.EndIf, null, [])),
        KeywordEndFor => Parse.Return(new HclTemplateDirective(HclDirectiveKind.EndFor, null, [])),
        KeywordFor =>
            from vars in ForVariables
            from kwIn in Keyword(KeywordIn)
            from collection in Parse.Ref(() => Expr!)
            select new HclTemplateDirective(HclDirectiveKind.For, collection, vars),
        _ => input => Result.Empty<HclTemplateDirective>(input, ExpectedDirective)
    };

    // Reads template content up to the closing quote (quoted) or the end of the span (heredoc).
    // Quoted strings decode backslash escapes and may not span lines; heredocs are taken verbatim.
    private static Result<List<HclTemplatePart>> TemplateParts(TextSpan input, bool quoted)
    {
        var parts = new List<HclTemplatePart>();
        var text = new StringBuilder();
        var rem = input;

        void FlushText()
        {
            if (text.Length == 0) return;
            parts.Add(new HclTemplateText(text.ToString()));
            text.Clear();
        }

        while (true)
        {
            if (rem.IsAtEnd)
            {
                if (quoted) return Result.Empty<List<HclTemplatePart>>(rem, ExpectedClosingQuote);
                FlushText();
                return Result.Value(parts, input, rem);
            }

            if (StartsWith(rem, EscapedInterpolationOpen) || StartsWith(rem, EscapedDirectiveOpen))
            {
                // "$${" / "%%{" are the literal text "${" / "%{".
                text.Append(rem.Source![rem.Position.Absolute]).Append('{');
                rem = rem.Skip(EscapedInterpolationOpen.Length);
                continue;
            }

            if (StartsWith(rem, InterpolationOpen))
            {
                FlushText();
                var interp = Interpolation(rem.Skip(InterpolationOpen.Length));
                if (!interp.HasValue) return Result.CastEmpty<HclExpr, List<HclTemplatePart>>(interp);
                parts.Add(new HclTemplateInterpolation(interp.Value));
                rem = interp.Remainder;
                continue;
            }

            if (StartsWith(rem, DirectiveOpen))
            {
                FlushText();
                var directive = Directive(rem.Skip(DirectiveOpen.Length));
                if (!directive.HasValue) return Result.CastEmpty<HclTemplateDirective, List<HclTemplatePart>>(directive);
                parts.Add(directive.Value);
                rem = directive.Remainder;
                continue;
            }

            var c = rem.ConsumeChar();
            if (quoted)
            {
                if (c.Value == '"')
                {
                    FlushText();
                    return Result.Value(parts, input, c.Remainder);
                }
                if (c.Value == '\n')
                {
                    return Result.Empty<List<HclTemplatePart>>(rem, ExpectedClosingQuote);
                }
                if (c.Value == '\\')
                {
                    rem = ReadEscape(c.Remainder, text);
                    continue;
                }
            }

            text.Append(c.Value);
            rem = c.Remainder;
        }
    }

    // Decodes one escape sequence after a backslash; unknown escapes are kept as written.
    private static TextSpan ReadEscape(TextSpan input, StringBuilder text)
    {
        var e = input.ConsumeChar();
        if (!e.HasValue)
        {
            text.Append('\\');
            return input;
        }

        switch (e.Value)
        {
            case 'n': text.Append('\n'); return e.Remainder;
            case 'r': text.Append('\r'); return e.Remainder;
            case 't': text.Append('\t'); return e.Remainder;
            case '"': text.Append('"'); return e.Remainder;
            case '\\': text.Append('\\'); return e.Remainder;
            case 'u' or 'U':
                var digits = e.Value == 'u' ? 4 : 8;
                if (e.Remainder.Length >= digits
                    && int.TryParse(e.Remainder.First(digits).ToStringValue(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)
                    && code is >= 0 and <= 0x10FFFF)
                {
                    text.Append(char.ConvertFromUtf32(code));
                    return e.Remainder.Skip(digits);
                }
                break;
        }

        text.Append('\\').Append(e.Value);
        return e.Remainder;
    }

    private static Result<HclExpr> QuotedTemplateAt(TextSpan input)
    {
        var open = input.ConsumeChar();
        if (!open.HasValue || open.Value != '"') return Result.Empty<HclExpr>(input, ExpectedClosingQuote);

        var parts = TemplateParts(open.Remainder, quoted: true);
        return parts.HasValue
            ? Result.Value<HclExpr>(new HclTemplate(parts.Value, input.Position.Line), input, parts.Remainder)
            : Result.CastEmpty<List<HclTemplatePart>, HclExpr>(parts);
    }

    // <<EOT / <<-EOT heredoc: the body runs until a line consisting only of the delimiter.
    private static Result<HclExpr> HeredocAt(TextSpan input)
    {
        if (!StartsWith(input, HeredocOpen)) return Result.Empty<HclExpr>(input, HeredocOpen);

        var src = input.Source!;
        var start = input.Position.Absolute;
        var end = start + input.Length;
        var i = start + HeredocOpen.Length;
        if (i < end && src[i] == '-') i++;

        var delimiterStart = i;
        if (i >= end || !IsIdentifierStart(src[i])) return Result.Empty<HclExpr>(input, ExpectedHeredocDelimiter);
        while (i < end && IsIdentifierPart(src[i])) i++;
        var delimiter = src[delimiterStart..i];

        while (i < end && src[i] is ' ' or '\t' or '\r') i++;
        if (i >= end || src[i] != '\n') return Result.Empty<HclExpr>(input.Skip(i - start), ExpectedHeredocNewline);

        var bodyStart = i + 1;
        for (var lineStart = bodyStart; lineStart < end;)
        {
            var newline = src.IndexOf('\n', lineStart, end - lineStart);
            var lineEnd = newline < 0 ? end : newline;

            if (src.AsSpan(lineStart, lineEnd - lineStart).Trim().Equals(delimiter, StringComparison.Ordinal))
            {
                var body = input.Skip(bodyStart - start).First(lineStart - bodyStart);
                var parts = TemplateParts(body, quoted: false);
                if (!parts.HasValue) return Result.CastEmpty<List<HclTemplatePart>, HclExpr>(parts);
                return Result.Value<HclExpr>(new HclTemplate(parts.Value, input.Position.Line), input, input.Skip(lineEnd - start));
            }

            lineStart = lineEnd + 1;
        }

        return Result.Empty<HclExpr>(input.Skip(bodyStart - start), ExpectedHeredocClose);
    }

    /// <summary>A quoted string template.</summary>
    public static TextParser<HclExpr> QuotedTemplate { get; } = Lead<HclExpr>(QuotedTemplateAt);

    private static TextParser<HclExpr> Heredoc { get; } = Lead<HclExpr>(HeredocAt);

    // ---- Expressions ----------------------------------------------------------------------------

    private static TextParser<HclExpr> NumberLiteral { get; } =
        Token(NumberSpan).Select(s => (HclExpr)new HclLiteral(HclLiteralKind.Number, s.ToStringValue(), s.Position.Line));

    private static TextParser<HclExpr> Parenthesized { get; } =
        from open in Symbol(OpenParen)
        from expr in Parse.Ref(() => Expr!)
        from close in Symbol(CloseParen)
        select expr;

    private static TextParser<HclExpr[]> TupleItems { get; } =
        Parse.Ref(() => Expr!)
            .Then(e => Symbol(Comma).Optional().Value(e))
            .Many();

    private static TextParser<(string? Key, string Value, HclExpr Collection)> ForIntro { get; } =
        from kwFor in Keyword(KeywordFor)
        from vars in ForVariables
        from kwIn in Keyword(KeywordIn)
        from collection in Parse.Ref(() => Expr!)
        from colon in Op(ColonOp)
        select (vars.Count == 2 ? vars[0] : null, vars[^1], collection);

    private static TextParser<HclExpr?> ForCondition { get; } =
        (from kwIf in Keyword(KeywordIf)
         from condition in Parse.Ref(() => Expr!)
         select condition).OptionalOrDefault();

    private static TextParser<HclExpr> ForTupleBody(int line) =>
        from intro in ForIntro
        from value in Parse.Ref(() => Expr!)
        from condition in ForCondition
        select (HclExpr)new HclFor(intro.Key, intro.Value, intro.Collection, null, value, condition, line);

    private static TextParser<HclExpr> ForObjectBody(int line) =>
        from intro in ForIntro
        from key in Parse.Ref(() => Expr!)
        from arrow in Op(Arrow)
        from value in Parse.Ref(() => Expr!)
        from grouping in Op(Ellipsis).Optional()
        from condition in ForCondition
        select (HclExpr)new HclFor(intro.Key, intro.Value, intro.Collection, key, value, condition, line);

    private static TextParser<HclExpr> Bracketed { get; } =
        from open in Symbol(OpenBracket)
        from expr in ForTupleBody(open.Position.Line)
            .Or(TupleItems.Select(items => (HclExpr)new HclTuple(items, open.Position.Line)))
        from close in Symbol(CloseBracket)
        select expr;

    // An object key: a bare identifier is a literal name; anything else is an expression.
    private static TextParser<(string? Name, HclExpr? Expression)> ObjectKey { get; } =
        Parenthesized.Or(QuotedTemplate).Or(NumberLiteral).Select(e => ((string?)null, (HclExpr?)e))
            .Or(Identifier.Select(name => ((string?)name, (HclExpr?)null)));

    private static TextParser<HclObjectItem> ObjectItem { get; } =
        from key in ObjectKey
        from separator in AssignToken.Or(Op(ColonOp))
        from value in Parse.Ref(() => Expr!)
        from comma in Symbol(Comma).Optional()
        select new HclObjectItem(key.Name, key.Expression, value);

    private static TextParser<HclExpr> Braced { get; } =
        from open in Symbol(OpenBrace)
        from expr in ForObjectBody(open.Position.Line)
            .Or(ObjectItem.Many().Select(items => (HclExpr)new HclObject(items, open.Position.Line)))
        from close in Symbol(CloseBrace)
        select expr;

    private static TextParser<HclExpr[]> CallArguments { get; } =
        from open in SameLine(Span.EqualTo(OpenParen))
        from args in Parse.Ref(() => Expr!)
            .Then(e => Op(Ellipsis).Optional().IgnoreThen(Symbol(Comma).Optional()).Value(e))
            .Many()
        from close in Symbol(CloseParen)
        select args;

    // An identifier-led expression: a function call (optionally provider::namespaced), a literal
    // keyword, or a variable at the root of a traversal.
    private static TextParser<HclExpr> IdentifierLed { get; } =
        from head in IdentifierToken
        from qualifiers in (from colons in Op(DoubleColon) from part in Identifier select part).Many()
        from args in CallArguments.Select(a => (HclExpr[]?)a).OptionalOrDefault()
        select BuildIdentifierLed(head, qualifiers, args);

    private static HclExpr BuildIdentifierLed(TextSpan head, string[] qualifiers, HclExpr[]? args)
    {
        var line = head.Position.Line;
        var name = qualifiers.Length == 0
            ? head.ToStringValue()
            : string.Join(DoubleColon, qualifiers.Prepend(head.ToStringValue()));

        if (args != null) return new HclCall(name, args, line);

        return name switch
        {
            KeywordTrue or KeywordFalse => new HclLiteral(HclLiteralKind.Bool, name, line),
            KeywordNull => new HclLiteral(HclLiteralKind.Null, name, line),
            _ => new HclVariable(name, line)
        };
    }

    private static TextParser<HclExpr> Atomic { get; } =
        NumberLiteral
            .Or(QuotedTemplate)
            .Or(Heredoc)
            .Or(Parenthesized)
            .Or(Bracketed)
            .Or(Braced)
            .Or(IdentifierLed);

    // `.name`, `.0` (legacy index), `.*`, `[key]`, `[*]`.
    private static TextParser<Func<HclExpr, HclExpr>> Postfix { get; } =
        (from dot in Op(DotOp, DotOp)
         from segment in Identifier.Select(s => (string?)s)
             .Or(Token(Span.MatchedBy(Digits)).Select(s => (string?)s.ToStringValue()))
             .Or(Symbol(Star).Select(_ => (string?)null))
         select (Func<HclExpr, HclExpr>)(target => segment == null
             ? new HclSplat(target, target.Line)
             : new HclGetAttr(target, segment, target.Line)))
        .Or(from open in SameLine(Span.EqualTo(OpenBracket))
            from key in Symbol(Star).Select(_ => (HclExpr?)null).Or(Parse.Ref(() => Expr!).Select(e => (HclExpr?)e))
            from close in Symbol(CloseBracket)
            select (Func<HclExpr, HclExpr>)(target => key == null
                ? new HclSplat(target, target.Line)
                : new HclIndex(target, key, target.Line)));

    private static TextParser<HclExpr> Traversal { get; } =
        from head in Atomic
        from steps in Postfix.Many()
        select steps.Aggregate(head, (expr, step) => step(expr));

    private static TextParser<HclExpr> Unary { get; } =
        from ops in Op(NotOp, NotAfterNot).Or(Op(MinusOp)).Many()
        from operand in Traversal
        select Enumerable.Reverse(ops).Aggregate(operand, (expr, op) => (HclExpr)new HclUnary(op.ToStringValue(), expr, op.Position.Line));

    private static TextParser<HclExpr> Binary(TextParser<TextSpan> op, TextParser<HclExpr> operand) =>
        Parse.Chain(op, operand, (o, left, right) => (HclExpr)new HclBinary(o.ToStringValue(), left, right, left.Line));

    private static TextParser<HclExpr> Multiplicative { get; } =
        Binary(Op(MultiplyOp).Or(Op(DivideOp)).Or(Op(ModuloOp)), Unary);

    private static TextParser<HclExpr> Additive { get; } =
        Binary(Op(PlusOp).Or(Op(MinusOp)), Multiplicative);

    private static TextParser<HclExpr> Comparison { get; } =
        Binary(Op(LessOrEqualOp).Or(Op(GreaterOrEqualOp)).Or(Op(LessOp, NotAfterLess)).Or(Op(GreaterOp, NotAfterGreater)), Additive);

    private static TextParser<HclExpr> Equality { get; } =
        Binary(Op(EqualOp).Or(Op(NotEqualOp)), Comparison);

    private static TextParser<HclExpr> LogicalAnd { get; } = Binary(Op(AndOp), Equality);

    private static TextParser<HclExpr> LogicalOr { get; } = Binary(Op(OrOp), LogicalAnd);

    /// <summary>A complete expression, including the <c>cond ? a : b</c> conditional.</summary>
    public static TextParser<HclExpr> Expr { get; } =
        from condition in LogicalOr
        from branches in (
            from question in Op(QuestionOp)
            from whenTrue in Parse.Ref(() => Expr!)
            from colon in Op(ColonOp)
            from whenFalse in Parse.Ref(() => Expr!)
            select (WhenTrue: whenTrue, WhenFalse: whenFalse)).Optional()
        select branches is { } b
            ? new HclConditional(condition, b.WhenTrue, b.WhenFalse, condition.Line)
            : condition;

    /// <summary>A block label: a quoted string or a bare identifier.</summary>
    public static TextParser<string> Label { get; } =
        QuotedTemplate.Select(t => ((HclTemplate)t).LiteralValue ?? string.Empty)
            .Or(Identifier);
}
