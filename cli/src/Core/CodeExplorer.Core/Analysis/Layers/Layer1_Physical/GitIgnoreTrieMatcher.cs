using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Immutable chain node of compiled <see cref="GitIgnoreScopeTrieIndex"/> filters passed down directory recursion.
/// Allows incremental per-directory compilation of nested <c>.gitignore</c> / <c>.codeexplorerignore</c> rules
/// without scanning unrelated sibling repositories or hash-looking up path prefixes on every file.
/// </summary>
public sealed class GitIgnoreScopeChain
{
    internal GitIgnoreScopeChain(GitIgnoreScopeChain[] frames)
    {
        Frames = frames;
    }

    internal GitIgnoreScopeChain(GitIgnoreScopeTrieIndex index, string? scopeRelativePath, string? scopePrefix)
    {
        Index = index;
        ScopeRelativePath = scopeRelativePath;
        ScopePrefix = scopePrefix;
        Frames = [this];
    }

    internal GitIgnoreScopeTrieIndex Index { get; init; } = null!;
    public string? ScopeRelativePath { get; init; }
    internal string? ScopePrefix { get; init; }
    internal GitIgnoreScopeChain[] Frames { get; }

    internal GitIgnoreScopeChain AppendChild(GitIgnoreScopeTrieIndex childIndex, string scopeRelativePath, string scopePrefix)
    {
        var nextFrames = new GitIgnoreScopeChain[Frames.Length + 1];
        Array.Copy(Frames, nextFrames, Frames.Length);
        var leaf = new GitIgnoreScopeChain(nextFrames)
        {
            Index = childIndex,
            ScopeRelativePath = scopeRelativePath,
            ScopePrefix = scopePrefix
        };
        nextFrames[^1] = leaf;
        return leaf;
    }
}

/// <summary>
/// Standalone zero-regex (with minimal fallback) Trie-based <c>.gitignore</c> / <c>.codeexplorerignore</c> matcher
/// supporting both path-indexed evaluation and incremental <see cref="GitIgnoreScopeChain"/> subtree compilation.
/// </summary>
public sealed class GitIgnoreTrieMatcher
{
    private static readonly string[] DefaultIgnoreFilePatterns =
    [
        "*.min.js",
        "*.min.mjs",
        "*.min.cjs",
        "*.min.css",
        "*.bundle.js",
        "*.bundle.min.js"
    ];

    private const char SlashChar = '/';
    private const char BackslashChar = '\\';
    private const char StarChar = '*';
    private const char BangChar = '!';
    private const char HashChar = '#';
    private const char OpenBracketChar = '[';
    private const string SlashStr = "/";
    private const string GlobstarPrefix = "**/";
    private const string EscapedBangPrefix = @"\!";
    private const string AnchoredPrefix = "^";
    private const string UnanchoredPrefix = "(^|/)";
    private const string DirOnlySuffix = "($|/)";
    private const string FileOrDirSuffix = "($|/|\\.)";
    private const string EscapedStar = "\\*";
    private const string RegexAny = ".*";
    private const string EscapedQuestion = "\\?";
    private const string RegexSingle = ".";

    public static readonly string[] IgnoreFileNames = [".gitignore", ".codeexplorerignore"];

    private static readonly ConcurrentDictionary<string, Regex> SharedFallbackRegexCache =
        new(StringComparer.Ordinal);

    private readonly string _workspaceRoot;
    private readonly WorkspaceFileFilter _filter;
    private readonly GitIgnoreScopeTrieIndex _globalIndex = new();
    private readonly Dictionary<string, GitIgnoreScopeTrieIndex> _scopedIndices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<GitIgnoreRuleEntry> _negatedRules = [];
    private readonly Lock _scopeMapLock = new();
    private int _ruleCounter;

    public string WorkspaceRoot => _workspaceRoot;

    public GitIgnoreScopeChain RootScope { get; }

