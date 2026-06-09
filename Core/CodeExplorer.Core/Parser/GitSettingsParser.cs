using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser;

public static class GitSettingsParser
{
    public static GitSettingsNode? Parse(string workspaceId, string workspacePath)
    {
        var gitDir = Path.Combine(workspacePath, ".git");
        if (!Directory.Exists(gitDir))
        {
            return null;
        }

        var branch = "Unknown";
        try
        {
            var headPath = Path.Combine(gitDir, "HEAD");
            if (File.Exists(headPath))
            {
                var headContent = File.ReadAllText(headPath).Trim();
                if (headContent.StartsWith("ref:"))
                {
                    branch = headContent.Substring("ref:".Length).Trim();
                    if (branch.StartsWith("refs/heads/"))
                    {
                        branch = branch.Substring("refs/heads/".Length);
                    }
                }
                else if (headContent.Length == 40)
                {
                    branch = $"Detached HEAD ({headContent.Substring(0, 7)})";
                }
            }
        }
        catch
        {
            // Ignore and fallback
        }

        var originUrl = "";
        var userName = "";
        var userEmail = "";

        try
        {
            var configPath = Path.Combine(gitDir, "config");
            if (File.Exists(configPath))
            {
                var currentSection = "";
                foreach (var rawLine in File.ReadLines(configPath))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                        continue;

                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    var eqIndex = line.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        var key = line.Substring(0, eqIndex).Trim();
                        var val = line.Substring(eqIndex + 1).Trim().Trim('"');

                        if (currentSection.Equals("remote \"origin\"", StringComparison.OrdinalIgnoreCase) &&
                            key.Equals("url", StringComparison.OrdinalIgnoreCase))
                        {
                            originUrl = val;
                        }
                        else if (currentSection.Equals("user", StringComparison.OrdinalIgnoreCase))
                        {
                            if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                            {
                                userName = val;
                            }
                            else if (key.Equals("email", StringComparison.OrdinalIgnoreCase))
                            {
                                userEmail = val;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore and fallback
        }

        var id = $"{workspaceId}:gitsettings";
        return new GitSettingsNode(
            id,
            "Git Settings",
            branch,
            originUrl,
            userName,
            userEmail,
            string.Empty
        );
    }
}
