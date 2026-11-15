using Superpower.Model;

namespace CodeExplorer.Parsers.Terraform.Hcl;

/// <summary>
/// Extracts semantic <see cref="HclReference"/> instances and string/heredoc template interpolations from HCL AST nodes.
/// </summary>
public static class HclReferenceCollector
{
    internal const string DefaultVia = "attr";
    private const string DependsOnAttrName = "depends_on";
    private const string VarPrefix = "var";
    private const string LocalPrefix = "local";
    private const string ModulePrefix = "module";
    private const string DataPrefix = "data";
    private const string DependencyPrefix = "dependency";
    private const string EachPrefix = "each";
    private const string SelfPrefix = "self";
    private const string CountPrefix = "count";
    private const string PathPrefix = "path";
    private const string TerraformPrefix = "terraform";
    private const string InterpOpen = "${";
    private const string DirectiveOpen = "%{";
    private const string ForKeyword = "for";
    private const string InKeyword = "in";
    private const string IfKeyword = "if";
    private const string ElseKeyword = "else";
    private const string EndifKeyword = "endif";
    private const string EndforKeyword = "endfor";
    private const char DotSeparator = '.';
    private const char UnderscoreChar = '_';

    private static readonly HashSet<string> NonResourceRoots = new(StringComparer.Ordinal)
    {
        "true", "false", "null",
        "for", "in", "if",
        "string", "number", "bool", "any", "list", "map", "set", "object", "tuple", "optional"
    };

    /// <summary>
    /// Collects all semantic <see cref="HclReference"/> instances from a block and its nested blocks.
    /// </summary>
    public static IReadOnlyList<HclReference> CollectReferences(this HclBlock block)
    {
        var list = new List<HclReference>();
        CollectBlockReferences(block, list);
        return list;
    }

    /// <summary>
    /// Collects all semantic <see cref="HclReference"/> instances from an expression.
    /// </summary>
    public static IReadOnlyList<HclReference> CollectReferences(this HclExpr expr, string via = DefaultVia)
    {
        var list = new List<HclReference>();
        CollectExprReferences(expr, via, list);
        return list;
    }

    internal static HclExpr ParseStringOrTemplate(Token<HclToken> token)
    {
        var unquoted = HclTokenizer.UnquoteString(token.ToStringValue());
        var line = token.Position.Line;
        if (!unquoted.Contains(InterpOpen, StringComparison.Ordinal) &&
            !unquoted.Contains(DirectiveOpen, StringComparison.Ordinal))
        {
            return new HclExpr.Literal(HclLiteralKind.String, unquoted, line);
        }

        var (interpolations, boundVars) = ExtractInterpolatedExpressions(unquoted, line);
        var refs = new List<HclReference>();
        foreach (var expr in interpolations)
        {
            CollectExprReferences(expr, DefaultVia, refs);
        }
        if (boundVars.Count > 0)
        {
            refs.RemoveAll(r => r.Kind == HclRefKind.Resource && boundVars.Contains(r.TargetScope));
        }
        return new HclExpr.Template(unquoted, interpolations, refs, line);
    }

    internal static HclExpr ParseHeredocOrTemplate(Token<HclToken> token)
    {
        var body = HclTokenizer.ExtractHeredocBody(token.ToStringValue());
        var line = token.Position.Line;
        if (!body.Contains(InterpOpen, StringComparison.Ordinal) &&
            !body.Contains(DirectiveOpen, StringComparison.Ordinal))
        {
            return new HclExpr.Literal(HclLiteralKind.Heredoc, body, line);
        }

        var (interpolations, boundVars) = ExtractInterpolatedExpressions(body, line);
        var refs = new List<HclReference>();
        foreach (var expr in interpolations)
        {
            CollectExprReferences(expr, DefaultVia, refs);
        }
        if (boundVars.Count > 0)
        {
            refs.RemoveAll(r => r.Kind == HclRefKind.Resource && boundVars.Contains(r.TargetScope));
        }
        return new HclExpr.Template(body, interpolations, refs, line);
    }