    public GitIgnoreTrieMatcher(string workspaceRoot, WorkspaceFileFilter? fileFilter = null)
    {
        _workspaceRoot = workspaceRoot;
        _filter = fileFilter ?? WorkspaceFileFilter.Default;
        RootScope = new GitIgnoreScopeChain(_globalIndex, null, null);

        foreach (var dir in _filter.ActiveExcludedDirectoryNames)
        {
            if (!_filter.IsExcludedDirectory(dir)) continue;
            AddPattern(dir + SlashStr);
        }

        if (!_filter.IncludeDistAndMinifiedArtifacts)
        {
            foreach (var pattern in DefaultIgnoreFilePatterns)
            {
                AddPattern(pattern);
            }

            foreach (var ignoreFileName in IgnoreFileNames)
            {
                LoadFile(Path.Combine(workspaceRoot, ignoreFileName));
            }
            LoadWorkspaceSettings(workspaceRoot);
        }
    }

    public GitIgnoreScopeChain CompileChildScope(
        GitIgnoreScopeChain parentChain,
        string relativeDir,
        string? gitIgnoreFilePath,
        string? ceIgnoreFilePath,
        IReadOnlyList<string>? extraRelativePatterns = null)
    {
        if (_filter.IncludeDistAndMinifiedArtifacts && extraRelativePatterns is not { Count: > 0 })
        {
            return parentChain;
        }

        var hasGitIgnore = !string.IsNullOrEmpty(gitIgnoreFilePath) && !_filter.IncludeDistAndMinifiedArtifacts;
        var hasCeIgnore = !string.IsNullOrEmpty(ceIgnoreFilePath) && !_filter.IncludeDistAndMinifiedArtifacts;
        var hasExtra = extraRelativePatterns is { Count: > 0 };
        if (!hasGitIgnore && !hasCeIgnore && !hasExtra)
        {
            return parentChain;
        }

        var normScope = string.IsNullOrEmpty(relativeDir)
            ? null
            : relativeDir.Replace(BackslashChar, SlashChar).Trim(SlashChar);

        if (normScope == null)
        {
            if (hasExtra)
            {
                for (var i = 0; i < extraRelativePatterns!.Count; i++)
                {
                    AddPattern(extraRelativePatterns[i]);
                }
            }
            return parentChain;
        }

        var childIndex = new GitIgnoreScopeTrieIndex();
        if (hasGitIgnore && File.Exists(gitIgnoreFilePath!))
        {
            foreach (var line in File.ReadLines(gitIgnoreFilePath!))
            {
                CompilePatternIntoTarget(childIndex, line, normScope);
            }
        }
        if (hasCeIgnore && File.Exists(ceIgnoreFilePath!))
        {
            foreach (var line in File.ReadLines(ceIgnoreFilePath!))
            {
                CompilePatternIntoTarget(childIndex, line, normScope);
            }
        }
        if (hasExtra)
        {
            for (var i = 0; i < extraRelativePatterns!.Count; i++)
            {
                CompilePatternIntoTarget(childIndex, extraRelativePatterns[i], normScope);
            }
        }

        if (childIndex.IsEmpty)
        {
            return parentChain;
        }

        lock (_scopeMapLock)
        {
            _scopedIndices[normScope] = childIndex;
        }

        return parentChain.AppendChild(childIndex, normScope, normScope + SlashStr);
    }

