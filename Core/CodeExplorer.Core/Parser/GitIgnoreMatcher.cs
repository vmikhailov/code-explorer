namespace CodeExplorer.Parser;

public class GitIgnoreMatcher
{
    private readonly List<(string Pattern, System.Text.RegularExpressions.Regex Regex, bool IsDirectoryOnly)> _rules = new();

    public GitIgnoreMatcher(string workspaceRoot)
    {
        var gitignorePath = Path.Combine(workspaceRoot, ".gitignore");
        if (!File.Exists(gitignorePath)) return;

        foreach (var line in File.ReadLines(gitignorePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

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

            var escaped = System.Text.RegularExpressions.Regex.Escape(trimmed);
            var regexPattern = escaped
                .Replace("\\*", ".*")
                .Replace("\\?", ".");

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
                var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
                _rules.Add((trimmed, regex, isDirectoryOnly));
            }
            catch
            {
                // Ignore malformed patterns
            }
        }
    }

    public bool IsIgnored(string relativePath, bool isDirectory)
    {
        relativePath = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(relativePath)) return false;

        foreach (var rule in _rules)
        {
            if (rule.IsDirectoryOnly && !isDirectory) continue;

            if (rule.Regex.IsMatch(relativePath))
            {
                return true;
            }
        }

        return false;
    }
}
