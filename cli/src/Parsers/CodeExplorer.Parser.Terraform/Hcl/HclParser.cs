using Superpower.Model;

namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// Parses an HCL file into <see cref="HclFile"/>. Attributes and block headers are read with the
/// <see cref="HclGrammar"/> parsers; this class drives the body loop so a syntax error costs only
/// the attribute or block it occurs in. The parser skips past the error, records an
/// <see cref="HclDiagnostic"/>, and continues with the next body item. It never throws on malformed
/// input.
/// </summary>
public static class HclParser
{
    private const string MissingCloseBrace = "missing '}' at end of file";
    private const string UnexpectedCloseBrace = "unexpected '}'";
    private const string ExpectedBlockOrAttribute = "expected an attribute or block";
    private const string TrailingTokens = "unexpected tokens after attribute value";
    private const string BlockCommentClose = "*/";

    private static readonly Dictionary<string, HclBlockKind> TopLevelKinds = new(StringComparer.Ordinal)
    {
        ["resource"] = HclBlockKind.Resource,
        ["data"] = HclBlockKind.Data,
        ["module"] = HclBlockKind.Module,
        ["variable"] = HclBlockKind.Variable,
        ["output"] = HclBlockKind.Output,
        ["provider"] = HclBlockKind.Provider,
        ["locals"] = HclBlockKind.Locals,
        ["terraform"] = HclBlockKind.Terraform,
        ["dependency"] = HclBlockKind.Dependency,
        ["dependencies"] = HclBlockKind.Dependencies,
        ["include"] = HclBlockKind.Include
    };

    public static HclFile Parse(string source)
    {
        var diagnostics = new List<HclDiagnostic>();
        var body = ParseBody(new TextSpan(source ?? string.Empty), nested: false, diagnostics);
        return new HclFile(body.Attributes, body.Blocks, diagnostics);
    }

    private readonly record struct Body(List<HclAttribute> Attributes, List<HclBlock> Blocks, TextSpan Remainder);

    // Parses body items until the closing '}' (nested) or end of input (top level). The returned
    // remainder is just past the closing brace.
    private static Body ParseBody(TextSpan input, bool nested, List<HclDiagnostic> diagnostics)
    {
        var attributes = new List<HclAttribute>();
        var blocks = new List<HclBlock>();
        var rem = input;

        while (true)
        {
            rem = HclGrammar.Trivia(rem).Remainder;

            if (rem.IsAtEnd)
            {
                if (nested) diagnostics.Add(new HclDiagnostic(rem.Position.Line, MissingCloseBrace));
                return new Body(attributes, blocks, rem);
            }

            var next = rem.ConsumeChar();
            if (next.Value == '}')
            {
                if (nested) return new Body(attributes, blocks, next.Remainder);
                diagnostics.Add(new HclDiagnostic(rem.Position.Line, UnexpectedCloseBrace));
                rem = next.Remainder;
                continue;
            }

            rem = ParseItem(rem, nested, attributes, blocks, diagnostics);
        }
    }

    // Parses one attribute or block starting at `start`; on a syntax error, records it and returns
    // the position after the skipped region.
    private static TextSpan ParseItem(
        TextSpan start, bool nested, List<HclAttribute> attributes, List<HclBlock> blocks, List<HclDiagnostic> diagnostics)
    {
        var name = HclGrammar.IdentifierToken(start);
        if (!name.HasValue)
        {
            diagnostics.Add(new HclDiagnostic(start.Position.Line, ExpectedBlockOrAttribute));
            return SkipJunk(start);
        }

        var line = name.Value.Position.Line;
        var assign = HclGrammar.AssignToken(name.Remainder);
        if (assign.HasValue)
        {
            var valueStart = HclGrammar.Trivia(assign.Remainder).Remainder;
            var value = HclGrammar.Expr(valueStart);
            if (!value.HasValue)
            {
                diagnostics.Add(Diagnostic(value.ErrorPosition, start, value.FormatErrorMessageFragment()));
                return Recover(start);
            }

            var valueText = valueStart.Until(value.Remainder).ToStringValue();
            attributes.Add(new HclAttribute(name.Value.ToStringValue(), value.Value, valueText, line));

            if (!AtItemEnd(value.Remainder))
            {
                diagnostics.Add(new HclDiagnostic(value.Remainder.Position.Line, TrailingTokens));
                return Recover(value.Remainder);
            }
            return value.Remainder;
        }

        var labels = new List<string>();
        var rem = name.Remainder;
        while (HclGrammar.Label(rem) is { HasValue: true } label)
        {
            labels.Add(label.Value);
            rem = label.Remainder;
        }

        var open = HclGrammar.BlockOpenToken(rem);
        if (!open.HasValue)
        {
            diagnostics.Add(Diagnostic(open.ErrorPosition, rem, open.FormatErrorMessageFragment()));
            return Recover(start);
        }

        var body = ParseBody(open.Remainder, nested: true, diagnostics);
        var keyword = name.Value.ToStringValue();
        var kind = !nested && TopLevelKinds.TryGetValue(keyword, out var k) ? k : HclBlockKind.Other;
        blocks.Add(new HclBlock(kind, keyword, labels, body.Attributes, body.Blocks, line));
        return body.Remainder;
    }