    private static (List<HclExpr> Expressions, HashSet<string> BoundVariables) ExtractInterpolatedExpressions(string text, int baseLine)
    {
        var results = new List<HclExpr>();
        var boundVars = new HashSet<string>(StringComparer.Ordinal);
        var i = 0;

        while (i < text.Length - 1)
        {
            var isInterp = text[i] == '$' && text[i + 1] == '{';
            var isDirective = text[i] == '%' && text[i + 1] == '{';
            if (isInterp || isDirective)
            {
                if (i > 0 && text[i - 1] == text[i])
                {
                    i += 2;
                    continue;
                }

                var start = i + 2;
                var depth = 1;
                var cursor = start;
                while (cursor < text.Length && depth > 0)
                {
                    var ch = text[cursor];
                    if (ch == '"')
                    {
                        cursor++;
                        while (cursor < text.Length && text[cursor] != '"')
                        {
                            if (text[cursor] == '\\') cursor++;
                            cursor++;
                        }
                    }
                    else if (ch == '{')
                    {
                        depth++;
                    }
                    else if (ch == '}')
                    {
                        depth--;
                        if (depth == 0) break;
                    }
                    cursor++;
                }

                if (cursor > start)
                {
                    var snippet = text[start..cursor].Trim();
                    if (snippet.StartsWith('~'))
                    {
                        snippet = snippet[1..].TrimStart();
                    }
                    if (snippet.EndsWith('~'))
                    {
                        snippet = snippet[..^1].TrimEnd();
                    }
                    var lineOffset = CountNewlines(text, 0, start);
                    var tokenized = HclTokenizer.Instance.TryTokenize(snippet);
                    if (tokenized.HasValue)
                    {
                        ParseAllExpressionsInSnippet(tokenized.Value, baseLine + lineOffset, isDirective, results, boundVars);
                    }
                }

                i = cursor + 1;
                continue;
            }
            i++;
        }

        return (results, boundVars);
    }

    private static int CountNewlines(string text, int start, int length)
    {
        var count = 0;
        var end = Math.Min(text.Length, start + length);
        for (var i = start; i < end; i++)
        {
            if (text[i] == '\n') count++;
        }
        return count;
    }

    private static void ParseAllExpressionsInSnippet(
        TokenList<HclToken> tokens,
        int baseLine,
        bool isDirective,
        List<HclExpr> sink,
        HashSet<string> boundVars)
    {
        var cur = tokens;
        if (isDirective && !cur.IsAtEnd)
        {
            var first = cur.ConsumeToken();
            if (first.HasValue && first.Value.Kind == HclToken.Identifier)
            {
                var kw = first.Value.ToStringValue();
                if (string.Equals(kw, ElseKeyword, StringComparison.Ordinal) ||
                    string.Equals(kw, EndifKeyword, StringComparison.Ordinal) ||
                    string.Equals(kw, EndforKeyword, StringComparison.Ordinal))
                {
                    return;
                }
                if (string.Equals(kw, IfKeyword, StringComparison.Ordinal))
                {
                    cur = first.Remainder;
                }
                else if (string.Equals(kw, ForKeyword, StringComparison.Ordinal))
                {
                    cur = first.Remainder;
                    while (!cur.IsAtEnd)
                    {
                        var step = cur.ConsumeToken();
                        if (!step.HasValue) break;
                        cur = step.Remainder;
                        if (step.Value.Kind == HclToken.Identifier)
                        {
                            var ident = step.Value.ToStringValue();
                            if (string.Equals(ident, InKeyword, StringComparison.Ordinal))
                            {
                                break;
                            }
                            boundVars.Add(ident);
                        }
                    }
                }
            }
        }

        while (!cur.IsAtEnd)
        {
            var expr = HclParser.ParseExpression(cur);
            if (expr.HasValue)
            {
                sink.Add(WithAdjustedLine(expr.Value, baseLine));
                cur = expr.Remainder;
            }
            else
            {
                cur = cur.ConsumeToken().Remainder;
            }
        }
    }

    private static HclExpr WithAdjustedLine(HclExpr expr, int baseLine) => expr switch
    {
        HclExpr.Traversal tr => tr with { Line = baseLine },
        HclExpr.Call call => new HclExpr.Call(
            call.FunctionName,
            call.Arguments.Select(a => WithAdjustedLine(a, baseLine)).ToList(),
            baseLine),
        HclExpr.Complex cx => new HclExpr.Complex(
            cx.RawText,
            cx.SubExpressions.Select(s => WithAdjustedLine(s, baseLine)).ToList(),
            cx.ExtractedReferences.Select(r => r with { Line = baseLine }).ToList(),
            baseLine),
        _ => expr
    };

