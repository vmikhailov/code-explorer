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

    private sealed record Rule(
        string OriginalPattern,
        string CleanPattern,
        Regex Regex,
        bool IsDirectoryOnly,
        bool IsNegated,
        bool IsAnchored,
        string? Scope);

    public static readonly string[] IgnoreFileNames = [".gitignore", ".codeexplorerignore"];

    private readonly string _workspaceRoot;
    private readonly WorkspaceFileFilter _filter;
    private readonly List<Rule> _rules = [];

    public string WorkspaceRoot => _workspaceRoot;

    public GitIgnoreMatcher(string workspaceRoot, WorkspaceFileFilter? fileFilter = null)
    {
        _workspaceRoot = workspaceRoot;
        _filter = fileFilter ?? WorkspaceFileFilter.Default;
        foreach (var dir in _filter.ActiveExcludedDirectoryNames)
        {
            AddPattern(dir + "/");
        }

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

    public void LoadScopedDirectory(string currentDir, string relativeDir)
    {
        if (string.IsNullOrEmpty(relativeDir)) return;

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
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _rules.Add(new Rule(
                pattern,
                rawPattern.Replace('\\', '/').Trim('/'),
                regex,
                isDirectoryOnly,
                isNegated,
                isAnchored,
                string.IsNullOrEmpty(scope) ? null : scope));
        }
        catch
        {
            // Ignore malformed patterns
        }
    }

    public bool IsIgnored(string relativePath, bool isDirectory)
    {
        relativePath = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(relativePath)) return false;

        if (isDirectory)
        {
            var ignored = false;
            var lastExclusionIndex = -1;

            for (var i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                var testPath = relativePath;

                if (rule.Scope != null)
                {
                    if (string.Equals(relativePath, rule.Scope, StringComparison.OrdinalIgnoreCase))
                    {
                        // A scoped rule loaded from inside a directory cannot ignore the directory itself
                        continue;
                    }

                    if (!relativePath.StartWithIC(rule.Scope + "/"))
                    {
                        continue;
                    }

                    testPath = relativePath[(rule.Scope.Length + 1)..];
                }

                if (rule.Regex.IsMatch(testPath))
                {
                    if (!string.IsNullOrEmpty(_workspaceRoot) &&
                        _filter.CheckDirectoryExclusion(rule.CleanPattern, Path.Combine(_workspaceRoot, relativePath)) == false)
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
                        lastExclusionIndex = i;
                    }
                }
            }

            if (ignored && HasLaterNegationUnderneath(relativePath, lastExclusionIndex))
            {
                return false;
            }

            return ignored;
        }
        else
        {
            var ignoredByDir = false;
            var ignoredByFile = false;

            for (var i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                var testPath = relativePath;

                if (rule.Scope != null)
                {
                    if (!relativePath.StartWithIC(rule.Scope + "/"))
                    {
                        continue;
                    }

                    testPath = relativePath[(rule.Scope.Length + 1)..];
                }

                if (rule.IsDirectoryOnly)
                {
                    var m = rule.Regex.Match(testPath);
                    if (m.Success && m.Value.EndsWith('/'))
                    {
                        if (!string.IsNullOrEmpty(_workspaceRoot) &&
                            _filter.CheckDirectoryExclusion(rule.CleanPattern, Path.Combine(_workspaceRoot, relativePath)) == false)
                        {
                            continue;
                        }

                        if (rule.IsNegated)
                        {
                            ignoredByDir = false;
                        }
                        else
                        {
                            ignoredByDir = true;
                        }
                    }
                }
                else
                {
                    if (rule.Regex.IsMatch(testPath))
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

            return ignoredByDir || ignoredByFile;
        }
    }

    private bool HasLaterNegationUnderneath(string relativeDir, int afterRuleIndex)
    {
        if (afterRuleIndex < 0) return false;

        var dirPrefix = relativeDir.EndsWith('/') ? relativeDir : relativeDir + "/";

        for (var i = afterRuleIndex + 1; i < _rules.Count; i++)
        {
            var rule = _rules[i];
            if (!rule.IsNegated) continue;

            if (rule.Scope != null)
            {
                if (rule.Scope.StartWithIC(dirPrefix))
                {
                    return true;
                }

                if (dirPrefix.StartWithIC(rule.Scope + "/"))
                {
                    var relToScope = dirPrefix[(rule.Scope.Length + 1)..];
                    if (rule.CleanPattern.StartWithIC(relToScope))
                    {
                        return true;
                    }
                }
            }
            else
            {
                if (rule.CleanPattern.StartWithIC(dirPrefix))
                {
                    return true;
                }

                if (rule.IsAnchored && rule.CleanPattern.StartWithIC(relativeDir + "/"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
