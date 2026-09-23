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

        current.Parsers.Add((parser, pattern));
    }

    public ILibraryParser? Match(string import)
    {
        return MatchAll(import).FirstOrDefault();
    }

    public List<ILibraryParser> MatchAll(string import)
    {
        if (string.IsNullOrEmpty(import)) return [];

        var importSegments = import.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<MatchResult>();

        MatchRecursive(_root, importSegments, 0, results);

        if (results.Count == 0) return [];

        return results
            .OrderByDescending(r => r.Pattern.Length)
            .ThenBy(r => r.Pattern, StringComparer.Ordinal)
            .Select(r => r.Parser)
            .Distinct()
            .ToList();
    }

    private void MatchRecursive(TrieNode node, string[] importSegments, int index, List<MatchResult> results)
    {
        // 1. If we have reached a terminal node with parsers
        if (node.Parsers.Count > 0)
        {
            // A registered parser matches its own package root AND any deeper subpaths (e.g. "mysql2" matches "mysql2/promise")
            foreach (var (p, pat) in node.Parsers)
            {
                results.Add(new MatchResult(pat, p));
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
                        if (childNode.Parsers.Count > 0)
                        {
                            foreach (var (p, pat) in childNode.Parsers)
                            {
                                results.Add(new MatchResult(pat, p));
                            }
                        }
                        MatchRecursive(childNode, importSegments, index + 1, results);
                    }
                }
                // Wildcard segment (e.g. *)
                else if (childKey == "*")
                {
                    MatchRecursive(childNode, importSegments, index + 1, results);

                    if (childNode.Parsers.Count > 0)
                    {
                        foreach (var (p, pat) in childNode.Parsers)
                        {
                            results.Add(new MatchResult(pat, p));
                        }
                    }
                }
                // Fallback namespace match (using IsLibraryMatch)
                else if (ILibraryParser.IsLibraryMatch(segment, childKey))
                {
                    if (childNode.Parsers.Count > 0 && index == importSegments.Length - 1)
                    {
                        foreach (var (p, pat) in childNode.Parsers)
                        {
                            results.Add(new MatchResult(pat, p));
                        }
                    }
                    MatchRecursive(childNode, importSegments, index + 1, results);
                }
            }
        }
        else // index == importSegments.Length
        {
            // If the import is fully consumed, but the pattern had a wildcard segment at the end (e.g., @nestjs/* matches @nestjs)
            foreach (var (childKey, childNode) in node.Children)
            {
                if (childKey == "*" && childNode.Parsers.Count > 0)
                {
                    foreach (var (p, pat) in childNode.Parsers)
                    {
                        results.Add(new MatchResult(pat, p));
                    }
                }
            }
        }
    }

    private class TrieNode
    {
        public Dictionary<string, TrieNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(ILibraryParser Parser, string Pattern)> Parsers { get; } = [];
    }

    private record struct MatchResult(string Pattern, ILibraryParser Parser);
}
