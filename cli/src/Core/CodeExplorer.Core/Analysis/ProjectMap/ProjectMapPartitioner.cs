namespace CodeExplorer.Core.Analysis.ProjectMap;

/// <summary>
/// Groups a project's non-test files into architectural modules based on the folder tree.
/// Pure function over file paths: no graph access, no persisted nodes.
/// </summary>
public static class ProjectMapPartitioner
{
    public const int MinModules = 3;
    public const int MaxModules = 30;
    public const int MaxFileNodes = 60;
    private const double SourceRootShare = 0.85;
    private const int MaxSourceRootDepth = 8;
    internal const string RootKey = "\u0000root";
    internal const string OtherKey = "\u0000other";

    public sealed record PartitionResult(
        string SourceRoot,
        ProjectMapGranularity Granularity,
        List<ProjectMapModule> Modules,
        Dictionary<string, string> FileToModule);

    public static PartitionResult Partition(string projectPath, IReadOnlyList<ProjectMapFile> files)
    {
        var projectPrefix = NormalizeDir(projectPath);
        var relative = files
            .DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(f => (File: f, Rel: ToRelative(f.Path, projectPrefix)))
            .Where(x => x.Rel.Length > 0)
            .ToList();

        var sourceRoot = DetectSourceRoot(relative.Select(r => r.Rel).ToList());
        var underRoot = relative
            .Select(r => (r.File, Rel: r.Rel.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase)
                ? r.Rel[sourceRoot.Length..]
                : r.Rel[(r.Rel.LastIndexOf('/') + 1)..]))
            .ToList();

        var depth1 = underRoot.ToDictionary(r => r.File.Path, r => KeyAtDepth(r.Rel, 1), StringComparer.OrdinalIgnoreCase);
        var granularity = ProjectMapGranularity.Module;
        var keys = depth1;

        if (CountFolderKeys(depth1) < MinModules)
        {
            var depth2 = underRoot.ToDictionary(r => r.File.Path, r => KeyAtDepth(r.Rel, 2), StringComparer.OrdinalIgnoreCase);
            if (CountFolderKeys(depth2) >= MinModules)
            {
                keys = depth2;
                granularity = ProjectMapGranularity.SubModule;
            }
            else if (underRoot.Count <= MaxFileNodes)
            {
                keys = underRoot.ToDictionary(r => r.File.Path, r => r.Rel, StringComparer.OrdinalIgnoreCase);
                granularity = ProjectMapGranularity.File;
            }
            else if (CountFolderKeys(depth2) > CountFolderKeys(depth1))
            {
                keys = depth2;
                granularity = ProjectMapGranularity.SubModule;
            }
        }

        var limit = granularity == ProjectMapGranularity.File ? MaxFileNodes : MaxModules;
        keys = MergeOverflow(keys, limit);

        var baseDir = CombinePath(projectPrefix, sourceRoot);
        var modules = new Dictionary<string, ProjectMapModule>(StringComparer.OrdinalIgnoreCase);
        var fileToModule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lineCounts = relative.ToDictionary(r => r.File.Path, r => r.File.LineCount ?? 0, StringComparer.OrdinalIgnoreCase);

        foreach (var (filePath, key) in keys)
        {
            if (!modules.TryGetValue(key, out var module))
            {
                module = CreateModule(key, baseDir, granularity, keys, filePath);
                modules[key] = module;
            }

            module.Files.Add(filePath);
            module.LineCount += lineCounts.GetValueOrDefault(filePath);
            fileToModule[filePath] = module.Id;
        }

        var ordered = modules.Values
            .OrderBy(m => m.IsMerged)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PartitionResult(sourceRoot, granularity, ordered, fileToModule);
    }

    /// <summary>
    /// Walks down single dominant directories (src/, src/main/java/com/acme/...) until the tree branches.
    /// </summary>
    internal static string DetectSourceRoot(IReadOnlyList<string> relativePaths)
    {
        var root = "";
        if (relativePaths.Count == 0) return root;

        for (var depth = 0; depth < MaxSourceRootDepth; depth++)
        {
            var candidates = relativePaths
                .Where(p => p.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                .Select(p => p[root.Length..])
                .Where(p => p.Contains('/'))
                .GroupBy(p => p[..p.IndexOf('/')], StringComparer.OrdinalIgnoreCase)
                .Select(g => (Dir: g.Key, Count: g.Count()))
                .OrderByDescending(g => g.Count)
                .ToList();

            if (candidates.Count == 0) break;

            var top = candidates[0];
            if (top.Count < relativePaths.Count * SourceRootShare) break;

            root += top.Dir + "/";
        }

        return root;
    }

    private static string KeyAtDepth(string rel, int depth)
    {
        var segments = rel.Split('/');
        if (segments.Length <= 1) return RootKey;
        var take = Math.Min(depth, segments.Length - 1);
        return string.Join('/', segments.Take(take));
    }

    private static int CountFolderKeys(Dictionary<string, string> keys) =>
        keys.Values.Where(k => k != RootKey).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static Dictionary<string, string> MergeOverflow(Dictionary<string, string> keys, int limit)
    {
        var groups = keys.GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Key: g.Key, Count: g.Count()))
            .ToList();
        if (groups.Count <= limit) return keys;

        var keep = groups
            .OrderByDescending(g => g.Key == RootKey)
            .ThenByDescending(g => g.Count)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(limit - 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return keys.ToDictionary(
            kv => kv.Key,
            kv => keep.Contains(kv.Value) ? kv.Value : OtherKey,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ProjectMapModule CreateModule(
        string key,
        string baseDir,
        ProjectMapGranularity granularity,
        Dictionary<string, string> allKeys,
        string firstFilePath)
    {
        if (key == RootKey)
        {
            var rootPath = baseDir.TrimEnd('/');
            return new ProjectMapModule
            {
                Id = $"pm:{rootPath}:(root)",
                Name = "(root files)",
                Path = rootPath,
                IsRootFiles = true
            };
        }

        if (key == OtherKey)
        {
            var mergedCount = allKeys.Values.Count(v => v == OtherKey);
            var rootPath = baseDir.TrimEnd('/');
            return new ProjectMapModule
            {
                Id = $"pm:{rootPath}:(other)",
                Name = $"(other: {mergedCount} files)",
                Path = rootPath,
                IsMerged = true
            };
        }

        if (granularity == ProjectMapGranularity.File)
        {
            var filePath = firstFilePath.Replace('\\', '/');
            return new ProjectMapModule { Id = $"pm:{filePath}", Name = FileDisplayName(key, allKeys), Path = filePath };
        }

        var path = CombinePath(baseDir, key).TrimEnd('/');
        return new ProjectMapModule { Id = $"pm:{path}", Name = key, Path = path };
    }

    private static string FileDisplayName(string relKey, Dictionary<string, string> allKeys)
    {
        var fileName = relKey[(relKey.LastIndexOf('/') + 1)..];
        var duplicates = allKeys.Values
            .Where(v => v != RootKey && v != OtherKey)
            .Count(v => v.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase) || v.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        return duplicates > 1 ? relKey : fileName;
    }

    private static string NormalizeDir(string? path)
    {
        var p = (path ?? "").Replace('\\', '/').Trim('/');
        return p.Length == 0 ? "" : p + "/";
    }

    private static string ToRelative(string filePath, string projectPrefix)
    {
        var p = filePath.Replace('\\', '/').TrimStart('/');
        if (projectPrefix.Length == 0) return p;
        return p.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase) ? p[projectPrefix.Length..] : p;
    }

    private static string CombinePath(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        return a.TrimEnd('/') + "/" + b.TrimStart('/');
    }
}
