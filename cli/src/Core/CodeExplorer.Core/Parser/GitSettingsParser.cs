using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser;

public static class GitSettingsParser
{
    public static GitSettingsNode? Parse(string workspaceId, string workspacePath)
        => Parse(workspaceId, workspacePath, workspacePath);

    public static GitSettingsNode? Parse(string workspaceId, string workspacePath, string repoDir)
    {
        var gitPath = Path.Combine(repoDir, ".git");
        string actualGitDir;
        if (Directory.Exists(gitPath))
        {
            actualGitDir = gitPath;
        }
        else if (File.Exists(gitPath))
        {
            try
            {
                var content = File.ReadAllText(gitPath).Trim();
                if (content.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
                {
                    var relGitDir = content["gitdir:".Length..].Trim();
                    actualGitDir = Path.IsPathRooted(relGitDir)
                        ? relGitDir
                        : Path.GetFullPath(Path.Combine(repoDir, relGitDir));
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        if (!Directory.Exists(actualGitDir))
        {
            return null;
        }

        var branch = "Unknown";
        var commitHash = "";
        try
        {
            var headPath = Path.Combine(actualGitDir, "HEAD");
            if (File.Exists(headPath))
            {
                var headContent = File.ReadAllText(headPath).Trim();
                if (headContent.StartsWith("ref:", StringComparison.Ordinal))
                {
                    var refPath = headContent["ref:".Length..].Trim();
                    branch = refPath;
                    if (branch.StartsWith("refs/heads/", StringComparison.Ordinal))
                    {
                        branch = branch["refs/heads/".Length..];
                    }

                    try
                    {
                        var refFile = Path.Combine(actualGitDir, refPath.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(refFile))
                        {
                            commitHash = File.ReadAllText(refFile).Trim();
                        }
                        else
                        {
                            var packedRefs = Path.Combine(actualGitDir, "packed-refs");
                            if (File.Exists(packedRefs))
                            {
                                foreach (var line in File.ReadLines(packedRefs))
                                {
                                    var trimmed = line.Trim();
                                    if (trimmed.StartsWith('#') || trimmed.StartsWith('^') || string.IsNullOrEmpty(trimmed))
                                        continue;
                                    var sp = trimmed.IndexOf(' ');
                                    if (sp > 0 && trimmed[(sp + 1)..].Trim() == refPath)
                                    {
                                        commitHash = trimmed[..sp].Trim();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Commit resolution fallback
                    }
                }
                else if (headContent.Length >= 40)
                {
                    commitHash = headContent;
                    branch = $"Detached HEAD ({headContent[..7]})";
                }
            }
        }
        catch
        {
            // Ignore and fallback
        }

        var originUrl = "";
        var fallbackRemoteUrl = "";
        var userName = "";
        var userEmail = "";

        try
        {
            var configPath = Path.Combine(actualGitDir, "config");
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
                        currentSection = line[1..^1].Trim();
                        continue;
                    }

                    var eqIndex = line.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        var key = line[..eqIndex].Trim();
                        var val = line[(eqIndex + 1)..].Trim().Trim('"');

                        if (currentSection.Equals("remote \"origin\"", StringComparison.OrdinalIgnoreCase) &&
                            key.Equals("url", StringComparison.OrdinalIgnoreCase))
                        {
                            originUrl = val;
                        }
                        else if (currentSection.StartsWith("remote \"", StringComparison.OrdinalIgnoreCase) &&
                            key.Equals("url", StringComparison.OrdinalIgnoreCase) &&
                            string.IsNullOrEmpty(fallbackRemoteUrl))
                        {
                            fallbackRemoteUrl = val;
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

        if (string.IsNullOrEmpty(originUrl) && !string.IsNullOrEmpty(fallbackRemoteUrl))
        {
            originUrl = fallbackRemoteUrl;
        }

        // Global ~/.gitconfig fallback for user name and email
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userEmail))
        {
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var globalConfig = Path.Combine(home, ".gitconfig");
                if (File.Exists(globalConfig))
                {
                    var currentSection = "";
                    foreach (var rawLine in File.ReadLines(globalConfig))
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                            continue;
                        if (line.StartsWith('[') && line.EndsWith(']'))
                        {
                            currentSection = line[1..^1].Trim();
                            continue;
                        }
                        var eqIndex = line.IndexOf('=');
                        if (eqIndex > 0 && currentSection.Equals("user", StringComparison.OrdinalIgnoreCase))
                        {
                            var key = line[..eqIndex].Trim();
                            var val = line[(eqIndex + 1)..].Trim().Trim('"');
                            if (key.Equals("name", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(userName))
                                userName = val;
                            else if (key.Equals("email", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(userEmail))
                                userEmail = val;
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }

        // Compute relative path from workspace root
        var relPath = "";
        try
        {
            relPath = Path.GetRelativePath(workspacePath, repoDir).Replace('\\', '/');
            if (relPath == "." || relPath.StartsWith("../") || relPath.StartsWith("..\\"))
            {
                relPath = "";
            }
        }
        catch
        {
            relPath = "";
        }

        var id = string.IsNullOrEmpty(relPath)
            ? $"{workspaceId}:gitsettings"
            : $"{workspaceId}:gitsettings:{relPath}";

        var repoName = !string.IsNullOrEmpty(relPath)
            ? Path.GetFileName(repoDir.TrimEnd('/', '\\'))
            : Path.GetFileName(workspacePath.TrimEnd('/', '\\'));

        var name = string.IsNullOrEmpty(relPath)
            ? "Git Settings"
            : $"Git Settings ({repoName})";

        var extensions = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(commitHash))
        {
            extensions["commit_hash"] = commitHash;
            extensions["head_commit"] = commitHash;
        }
        if (!string.IsNullOrEmpty(repoName))
        {
            extensions["repo_name"] = repoName;
        }

        return new GitSettingsNode(
            id,
            name,
            branch,
            originUrl,
            userName,
            userEmail,
            relPath,
            extensions
        );
    }

    public static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
