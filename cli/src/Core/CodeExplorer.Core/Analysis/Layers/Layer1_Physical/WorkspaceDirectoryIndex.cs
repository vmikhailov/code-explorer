using System.Collections.Concurrent;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Per-scan directory topology index and single-pass parsed project descriptor memo.
/// Holds only directory structure metadata (file path listings, child directory links, git roots,
/// configured projects) and already-parsed <see cref="ProjectParseResult"/> objects — never raw file contents.
/// </summary>
public sealed class WorkspaceDirectoryIndex
{
    private static readonly AsyncLocal<WorkspaceDirectoryIndex?> AmbientCurrent = new();

    private readonly ConcurrentDictionary<string, string[]> _directoryFiles =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _directChildDirectories =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, byte> _gitRepositoryRoots =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, WorkspaceProjectSetting> _configuredProjects =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<(Type ParserType, string NormalizedDir), (bool Matched, ProjectParseResult? Result)> _parsedProjects =
        new();

    public static WorkspaceDirectoryIndex? Current => AmbientCurrent.Value;

    public static DirectoryIndexScope EnterScope(WorkspaceDirectoryIndex index)
    {
        var prev = AmbientCurrent.Value;
        AmbientCurrent.Value = index;
        return new DirectoryIndexScope(prev, static restored => AmbientCurrent.Value = restored);
    }

    public static string NormalizePath(string path) => path.NormalizeFullPath(trimTrailingSlash: true);

