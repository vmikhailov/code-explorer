namespace CodeExplorer.Core.Parser;

internal static class WorkspaceCandidateEnumerator
{
    private const string DotGitIgnoreName = ".gitignore";
    private const string DotCeIgnoreName = ".codeexplorerignore";

    public static List<string> EnumerateWithTrie(
        WorkspaceFileFilter filter,
        string targetPath,
        string workspaceRoot,
        GitIgnoreTrieMatcher gitTrie,
        Action<string>? log = null)
    {
        var result = new List<string>();
        if (!Directory.Exists(targetPath)) return result;

        var initialParsers = filter.ResolveAncestorProjectParsers(targetPath, workspaceRoot);
        var ownedFiles = new List<string>();
        var unownedFallbackFiles = new List<string>();
        var hasAnyOwnedDirectory = false;
        var scratchPatterns = new List<string>();

        var stack = new Stack<(string Dir, GitIgnoreScopeChain GitScope, IReadOnlyList<IProjectParser>? InheritedParsers)>();
        stack.Push((targetPath, gitTrie.RootScope, initialParsers));

        while (stack.Count > 0)
        {
            var (currentDir, parentScope, inheritedParsers) = stack.Pop();
            var isRoot = string.Equals(currentDir, workspaceRoot, StringComparison.OrdinalIgnoreCase);
            var relativeDir = isRoot ? string.Empty : currentDir.GetNormalizedRelativePath(workspaceRoot);
            var dirName = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

            if (!isRoot && filter.IsExcludedDirectory(dirName, currentDir, inheritedParsers)) continue;
            if (!isRoot && GitSettingsParser.IsGitWorktreeDirectory(currentDir)) continue;

            var keptForNegation = false;
            if (!string.IsNullOrEmpty(relativeDir) && gitTrie.IsIgnoredDirectoryInScope(parentScope, relativeDir, out keptForNegation))
            {
                continue;
            }

            DirectoryInfo dirInfo;
            FileInfo[] files;
            DirectoryInfo[] subDirs;
            try
            {
                dirInfo = new DirectoryInfo(currentDir);
                files = dirInfo.GetFiles();
                subDirs = dirInfo.GetDirectories();
            }
            catch
            {
                continue;
            }

            string? gitIgnoreFile = null;
            string? ceIgnoreFile = null;
            for (var i = 0; i < files.Length; i++)
            {
                var fn = files[i].Name;
                if (fn.Equals(DotGitIgnoreName, StringComparison.OrdinalIgnoreCase)) gitIgnoreFile = files[i].FullName;
                else if (fn.Equals(DotCeIgnoreName, StringComparison.OrdinalIgnoreCase)) ceIgnoreFile = files[i].FullName;
            }

            var activeParsers = filter.ResolveDirectoryProjectParsers(currentDir, files, inheritedParsers);
            var parsersToNotify = activeParsers is { Count: > 0 } ? activeParsers : filter.ConnectedProjectParsers;

            scratchPatterns.Clear();
            for (var i = 0; i < parsersToNotify.Count; i++)
            {
                parsersToNotify[i].CollectDirectoryIgnorePatterns(currentDir, relativeDir, workspaceRoot, scratchPatterns, log);
            }
            for (var p = 0; p < scratchPatterns.Count; p++) gitTrie.AddPattern(scratchPatterns[p]);

            var activeScope = gitTrie.CompileChildScope(parentScope, relativeDir, gitIgnoreFile, ceIgnoreFile, scratchPatterns);
            var isOwned = activeParsers is { Count: > 0 };
            if (isOwned) hasAnyOwnedDirectory = true;

            CollectDirectoryFiles(filter, workspaceRoot, isRoot, !keptForNegation, files, activeScope, gitTrie, activeParsers, isOwned, ownedFiles, unownedFallbackFiles);

            var nextInherited = activeParsers is { Count: > 0 } && activeParsers.Any(p => !p.IsFallback)
                ? activeParsers.Where(p => !p.IsFallback).ToList()
                : inheritedParsers;

            for (var i = 0; i < subDirs.Length; i++)
            {
                stack.Push((subDirs[i].FullName, activeScope, nextInherited));
            }
        }

        result.AddRange(ownedFiles);
        if (!hasAnyOwnedDirectory && unownedFallbackFiles.Count > 0)
        {
            result.AddRange(unownedFallbackFiles);
        }
        return result;
    }

