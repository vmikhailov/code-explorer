using System.Text.RegularExpressions;

namespace CodeExplorer.Cypher.Parser;

/// <summary>
/// Token-based security validator for Cypher queries.
/// Analyzes the lexical token stream to detect mutating clauses (CREATE, DELETE, SET, MERGE, REMOVE, DROP, DETACH)
/// without false positives on substrings like 'databaseType' (ba[set]ype), 'offset', or values inside string literals and comments.
/// </summary>
public static class CypherSecurityValidator
{
    private static readonly HashSet<CypherToken> MutatingTokens =
        [.. CypherTokenizer.MutatingKeywords];

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex FallbackMutatingRegex =
        new(@"\b(CREATE|DELETE|SET|MERGE|REMOVE|DROP|DETACH|ALTER|TRUNCATE)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex SingleLineCommentRegex = new(@"//.*", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex BlockCommentRegex = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex SingleQuotedStringRegex = new(@"(?s)'(?:[^'\\]|\\.|'')*'", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex DoubleQuotedStringRegex = new(@"(?s)""(?:[^""\\]|\\.|"""")*""", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex BacktickIdentifierRegex = new(@"`[^`]+`", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex PropertyAccessRegex = new(@"\.\s*[a-zA-Z0-9_]+", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex LabelPrefixRegex = new(@":\s*[a-zA-Z0-9_]+", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    /// Validates that the query is strictly a read-only query.
    /// Throws InvalidOperationException if any mutating keyword token is detected.
    /// </summary>
    public static void ValidateReadOnly(string cypherQuery)
    {
        if (string.IsNullOrWhiteSpace(cypherQuery)) return;

        var tokenListResult = CypherTokenizer.Instance.TryTokenize(cypherQuery);
        if (!tokenListResult.HasValue)
        {
            ValidateFallback(cypherQuery);
            return;
        }

        var tokens = tokenListResult.Value;
        CypherToken prevKind = CypherToken.None;

        foreach (var token in tokens)
        {
            if (MutatingTokens.Contains(token.Kind))
            {
                // In Cypher, property access (e.g. n.set, n.drop) or labels (e.g. :Set) are not mutations.
                if (prevKind is not CypherToken.Dot and not CypherToken.Colon)
                {
                    throw new InvalidOperationException("Security violation: Mutating queries are not allowed.");
                }
            }

            prevKind = token.Kind;
        }
    }

    private static void ValidateFallback(string cypherQuery)
    {
        var cleaned = StripStringsAndComments(cypherQuery);
        if (FallbackMutatingRegex.IsMatch(cleaned))
        {
            throw new InvalidOperationException("Security violation: Mutating queries are not allowed.");
        }
    }

    private static string StripStringsAndComments(string text)
    {
        // Remove single-line comments // ...
        var noComments = SingleLineCommentRegex.Replace(text, "");
        // Remove block comments /* ... */
        noComments = BlockCommentRegex.Replace(noComments, "");
        // Remove string literals '...' and "..." (supporting multiline and escaped quotes \' and '')
        var noStrings = SingleQuotedStringRegex.Replace(noComments, "");
        noStrings = DoubleQuotedStringRegex.Replace(noStrings, "");
        // Remove backtick identifiers `...`
        var noBackticks = BacktickIdentifierRegex.Replace(noStrings, "");
        // Strip property access (e.g. .set, .merge) and labels (e.g. :Set) so non-mutating tokens are not flagged
        var noProps = PropertyAccessRegex.Replace(noBackticks, "");
        return LabelPrefixRegex.Replace(noProps, "");
    }
}
