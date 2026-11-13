namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// Recursive-descent HCL parser.
/// Grammar (simplified):
///   file       := (block | attribute | NEWLINE)*
///   block      := IDENT (STRING | IDENT)* '{' body '}'
///   attribute  := IDENT '=' expr
///   body       := (block | attribute | NEWLINE)*
///
/// Expressions are not fully evaluated. For each attribute value and nested-block
/// attribute we extract any traversal references (var.X, local.X, module.X[.Y],
/// data.T.N, dependency.X.outputs.Y, &lt;resource_type&gt;.N) — including those inside
/// "${ ... }" string interpolations — and record them as flat <see cref="HclReference"/>s.
///
/// Resilience: on an unexpected token, skip to the next NEWLINE or balance a brace and
/// continue; never throw past a file (indexing must not fail a scan on one bad file).
/// </summary>
public sealed class HclParser
{
    private readonly IReadOnlyList<HclToken> _tokens;
    private int _pos;
    private readonly List<HclReference> _references = [];

    // Ref-key of the top-level block currently being parsed (e.g. "aws_security_group.internal"),
    // or null at file top level. References harvested inside the block are anchored to it.
    private string? _enclosingKey;

    // True while parsing the value of a `depends_on` meta-argument, so traversals harvested there
    // are tagged as explicit ordering dependencies rather than implicit data references.
    private bool _inDependsOn;

    public HclParser(IReadOnlyList<HclToken> tokens) => _tokens = tokens;

    private HclToken Cur => _tokens[Math.Min(_pos, _tokens.Count - 1)];
    private HclToken Peek(int ahead = 1)
    {
        var i = _pos + ahead;
        return i < _tokens.Count ? _tokens[i] : _tokens[^1];
    }
    private bool Eof => Cur.Kind == HclTokenKind.Eof;
    private void Next() { if (_pos < _tokens.Count - 1) _pos++; }

    private void SkipNewlines()
    {
        while (Cur.Kind == HclTokenKind.Newline) Next();
    }

    public HclFile ParseFile()
    {
        var blocks = new List<HclBlock>();
        var topAttrs = new List<HclAttribute>();

        var guard = 0;
        while (!Eof)
        {
            if (++guard > _tokens.Count + 16) break; // hard safety against any non-advancing loop

            SkipNewlines();
            if (Eof) break;

            if (Cur.Kind == HclTokenKind.Identifier)
            {
                // Decide block vs attribute by what follows the identifier (and its labels).
                if (LooksLikeAttribute())
                {
                    var attr = ParseAttribute();
                    if (attr != null) topAttrs.Add(attr);
                }
                else
                {
                    var block = ParseBlock();
                    if (block != null) blocks.Add(block);
                    else RecoverToNextLine();
                }
            }
            else
            {
                // Unexpected at top level — recover.
                RecoverToNextLine();
            }
        }

        return new HclFile(blocks, topAttrs, _references);
    }

    /// <summary>True if the current IDENT begins an `IDENT = ...` attribute (not a block).</summary>
    private bool LooksLikeAttribute()
    {
        // current is Identifier; look at the very next token
        return Peek().Kind == HclTokenKind.Equals;
    }

    private HclBlock? ParseBlock()
    {
        if (Cur.Kind != HclTokenKind.Identifier) return null;
        var type = Cur.Text;
        var line = Cur.Line;
        Next();

        // Labels: zero or more STRING or bare IDENT before the opening brace.
        var labels = new List<string>();
        while (Cur.Kind is HclTokenKind.String or HclTokenKind.Identifier)
        {
            labels.Add(Cur.Text);
            Next();
        }

        if (Cur.Kind != HclTokenKind.LBrace)
        {
            // Not actually a block — recover.
            return null;
        }
        Next(); // consume '{'

        // For a top-level block, set the enclosing ref-key so references inside it (including in
        // nested blocks) are anchored to this declaration. Nested blocks inherit it unchanged.
        var previousEnclosing = _enclosingKey;
        if (_enclosingKey == null)
        {
            _enclosingKey = EnclosingKeyFor(type, labels);
        }

        var (attrs, nested) = ParseBody();

        _enclosingKey = previousEnclosing;
        return new HclBlock(type, labels, attrs, nested, line);
    }

