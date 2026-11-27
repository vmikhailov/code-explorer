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

            var activeScope = gitTrie.CompileChildScope(parentScope, relativeDir, gitIgnoreFile, ceIgnoreFile);
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

/// <summary>
/// Allocation-free path/metadata helpers for single-pass directory discovery without <see cref="FileInfo"/> wrappers.
/// </summary>
public static class WorkspacePathFilterHelpers
{
    private const string BinFolderName = "bin";
    private const long IncludedArtifactMaxBytes = 2L * 1024 * 1024;

    private static readonly HashSet<string> NonManifestTriggerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cfc", ".cfm", ".cfml",
        ".php", ".phtml", ".inc",
        ".pl", ".pm", ".t", ".psgi",
        ".py"
    };

    public static bool ShouldSkipFileMetadata(WorkspaceFileFilter filter, string fileName, long sizeBytes)
    {
        if (filter.ShouldSkipFileName(fileName)) return true;
        var maxAllowedSize = filter.IncludeDistAndMinifiedArtifacts ? IncludedArtifactMaxBytes : WorkspaceFileFilter.MaxSourceFileSize;
        return sizeBytes > maxAllowedSize;
    }

    public static IReadOnlyList<IProjectParser>? MatchDirectProjectParsersByPaths(
        WorkspaceFileFilter filter,
        string currentDir,
        string[] dirFilePaths,
        bool allowFallback)
    {
        var matched = MatchDirectoryProjectParsersByPaths(filter, currentDir, dirFilePaths, includeFallback: false);
        if (matched is { Count: > 0 } || !allowFallback)
        {
            return matched;
        }
        return MatchDirectoryProjectParsersByPaths(filter, currentDir, dirFilePaths, includeFallback: true);
    }

    private static List<IProjectParser>? MatchDirectoryProjectParsersByPaths(
        WorkspaceFileFilter filter,
        string currentDir,
        string[] dirFilePaths,
        bool includeFallback)
    {
        var hasCachedChildDirs = PhysicalFileStore.HasCachedDirectChildDirectories(currentDir);
        if (!includeFallback && dirFilePaths.Length == 0 && !hasCachedChildDirs)
        {
            var parentDir = Path.GetDirectoryName(currentDir.TrimEnd('/', '\\'));
            var parentName = parentDir != null ? Path.GetFileName(parentDir) : string.Empty;
            if (!parentName.Equals(BinFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        if (!includeFallback && dirFilePaths.Length > 0 && !hasCachedChildDirs)
        {
            var hasTrigger = false;
            for (var f = 0; f < dirFilePaths.Length; f++)
            {
                var fn = Path.GetFileName(dirFilePaths[f]);
                var ext = Path.GetExtension(fn);
                if (filter.IsManifestFile(fn) || (!string.IsNullOrEmpty(ext) && NonManifestTriggerExtensions.Contains(ext)))
                {
                    hasTrigger = true;
                    break;
                }
            }
            if (!hasTrigger)
            {
                var parentDir = Path.GetDirectoryName(currentDir.TrimEnd('/', '\\'));
                var parentName = parentDir != null ? Path.GetFileName(parentDir) : string.Empty;
                if (!parentName.Equals(BinFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
        }

        var parsers = filter.ConnectedProjectParsers;
        List<IProjectParser>? matched = null;
        for (var p = 0; p < parsers.Count; p++)
        {
            var parser = parsers[p];
            if (parser.IsFallback != includeFallback) continue;

            var hasManifest = false;
            for (var f = 0; f < dirFilePaths.Length; f++)
            {
                if (parser.IsManifestFile(Path.GetFileName(dirFilePaths[f])))
                {
                    hasManifest = true;
                    break;
                }
            }

            if (hasManifest || parser.IsProjectDirectory(currentDir, dirFilePaths))
            {
                matched ??= [];
                matched.Add(parser);
            }
        }

        return matched;
    }

    public static IReadOnlyList<IProjectParser>? MatchFileSupportedProjectParsersByPaths(
        WorkspaceFileFilter filter,
        string[] dirFilePaths,
        bool includeFallback)
    {
        if (dirFilePaths.Length == 0) return null;

        var parsers = filter.ConnectedProjectParsers;
        List<IProjectParser>? matched = null;
        for (var p = 0; p < parsers.Count; p++)
        {
            var parser = parsers[p];
            if (parser.IsFallback != includeFallback) continue;

            for (var f = 0; f < dirFilePaths.Length; f++)
            {
                var fn = Path.GetFileName(dirFilePaths[f]);
                var ext = Path.GetExtension(fn);
                if (parser.IsManifestFile(fn) || IsNonManifestExtensionForParser(parser.ProjectType, ext))
                {
                    matched ??= [];
                    matched.Add(parser);
                    break;
                }
            }
        }

        return matched;
    }

    private static bool IsNonManifestExtensionForParser(string projectType, string ext) =>
        projectType switch
        {
            "php" => ext.Equals(".php", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".phtml", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".inc", StringComparison.OrdinalIgnoreCase),
            "perl" => ext.Equals(".pl", StringComparison.OrdinalIgnoreCase) ||
                      ext.Equals(".pm", StringComparison.OrdinalIgnoreCase) ||
                      ext.Equals(".t", StringComparison.OrdinalIgnoreCase) ||
                      ext.Equals(".psgi", StringComparison.OrdinalIgnoreCase),
            "python" => ext.Equals(".py", StringComparison.OrdinalIgnoreCase),
            "coldfusion" => ext.Equals(".cfc", StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".cfm", StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".cfml", StringComparison.OrdinalIgnoreCase),
            "script" => ext.Equals(".sh", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".bash", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".zsh", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
}
