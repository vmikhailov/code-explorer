using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

public class GitIgnoreMatcher
{
    private readonly List<(string Pattern, Regex Regex, bool IsDirectoryOnly)> _rules = [];

    public GitIgnoreMatcher(string workspaceRoot)
    {
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

    public void AddPattern(string pattern)
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
            _rules.Add((trimmed, regex, isDirectoryOnly));
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
            if (rule.IsDirectoryOnly && !isDirectory)
            {
                // Directory-only rules like "foo/" match a file if it is inside that directory (followed by '/')
                var m = rule.Regex.Match(relativePath);
                if (m.Success && m.Value.EndsWith('/'))
                {
                    return true;
                }
                continue;
            }

            if (rule.Regex.IsMatch(relativePath))
            {
                return true;
            }
        }

        return false;
    }
}