    private static void CollectBlockReferences(HclBlock block, List<HclReference> sink)
    {
        foreach (var attr in block.Attributes)
        {
            var via = string.Equals(attr.Name, DependsOnAttrName, StringComparison.Ordinal)
                ? DependsOnAttrName
                : attr.Name;
            CollectExprReferences(attr.Value, via, sink);
        }

        foreach (var nested in block.NestedBlocks)
        {
            CollectBlockReferences(nested, sink);
        }
    }

    internal static void CollectExprReferences(HclExpr expr, string via, List<HclReference> sink)
    {
        switch (expr)
        {
            case HclExpr.Traversal tr:
                var classified = TryClassifyTraversal(tr.Parts, tr.Line, via);
                if (classified != null)
                {
                    sink.Add(classified);
                }
                break;

            case HclExpr.Call call:
                foreach (var arg in call.Arguments)
                {
                    CollectExprReferences(arg, via, sink);
                }
                break;

            case HclExpr.Tuple tuple:
                foreach (var el in tuple.Elements)
                {
                    CollectExprReferences(el, via, sink);
                }
                break;

            case HclExpr.ObjectExpr obj:
                foreach (var entry in obj.Entries)
                {
                    var entryVia = string.IsNullOrEmpty(via) ? entry.Name : via;
                    CollectExprReferences(entry.Value, entryVia, sink);
                }
                break;

            case HclExpr.Template tpl:
                foreach (var r in tpl.ExtractedReferences)
                {
                    sink.Add(r with { Via = via });
                }
                break;

            case HclExpr.Complex cx:
                foreach (var r in cx.ExtractedReferences)
                {
                    sink.Add(r with { Via = via });
                }
                break;
        }
    }

    public static HclReference? TryClassifyTraversal(IReadOnlyList<string> parts, int line, string via)
    {
        if (parts.Count < 2)
        {
            return null;
        }

        var head = parts[0];
        return head switch
        {
            VarPrefix => new HclReference(
                HclRefKind.Var,
                VarPrefix,
                parts[1],
                parts.Count > 2 ? JoinTail(parts, 2) : null,
                line,
                via),

            LocalPrefix => new HclReference(
                HclRefKind.Local,
                LocalPrefix,
                parts[1],
                parts.Count > 2 ? JoinTail(parts, 2) : null,
                line,
                via),

            ModulePrefix => new HclReference(
                HclRefKind.Module,
                ModulePrefix,
                parts[1],
                parts.Count > 2 ? JoinTail(parts, 2) : null,
                line,
                via),

            DataPrefix when parts.Count >= 3 => new HclReference(
                HclRefKind.Data,
                parts[1],
                parts[2],
                parts.Count > 3 ? JoinTail(parts, 3) : null,
                line,
                via),

            DependencyPrefix => new HclReference(
                HclRefKind.Dependency,
                DependencyPrefix,
                parts[1],
                parts.Count > 2 ? JoinTail(parts, 2) : null,
                line,
                via),

            EachPrefix => new HclReference(HclRefKind.Each, EachPrefix, parts[1], null, line, via),
            SelfPrefix => new HclReference(HclRefKind.Self, SelfPrefix, parts[1], null, line, via),
            CountPrefix => new HclReference(HclRefKind.Count, CountPrefix, parts[1], null, line, via),
            PathPrefix => new HclReference(HclRefKind.Path, PathPrefix, parts[1], null, line, via),
            TerraformPrefix => new HclReference(HclRefKind.Terraform, TerraformPrefix, parts[1], null, line, via),

            _ when LooksLikeResourceType(head) => new HclReference(
                HclRefKind.Resource,
                head,
                parts[1],
                parts.Count > 2 ? JoinTail(parts, 2) : null,
                line,
                via),

            _ => null
        };
    }

    private static string JoinTail(IReadOnlyList<string> parts, int startIndex)
    {
        if (parts.Count - startIndex == 1)
        {
            return parts[startIndex];
        }
        return string.Join(DotSeparator, parts.Skip(startIndex));
    }

    private static bool LooksLikeResourceType(string identifier)
    {
        if (string.IsNullOrEmpty(identifier) || NonResourceRoots.Contains(identifier))
        {
            return false;
        }
        return identifier.Contains(UnderscoreChar, StringComparison.Ordinal);
    }
}
