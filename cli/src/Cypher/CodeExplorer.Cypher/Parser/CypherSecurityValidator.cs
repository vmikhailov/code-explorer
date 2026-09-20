using System.Text.RegularExpressions;
using Superpower;

namespace CodeExplorer.Cypher.Parser;

/// <summary>
/// Token-based security validator for Cypher queries.
/// Analyzes the lexical token stream to detect mutating clauses (CREATE, DELETE, SET, MERGE, REMOVE, DROP, DETACH)
/// without false positives on substrings like 'databaseType' (ba[set]ype), 'offset', or values inside string literals and comments.
/// </summary>
public static class CypherSecurityValidator
{
    private static readonly HashSet<CypherToken> MutatingTokens =
    [
        CypherToken.Create,
        CypherToken.Delete,
        CypherToken.Set,
        CypherToken.Merge,
        CypherToken.Remove,
        CypherToken.Drop,
        CypherToken.Detach,
        CypherToken.Alter,
        CypherToken.Truncate
    ];

    private static readonly Regex FallbackMutatingRegex =
        new(@"\b(CREATE|DELETE|SET|MERGE|REMOVE|DROP|DETACH|ALTER|TRUNCATE)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        var noComments = Regex.Replace(text, @"//.*", "");
        // Remove block comments /* ... */
        noComments = Regex.Replace(noComments, @"/\*[\s\S]*?\*/", "");
        // Remove string literals '...' and "..."
        var noStrings = Regex.Replace(noComments, @"'([^'\\]|\\.)*'", "");
        noStrings = Regex.Replace(noStrings, @"""([^""\\]|\\.)*""", "");
        // Remove backtick identifiers `...`
        return Regex.Replace(noStrings, @"`[^`]+`", "");
    }
}
