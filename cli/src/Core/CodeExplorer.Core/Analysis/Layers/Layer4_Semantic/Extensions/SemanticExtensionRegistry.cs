using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Fast Trie-based and wildcard pattern registry for matching active ISemanticExtension instances against imports and namespaces.
/// </summary>
public class SemanticExtensionRegistry
{
    private readonly TrieNode _root = new();
    private readonly List<WildcardPattern> _wildcardPatterns = [];
    private readonly ConcurrentDictionary<string, ISemanticExtension?> _matchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<ISemanticExtension>> _matchAllCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly record struct WildcardPattern(string Pattern, Regex Regex, ISemanticExtension Extension);

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

        // 1. Wildcard / Glob pattern (* or ?) compiled for general import matching
        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var regex = CompileGlobRegex(pattern);
            _wildcardPatterns.Add(new WildcardPattern(pattern, regex, extension));
        }

        // 2. Only register in hierarchical Trie if pattern does not start with a leading wildcard
        if (!pattern.StartsWith('*') && !pattern.StartsWith('?'))
        {
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
    }

    private static Regex CompileGlobRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern);
        var regexStr = "^" + escaped.Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return new Regex(regexStr, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    public ISemanticExtension? Match(string import)
    {
        if (string.IsNullOrEmpty(import)) return null;
        return _matchCache.GetOrAdd(import, imp => MatchAll(imp).FirstOrDefault());
    }

    public List<ISemanticExtension> MatchAll(string import)
    {
        if (string.IsNullOrEmpty(import)) return [];
        return _matchAllCache.GetOrAdd(import, ComputeMatchAll);
    }

    private List<ISemanticExtension> ComputeMatchAll(string import)
    {
        var results = new List<MatchResult>();

        // 1. Fast Trie-based match on path segments
        var importSegments = import.Split('/', StringSplitOptions.RemoveEmptyEntries);
        MatchRecursive(_root, importSegments, 0, results);

        // 2. Relative import fallback for Trie (e.g. ./foo/bar -> foo/bar)
        if (import.StartsWith('.'))
        {
            var trimmed = import.TrimStart('.', '/');
            if (!string.IsNullOrEmpty(trimmed))
            {
                var trimmedSegments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
                MatchRecursive(_root, trimmedSegments, 0, results);
            }
        }

        // 3. Glob / Wildcard pattern matching (e.g. *rabbit*, *pubsub*, *EventBus*)
        if (_wildcardPatterns.Count > 0)
        {
            var normalizedImport = import.Contains('\\') ? import.Replace('\\', '/') : import;
            foreach (var wp in _wildcardPatterns)
            {
                if (wp.Regex.IsMatch(normalizedImport))
                {
                    results.Add(new MatchResult(wp.Pattern, wp.Extension));
                }
            }
        }

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
                    if (segment.StartWithIC(prefix))
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