    // Computes the ref-key a top-level block is referenced by, matching the declaration mapper:
    //   resource "T" "N" -> "T.N";  data "T" "N" -> "data.T.N";  module/provider/dependency "N"
    //   -> "<type>.N";  output "N" -> "output.N";  variable "N" -> "var.N".  `output` and
    //   `variable` each wrap a single declaration node, so references in their values anchor to it.
    //   `locals` (one block, many members) and blocks that declare nothing addressable return null,
    //   so their inner references anchor to the file.
    private static string? EnclosingKeyFor(string type, IReadOnlyList<string> labels)
    {
        switch (type)
        {
            case "resource":
                return labels.Count >= 2 ? $"{labels[0]}.{labels[1]}" : null;
            case "data":
                return labels.Count >= 2 ? $"data.{labels[0]}.{labels[1]}" : null;
            case "module":
            case "provider":
            case "dependency":
                return labels.Count >= 1 ? $"{type}.{labels[0]}" : null;
            case "output":
                return labels.Count >= 1 ? $"output.{labels[0]}" : null;
            case "variable":
                return labels.Count >= 1 ? $"var.{labels[0]}" : null;
            default:
                return null;
        }
    }

    private (List<HclAttribute> Attrs, List<HclBlock> Nested) ParseBody()
    {
        var attrs = new List<HclAttribute>();
        var nested = new List<HclBlock>();

        var guard = 0;
        while (!Eof && Cur.Kind != HclTokenKind.RBrace)
        {
            if (++guard > _tokens.Count + 16) break;

            SkipNewlines();
            if (Eof || Cur.Kind == HclTokenKind.RBrace) break;

            if (Cur.Kind == HclTokenKind.Identifier)
            {
                if (LooksLikeAttribute())
                {
                    var attr = ParseAttribute();
                    if (attr != null) attrs.Add(attr);
                }
                else
                {
                    var before = _pos;
                    var block = ParseBlock();
                    if (block != null) nested.Add(block);
                    else if (_pos == before) Next(); // ensure progress
                }
            }
            else
            {
                // Unknown construct in body — skip one token to stay resilient.
                Next();
            }
        }

        if (Cur.Kind == HclTokenKind.RBrace) Next(); // consume '}'
        return (attrs, nested);
    }

    private HclAttribute? ParseAttribute()
    {
        var name = Cur.Text;
        var line = Cur.Line;
        Next();                 // identifier
        if (Cur.Kind != HclTokenKind.Equals) return null;
        Next();                 // '='

        _inDependsOn = string.Equals(name, "depends_on", StringComparison.Ordinal);
        var expr = ParseExpressionValue(line);
        _inDependsOn = false;
        return new HclAttribute(name, expr, line);
    }

