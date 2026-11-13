namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// HCL tokenizer. Resilient: on malformed input, emit an <see cref="HclTokenKind.Unknown"/>
/// token and continue rather than throwing — indexing must never fail a whole scan on one bad file.
///
/// Comments (# // and /* */) and insignificant whitespace are consumed and not emitted.
/// Newlines ARE emitted (HCL is newline-sensitive between attributes), but runs of blank
/// lines collapse to a single Newline token to simplify the parser.
/// </summary>
public sealed class HclLexer
{
    private readonly string _src;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public HclLexer(string src) => _src = src ?? string.Empty;

    private bool Eof => _pos >= _src.Length;
    private char Cur => _pos < _src.Length ? _src[_pos] : '\0';
    private char Peek(int ahead = 1) => _pos + ahead < _src.Length ? _src[_pos + ahead] : '\0';

    private void Advance()
    {
        if (Eof) return;
        if (_src[_pos] == '\n') { _line++; _col = 1; }
        else { _col++; }
        _pos++;
    }

    public IReadOnlyList<HclToken> Tokenize()
    {
        var tokens = new List<HclToken>();
        var lastWasNewline = false;

        while (!Eof)
        {
            var c = Cur;

            // Whitespace (except newline) — skip.
            if (c is ' ' or '\t' or '\r')
            {
                Advance();
                continue;
            }

            // Newline — significant, but collapse consecutive ones.
            if (c == '\n')
            {
                var line = _line; var col = _col;
                Advance();
                if (!lastWasNewline)
                {
                    tokens.Add(new HclToken(HclTokenKind.Newline, "\n", line, col));
                    lastWasNewline = true;
                }
                continue;
            }

            // Comments — consume, do not emit.
            if (c == '#' || (c == '/' && Peek() == '/'))
            {
                SkipLineComment();
                continue;
            }
            if (c == '/' && Peek() == '*')
            {
                SkipBlockComment();
                continue;
            }

            lastWasNewline = false;
            var startLine = _line;
            var startCol = _col;

            // Heredoc: <<EOT ... EOT  or  <<-EOT ... EOT
            if (c == '<' && Peek() == '<')
            {
                tokens.Add(ReadHeredoc(startLine, startCol));
                continue;
            }

            // String literal.
            if (c == '"')
            {
                tokens.Add(ReadString(startLine, startCol));
                continue;
            }

            // Number.
            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(startLine, startCol));
                continue;
            }

