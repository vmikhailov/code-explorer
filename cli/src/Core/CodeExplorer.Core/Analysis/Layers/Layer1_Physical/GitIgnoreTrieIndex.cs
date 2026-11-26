using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

internal readonly record struct GitIgnoreRuleEntry(
    int Order,
    string CleanPattern,
    bool IsDirectoryOnly,
    bool IsNegated,
    bool IsAnchored,
    string? Scope,
    string? ScopePrefix);

internal sealed class GitIgnoreDecisionSlot
{
    public GitIgnoreRuleEntry? AnyRule;
    public int AnyOrder = -1;
    public GitIgnoreRuleEntry? DirOnlyRule;
    public int DirOnlyOrder = -1;

    public void Add(in GitIgnoreRuleEntry rule)
    {
        if (rule.IsDirectoryOnly)
        {
            if (rule.Order > DirOnlyOrder)
            {
                DirOnlyRule = rule;
                DirOnlyOrder = rule.Order;
            }
        }
        else if (rule.Order > AnyOrder)
        {
            AnyRule = rule;
            AnyOrder = rule.Order;
        }
    }
}

internal sealed class GitIgnoreCharTrieNode
{
    public GitIgnoreCharTrieNode?[]? AsciiChildren;
    public Dictionary<char, GitIgnoreCharTrieNode>? NonAsciiChildren;
    public GitIgnoreDecisionSlot? Terminal;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GitIgnoreCharTrieNode GetOrAddChild(char rawChar)
    {
        var ch = (uint)rawChar <= 127u
            ? (rawChar is >= 'A' and <= 'Z' ? (char)(rawChar | 0x20) : rawChar)
            : char.ToLowerInvariant(rawChar);

        if ((uint)ch < 128u)
        {
            AsciiChildren ??= new GitIgnoreCharTrieNode?[128];
            return AsciiChildren[ch] ??= new GitIgnoreCharTrieNode();
        }

        NonAsciiChildren ??= new Dictionary<char, GitIgnoreCharTrieNode>();
        if (!NonAsciiChildren.TryGetValue(ch, out var next))
        {
            next = new GitIgnoreCharTrieNode();
            NonAsciiChildren[ch] = next;
        }
        return next;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GitIgnoreCharTrieNode? TryGetChild(char rawChar)
    {
        if ((uint)rawChar < 128u)
        {
            var arr = AsciiChildren;
            if (arr == null) return null;
            var idx = rawChar is >= 'A' and <= 'Z' ? rawChar | 0x20 : rawChar;
            return arr[idx];
        }

        if (NonAsciiChildren == null) return null;
        NonAsciiChildren.TryGetValue(char.ToLowerInvariant(rawChar), out var next);
        return next;
    }
}

internal sealed class GitIgnoreWildcardSegmentEdge(string pattern, GitIgnoreSegmentTrieNode target)
{
    public string Pattern { get; } = pattern;
    public GitIgnoreSegmentTrieNode Target { get; } = target;
}

internal sealed class GitIgnoreSegmentTrieNode
{
    public Dictionary<string, GitIgnoreSegmentTrieNode>? ExactChildren;
    public GitIgnoreSegmentTrieNode? SingleStarChild;
    public GitIgnoreSegmentTrieNode? GlobstarChild;
    public List<GitIgnoreWildcardSegmentEdge>? WildcardChildren;
    public GitIgnoreDecisionSlot? Terminal;
    public int MaxDescendantNegationOrder = -1;
}

internal sealed record GitIgnoreWildcardSegmentRule(string Pattern, GitIgnoreDecisionSlot Slot);

internal sealed record GitIgnoreFallbackRegexRule(GitIgnoreRuleEntry Entry, Regex Regex, string? RequiredLiteral);

internal sealed class GitIgnoreScopeTrieIndex
{
    private const char SlashChar = '/';
    private const char DotChar = '.';
    private const string GlobstarSegment = "**";
    private const string SingleStarSegment = "*";

    public readonly Dictionary<string, GitIgnoreDecisionSlot> ExactSegments = new(StringComparer.OrdinalIgnoreCase);
    public readonly GitIgnoreCharTrieNode ReverseSuffixRoot = new();
    public readonly GitIgnoreCharTrieNode ForwardPrefixRoot = new();
    public readonly List<GitIgnoreWildcardSegmentRule> UnanchoredWildcards = [];
    public readonly GitIgnoreSegmentTrieNode AnchoredRoot = new();
    public readonly List<GitIgnoreFallbackRegexRule> FallbackRules = [];
    public bool HasReverseSuffixRules;
    public bool HasForwardPrefixRules;
    public bool HasAnchoredRules;
    public bool HasExactSegmentWithDot;

    public bool IsEmpty =>
        ExactSegments.Count == 0 &&
        !HasReverseSuffixRules &&
        !HasForwardPrefixRules &&
        UnanchoredWildcards.Count == 0 &&
        !HasAnchoredRules &&
        FallbackRules.Count == 0;

    public void AddExactSegment(string segment, in GitIgnoreRuleEntry entry)
    {
        if (segment.Contains(DotChar))
        {
            HasExactSegmentWithDot = true;
        }
        if (!ExactSegments.TryGetValue(segment, out var slot))
        {
            slot = new GitIgnoreDecisionSlot();
            ExactSegments[segment] = slot;
        }
        slot.Add(in entry);
    }

    public void AddReverseSuffix(ReadOnlySpan<char> suffix, in GitIgnoreRuleEntry entry)
    {
        HasReverseSuffixRules = true;
        var current = ReverseSuffixRoot;
        for (var i = suffix.Length - 1; i >= 0; i--)
        {
            current = current.GetOrAddChild(suffix[i]);
        }
        current.Terminal ??= new GitIgnoreDecisionSlot();
        current.Terminal.Add(in entry);
    }