    /// <summary>
    /// Consume an attribute value until the terminating NEWLINE (respecting bracket/brace/paren
    /// nesting), building a coarse <see cref="HclExpr"/> and harvesting traversal references.
    /// </summary>
    private HclExpr ParseExpressionValue(int line)
    {
        var startTok = Cur;
        var textParts = new List<string>();
        var depth = 0;
        var isObject = startTok.Kind == HclTokenKind.LBrace;
        var isTuple = startTok.Kind == HclTokenKind.LBracket;
        // Top-level keys of an object literal: identifiers at brace-depth 1 followed by '=' (or ':').
        List<string>? objectKeys = isObject ? [] : null;
        // Top-level string elements of a tuple literal (e.g. the entries of paths = ["../a","../b"]).
        List<string>? tupleItems = isTuple ? [] : null;
        // True when the next token begins a new object element (after '{' or a depth-1 ',').
        var atObjectElementStart = true;

        while (!Eof)
        {
            var k = Cur.Kind;
            if (depth == 0 && (k == HclTokenKind.Newline || k == HclTokenKind.RBrace || k == HclTokenKind.Comma))
            {
                break;
            }

            switch (k)
            {
                case HclTokenKind.LBrace or HclTokenKind.LBracket or HclTokenKind.LParen:
                    depth++;
                    if (isObject && depth == 1) atObjectElementStart = true;
                    break;
                case HclTokenKind.RBrace or HclTokenKind.RBracket or HclTokenKind.RParen:
                    depth--;
                    if (depth < 0) depth = 0;
                    break;
                case HclTokenKind.String or HclTokenKind.Heredoc:
                    // A string element directly inside the tuple (bracket-depth 1) is a list item.
                    if (tupleItems != null && depth == 1)
                    {
                        tupleItems.Add(Cur.Text);
                    }
                    HarvestInterpolations(Cur.Text, Cur.Line);
                    if (depth == 1) atObjectElementStart = false;
                    break;
                case HclTokenKind.Identifier:
                    // Only an element-leading identifier before '=' or ':' is a key.
                    if (objectKeys != null && depth == 1 && atObjectElementStart &&
                        (Peek().Kind == HclTokenKind.Equals || Peek().Kind == HclTokenKind.Colon))
                    {
                        objectKeys.Add(Cur.Text);
                    }
                    if (depth == 1) atObjectElementStart = false;
                    // Capture IDENT ('.' IDENT | '[' .. ']')* as one unit and advance the cursor
                    // past the whole traversal, so interior segments are not examined again.
                    var travEnd = TryHarvestTraversalFromTokens();
                    while (_pos < travEnd)
                    {
                        textParts.Add(Cur.Text);
                        Next();
                    }
                    continue;
                case HclTokenKind.Comma or HclTokenKind.Newline:
                    // Both separate object elements at depth 1; the next identifier starts a key.
                    if (depth == 1) atObjectElementStart = true;
                    break;
                default:
                    if (depth == 1) atObjectElementStart = false;
                    break;
            }

            textParts.Add(Cur.Text);
            Next();
        }

        var literal = JoinExprText(textParts);
        var kind = startTok.Kind switch
        {
            HclTokenKind.String or HclTokenKind.Heredoc => HclExprKind.Template,
            HclTokenKind.Number or HclTokenKind.Bool or HclTokenKind.Null => HclExprKind.Literal,
            HclTokenKind.LBracket => HclExprKind.Tuple,
            HclTokenKind.LBrace => HclExprKind.ObjectExpr,
            _ => HclExprKind.Unknown
        };
        return new HclExpr(kind, literal, null, null, null, line,
            objectKeys is { Count: > 0 } ? objectKeys : null,
            tupleItems is { Count: > 0 } ? tupleItems : null);
    }

    /// <summary>
    /// Joins expression tokens into a display string with single spaces, EXCEPT around "." tokens
    /// so dotted traversals read naturally (e.g. "module.vpc.id" rather than "module . vpc . id").
    /// </summary>
    private static string JoinExprText(IReadOnlyList<string> parts)
    {
        var sb = new System.Text.StringBuilder();
        for (var idx = 0; idx < parts.Count; idx++)
        {
            var part = parts[idx];
            var isDot = part == ".";
            var prevIsDot = idx > 0 && parts[idx - 1] == ".";
            // No leading space before a "." or immediately after a "."; otherwise space-separate.
            if (sb.Length > 0 && !isDot && !prevIsDot)
            {
                sb.Append(' ');
            }
            sb.Append(part);
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// At an Identifier token that may start a traversal. Reads IDENT ('.' IDENT | '[' .. ']' )*
    /// and records it as a reference candidate (any dotted path, head kept as-is). Verification
    /// against declared symbols happens later in the Layer 4 enricher — no lexical filter here.
    /// Does NOT move the cursor; returns the token index just past the whole traversal so the
    /// caller can consume it as a unit (always &gt; _pos, guaranteeing forward progress).
    /// </summary>
    private int TryHarvestTraversalFromTokens()
    {
        var (path, next) = ReadTraversal(_tokens, _pos);

        if (path.Count >= 2)
        {
            _references.Add(new HclReference(path[0], path, Cur.Line, _enclosingKey, _inDependsOn));
        }

        return next;
    }

    /// <summary>
    /// Reads a traversal starting at an identifier token at <paramref name="start"/>: the head
    /// identifier followed by zero or more <c>.segment</c> (identifier or splat <c>*</c>) and
    /// <c>[ ... ]</c> index groups (bracket-balanced and skipped). Returns the dotted path
    /// (head + segments) and the index just past the whole traversal. Pure over the supplied token
    /// list, so it serves both the main parse walk and interpolation inner-text.
    /// </summary>
    private static (List<string> Path, int Next) ReadTraversal(IReadOnlyList<HclToken> tokens, int start)
    {
        var path = new List<string> { tokens[start].Text };
        var i = start + 1;
        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Kind == HclTokenKind.Dot && i + 1 < tokens.Count &&
                (tokens[i + 1].Kind == HclTokenKind.Identifier || tokens[i + 1].Kind == HclTokenKind.Star))
            {
                path.Add(tokens[i + 1].Text);
                i += 2;
                continue;
            }
            if (t.Kind == HclTokenKind.LBracket)
            {
                var d = 1; i++;
                while (i < tokens.Count && d > 0)
                {
                    if (tokens[i].Kind == HclTokenKind.LBracket) d++;
                    else if (tokens[i].Kind == HclTokenKind.RBracket) d--;
                    i++;
                }
                continue;
            }
            break;
        }
        return (path, i);
    }