            // Identifier / keyword.
            if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifier(startLine, startCol));
                continue;
            }

            // Punctuation.
            var punct = c switch
            {
                '{' => HclTokenKind.LBrace,
                '}' => HclTokenKind.RBrace,
                '[' => HclTokenKind.LBracket,
                ']' => HclTokenKind.RBracket,
                '(' => HclTokenKind.LParen,
                ')' => HclTokenKind.RParen,
                '=' when Peek() != '=' => HclTokenKind.Equals,
                '.' => HclTokenKind.Dot,
                ',' => HclTokenKind.Comma,
                ':' => HclTokenKind.Colon,
                '*' => HclTokenKind.Star,
                // Other operators lex as Unknown (unused by reference harvesting).
                _ => HclTokenKind.Unknown
            };

            tokens.Add(new HclToken(punct, c.ToString(), startLine, startCol));
            Advance();
        }

        tokens.Add(new HclToken(HclTokenKind.Eof, string.Empty, _line, _col));
        return tokens;
    }

    private void SkipLineComment()
    {
        while (!Eof && Cur != '\n') Advance();
    }

    private void SkipBlockComment()
    {
        Advance(); // '/'
        Advance(); // '*'
        while (!Eof)
        {
            if (Cur == '*' && Peek() == '/')
            {
                Advance(); // '*'
                Advance(); // '/'
                return;
            }
            Advance();
        }
        // Unterminated block comment: tolerated (reached EOF).
    }

    private HclToken ReadString(int line, int col)
    {
        var sb = new System.Text.StringBuilder();
        Advance(); // opening quote

        // A `"` terminates the outer string only at interpolation depth 0; a nested string inside a
        // `${ ... }` interpolation is consumed whole.
        var interpDepth = 0;

        while (!Eof)
        {
            var c = Cur;

            if (c == '\\')
            {
                // Preserve escape sequences verbatim; value text isn't re-interpreted.
                sb.Append(c);
                Advance();
                if (!Eof) { sb.Append(Cur); Advance(); }
                continue;
            }

            // Closing quote only at interpolation depth 0.
            if (c == '"' && interpDepth == 0)
            {
                break;
            }

            if (c == '\n')
            {
                // Unterminated string: stop at newline, stay resilient.
                break;
            }

            // `$$` / `%%` escape a literal `${` / `%{`: emit both chars and do not open an
            // interpolation.
            if ((c == '$' || c == '%') && Peek() == c)
            {
                sb.Append(c);
                Advance();
                sb.Append(Cur);
                Advance();
                continue;
            }

            // Enter an interpolation on "${".
            if (c == '$' && Peek() == '{')
            {
                interpDepth++;
                sb.Append(c);          // '$'
                Advance();
                sb.Append(Cur);        // '{'
                Advance();
                continue;
            }

            if (interpDepth > 0)
            {
                if (c == '{')
                {
                    interpDepth++;
                }
                else if (c == '}')
                {
                    interpDepth--;
                }
                else if (c == '"')
                {
                    // A nested string inside the interpolation — consume it whole (with escapes)
                    // so its content (including '}' and '"') does not affect interpolation depth.
                    sb.Append(c);
                    Advance();
                    while (!Eof && Cur != '"')
                    {
                        if (Cur == '\\')
                        {
                            sb.Append(Cur);
                            Advance();
                            if (!Eof) { sb.Append(Cur); Advance(); }
                            continue;
                        }
                        if (Cur == '\n') break; // resilience: don't run past a line
                        sb.Append(Cur);
                        Advance();
                    }
                    if (!Eof && Cur == '"') { sb.Append(Cur); Advance(); }
                    continue;
                }
            }

            sb.Append(c);
            Advance();
        }

        if (!Eof && Cur == '"') Advance(); // closing quote
        return new HclToken(HclTokenKind.String, sb.ToString(), line, col);
    }

    private HclToken ReadHeredoc(int line, int col)
    {
        Advance(); // '<'
        Advance(); // '<'
        if (!Eof && Cur == '-') Advance(); // <<- indented form

        // Read the delimiter identifier (e.g. EOT, EOF).
        var delim = new System.Text.StringBuilder();
        while (!Eof && (char.IsLetterOrDigit(Cur) || Cur == '_'))
        {
            delim.Append(Cur);
            Advance();
        }
        var delimiter = delim.ToString();

        // Consume to end of the opening line.
        while (!Eof && Cur != '\n') Advance();
        if (!Eof) Advance(); // the newline

        var body = new System.Text.StringBuilder();
        if (delimiter.Length == 0)
        {
            return new HclToken(HclTokenKind.Heredoc, string.Empty, line, col);
        }

        // Read lines until one that (trimmed) equals the delimiter.
        while (!Eof)
        {
            var lineStart = _pos;
            var sb = new System.Text.StringBuilder();
            while (!Eof && Cur != '\n')
            {
                sb.Append(Cur);
                Advance();
            }
            if (!Eof) Advance(); // consume newline

            if (sb.ToString().Trim() == delimiter)
            {
                break;
            }
            body.Append(sb);
            body.Append('\n');

            // Break if a line read made no progress (forward-progress guard).
            if (_pos == lineStart) break;
        }

        return new HclToken(HclTokenKind.Heredoc, body.ToString(), line, col);
    }

    private HclToken ReadNumber(int line, int col)
    {
        var sb = new System.Text.StringBuilder();

        // Integer part.
        while (!Eof && char.IsDigit(Cur)) { sb.Append(Cur); Advance(); }

        // At most one decimal point, and only when followed by a digit.
        if (!Eof && Cur == '.' && char.IsDigit(Peek()))
        {
            sb.Append(Cur); Advance();
            while (!Eof && char.IsDigit(Cur)) { sb.Append(Cur); Advance(); }
        }

        // Optional exponent: e/E, an optional sign, then digits.
        if (!Eof && (Cur == 'e' || Cur == 'E'))
        {
            var sign = Peek();
            var hasSign = sign is '+' or '-';
            var digitAt = hasSign ? Peek(2) : sign;
            if (char.IsDigit(digitAt))
            {
                sb.Append(Cur); Advance();            // e/E
                if (hasSign) { sb.Append(Cur); Advance(); }
                while (!Eof && char.IsDigit(Cur)) { sb.Append(Cur); Advance(); }
            }
        }

        return new HclToken(HclTokenKind.Number, sb.ToString(), line, col);
    }

    private HclToken ReadIdentifier(int line, int col)
    {
        var sb = new System.Text.StringBuilder();
        // '-' is a valid HCL identifier char.
        while (!Eof && (char.IsLetterOrDigit(Cur) || Cur == '_' || Cur == '-'))
        {
            sb.Append(Cur);
            Advance();
        }
        var text = sb.ToString();
        var kind = text switch
        {
            "true" or "false" => HclTokenKind.Bool,
            "null" => HclTokenKind.Null,
            _ => HclTokenKind.Identifier
        };
        return new HclToken(kind, text, line, col);
    }
}

/// <summary>A lexed token: kind, source text, and 1-indexed position.</summary>
public readonly record struct HclToken(HclTokenKind Kind, string Text, int Line, int Column);

public enum HclTokenKind
{
    Identifier, String, Number, Bool, Null,
    LBrace, RBrace, LBracket, RBracket, LParen, RParen,
    Equals, Dot, Comma, Colon, Star, Newline, Heredoc, Comment, Eof, Unknown
}
