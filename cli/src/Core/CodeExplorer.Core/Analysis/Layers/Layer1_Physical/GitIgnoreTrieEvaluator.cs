namespace CodeExplorer.Core.Parser;

internal static class GitIgnoreTrieEvaluator
{
    private const char SlashChar = '/';

    public static void EvaluateScopeForDirectory(
        GitIgnoreScopeTrieIndex index,
        string workspaceRoot,
        WorkspaceFileFilter filter,
        string fullRelativePath,
        ReadOnlySpan<char> candidatePath,
        ref GitIgnoreRuleEntry? winningRule,
        ref int winningOrder)
    {
        var exactLookup = index.ExactSegments.GetAlternateLookup<ReadOnlySpan<char>>();
        var rem = candidatePath;

        while (!rem.IsEmpty)
        {
            var slashIdx = rem.IndexOf(SlashChar);
            var seg = slashIdx >= 0 ? rem[..slashIdx] : rem;
            rem = slashIdx >= 0 ? rem[(slashIdx + 1)..] : ReadOnlySpan<char>.Empty;
            if (seg.IsEmpty) continue;

            if (exactLookup.TryGetValue(seg, out var exactSlot))
            {
                ConsiderDirectorySlot(exactSlot, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
            if (index.HasReverseSuffixRules)
            {
                MatchReverseSuffixDir(index.ReverseSuffixRoot, seg, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
            if (index.HasForwardPrefixRules)
            {
                MatchForwardPrefixDir(index.ForwardPrefixRoot, seg, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
            for (var w = 0; w < index.UnanchoredWildcards.Count; w++)
            {
                var wild = index.UnanchoredWildcards[w];
                if (GitIgnoreGlobHelper.MatchWildcardSpan(seg, wild.Pattern.AsSpan()))
                {
                    ConsiderDirectorySlot(wild.Slot, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
                }
            }
        }

        if (index.HasAnchoredRules)
        {
            WalkAnchoredTrieForDirectory(index.AnchoredRoot, candidatePath, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }

        for (var i = 0; i < index.FallbackRules.Count; i++)
        {
            var fb = index.FallbackRules[i];
            if (fb.Entry.Order <= winningOrder) continue;
            if (fb.RequiredLiteral != null && candidatePath.IndexOf(fb.RequiredLiteral.AsSpan(), StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            if (fb.Regex.IsMatch(candidatePath))
            {
                var fbEntry = fb.Entry;
                ConsiderDirectoryRule(in fbEntry, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
        }
    }

    public static void EvaluateScopeForFile(
        GitIgnoreScopeTrieIndex index,
        string workspaceRoot,
        WorkspaceFileFilter filter,
        string fullRelativePath,
        ReadOnlySpan<char> candidatePath,
        bool skipDirectoryOnlyRules,
        ref GitIgnoreRuleEntry? winningRule,
        ref int winningOrder)
    {
        if (skipDirectoryOnlyRules)
        {
            var lastSlash = candidatePath.LastIndexOf(SlashChar);
            var leafSeg = lastSlash >= 0 ? candidatePath[(lastSlash + 1)..] : candidatePath;
            if (!leafSeg.IsEmpty)
            {
                EvaluateSingleFileSegment(index, leafSeg, false, true, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
        }
        else
        {
            var rem = candidatePath;
            while (!rem.IsEmpty)
            {
                var slashIdx = rem.IndexOf(SlashChar);
                var isAncestorDir = slashIdx >= 0;
                var seg = isAncestorDir ? rem[..slashIdx] : rem;
                rem = isAncestorDir ? rem[(slashIdx + 1)..] : ReadOnlySpan<char>.Empty;
                if (!seg.IsEmpty)
                {
                    EvaluateSingleFileSegment(index, seg, isAncestorDir, false, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
                }
            }
        }

        if (index.HasAnchoredRules)
        {
            WalkAnchoredTrieForFile(index.AnchoredRoot, candidatePath, skipDirectoryOnlyRules, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }

        for (var i = 0; i < index.FallbackRules.Count; i++)
        {
            var fb = index.FallbackRules[i];
            if (fb.Entry.Order <= winningOrder) continue;
            if (skipDirectoryOnlyRules && fb.Entry.IsDirectoryOnly) continue;
            if (fb.RequiredLiteral != null && candidatePath.IndexOf(fb.RequiredLiteral.AsSpan(), StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (fb.Entry.IsDirectoryOnly)
            {
                var m = fb.Regex.Match(candidatePath.ToString());
                if (m.Success && m.Value.EndsWith(SlashChar))
                {
                    var fbEntry = fb.Entry;
                    ConsiderFileRule(in fbEntry, true, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
                }
            }
            else if (fb.Regex.IsMatch(candidatePath))
            {
                var fbEntry = fb.Entry;
                ConsiderFileRule(in fbEntry, false, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
        }
    }

    public static bool HasDescendantNegationInAnchoredTrie(
        GitIgnoreSegmentTrieNode root,
        ReadOnlySpan<char> dirPath,
        int afterRuleIndex)
    {
        if (root.MaxDescendantNegationOrder <= afterRuleIndex) return false;

        var current = root;
        var rem = dirPath;
        while (!rem.IsEmpty)
        {
            var slashIdx = rem.IndexOf(SlashChar);
            var seg = slashIdx >= 0 ? rem[..slashIdx] : rem;
            rem = slashIdx >= 0 ? rem[(slashIdx + 1)..] : ReadOnlySpan<char>.Empty;

            if (current.ExactChildren == null) return false;
            var lookup = current.ExactChildren.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!lookup.TryGetValue(seg, out var next) || next.MaxDescendantNegationOrder <= afterRuleIndex)
            {
                return false;
            }
            current = next;
        }

        return current.MaxDescendantNegationOrder > afterRuleIndex;
    }

    private static void EvaluateSingleFileSegment(
        GitIgnoreScopeTrieIndex index,
        ReadOnlySpan<char> seg,
        bool isAncestorDir,
        bool skipDirOnly,
        string workspaceRoot,
        WorkspaceFileFilter filter,
        string fullRelativePath,
        ref GitIgnoreRuleEntry? winningRule,
        ref int winningOrder)
    {
        var exactLookup = index.ExactSegments.GetAlternateLookup<ReadOnlySpan<char>>();
        if (exactLookup.TryGetValue(seg, out var exactSlot))
        {
            ConsiderFileSlot(exactSlot, isAncestorDir, skipDirOnly, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        if (index.HasReverseSuffixRules)
        {
            MatchReverseSuffixFile(index.ReverseSuffixRoot, seg, isAncestorDir, skipDirOnly, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        if (index.HasForwardPrefixRules)
        {
            MatchForwardPrefixFile(index.ForwardPrefixRoot, seg, isAncestorDir, skipDirOnly, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        for (var w = 0; w < index.UnanchoredWildcards.Count; w++)
        {
            var wild = index.UnanchoredWildcards[w];
            if (GitIgnoreGlobHelper.MatchWildcardSpan(seg, wild.Pattern.AsSpan()))
            {
                ConsiderFileSlot(wild.Slot, isAncestorDir, skipDirOnly, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
        }
    }

    private static void WalkAnchoredTrieForDirectory(
        GitIgnoreSegmentTrieNode node,
        ReadOnlySpan<char> remainingPath,
        string workspaceRoot,
        WorkspaceFileFilter filter,
        string fullRelativePath,
        ref GitIgnoreRuleEntry? winningRule,
        ref int winningOrder)
    {
        if (node.Terminal != null)
        {
            ConsiderDirectorySlot(node.Terminal, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        if (node.GlobstarChild != null)
        {
            WalkAnchoredTrieForDirectory(node.GlobstarChild, remainingPath, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        if (remainingPath.IsEmpty) return;

        var slashIdx = remainingPath.IndexOf(SlashChar);
        var seg = slashIdx >= 0 ? remainingPath[..slashIdx] : remainingPath;
        var nextRemaining = slashIdx >= 0 ? remainingPath[(slashIdx + 1)..] : ReadOnlySpan<char>.Empty;

        if (node.ExactChildren != null)
        {
            var lookup = node.ExactChildren.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.TryGetValue(seg, out var exactNext))
            {
                WalkAnchoredTrieForDirectory(exactNext, nextRemaining, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
        }
        if (node.SingleStarChild != null)
        {
            WalkAnchoredTrieForDirectory(node.SingleStarChild, nextRemaining, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        if (node.WildcardChildren != null)
        {
            for (var i = 0; i < node.WildcardChildren.Count; i++)
            {
                var edge = node.WildcardChildren[i];
                if (GitIgnoreGlobHelper.MatchWildcardSpan(seg, edge.Pattern.AsSpan()))
                {
                    WalkAnchoredTrieForDirectory(edge.Target, nextRemaining, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
                }
            }
        }
        if (node.GlobstarChild != null)
        {
            WalkAnchoredTrieForDirectory(node, nextRemaining, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
    }

    private static void WalkAnchoredTrieForFile(
        GitIgnoreSegmentTrieNode node,
        ReadOnlySpan<char> remainingPath,
        bool skipDirectoryOnlyRules,
        string workspaceRoot,
        WorkspaceFileFilter filter,
        string fullRelativePath,
        ref GitIgnoreRuleEntry? winningRule,
        ref int winningOrder)
    {
        var isAncestorDirectoryPosition = !remainingPath.IsEmpty;
        if (node.Terminal != null)
        {
            ConsiderFileSlot(node.Terminal, isAncestorDirectoryPosition, skipDirectoryOnlyRules, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        if (node.GlobstarChild != null)
        {
            WalkAnchoredTrieForFile(node.GlobstarChild, remainingPath, skipDirectoryOnlyRules, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        if (remainingPath.IsEmpty) return;

        var slashIdx = remainingPath.IndexOf(SlashChar);
        var seg = slashIdx >= 0 ? remainingPath[..slashIdx] : remainingPath;
        var nextRemaining = slashIdx >= 0 ? remainingPath[(slashIdx + 1)..] : ReadOnlySpan<char>.Empty;

        if (node.ExactChildren != null)
        {
            var lookup = node.ExactChildren.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.TryGetValue(seg, out var exactNext))
            {
                WalkAnchoredTrieForFile(exactNext, nextRemaining, skipDirectoryOnlyRules, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
            }
        }
        if (node.SingleStarChild != null)
        {
            WalkAnchoredTrieForFile(node.SingleStarChild, nextRemaining, skipDirectoryOnlyRules, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
        if (node.WildcardChildren != null)
        {
            for (var i = 0; i < node.WildcardChildren.Count; i++)
            {
                var edge = node.WildcardChildren[i];
                if (GitIgnoreGlobHelper.MatchWildcardSpan(seg, edge.Pattern.AsSpan()))
                {
                    WalkAnchoredTrieForFile(edge.Target, nextRemaining, skipDirectoryOnlyRules, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
                }
            }
        }
        if (node.GlobstarChild != null)
        {
            WalkAnchoredTrieForFile(node, nextRemaining, skipDirectoryOnlyRules, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
    }

    private static void MatchReverseSuffixDir(GitIgnoreCharTrieNode root, ReadOnlySpan<char> segment, string workspaceRoot, WorkspaceFileFilter filter, string fullRelativePath, ref GitIgnoreRuleEntry? winningRule, ref int winningOrder)
    {
        var current = root;
        for (var i = segment.Length - 1; i >= 0; i--)
        {
            var next = current.TryGetChild(segment[i]);
            if (next == null) break;
            current = next;
            if (current.Terminal != null) ConsiderDirectorySlot(current.Terminal, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
    }

    private static void MatchReverseSuffixFile(GitIgnoreCharTrieNode root, ReadOnlySpan<char> segment, bool isAncestorDir, bool skipDirOnly, string workspaceRoot, WorkspaceFileFilter filter, string fullRelativePath, ref GitIgnoreRuleEntry? winningRule, ref int winningOrder)
    {
        var current = root;
        for (var i = segment.Length - 1; i >= 0; i--)
        {
            var next = current.TryGetChild(segment[i]);
            if (next == null) break;
            current = next;
            if (current.Terminal != null) ConsiderFileSlot(current.Terminal, isAncestorDir, skipDirOnly, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
    }

    private static void MatchForwardPrefixDir(GitIgnoreCharTrieNode root, ReadOnlySpan<char> segment, string workspaceRoot, WorkspaceFileFilter filter, string fullRelativePath, ref GitIgnoreRuleEntry? winningRule, ref int winningOrder)
    {
        var current = root;
        for (var i = 0; i < segment.Length; i++)
        {
            var next = current.TryGetChild(segment[i]);
            if (next == null) break;
            current = next;
            if (current.Terminal != null) ConsiderDirectorySlot(current.Terminal, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
    }

    private static void MatchForwardPrefixFile(GitIgnoreCharTrieNode root, ReadOnlySpan<char> segment, bool isAncestorDir, bool skipDirOnly, string workspaceRoot, WorkspaceFileFilter filter, string fullRelativePath, ref GitIgnoreRuleEntry? winningRule, ref int winningOrder)
    {
        var current = root;
        for (var i = 0; i < segment.Length; i++)
        {
            var next = current.TryGetChild(segment[i]);
            if (next == null) break;
            current = next;
            if (current.Terminal != null) ConsiderFileSlot(current.Terminal, isAncestorDir, skipDirOnly, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        }
    }

    private static void ConsiderDirectorySlot(GitIgnoreDecisionSlot slot, string workspaceRoot, WorkspaceFileFilter filter, string fullRelativePath, ref GitIgnoreRuleEntry? winningRule, ref int winningOrder)
    {
        if (slot.AnyRule is { } anyRule && anyRule.Order > winningOrder) ConsiderDirectoryRule(in anyRule, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        if (slot.DirOnlyRule is { } dirRule && dirRule.Order > winningOrder) ConsiderDirectoryRule(in dirRule, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
    }

    private static void ConsiderDirectoryRule(in GitIgnoreRuleEntry rule, string workspaceRoot, WorkspaceFileFilter filter, string fullRelativePath, ref GitIgnoreRuleEntry? winningRule, ref int winningOrder)
    {
        if (rule.Order <= winningOrder) return;
        if (!string.IsNullOrEmpty(workspaceRoot) && filter.CheckDirectoryExclusion(rule.CleanPattern, Path.Combine(workspaceRoot, fullRelativePath)) == false) return;
        winningRule = rule;
        winningOrder = rule.Order;
    }

    private static void ConsiderFileSlot(GitIgnoreDecisionSlot slot, bool isAncestorDir, bool skipDirOnly, string workspaceRoot, WorkspaceFileFilter filter, string fullRelativePath, ref GitIgnoreRuleEntry? winningRule, ref int winningOrder)
    {
        if (slot.AnyRule is { } anyRule && anyRule.Order > winningOrder) ConsiderFileRule(in anyRule, false, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
        if (isAncestorDir && !skipDirOnly && slot.DirOnlyRule is { } dirRule && dirRule.Order > winningOrder) ConsiderFileRule(in dirRule, true, workspaceRoot, filter, fullRelativePath, ref winningRule, ref winningOrder);
    }

    private static void ConsiderFileRule(in GitIgnoreRuleEntry rule, bool isDirectoryCheck, string workspaceRoot, WorkspaceFileFilter filter, string fullRelativePath, ref GitIgnoreRuleEntry? winningRule, ref int winningOrder)
    {
        if (rule.Order <= winningOrder) return;
        if (isDirectoryCheck && !string.IsNullOrEmpty(workspaceRoot) && filter.CheckDirectoryExclusion(rule.CleanPattern, Path.Combine(workspaceRoot, fullRelativePath)) == false) return;
        winningRule = rule;
        winningOrder = rule.Order;
    }
}