    public bool IsIgnoredDirectoryInScope(GitIgnoreScopeChain scopeChain, string relativePath, out bool keptForLaterNegation)
    {
        keptForLaterNegation = false;
        relativePath = relativePath.Replace(BackslashChar, SlashChar).Trim(SlashChar);
        if (string.IsNullOrEmpty(relativePath)) return false;

        GitIgnoreRuleEntry? winningRule = null;
        var winningOrder = -1;
        var fullSpan = relativePath.AsSpan();
        var frames = scopeChain.Frames;

        for (var i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            if (frame.ScopePrefix == null)
            {
                GitIgnoreTrieEvaluator.EvaluateScopeForDirectory(frame.Index, _workspaceRoot, _filter, relativePath, fullSpan, ref winningRule, ref winningOrder);
            }
            else if (fullSpan.Length > frame.ScopePrefix.Length &&
                     fullSpan.StartsWith(frame.ScopePrefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                GitIgnoreTrieEvaluator.EvaluateScopeForDirectory(frame.Index, _workspaceRoot, _filter, relativePath, fullSpan[frame.ScopePrefix.Length..], ref winningRule, ref winningOrder);
            }
        }

        if (winningRule is not { IsNegated: false } activeExclusion)
        {
            return false;
        }

        if (!_negatedRules.IsEmpty && HasLaterNegationUnderneath(relativePath, activeExclusion.Order))
        {
            keptForLaterNegation = true;
            return false;
        }

        return true;
    }

    public bool IsIgnoredFileInScope(GitIgnoreScopeChain scopeChain, string relativePath, bool skipDirectoryOnlyRules)
    {
        relativePath = relativePath.Replace(BackslashChar, SlashChar).Trim(SlashChar);
        if (string.IsNullOrEmpty(relativePath)) return false;

        GitIgnoreRuleEntry? winningRule = null;
        var winningOrder = -1;
        var fullSpan = relativePath.AsSpan();
        var frames = scopeChain.Frames;

        for (var i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            if (frame.ScopePrefix == null)
            {
                GitIgnoreTrieEvaluator.EvaluateScopeForFile(frame.Index, _workspaceRoot, _filter, relativePath, fullSpan, skipDirectoryOnlyRules, ref winningRule, ref winningOrder);
            }
            else if (fullSpan.Length > frame.ScopePrefix.Length &&
                     fullSpan.StartsWith(frame.ScopePrefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                GitIgnoreTrieEvaluator.EvaluateScopeForFile(frame.Index, _workspaceRoot, _filter, relativePath, fullSpan[frame.ScopePrefix.Length..], skipDirectoryOnlyRules, ref winningRule, ref winningOrder);
            }
        }

        return winningRule is { IsNegated: false };
    }

    public void LoadScopedDirectory(string currentDir, string relativeDir)
    {
        if (string.IsNullOrEmpty(relativeDir) || _filter.IncludeDistAndMinifiedArtifacts) return;

        foreach (var ignoreFileName in IgnoreFileNames)
        {
            var filePath = Path.Combine(currentDir, ignoreFileName);
            if (File.Exists(filePath))
            {
                LoadScopedFile(filePath, relativeDir);
            }
        }
    }

    public void LoadWorkspaceSettings(string workspaceRoot, string? scope = null)
    {
        var settings = WorkspaceSettings.Load(workspaceRoot);
        foreach (var pattern in settings.Exclude)
        {
            AddPattern(pattern, scope);
        }
    }

    public void LoadFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        foreach (var line in File.ReadLines(filePath))
        {
            AddPattern(line);
        }
    }

    public void LoadScopedFile(string filePath, string scopeRelativePath)
    {
        if (!File.Exists(filePath)) return;

        var normalizedScope = scopeRelativePath.Replace(BackslashChar, SlashChar).Trim(SlashChar);
        foreach (var line in File.ReadLines(filePath))
        {
            AddPattern(line, normalizedScope);
        }
    }

    public void AddPattern(string pattern, string? scope = null)
    {
        var normScope = string.IsNullOrEmpty(scope) ? null : scope.Replace(BackslashChar, SlashChar).Trim(SlashChar);
        var targetIndex = GetOrCreateScopeIndex(normScope);
        CompilePatternIntoTarget(targetIndex, pattern, normScope);
    }

    public bool IsIgnored(string relativePath, bool isDirectory)
        => isDirectory
            ? IsIgnoredDirectory(relativePath, out _)
            : IsIgnoredFile(relativePath, skipDirectoryOnlyRules: false);

    public bool IsIgnoredDirectory(string relativePath, out bool keptForLaterNegation)
    {
        keptForLaterNegation = false;
        relativePath = relativePath.Replace(BackslashChar, SlashChar).Trim(SlashChar);
        if (string.IsNullOrEmpty(relativePath)) return false;

        GitIgnoreRuleEntry? winningRule = null;
        var winningOrder = -1;

        GitIgnoreTrieEvaluator.EvaluateScopeForDirectory(_globalIndex, _workspaceRoot, _filter, relativePath, relativePath.AsSpan(), ref winningRule, ref winningOrder);

        if (_scopedIndices.Count > 0)
        {
            var lookup = _scopedIndices.GetAlternateLookup<ReadOnlySpan<char>>();
            var span = relativePath.AsSpan();
            var searchFrom = 0;
            while (searchFrom < span.Length)
            {
                var nextRel = span[searchFrom..].IndexOf(SlashChar);
                if (nextRel < 0) break;
                var slashIdx = searchFrom + nextRel;
                if (lookup.TryGetValue(span[..slashIdx], out var scopedIndex))
                {
                    GitIgnoreTrieEvaluator.EvaluateScopeForDirectory(scopedIndex, _workspaceRoot, _filter, relativePath, span[(slashIdx + 1)..], ref winningRule, ref winningOrder);
                }
                searchFrom = slashIdx + 1;
            }
        }

        if (winningRule is not { IsNegated: false } activeExclusion)
        {
            return false;
        }

        if (!_negatedRules.IsEmpty && HasLaterNegationUnderneath(relativePath, activeExclusion.Order))
        {
            keptForLaterNegation = true;
            return false;
        }

        return true;
    }

    public bool IsIgnoredFile(string relativePath, bool skipDirectoryOnlyRules)
    {
        relativePath = relativePath.Replace(BackslashChar, SlashChar).Trim(SlashChar);
        if (string.IsNullOrEmpty(relativePath)) return false;

        GitIgnoreRuleEntry? winningRule = null;
        var winningOrder = -1;

        GitIgnoreTrieEvaluator.EvaluateScopeForFile(_globalIndex, _workspaceRoot, _filter, relativePath, relativePath.AsSpan(), skipDirectoryOnlyRules, ref winningRule, ref winningOrder);

        if (_scopedIndices.Count > 0)
        {
            var lookup = _scopedIndices.GetAlternateLookup<ReadOnlySpan<char>>();
            var span = relativePath.AsSpan();
            var searchFrom = 0;
            while (searchFrom < span.Length)
            {
                var nextRel = span[searchFrom..].IndexOf(SlashChar);
                if (nextRel < 0) break;
                var slashIdx = searchFrom + nextRel;
                if (lookup.TryGetValue(span[..slashIdx], out var scopedIndex))
                {
                    GitIgnoreTrieEvaluator.EvaluateScopeForFile(scopedIndex, _workspaceRoot, _filter, relativePath, span[(slashIdx + 1)..], skipDirectoryOnlyRules, ref winningRule, ref winningOrder);
                }
                searchFrom = slashIdx + 1;
            }
        }

        return winningRule is { IsNegated: false };
    }

    private void CompilePatternIntoTarget(GitIgnoreScopeTrieIndex targetIndex, string pattern, string? normScope)
    {
        var trimmed = pattern.Trim().Replace(BackslashChar, SlashChar);
        if (string.IsNullOrEmpty(trimmed) || trimmed[0] == HashChar) return;

        var isNegated = false;
        if (trimmed[0] == BangChar)
        {
            isNegated = true;
            trimmed = trimmed[1..].Trim();
            if (string.IsNullOrEmpty(trimmed)) return;
        }
        else if (trimmed.StartsWith(EscapedBangPrefix, StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
        }

        var isDirectoryOnly = false;
        if (trimmed[^1] == SlashChar)
        {
            isDirectoryOnly = true;
            trimmed = trimmed[..^1];
            if (string.IsNullOrEmpty(trimmed)) return;
        }

        var isAnchored = false;
        if (trimmed[0] == SlashChar)
        {
            isAnchored = true;
            trimmed = trimmed[1..];
        }
        else if (trimmed.Contains(SlashChar) && !trimmed.StartsWith(GlobstarPrefix, StringComparison.Ordinal))
        {
            isAnchored = true;
        }

        var rawPattern = trimmed.StartsWith(GlobstarPrefix, StringComparison.Ordinal)
            ? trimmed[GlobstarPrefix.Length..]
            : trimmed;

        var cleanPat = rawPattern.Trim(SlashChar);
        if (string.IsNullOrEmpty(cleanPat)) return;

        var order = Interlocked.Increment(ref _ruleCounter) - 1;
        var entry = new GitIgnoreRuleEntry(
            order,
            cleanPat,
            isDirectoryOnly,
            isNegated,
            isAnchored,
            normScope,
            normScope != null ? normScope + SlashStr : null);

        if (isNegated)
        {
            _negatedRules.Add(entry);
        }

        CompileRuleIntoTrie(targetIndex, cleanPat, in entry);
    }

    private GitIgnoreScopeTrieIndex GetOrCreateScopeIndex(string? normScope)
    {
        if (normScope == null) return _globalIndex;
        lock (_scopeMapLock)
        {
            if (!_scopedIndices.TryGetValue(normScope, out var index))
            {
                index = new GitIgnoreScopeTrieIndex();
                _scopedIndices[normScope] = index;
            }
            return index;
        }
    }

    private static void CompileRuleIntoTrie(GitIgnoreScopeTrieIndex index, string cleanPat, in GitIgnoreRuleEntry entry)
    {
        if (cleanPat.Contains(OpenBracketChar) || (!entry.IsAnchored && cleanPat.Contains(SlashChar)))
        {
            AddFallbackRegexRule(index, cleanPat, in entry);
            return;
        }

        if (entry.IsAnchored)
        {
            index.AddAnchoredPath(cleanPat.AsSpan(), in entry);
            return;
        }

        var span = cleanPat.AsSpan();
        if (!GitIgnoreGlobHelper.ContainsWildcard(span))
        {
            index.AddExactSegment(cleanPat, in entry);
            return;
        }

        if (span[0] == StarChar && span.Length > 1 && !GitIgnoreGlobHelper.ContainsWildcard(span[1..]))
        {
            index.AddReverseSuffix(span[1..], in entry);
            return;
        }

        if (span[^1] == StarChar && span.Length > 1 && !GitIgnoreGlobHelper.ContainsWildcard(span[..^1]))
        {
            index.AddForwardPrefix(span[..^1], in entry);
            return;
        }

        index.AddUnanchoredWildcard(cleanPat, in entry);
    }

    private static void AddFallbackRegexRule(GitIgnoreScopeTrieIndex index, string cleanPat, in GitIgnoreRuleEntry entry)
    {
        try
        {
            var escaped = Regex.Escape(cleanPat);
            var regexPattern = escaped.Replace(EscapedStar, RegexAny).Replace(EscapedQuestion, RegexSingle);
            regexPattern = (entry.IsAnchored ? AnchoredPrefix : UnanchoredPrefix)
                           + regexPattern
                           + (entry.IsDirectoryOnly ? DirOnlySuffix : FileOrDirSuffix);
            var regex = SharedFallbackRegexCache.GetOrAdd(
                regexPattern,
                static pat => new Regex(pat, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            index.FallbackRules.Add(new GitIgnoreFallbackRegexRule(entry, regex, GitIgnoreGlobHelper.ExtractLongestLiteralToken(cleanPat)));
        }
        catch
        {
            // Ignore malformed patterns
        }
    }

    private bool HasLaterNegationUnderneath(string relativeDir, int afterRuleIndex)
    {
        if (afterRuleIndex < 0 || _negatedRules.IsEmpty) return false;

        if (GitIgnoreTrieEvaluator.HasDescendantNegationInAnchoredTrie(_globalIndex.AnchoredRoot, relativeDir.AsSpan(), afterRuleIndex))
        {
            return true;
        }

        var dirPrefix = relativeDir.EndsWith(SlashChar) ? relativeDir : relativeDir + SlashStr;
        foreach (var rule in _negatedRules)
        {
            if (rule.Order <= afterRuleIndex) continue;

            if (rule.Scope != null && rule.ScopePrefix != null)
            {
                if (rule.Scope.StartWithIC(dirPrefix)) return true;
                if (dirPrefix.StartWithIC(rule.ScopePrefix))
                {
                    var relToScope = dirPrefix[rule.ScopePrefix.Length..];
                    if (rule.CleanPattern.StartWithIC(relToScope)) return true;
                }
            }
            else if (rule.CleanPattern.StartWithIC(dirPrefix))
            {
                return true;
            }
        }

        return false;
    }
}
