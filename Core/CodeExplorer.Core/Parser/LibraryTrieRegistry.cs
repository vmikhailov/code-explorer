namespace CodeExplorer.Core.Parser;

public class LibraryTrieRegistry
{
    private readonly TrieNode _root = new();

    public LibraryTrieRegistry(IEnumerable<ILibraryParser> parsers)
    {
        foreach (var parser in parsers)
        {
            foreach (var pattern in parser.SupportedPatterns)
            {
                AddPattern(pattern, parser);
            }
        }
    }

    private void AddPattern(string pattern, ILibraryParser parser)
    {
        if (string.IsNullOrEmpty(pattern)) return;

        var segments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = _root;

        foreach (var segment in segments)
        {
            if (!current.Children.TryGetValue(segment, out var child))
            {
                child = new TrieNode();
                current.Children[segment] = child;
            }
            current = child;
        }

        current.Parser = parser;
        current.Pattern = pattern;
    }

    public ILibraryParser? Match(string import)
    {
        if (string.IsNullOrEmpty(import)) return null;

        var importSegments = import.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<MatchResult>();

        MatchRecursive(_root, importSegments, 0, results);

        if (results.Count == 0) return null;

        // Best match wins: longest pattern length first, then alphabetical tie-breaker.
        return results
            .OrderByDescending(r => r.Pattern.Length)
            .ThenBy(r => r.Pattern, StringComparer.Ordinal)
            .First()
            .Parser;
    }

    private void MatchRecursive(TrieNode node, string[] importSegments, int index, List<MatchResult> results)
    {
        // 1. If we have reached a terminal node with a parser
        if (node.Parser != null)
        {
            // If we've fully consumed the import, it's a valid match.
            if (index == importSegments.Length)
            {
                results.Add(new MatchResult(node.Pattern!, node.Parser));
            }
        }

        // 2. If we still have segments left to match in the import
        if (index < importSegments.Length)
        {
            var segment = importSegments[index];

            foreach (var (childKey, childNode) in node.Children)
            {
                // Exact match (case insensitive)
                if (childKey.Equals(segment, StringComparison.OrdinalIgnoreCase))
                {
                    MatchRecursive(childNode, importSegments, index + 1, results);
                }
                // Prefix wildcard (e.g. firebase*)
                else if (childKey.EndsWith("*") && childKey.Length > 1)
                {
                    var prefix = childKey[..^1];
                    if (segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        // If this child represents a terminal node, it can match this segment and consume the rest
                        if (childNode.Parser != null)
                        {
                            results.Add(new MatchResult(childNode.Pattern!, childNode.Parser));
                        }
                        // Also recurse to match deeper segments
                        MatchRecursive(childNode, importSegments, index + 1, results);
                    }
                }
                // Wildcard segment (e.g. *)
                else if (childKey == "*")
                {
                    // Matches current segment, recurse
                    MatchRecursive(childNode, importSegments, index + 1, results);

                    // If the wildcard is terminal (e.g., @nestjs/* matching @nestjs/common/core),
                    // it matches all remaining segments.
                    if (childNode.Parser != null)
                    {
                        results.Add(new MatchResult(childNode.Pattern!, childNode.Parser));
                    }
                }
                // Fallback namespace match (using IsLibraryMatch)
                else if (ILibraryParser.IsLibraryMatch(segment, childKey))
                {
                    // If the child is a terminal node, it can match
                    if (childNode.Parser != null && index == importSegments.Length - 1)
                    {
                        results.Add(new MatchResult(childNode.Pattern!, childNode.Parser));
                    }
                    // Also recurse
                    MatchRecursive(childNode, importSegments, index + 1, results);
                }
            }
        }
        else // index == importSegments.Length
        {
            // If the import is fully consumed, but the pattern had a wildcard segment at the end (e.g., @nestjs/* matches @nestjs)
            foreach (var (childKey, childNode) in node.Children)
            {
                if (childKey == "*" && childNode.Parser != null)
                {
                    results.Add(new MatchResult(childNode.Pattern!, childNode.Parser));
                }
            }
        }
    }

    private class TrieNode
    {
        public Dictionary<string, TrieNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ILibraryParser? Parser { get; set; }
        public string? Pattern { get; set; }
    }

    private record struct MatchResult(string Pattern, ILibraryParser Parser);
}
