namespace CodeExplorer.Parsers.TypeScript;

/// <summary>
/// Length-preserving fallback normalizer for valid TypeScript syntax forms where upstream
/// tree-sitter-typescript produces false-positive ERROR nodes (e.g. untyped destructuring
/// binding patterns inside parenthesized function types such as <c>(({ table }) =&gt; ReactNode)</c>).
/// Invoked strictly on the cold path when <c>tree.RootNode.HasError</c> is true.
/// </summary>
internal static class TypeScriptParseErrorRecovery
{
    private const string ArrowToken = "=>";
    private const int MaxParamSpanLength = 512;

    public static string? TryBlankUntypedDestructuredArrowParams(string sourceText)
    {
        if (string.IsNullOrEmpty(sourceText))
        {
            return null;
        }

        char[]? buffer = null;
        var searchPos = 0;

        while (searchPos < sourceText.Length)
        {
            var arrowIdx = sourceText.IndexOf(ArrowToken, searchPos, StringComparison.Ordinal);
            if (arrowIdx < 0)
            {
                break;
            }

            TryRewriteParamListBeforeArrow(sourceText, arrowIdx, ref buffer);
            searchPos = arrowIdx + ArrowToken.Length;
        }

        return buffer != null ? new string(buffer) : null;
    }

    private static void TryRewriteParamListBeforeArrow(string sourceText, int arrowIdx, ref char[]? buffer)
    {
        var closeParenIdx = SkipWhitespaceLeft(sourceText, arrowIdx - 1);
        if (closeParenIdx <= 0 || sourceText[closeParenIdx] != ')')
        {
            return;
        }

        var openParenIdx = FindMatchingOpenParen(sourceText, closeParenIdx);
        if (openParenIdx < 0)
        {
            return;
        }

        RewriteTopLevelDestructuredParams(sourceText, openParenIdx + 1, closeParenIdx, ref buffer);
    }

    private static void RewriteTopLevelDestructuredParams(
        string sourceText,
        int start,
        int end,
        ref char[]? buffer)
    {
        var depth = 0;
        var segStart = start;

        for (var i = start; i <= end; i++)
        {
            var ch = i < end ? sourceText[i] : ',';
            if (i < end)
            {
                if (ch is '(' or '{' or '[' or '<') depth++;
                else if (ch is ')' or '}' or ']' or '>') depth--;
            }

            if (depth == 0 && ch == ',')
            {
                TryBlankSingleDestructuredParam(sourceText, segStart, i - 1, ref buffer);
                segStart = i + 1;
            }
        }
    }

    private static void TryBlankSingleDestructuredParam(
        string sourceText,
        int rawStart,
        int rawEnd,
        ref char[]? buffer)
    {
        var left = SkipWhitespaceRight(sourceText, rawStart, rawEnd);
        var right = SkipWhitespaceLeft(sourceText, rawEnd, left);
        if (left >= right)
        {
            return;
        }

        var openCh = sourceText[left];
        var closeCh = sourceText[right];
        var isObjectPattern = openCh == '{' && closeCh == '}';
        var isArrayPattern = openCh == '[' && closeCh == ']';
        if (!isObjectPattern && !isArrayPattern)
        {
            return;
        }

        if (!IsSimpleDestructuringContent(sourceText, left + 1, right - 1))
        {
            return;
        }

        buffer ??= sourceText.ToCharArray();
        buffer[left] = ' ';
        buffer[right] = ' ';
    }

    private static bool IsSimpleDestructuringContent(string sourceText, int start, int end)
    {
        var hasIdentifierChar = false;
        for (var i = start; i <= end; i++)
        {
            var ch = sourceText[i];
            if (ch is '{' or '}' or '[' or ']' or ';' or '(' or ')')
            {
                return false;
            }
            if (char.IsLetterOrDigit(ch) || ch is '_' or '$')
            {
                hasIdentifierChar = true;
            }
        }
        return hasIdentifierChar;
    }

    private static int FindMatchingOpenParen(string sourceText, int closeParenIdx)
    {
        var minIdx = Math.Max(0, closeParenIdx - MaxParamSpanLength);
        var depth = 0;
        for (var i = closeParenIdx; i >= minIdx; i--)
        {
            var ch = sourceText[i];
            if (ch is ')' or '}' or ']')
            {
                depth++;
            }
            else if (ch is '(' or '{' or '[')
            {
                depth--;
                if (depth == 0)
                {
                    return ch == '(' ? i : -1;
                }
            }
        }
        return -1;
    }

    private static int SkipWhitespaceLeft(string text, int idx, int minIdx = 0)
    {
        while (idx >= minIdx && char.IsWhiteSpace(text[idx]))
        {
            idx--;
        }
        return idx;
    }

    private static int SkipWhitespaceRight(string text, int idx, int maxIdx)
    {
        while (idx <= maxIdx && char.IsWhiteSpace(text[idx]))
        {
            idx++;
        }
        return idx;
    }
}