    private static HclDiagnostic Diagnostic(Position errorPosition, TextSpan fallback, string message) =>
        new(errorPosition.HasValue ? errorPosition.Line : fallback.Position.Line, message);

    // An attribute must be followed by a line break, a comment, a closing brace, or end of input.
    private static bool AtItemEnd(TextSpan rem)
    {
        var src = rem.Source!;
        var end = rem.Position.Absolute + rem.Length;
        var i = rem.Position.Absolute;
        while (i < end && src[i] is ' ' or '\t' or '\r') i++;
        if (i >= end) return true;
        return src[i] switch
        {
            '\n' or '}' or '#' => true,
            '/' => i + 1 < end && src[i + 1] is '/' or '*',
            _ => false
        };
    }

    /// <summary>
    /// Skips a malformed body item: everything up to the end of the line on which all brackets
    /// opened since <paramref name="from"/> are closed, or up to (not past) a '}' that closes the
    /// enclosing block. Strings, comments, and heredocs are skipped whole so brackets inside them
    /// are not counted. Always advances at least one character.
    /// </summary>
    private static TextSpan Recover(TextSpan from)
    {
        var src = from.Source!;
        var start = from.Position.Absolute;
        var end = start + from.Length;
        var depth = 0;
        var i = start;

        while (i < end)
        {
            var c = src[i];
            if (c == '\n' && depth == 0) { i++; break; }

            switch (c)
            {
                case '"':
                    i = SkipQuoted(src, i + 1, end);
                    continue;
                case '#':
                    i = SkipToLineEnd(src, i, end);
                    continue;
                case '/' when i + 1 < end && src[i + 1] == '/':
                    i = SkipToLineEnd(src, i, end);
                    continue;
                case '/' when i + 1 < end && src[i + 1] == '*':
                    var close = src.IndexOf(BlockCommentClose, i + 2, end - i - 2, StringComparison.Ordinal);
                    i = close < 0 ? end : close + 2;
                    continue;
                case '<' when i + 1 < end && src[i + 1] == '<':
                    i = SkipHeredoc(src, i, end);
                    continue;
                case '{' or '[' or '(':
                    depth++;
                    break;
                case '}' when depth == 0:
                    return Advance(from, Math.Max(i, start + 1));
                case '}' or ']' or ')':
                    depth = Math.Max(0, depth - 1);
                    break;
            }
            i++;
        }

        return Advance(from, Math.Max(i, start + 1));
    }

    // An item that does not start with an identifier: skip only the characters before the next
    // identifier, whitespace, or '}', so a stray character does not cost the block after it.
    // Strings and multi-line constructs fall back to Recover.
    private static TextSpan SkipJunk(TextSpan from)
    {
        var src = from.Source!;
        var start = from.Position.Absolute;
        var end = start + from.Length;
        var i = start;
        while (i < end && !char.IsWhiteSpace(src[i]) && !char.IsLetter(src[i]) && src[i] != '_' && src[i] != '}')
        {
            if (src[i] is '"' or '{' or '[' or '(' or '<' or '#' or '/') return Recover(from);
            i++;
        }
        return Advance(from, Math.Max(i, start + 1));
    }

    private static TextSpan Advance(TextSpan from, int absolute) =>
        from.Skip(Math.Min(absolute, from.Position.Absolute + from.Length) - from.Position.Absolute);

    private static int SkipQuoted(string src, int i, int end)
    {
        while (i < end && src[i] != '"' && src[i] != '\n')
        {
            i += src[i] == '\\' ? 2 : 1;
        }
        return Math.Min(end, i < end && src[i] == '"' ? i + 1 : i);
    }

    private static int SkipToLineEnd(string src, int i, int end)
    {
        var newline = src.IndexOf('\n', i, end - i);
        return newline < 0 ? end : newline;
    }

    // Skips `<<[-]DELIM` through its closing delimiter line; if unterminated, only the `<<`.
    private static int SkipHeredoc(string src, int i, int end)
    {
        var j = i + 2;
        if (j < end && src[j] == '-') j++;
        var delimiterStart = j;
        while (j < end && (char.IsLetterOrDigit(src[j]) || src[j] == '_')) j++;
        if (j == delimiterStart) return i + 2;

        var delimiter = src[delimiterStart..j];
        var lineStart = SkipToLineEnd(src, j, end) + 1;
        while (lineStart < end)
        {
            var lineEnd = SkipToLineEnd(src, lineStart, end);
            if (src.AsSpan(lineStart, lineEnd - lineStart).Trim().Equals(delimiter, StringComparison.Ordinal))
            {
                return lineEnd;
            }
            lineStart = lineEnd + 1;
        }
        return i + 2;
    }
}