    private static void CollectDirectoryFiles(
        WorkspaceFileFilter filter,
        string workspaceRoot,
        bool isRoot,
        bool skipDirOnly,
        FileInfo[] files,
        GitIgnoreScopeChain activeScope,
        GitIgnoreTrieMatcher gitTrie,
        IReadOnlyList<IProjectParser>? activeParsers,
        bool isOwned,
        List<string> ownedFiles,
        List<string> unownedFallbackFiles)
    {
        for (var i = 0; i < files.Length; i++)
        {
            var fileInfo = files[i];
            var relativeFile = fileInfo.FullName.GetNormalizedRelativePath(workspaceRoot);
            if (gitTrie.IsIgnoredFileInScope(activeScope, relativeFile, skipDirOnly)) continue;

            var isManifest = filter.IsManifestFile(fileInfo.Name);
            if (isOwned)
            {
                if (!isManifest && !filter.IsSupportedSourceOrConfigFile(relativeFile, activeParsers)) continue;
                if (!isManifest && filter.ShouldSkipFile(fileInfo)) continue;
                ownedFiles.Add(relativeFile);
            }
            else
            {
                var isContract = WorkspaceFileFilter.IsStandaloneContractOrEnvFile(relativeFile, isRoot);
                if (!isManifest && !isContract && !filter.IsSupportedSourceOrConfigFile(relativeFile, null)) continue;
                if (!isManifest && filter.ShouldSkipFile(fileInfo)) continue;

                if (isManifest || isContract) ownedFiles.Add(relativeFile);
                else unownedFallbackFiles.Add(relativeFile);
            }
        }
    }

    public static List<string> EnumerateWithLegacyMatcher(
        WorkspaceFileFilter filter,
        string targetPath,
        string workspaceRoot,
        GitIgnoreMatcher gitignore,
        Action<string>? log = null)
    {
        var trie = new GitIgnoreTrieMatcher(workspaceRoot, filter);
        return EnumerateWithTrie(filter, targetPath, workspaceRoot, trie, log);
    }

    public static IReadOnlyList<IProjectParser>? ResolveAncestorProjectParsers(
        WorkspaceFileFilter filter,
        string targetPath,
        string workspaceRoot)
    {
        const string slash = "/";
        var normTarget = Path.GetFullPath(targetPath).NormalizeSlashes().TrimEnd('/');
        var normRoot = Path.GetFullPath(workspaceRoot).NormalizeSlashes().TrimEnd('/');
        if (string.Equals(normTarget, normRoot, StringComparison.OrdinalIgnoreCase) ||
            !normTarget.StartWithIC(normRoot + slash))
        {
            return null;
        }

        var rel = normTarget[(normRoot.Length + 1)..];
        var segments = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<IProjectParser>? active = TryProbeDirectoryParsers(filter, workspaceRoot, null);
        var cur = workspaceRoot;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            cur = Path.Combine(cur, segments[i]);
            active = TryProbeDirectoryParsers(filter, cur, active);
        }
        return active;
    }

    private static IReadOnlyList<IProjectParser>? TryProbeDirectoryParsers(
        WorkspaceFileFilter filter,
        string dirPath,
        IReadOnlyList<IProjectParser>? inherited)
    {
        try
        {
            var files = new DirectoryInfo(dirPath).GetFiles();
            return filter.ResolveDirectoryProjectParsers(dirPath, files, inherited);
        }
        catch
        {
            return inherited;
        }
    }
}
