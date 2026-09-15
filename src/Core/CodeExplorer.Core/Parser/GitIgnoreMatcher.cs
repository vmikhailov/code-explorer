using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

public class GitIgnoreMatcher
{
    private static readonly string[] DefaultIgnorePatterns =
    [
        "node_modules/",
        "bin/",
        "obj/",
        "packages/",
        "dist/",
        "build/",
        ".Build/",
        ".next/",
        ".nuxt/",
        ".output/",
        "out/",
        "coverage/",
        ".git/",
        ".github/",
        ".turbo/",
        ".cache/",
        ".vscode/",
        ".idea/",
        ".vs/",
        "*.min.js",
        "*.min.css",
        "*.bundle.js",
        "*.bundle.min.js"
    ];

    private readonly List<(string Pattern, Regex Regex, bool IsDirectoryOnly, string? Scope)> _rules = [];

    public GitIgnoreMatcher(string workspaceRoot)
    {
        foreach (var pattern in DefaultIgnorePatterns)
        {
            AddPattern(pattern);
        }

        LoadFile(Path.Combine(workspaceRoot, ".gitignore"));
        LoadFile(Path.Combine(workspaceRoot, ".codeexplorerignore"));
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
        var trimmed = pattern.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) return;

        var isDirectoryOnly = false;

        if (trimmed.EndsWith('/'))
        {
            isDirectoryOnly = true;
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        var isAnchored = false;

        if (trimmed.StartsWith('/'))
        {
            isAnchored = true;
            trimmed = trimmed.Substring(1);
        }

        var escaped = Regex.Escape(trimmed);
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
            _rules.Add((trimmed, regex, isDirectoryOnly, string.IsNullOrEmpty(scope) ? null : scope));
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

        foreach (var rule in _rules)
        {
            var testPath = relativePath;
            if (rule.Scope != null)
            {
                if (string.Equals(relativePath, rule.Scope, StringComparison.OrdinalIgnoreCase))
                {
                    // A scoped rule loaded from inside a directory cannot ignore the directory itself
                    continue;
                }

                if (!relativePath.StartsWith(rule.Scope + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                testPath = relativePath.Substring(rule.Scope.Length + 1);
            }

            if (rule.IsDirectoryOnly && !isDirectory)
            {
                // Directory-only rules like "foo/" match a file if it is inside that directory (followed by '/')
                var m = rule.Regex.Match(testPath);
                if (m.Success && m.Value.EndsWith('/'))
                {
                    return true;
                }
                continue;
            }

            if (rule.Regex.IsMatch(testPath))
            {
                return true;
            }
        }

        return false;
    }
}