    public bool TryGetCachedProjectParse(IProjectParser parser, string directoryPath, out (bool Matched, ProjectParseResult? Result) cached)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key) && _parsedProjects.TryGetValue((parser.GetType(), key), out cached))
        {
            return true;
        }
        cached = default;
        return false;
    }

    public void StoreProjectParse(IProjectParser parser, string directoryPath, bool matched, ProjectParseResult? result)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key))
        {
            _parsedProjects[(parser.GetType(), key)] = (matched, result);
        }
    }

    public void SetDirectoryFiles(string directoryPath, string[] files)
    {
        var key = NormalizePath(directoryPath);
        if (string.IsNullOrEmpty(key)) return;

        _directoryFiles[key] = files;
        var childPath = key;
        var slash = childPath.LastIndexOf('/');
        while (slash > 0)
        {
            var parentKey = childPath[..slash];
            if (parentKey.Length == 2 && parentKey[1] == ':')
            {
                break;
            }

            var children = _directChildDirectories.GetOrAdd(
                parentKey,
                static _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
            if (!children.TryAdd(childPath, 1))
            {
                break;
            }

            childPath = parentKey;
            slash = childPath.LastIndexOf('/');
        }
    }

    public void RemoveDirectoryFileEntry(string fullPath)
    {
        var key = NormalizePath(fullPath);
        if (string.IsNullOrEmpty(key)) return;

        var lastSlash = key.LastIndexOf('/');
        if (lastSlash <= 0) return;

        var parentKey = key[..lastSlash];
        if (_directoryFiles.TryGetValue(parentKey, out var dirFiles) && dirFiles.Length > 0)
        {
            var targetName = key[(lastSlash + 1)..];
            var updated = dirFiles
                .Where(f => !Path.GetFileName(f).Equals(targetName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (updated.Length != dirFiles.Length)
            {
                _directoryFiles[parentKey] = updated;
            }
        }
    }

    public bool FileExistsInDirectory(string fullPath, bool defaultWhenUncached = false)
    {
        var key = NormalizePath(fullPath);
        if (string.IsNullOrEmpty(key)) return false;

        var lastSlash = key.LastIndexOf('/');
        if (lastSlash > 0 && _directoryFiles.TryGetValue(key[..lastSlash], out var dirFiles))
        {
            var targetName = key[(lastSlash + 1)..];
            for (var i = 0; i < dirFiles.Length; i++)
            {
                if (Path.GetFileName(dirFiles[i]).Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        return defaultWhenUncached || File.Exists(fullPath);
    }

    public bool TryGetCachedDirectoryFiles(string directoryPath, out string[] files)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key) && _directoryFiles.TryGetValue(key, out var cached))
        {
            files = cached;
            return true;
        }
        files = [];
        return false;
    }

    public bool HasCachedDirectory(string directoryPath)
    {
        var key = NormalizePath(directoryPath);
        return !string.IsNullOrEmpty(key) && _directoryFiles.ContainsKey(key);
    }

    public bool HasCachedDirectChildDirectories(string parentDirectoryPath)
    {
        var parentKey = NormalizePath(parentDirectoryPath);
        if (string.IsNullOrEmpty(parentKey) || !_directChildDirectories.TryGetValue(parentKey, out var children))
        {
            return false;
        }

        foreach (var childKey in children.Keys)
        {
            if (_directoryFiles.ContainsKey(childKey))
            {
                return true;
            }
        }
        return false;
    }

    public List<(string DirectoryPath, string[] Files)> GetCachedDirectChildDirectories(string parentDirectoryPath)
    {
        var parentKey = NormalizePath(parentDirectoryPath);
        if (string.IsNullOrEmpty(parentKey) || !_directChildDirectories.TryGetValue(parentKey, out var children))
        {
            return [];
        }

        var result = new List<(string DirectoryPath, string[] Files)>(children.Count);
        foreach (var childKey in children.Keys)
        {
            if (_directoryFiles.TryGetValue(childKey, out var arr))
            {
                result.Add((childKey, arr));
            }
        }
        return result;
    }

    public IEnumerable<string> EnumerateCachedFilesInSubtree(string rootDirectoryPath)
    {
        var rootKey = NormalizePath(rootDirectoryPath);
        if (string.IsNullOrEmpty(rootKey)) yield break;

        var stack = new Stack<string>();
        stack.Push(rootKey);
        while (stack.Count > 0)
        {
            var currentKey = stack.Pop();
            if (_directoryFiles.TryGetValue(currentKey, out var files))
            {
                for (var i = 0; i < files.Length; i++)
                {
                    yield return files[i];
                }
            }

            if (_directChildDirectories.TryGetValue(currentKey, out var children))
            {
                foreach (var childKey in children.Keys)
                {
                    stack.Push(childKey);
                }
            }
        }
    }

    public string[] GetDirectoryFiles(string directoryPath)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key) && _directoryFiles.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!Directory.Exists(directoryPath)) return [];
        try
        {
            return Directory.GetFiles(directoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static string[] GetDirectoryFilesOrFallback(string directoryPath) =>
        Current != null
            ? Current.GetDirectoryFiles(directoryPath)
            : (Directory.Exists(directoryPath) ? Directory.GetFiles(directoryPath) : []);

    public static bool HasCachedDirectoryInCurrent(string directoryPath) =>
        Current?.HasCachedDirectory(directoryPath) == true;

    public static bool HasCachedChildDirsInCurrent(string directoryPath) =>
        Current?.HasCachedDirectChildDirectories(directoryPath) == true;

    public static List<(string DirectoryPath, string[] Files)> GetCachedChildDirsInCurrent(string directoryPath) =>
        Current?.GetCachedDirectChildDirectories(directoryPath) ?? [];

    public static IEnumerable<string> EnumerateSubtreeInCurrent(string directoryPath) =>
        Current?.EnumerateCachedFilesInSubtree(directoryPath) ?? [];

    public static bool FileExistsInCurrentOrDisk(string fullPath) =>
        Current != null ? Current.FileExistsInDirectory(fullPath) : File.Exists(fullPath);

    public static bool IsGitRepoRootInCurrent(string directoryPath) =>
        Current?.IsGitRepositoryRoot(directoryPath) == true;

    public static bool HasChildGitReposInCurrent(string directoryPath) =>
        Current?.HasChildGitRepositories(directoryPath) == true;

    public static bool HasChildConfiguredProjectsInCurrent(string directoryPath) =>
        Current?.HasChildConfiguredProjects(directoryPath) == true;

    public static bool TryGetConfiguredProjectInCurrent(string directoryPath, out WorkspaceProjectSetting? setting)
    {
        if (Current != null)
        {
            return Current.TryGetConfiguredProject(directoryPath, out setting);
        }
        setting = null;
        return false;
    }

    public static bool HasAnyConfiguredProjectsInCurrent => Current?.HasAnyConfiguredProjects == true;

    public void RegisterGitRepositoryRoot(string directoryPath)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key))
        {
            _gitRepositoryRoots[key] = 1;
        }
    }

    public bool IsGitRepositoryRoot(string directoryPath)
    {
        var key = NormalizePath(directoryPath);
        return !string.IsNullOrEmpty(key) && _gitRepositoryRoots.ContainsKey(key);
    }

    public bool HasChildGitRepositories(string directoryPath)
    {
        var parentKey = NormalizePath(directoryPath);
        if (string.IsNullOrEmpty(parentKey)) return false;

        var prefix = parentKey + "/";
        foreach (var k in _gitRepositoryRoots.Keys)
        {
            if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public void RegisterConfiguredProjects(string workspaceRootPath, IReadOnlyList<WorkspaceProjectSetting>? projects)
    {
        if (projects is not { Count: > 0 }) return;
        foreach (var p in projects)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Path)) continue;
            var absPath = Path.Combine(workspaceRootPath, p.Path);
            var key = NormalizePath(absPath);
            if (!string.IsNullOrEmpty(key))
            {
                _configuredProjects[key] = p;
            }
        }
    }

    public bool TryGetConfiguredProject(string directoryPath, out WorkspaceProjectSetting? setting)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key) && _configuredProjects.TryGetValue(key, out setting))
        {
            return true;
        }
        setting = null;
        return false;
    }

    public bool HasChildConfiguredProjects(string directoryPath)
    {
        var parentKey = NormalizePath(directoryPath);
        if (string.IsNullOrEmpty(parentKey)) return false;

        var prefix = parentKey + "/";
        foreach (var k in _configuredProjects.Keys)
        {
            if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public bool HasAnyConfiguredProjects => !_configuredProjects.IsEmpty;
}
