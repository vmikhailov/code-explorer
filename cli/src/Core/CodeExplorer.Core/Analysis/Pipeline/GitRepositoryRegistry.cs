using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser;

public sealed class GitRepositoryRegistry
{
    public Dictionary<string, GitSettingsNode> Repositories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string repoAbsDir, GitSettingsNode settings)
    {
        var norm = Path.GetFullPath(repoAbsDir).Replace('\\', '/').TrimEnd('/');
        Repositories[norm] = settings;
    }

    public GitSettingsNode? FindForPath(string workspaceId, string absoluteWorkspacePath, string path)
    {
        var absPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(absoluteWorkspacePath, path));
        var norm = absPath.Replace('\\', '/').TrimEnd('/');

        GitSettingsNode? bestMatch = null;
        var bestLen = -1;

        foreach (var (repoDir, settings) in Repositories)
        {
            if (norm.Equals(repoDir, StringComparison.OrdinalIgnoreCase) ||
                norm.StartWithIC(repoDir + "/"))
            {
                if (repoDir.Length > bestLen)
                {
                    bestLen = repoDir.Length;
                    bestMatch = settings;
                }
            }
        }

        if (bestMatch != null) return bestMatch;

        var root = GitSettingsParser.FindRepoRoot(absPath);
        if (root != null)
        {
            var parsed = GitSettingsParser.Parse(workspaceId, absoluteWorkspacePath, root);
            if (parsed != null)
            {
                Register(root, parsed);
                return parsed;
            }
        }

        return null;
    }
}
