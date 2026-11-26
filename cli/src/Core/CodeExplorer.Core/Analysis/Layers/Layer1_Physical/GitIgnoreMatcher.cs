using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

public class GitIgnoreMatcher
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
    private const string SlashStr = "/";

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Regex> SharedRegexCache =
        new(StringComparer.Ordinal);

    private sealed record Rule(
        int Order,
        string OriginalPattern,
        string CleanPattern,
        Regex Regex,
        string? RequiredLiteral,
        bool IsDirectoryOnly,
        bool IsNegated,
        bool IsAnchored,
        string? Scope,
        string? ScopePrefix);

    public static readonly string[] IgnoreFileNames = [".gitignore", ".codeexplorerignore"];

    private readonly string _workspaceRoot;
    private readonly WorkspaceFileFilter _filter;
    private readonly List<Rule> _rules = [];
    private readonly List<Rule> _globalRules = [];
    private readonly Dictionary<string, List<Rule>> _scopedRulesByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Rule> _negatedRules = [];
    private bool _hasGlobalRuleAfterScoped;

    public string WorkspaceRoot => _workspaceRoot;

    public GitIgnoreMatcher(string workspaceRoot, WorkspaceFileFilter? fileFilter = null)
    {
        _workspaceRoot = workspaceRoot;
        _filter = fileFilter ?? WorkspaceFileFilter.Default;
        foreach (var dir in _filter.ActiveExcludedDirectoryNames)
        {
            if (!_filter.IsExcludedDirectory(dir)) continue;
            AddPattern(dir + "/");
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

        var normalizedScope = scopeRelativePath.Replace('\\', '/').Trim('/');
        foreach (var line in File.ReadLines(filePath))
        {
            AddPattern(line, normalizedScope);
        }
    }

    public void AddPattern(string pattern, string? scope = null)
    {
        var trimmed = pattern.Trim().Replace('\\', '/');
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) return;

        var isNegated = false;
        if (trimmed.StartsWith('!'))
        {
            isNegated = true;
            trimmed = trimmed[1..].Trim();
            if (string.IsNullOrEmpty(trimmed)) return;
        }
        else if (trimmed.StartsWith(@"\!"))
        {
            trimmed = trimmed[1..];
        }

        var isDirectoryOnly = false;
        if (trimmed.EndsWith('/'))
        {
            isDirectoryOnly = true;
            trimmed = trimmed[..^1];
        }

        var isAnchored = false;
        if (trimmed.StartsWith('/'))
        {
            isAnchored = true;
            trimmed = trimmed[1..];
        }
        else if (trimmed.Contains('/') && !trimmed.StartsWith("**/"))
        {
            isAnchored = true;
        }

        var rawPattern = trimmed;
        if (rawPattern.StartsWith("**/"))
        {
            rawPattern = rawPattern[3..];
        }

        var escaped = Regex.Escape(rawPattern);
        var regexPattern = escaped.Replace("\\*", ".*").Replace("\\?", ".");

        if (isAnchored)
        {
            regexPattern = "^" + regexPattern;
        }
        else
        {
            regexPattern = "(^|/)" + regexPattern;
        }

        if (isDirectoryOnly)
        {
            regexPattern += "($|/)";
        }
        else
        {
            regexPattern += "($|/|\\.)";
        }

        try
        {
            var normScope = string.IsNullOrEmpty(scope) ? null : scope.Replace('\\', SlashChar).Trim(SlashChar);
            var regex = SharedRegexCache.GetOrAdd(
                regexPattern,
                static pat => new Regex(pat, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            var cleanPat = rawPattern.Replace('\\', SlashChar).Trim(SlashChar);
            var requiredLiteral = ExtractLongestLiteralToken(cleanPat);
            var rule = new Rule(
                _rules.Count,
                pattern,
                cleanPat,
                regex,
                requiredLiteral,
                isDirectoryOnly,
                isNegated,
                isAnchored,
                normScope,
                normScope != null ? normScope + SlashStr : null);
            _rules.Add(rule);
            if (isNegated)
            {
                _negatedRules.Add(rule);
            }

            if (normScope == null)
            {
                _globalRules.Add(rule);
                if (_scopedRulesByScope.Count > 0)
                {
                    _hasGlobalRuleAfterScoped = true;
                }
            }
            else
            {
                if (!_scopedRulesByScope.TryGetValue(normScope, out var bucket))
                {
                    bucket = [];
                    _scopedRulesByScope[normScope] = bucket;
                }
                bucket.Add(rule);
            }
        }
        catch
        {
            // Ignore malformed patterns
        }
    }

    public bool IsIgnored(string relativePath, bool isDirectory)
        => isDirectory
            ? IsIgnoredDirectory(relativePath, out _)
            : IsIgnoredFile(relativePath, skipDirectoryOnlyRules: false);

    public bool IsIgnoredDirectory(string relativePath, out bool keptForLaterNegation)
    {
        keptForLaterNegation = false;
        relativePath = relativePath.Replace('\\', SlashChar).Trim(SlashChar);
        if (string.IsNullOrEmpty(relativePath)) return false;

        var ignored = false;
        var lastExclusionIndex = -1;

        if (_hasGlobalRuleAfterScoped)
        {
            EvaluateDirectoryRuleList(_rules, relativePath, relativePath, useRuleScope: true, ref ignored, ref lastExclusionIndex);
        }
        else
        {
            EvaluateDirectoryRuleList(_globalRules, relativePath, relativePath, useRuleScope: false, ref ignored, ref lastExclusionIndex);
            if (_scopedRulesByScope.Count > 0)
            {
                var lookup = _scopedRulesByScope.GetAlternateLookup<ReadOnlySpan<char>>();
                var span = relativePath.AsSpan();
                var searchFrom = 0;
                while (searchFrom < span.Length)
                {
                    var nextRel = span[searchFrom..].IndexOf(SlashChar);
                    if (nextRel < 0) break;
                    var slashIdx = searchFrom + nextRel;
                    if (lookup.TryGetValue(span[..slashIdx], out var scopedBucket))
                    {
                        var subPath = relativePath[(slashIdx + 1)..];
                        EvaluateDirectoryRuleList(scopedBucket, relativePath, subPath, useRuleScope: false, ref ignored, ref lastExclusionIndex);
                    }
                    searchFrom = slashIdx + 1;
                }
            }
        }

        if (ignored && _negatedRules.Count > 0 && HasLaterNegationUnderneath(relativePath, lastExclusionIndex))
        {
            keptForLaterNegation = true;
            return false;
        }

        return ignored;
    }

    private void EvaluateDirectoryRuleList(
        List<Rule> rules,
        string fullRelativePath,
        string candidatePath,
        bool useRuleScope,
        ref bool ignored,
        ref int lastExclusionIndex)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var testPath = candidatePath;

            if (useRuleScope && rule.Scope != null)
            {
                if (string.Equals(candidatePath, rule.Scope, StringComparison.OrdinalIgnoreCase) ||
                    rule.ScopePrefix == null ||
                    !candidatePath.StartWithIC(rule.ScopePrefix))
                {
                    continue;
                }
                testPath = candidatePath[rule.ScopePrefix.Length..];
            }

            if (rule.RequiredLiteral != null &&
                !testPath.Contains(rule.RequiredLiteral, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rule.Regex.IsMatch(testPath))
            {
                if (!string.IsNullOrEmpty(_workspaceRoot) &&
                    _filter.CheckDirectoryExclusion(rule.CleanPattern, Path.Combine(_workspaceRoot, fullRelativePath)) == false)
                {
                    continue;
                }

                if (rule.IsNegated)
                {
                    ignored = false;
                }
                else
                {
                    ignored = true;
                    lastExclusionIndex = rule.Order;
                }
            }
        }
    }

    public bool IsIgnoredFile(string relativePath, bool skipDirectoryOnlyRules)
    {
        relativePath = relativePath.Replace('\\', SlashChar).Trim(SlashChar);
        if (string.IsNullOrEmpty(relativePath)) return false;

        var ignoredByDir = false;
        var ignoredByFile = false;

        if (_hasGlobalRuleAfterScoped)
        {
            EvaluateFileRuleList(_rules, relativePath, relativePath, skipDirectoryOnlyRules, useRuleScope: true, ref ignoredByDir, ref ignoredByFile);
            return ignoredByDir || ignoredByFile;
        }

        EvaluateFileRuleList(_globalRules, relativePath, relativePath, skipDirectoryOnlyRules, useRuleScope: false, ref ignoredByDir, ref ignoredByFile);

        if (_scopedRulesByScope.Count > 0)
        {
            var lookup = _scopedRulesByScope.GetAlternateLookup<ReadOnlySpan<char>>();
            var span = relativePath.AsSpan();
            var searchFrom = 0;
            while (searchFrom < span.Length)
            {
                var nextRel = span[searchFrom..].IndexOf(SlashChar);
                if (nextRel < 0) break;
                var slashIdx = searchFrom + nextRel;
                if (lookup.TryGetValue(span[..slashIdx], out var scopedBucket))
                {
                    var subPath = relativePath[(slashIdx + 1)..];
                    EvaluateFileRuleList(scopedBucket, relativePath, subPath, skipDirectoryOnlyRules, useRuleScope: false, ref ignoredByDir, ref ignoredByFile);
                }
                searchFrom = slashIdx + 1;
            }
        }

        return ignoredByDir || ignoredByFile;
    }

    private void EvaluateFileRuleList(
        List<Rule> rules,
        string fullRelativePath,
        string candidatePath,
        bool skipDirectoryOnlyRules,
        bool useRuleScope,
        ref bool ignoredByDir,
        ref bool ignoredByFile)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (skipDirectoryOnlyRules && rule.IsDirectoryOnly)
            {
                continue;
            }

            var testPath = candidatePath;
            if (useRuleScope && rule.ScopePrefix != null)
            {
                if (!candidatePath.StartWithIC(rule.ScopePrefix))
                {
                    continue;
                }
                testPath = candidatePath[rule.ScopePrefix.Length..];
            }

            if (rule.RequiredLiteral != null &&
                !testPath.Contains(rule.RequiredLiteral, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rule.IsDirectoryOnly)
            {
                var m = rule.Regex.Match(testPath);
                if (m.Success && m.Value.EndsWith(SlashChar))
                {
                    if (!string.IsNullOrEmpty(_workspaceRoot) &&
                        _filter.CheckDirectoryExclusion(rule.CleanPattern, Path.Combine(_workspaceRoot, fullRelativePath)) == false)
                    {
                        continue;
                    }

                    ignoredByDir = !rule.IsNegated;
                }
            }
            else if (rule.Regex.IsMatch(testPath))
            {
                if (rule.IsNegated)
                {
                    ignoredByFile = false;
                    ignoredByDir = false;
                }
                else
                {
                    ignoredByFile = true;
                }
            }
        }
    }

    private bool HasLaterNegationUnderneath(string relativeDir, int afterRuleIndex)
    {
        if (afterRuleIndex < 0 || _negatedRules.Count == 0) return false;

        var dirPrefix = relativeDir.EndsWith(SlashChar) ? relativeDir : relativeDir + SlashStr;

        for (var i = 0; i < _negatedRules.Count; i++)
        {
            var rule = _negatedRules[i];
            if (rule.Order <= afterRuleIndex) continue;

            if (rule.Scope != null && rule.ScopePrefix != null)
            {
                if (rule.Scope.StartWithIC(dirPrefix))
                {
                    return true;
                }

                if (dirPrefix.StartWithIC(rule.ScopePrefix))
                {
                    var relToScope = dirPrefix[rule.ScopePrefix.Length..];
                    if (rule.CleanPattern.StartWithIC(relToScope))
                    {
                        return true;
                    }
                }
            }
            else
            {
                if (rule.CleanPattern.StartWithIC(dirPrefix) ||
                    (rule.IsAnchored && rule.CleanPattern.StartWithIC(dirPrefix)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ExtractLongestLiteralToken(string cleanPattern)
    {
        if (string.IsNullOrEmpty(cleanPattern) || cleanPattern.Contains('['))
        {
            return null;
        }

        string? best = null;
        var span = cleanPattern.AsSpan();
        var start = 0;
        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] is '*' or '?')
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