    /// <summary>Extract traversals that appear inside "${ ... }" interpolations in a string/heredoc.</summary>
    private void HarvestInterpolations(string text, int line)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("${")) return;

        foreach (var inner in ExtractInterpolations(text))
        {
            // Lex the interpolation expression and harvest traversals with the same token logic the
            // main walk uses, so hyphens, index access, and splats are treated identically.
            var tokens = new HclLexer(inner).Tokenize();
            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind != HclTokenKind.Identifier) continue;

                var (path, next) = ReadTraversal(tokens, i);
                if (path.Count >= 2)
                {
                    _references.Add(new HclReference(path[0], path, line, _enclosingKey, _inDependsOn));
                }
                // Advance past the harvested traversal (next is always > i) so interior segments
                // are not re-examined; -1 compensates for the loop's own increment.
                i = next - 1;
            }
        }
    }

    /// <summary>
    /// Extracts the inner text of every `${ ... }` interpolation in <paramref name="text"/>,
    /// tracking brace nesting so that `}` characters inside the interpolation (object literals,
    /// nested interpolations, function args) do not prematurely terminate it. String literals
    /// inside the interpolation are skipped so a `"}"` within them is not counted as a brace.
    /// </summary>
    private static IEnumerable<string> ExtractInterpolations(string text)
    {
        var results = new List<string>();
        var n = text.Length;
        var p = 0;

        while (p < n - 1)
        {
            // `$$` / `%%` escape a literal `${` / `%{`: skip both chars so the escaped opener is
            // not read as an interpolation.
            if ((text[p] == '$' || text[p] == '%') && text[p + 1] == text[p])
            {
                p += 2;
                continue;
            }

            // Find the next interpolation opener "${".
            if (text[p] != '$' || text[p + 1] != '{')
            {
                p++;
                continue;
            }

            var start = p + 2;   // first char inside the interpolation
            var i = start;
            var depth = 1;       // we are inside one '{'

            while (i < n && depth > 0)
            {
                var c = text[i];
                if (c == '"')
                {
                    // Skip a string literal: advance to the matching unescaped closing quote.
                    i++;
                    while (i < n)
                    {
                        if (text[i] == '\\') { i += 2; continue; }
                        if (text[i] == '"') { i++; break; }
                        i++;
                    }
                    continue;
                }
                if (c == '{') depth++;
                else if (c == '}') depth--;

                if (depth == 0) break;
                i++;
            }

            if (depth == 0 && i <= n)
            {
                results.Add(text.Substring(start, i - start));
                p = i + 1;       // continue after the closing '}'
            }
            else
            {
                // Unterminated interpolation — take the rest and stop.
                results.Add(text.Substring(start));
                break;
            }
        }

        return results;
    }

    private void RecoverToNextLine()
    {
        var guard = 0;
        while (!Eof && Cur.Kind != HclTokenKind.Newline)
        {
            Next();
            if (++guard > _tokens.Count) break;
        }
        SkipNewlines();
    }
}
