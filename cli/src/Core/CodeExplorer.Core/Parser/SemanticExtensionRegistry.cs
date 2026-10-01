namespace CodeExplorer.Core.Parser;

/// <summary>
/// Fast Trie-based registry for matching active ISemanticExtension instances against imports and namespaces.
/// </summary>
public class SemanticExtensionRegistry
{
    private readonly TrieNode _root = new();

    public SemanticExtensionRegistry(IEnumerable<ISemanticExtension> extensions)
    {
        foreach (var ext in extensions)
        {
            foreach (var pattern in ext.SupportedPatterns)
            {
                AddPattern(pattern, ext);
            }
        }
    }

    private void AddPattern(string pattern, ISemanticExtension extension)
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

        current.Extensions.Add((extension, pattern));
    }

    public ISemanticExtension? Match(string import)
    {
        return MatchAll(import).FirstOrDefault();
    }

    public List<ISemanticExtension> MatchAll(string import)
    {
        if (string.IsNullOrEmpty(import)) return [];

        var importSegments = import.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<MatchResult>();

        MatchRecursive(_root, importSegments, 0, results);

        if (results.Count == 0) return [];

        return
        [
            .. results.OrderByDescending(r => r.Pattern.Length).ThenBy(r => r.Pattern, StringComparer.Ordinal)
                .Select(r => r.Extension).Distinct()
        ];
    }

    private void MatchRecursive(TrieNode node, string[] importSegments, int index, List<MatchResult> results)
    {
        // 1. If we have reached a terminal node with extensions
        if (node.Extensions.Count > 0)
        {
            foreach (var (p, pat) in node.Extensions)
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
                        if (childNode.Extensions.Count > 0)
                        {
                            foreach (var (p, pat) in childNode.Extensions)
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

                    if (childNode.Extensions.Count > 0)
                    {
                        foreach (var (p, pat) in childNode.Extensions)
                        {
                            results.Add(new MatchResult(pat, p));
                        }
                    }
                }
                // Fallback namespace match
                else if (PackageDescriptor.IsLibraryMatch(segment, childKey))
                {
                    if (childNode.Extensions.Count > 0 && index == importSegments.Length - 1)
                    {
                        foreach (var (p, pat) in childNode.Extensions)
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
            foreach (var (childKey, childNode) in node.Children)
            {
                if (childKey == "*" && childNode.Extensions.Count > 0)
                {
                    foreach (var (p, pat) in childNode.Extensions)
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
        public List<(ISemanticExtension Extension, string Pattern)> Extensions { get; } = [];
    }

    private record struct MatchResult(string Pattern, ISemanticExtension Extension);
}