    public void AddForwardPrefix(ReadOnlySpan<char> prefix, in GitIgnoreRuleEntry entry)
    {
        HasForwardPrefixRules = true;
        var current = ForwardPrefixRoot;
        for (var i = 0; i < prefix.Length; i++)
        {
            current = current.GetOrAddChild(prefix[i]);
        }
        current.Terminal ??= new GitIgnoreDecisionSlot();
        current.Terminal.Add(in entry);
    }

    public void AddUnanchoredWildcard(string pattern, in GitIgnoreRuleEntry entry)
    {
        for (var i = 0; i < UnanchoredWildcards.Count; i++)
        {
            if (string.Equals(UnanchoredWildcards[i].Pattern, pattern, StringComparison.OrdinalIgnoreCase))
            {
                UnanchoredWildcards[i].Slot.Add(in entry);
                return;
            }
        }
        var slot = new GitIgnoreDecisionSlot();
        slot.Add(in entry);
        UnanchoredWildcards.Add(new GitIgnoreWildcardSegmentRule(pattern, slot));
    }

    public void AddAnchoredPath(ReadOnlySpan<char> cleanPath, in GitIgnoreRuleEntry entry)
    {
        HasAnchoredRules = true;
        var current = AnchoredRoot;
        var remaining = cleanPath;

        while (!remaining.IsEmpty)
        {
            if (entry.IsNegated && entry.Order > current.MaxDescendantNegationOrder)
            {
                current.MaxDescendantNegationOrder = entry.Order;
            }

            var slashIdx = remaining.IndexOf(SlashChar);
            var segSpan = slashIdx >= 0 ? remaining[..slashIdx] : remaining;
            remaining = slashIdx >= 0 ? remaining[(slashIdx + 1)..] : ReadOnlySpan<char>.Empty;

            if (segSpan.IsEmpty) continue;

            if (segSpan.Equals(GlobstarSegment.AsSpan(), StringComparison.Ordinal))
            {
                current.GlobstarChild ??= new GitIgnoreSegmentTrieNode();
                current = current.GlobstarChild;
            }
            else if (segSpan.Equals(SingleStarSegment.AsSpan(), StringComparison.Ordinal))
            {
                current.SingleStarChild ??= new GitIgnoreSegmentTrieNode();
                current = current.SingleStarChild;
            }
            else if (GitIgnoreGlobHelper.ContainsWildcard(segSpan))
            {
                var patStr = segSpan.ToString();
                current.WildcardChildren ??= [];
                GitIgnoreSegmentTrieNode? next = null;
                for (var i = 0; i < current.WildcardChildren.Count; i++)
                {
                    if (string.Equals(current.WildcardChildren[i].Pattern, patStr, StringComparison.OrdinalIgnoreCase))
                    {
                        next = current.WildcardChildren[i].Target;
                        break;
                    }
                }
                if (next == null)
                {
                    next = new GitIgnoreSegmentTrieNode();
                    current.WildcardChildren.Add(new GitIgnoreWildcardSegmentEdge(patStr, next));
                }
                current = next;
            }
            else
            {
                current.ExactChildren ??= new Dictionary<string, GitIgnoreSegmentTrieNode>(StringComparer.OrdinalIgnoreCase);
                var lookup = current.ExactChildren.GetAlternateLookup<ReadOnlySpan<char>>();
                if (!lookup.TryGetValue(segSpan, out var next))
                {
                    next = new GitIgnoreSegmentTrieNode();
                    lookup[segSpan] = next;
                }
                current = next;
            }
        }

        current.Terminal ??= new GitIgnoreDecisionSlot();
        current.Terminal.Add(in entry);
    }
}

internal static class GitIgnoreGlobHelper
{
    private const char StarChar = '*';
    private const char QuestionChar = '?';
    private const char OpenBracketChar = '[';

    public static bool ContainsWildcard(ReadOnlySpan<char> span) =>
        span.IndexOfAny(StarChar, QuestionChar) >= 0;

    public static bool MatchWildcardSpan(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        var tIdx = 0;
        var pIdx = 0;
        var starIdx = -1;
        var matchIdx = 0;

        while (tIdx < text.Length)
        {
            if (pIdx < pattern.Length &&
                (pattern[pIdx] == QuestionChar || char.ToLowerInvariant(pattern[pIdx]) == char.ToLowerInvariant(text[tIdx])))
            {
                tIdx++;
                pIdx++;
            }
            else if (pIdx < pattern.Length && pattern[pIdx] == StarChar)
            {
                starIdx = pIdx++;
                matchIdx = tIdx;
            }
            else if (starIdx >= 0)
            {
                pIdx = starIdx + 1;
                tIdx = ++matchIdx;
            }
            else
            {
                return false;
            }
        }

        while (pIdx < pattern.Length && pattern[pIdx] == StarChar)
        {
            pIdx++;
        }

        return pIdx == pattern.Length;
    }

    public static string? ExtractLongestLiteralToken(string cleanPattern)
    {
        if (string.IsNullOrEmpty(cleanPattern) || cleanPattern.Contains(OpenBracketChar))
        {
            return null;
        }

        string? best = null;
        var span = cleanPattern.AsSpan();
        var start = 0;
        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] is StarChar or QuestionChar)
            {
                var len = i - start;
                if (len >= 2 && (best == null || len > best.Length))
                {
                    best = cleanPattern.Substring(start, len);
                }
                start = i + 1;
            }
        }
        return best;
    }
}
