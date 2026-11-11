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
    internal const string RootFileKeyPrefix = "\u0000file:";
    internal const string OtherKey = "\u0000other";
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';
    private const char DotChar = '.';
    private const string SlashString = "/";
    private const string DesignerCsSuffix = ".Designer.cs";
    private const string ModelSnapshotSuffix = "ModelSnapshot.cs";
    private const string GeneratedShortCsSuffix = ".g.cs";
    private const string GeneratedLongCsSuffix = ".generated.cs";
    private const string AmbientDtsSuffix = ".d.ts";
    private const string ModuleIdPrefix = "pm:";
    private const string RootModuleIdSuffix = ":(root)";
    private const string OtherModuleIdSuffix = ":(other)";
    private const string RootFilesDisplayName = "(root files)";

    private static readonly HashSet<string> TransparentSourceRootSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "src", "source", "sources"
    };

    private static readonly HashSet<string> NonCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".xml", ".yaml", ".yml", ".md", ".txt", ".css", ".scss", ".less",
        ".html", ".htm", ".svg", ".png", ".jpg", ".jpeg", ".gif", ".ico", ". lock", ".config", ".csproj", ".sln", ".slnx", ".props", ".targets"
    };

    private static readonly HashSet<string> BoilerplateFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "GlobalUsings.cs", "GlobalSuppressions.cs", "AssemblyInfo.cs", "Program.Testing.cs", "_Imports.razor"
    };

    private static readonly HashSet<string> GeneratedFolderSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Migrations", "wwwroot", "obj", "bin", "node_modules", "dist", "Properties", "public_src", "public"
    };

    private static readonly HashSet<string> BuildToolScriptFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "gulpfile.js", "gulpfile.ts", "gulpHtmlcompressor.js", "htmlcompressor.js",
        "minify-html.js", "minify-js.js", "webpack.config.js", "webpack.config.ts",
        "rollup.config.js", "vite.config.js", "vite.config.ts", "cleanup-worker-trash.js",
        "eslint.config.js", "eslint.config.mjs", "eslint.config.cjs", "eslint.config.ts",
        "prettier.config.js", "prettier.config.mjs", "prettier.config.cjs", ".prettierrc.js", ".prettierrc.cjs",
        "postcss.config.js", "postcss.config.mjs", "postcss.config.cjs", "postcss.config.base.mjs",
        "tailwind.config.js", "tailwind.config.mjs", "tailwind.config.cjs", "tailwind.config.ts",
        "jest.config.js", "jest.config.mjs", "jest.config.cjs", "jest.config.ts", "jest.config.base.mjs",
        "jest.setup.js", "jest.setup.ts", "jest.polyfills.js", "jest.polyfills.cjs",
        "vitest.config.js", "vitest.config.mjs", "vitest.config.ts",
        "playwright.config.js", "playwright.config.ts", "cypress.config.js", "cypress.config.ts",
        "next.config.js", "next.config.mjs", "next.config.ts",
        "babel.config.js", "babel.config.cjs", "babel.config.mjs"
    };

    public static bool IsBuildToolScript(string path)
    {
        var norm = (path ?? string.Empty).Replace(BackslashChar, SlashChar).Trim(SlashChar);
        if (norm.Length == 0) return false;
        var fileName = norm[(norm.LastIndexOf(SlashChar) + 1)..];
        return BuildToolScriptFileNames.Contains(fileName);
    }

    public static bool IsIgnoredProjectMapFile(string path)
    {
        var norm = (path ?? string.Empty).Replace(BackslashChar, SlashChar).Trim(SlashChar);
        if (norm.Length == 0) return true;

        var segments = norm.Split(SlashChar);
        var fileName = segments[^1];

        if (BoilerplateFileNames.Contains(fileName) || BuildToolScriptFileNames.Contains(fileName)) return true;
        if (fileName.EndsWith(DesignerCsSuffix, StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(ModelSnapshotSuffix, StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(GeneratedShortCsSuffix, StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(GeneratedLongCsSuffix, StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(AmbientDtsSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var dotIdx = fileName.LastIndexOf(DotChar);
        if (dotIdx >= 0 && NonCodeExtensions.Contains(fileName[dotIdx..]))
        {
            return true;
        }

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (GeneratedFolderSegments.Contains(segments[i]))
            {
                return true;
            }
        }

        return false;
    }

    public sealed record PartitionResult(
        string SourceRoot,
        ProjectMapGranularity Granularity,
        List<ProjectMapModule> Modules,
        Dictionary<string, string> FileToModule);

    public static PartitionResult Partition(string projectPath, IReadOnlyList<ProjectMapFile> files)
    {
        var projectPrefix = NormalizeDir(projectPath);
        var filteredFiles = files.Where(f => !IsIgnoredProjectMapFile(f.Path)).ToList();
        if (filteredFiles.Count == 0)
        {
            filteredFiles = files.ToList();
        }

        var relative = filteredFiles
            .DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(f => (File: f, Rel: ToRelative(f.Path, projectPrefix)))
            .Where(x => x.Rel.Length > 0)
            .ToList();

        var sourceRoot = DetectSourceRoot(relative.Select(r => r.Rel).ToList());
        var underRoot = relative
            .Select(r => (r.File, Rel: r.Rel.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase)
                ? r.Rel[sourceRoot.Length..]
                : r.Rel[(r.Rel.LastIndexOf(SlashChar) + 1)..]))
            .ToList();

        var (keys, granularity) = SelectPartitionKeys(underRoot);

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

    private static (Dictionary<string, string> Keys, ProjectMapGranularity Granularity) SelectPartitionKeys(
        List<(ProjectMapFile File, string Rel)> underRoot)
    {
        var depth1 = underRoot.ToDictionary(r => r.File.Path, r => KeyAtDepth1(r.Rel), StringComparer.OrdinalIgnoreCase);
        if (CountFolderKeys(depth1) < MinModules)
        {
            var depth2 = BuildDepth2Keys(underRoot);
            if (CountFolderKeys(depth2) >= MinModules)
            {
                return (depth2, ProjectMapGranularity.SubModule);
            }
            if (underRoot.Count <= MaxFileNodes)
            {
                var fileKeys = underRoot.ToDictionary(r => r.File.Path, r => r.Rel, StringComparer.OrdinalIgnoreCase);
                return (fileKeys, ProjectMapGranularity.File);
            }
            if (CountFolderKeys(depth2) > CountFolderKeys(depth1))
            {
                return (depth2, ProjectMapGranularity.SubModule);
            }
            return (depth1, ProjectMapGranularity.Module);
        }

        return ExpandDominantAndTransparentFolders(underRoot, depth1);
    }

    private static (Dictionary<string, string> Keys, ProjectMapGranularity Granularity) ExpandDominantAndTransparentFolders(
        List<(ProjectMapFile File, string Rel)> underRoot,
        Dictionary<string, string> depth1)
    {
        var folderCounts = depth1.Values
            .Where(k => !IsRootFileKey(k) && k != RootKey)
            .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var foldersToExpand = folderCounts
            .Where(kv => TransparentSourceRootSegments.Contains(kv.Key) ||
                         (underRoot.Count >= 12 && kv.Value >= 6 && kv.Value >= underRoot.Count * 0.45))
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (foldersToExpand.Count == 0)
        {
            return (depth1, ProjectMapGranularity.Module);
        }

        var expanded = new Dictionary<string, string>(depth1, StringComparer.OrdinalIgnoreCase);
        var anySubfolderExpanded = false;
        var anyExpanded = false;
        foreach (var dom in foldersToExpand)
        {
            var domFiles = underRoot.Where(r => string.Equals(depth1[r.File.Path], dom, StringComparison.OrdinalIgnoreCase)).ToList();
            var isTransparentRoot = TransparentSourceRootSegments.Contains(dom);
            var hasNestedSubfolders = domFiles.Any(r => r.Rel.Split(SlashChar).Length >= 3);
            var subKeys = domFiles
                .Select(r => ExpandedChildKey(r.Rel, hasNestedSubfolders || isTransparentRoot))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!isTransparentRoot && (!hasNestedSubfolders || subKeys.Count < 2))
            {
                continue;
            }

            foreach (var df in domFiles)
            {
                expanded[df.File.Path] = ExpandedChildKey(df.Rel, hasNestedSubfolders || isTransparentRoot);
            }
            anyExpanded = true;
            if (hasNestedSubfolders) anySubfolderExpanded = true;
        }

        if (!anyExpanded)
        {
            return (depth1, ProjectMapGranularity.Module);
        }

        return (expanded, anySubfolderExpanded ? ProjectMapGranularity.SubModule : ProjectMapGranularity.Module);
    }

    /// <summary>
    /// Walks down single dominant directories (src/, src/main/java/com/acme/...) until the tree branches.
    /// </summary>
    internal static string DetectSourceRoot(IReadOnlyList<string> relativePaths)
    {
        var root = string.Empty;
        if (relativePaths.Count == 0) return root;

        for (var depth = 0; depth < MaxSourceRootDepth; depth++)
        {
            var candidates = relativePaths
                .Where(p => p.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                .Select(p => p[root.Length..])
                .Where(p => p.Contains(SlashChar))
                .GroupBy(p => p[..p.IndexOf(SlashChar)], StringComparer.OrdinalIgnoreCase)
                .Select(g => (Dir: g.Key, Count: g.Count()))
                .OrderByDescending(g => g.Count)
                .ToList();

            if (candidates.Count == 0) break;

            var top = candidates[0];
            if (top.Count < relativePaths.Count * SourceRootShare) break;

            root += top.Dir + SlashString;
        }

        return root;
    }

    private static bool IsRootFileKey(string key) =>
        key.StartsWith(RootFileKeyPrefix, StringComparison.Ordinal);

    private static string KeyAtDepth1(string rel)
    {
        var segments = rel.Split(SlashChar);
        return segments.Length <= 1 ? RootFileKeyPrefix + rel : segments[0];
    }

    private static Dictionary<string, string> BuildDepth2Keys(List<(ProjectMapFile File, string Rel)> underRoot)
    {
        var branchingTopFolders = underRoot
            .Select(r => r.Rel.Split(SlashChar))
            .Where(s => s.Length >= 3)
            .Select(s => s[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(underRoot.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (file, rel) in underRoot)
        {
            var segments = rel.Split(SlashChar);
            if (segments.Length <= 1)
            {
                result[file.Path] = RootFileKeyPrefix + rel;
                continue;
            }

            var shouldExpand = branchingTopFolders.Contains(segments[0]) || TransparentSourceRootSegments.Contains(segments[0]);
            result[file.Path] = ExpandedChildKey(rel, shouldExpand);
        }

        return result;
    }

    private static string ExpandedChildKey(string rel, bool expandDirectFiles)
    {
        var segments = rel.Split(SlashChar);
        if (segments.Length <= 1) return RootFileKeyPrefix + rel;
        if (segments.Length == 2)
        {
            return expandDirectFiles ? RootFileKeyPrefix + rel : segments[0];
        }
        return $"{segments[0]}/{segments[1]}";
    }

    private static int CountFolderKeys(Dictionary<string, string> keys) =>
        keys.Values.Where(k => k != RootKey && !IsRootFileKey(k)).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static Dictionary<string, string> MergeOverflow(Dictionary<string, string> keys, int limit)
    {
        var groups = keys.GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Key: g.Key, Count: g.Count()))
            .ToList();
        if (groups.Count <= limit) return keys;

        var keep = groups
            .OrderByDescending(g => !IsRootFileKey(g.Key) && g.Key != RootKey)
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
        if (IsRootFileKey(key))
        {
            var filePath = firstFilePath.Replace(BackslashChar, SlashChar);
            var relKey = key[RootFileKeyPrefix.Length..];
            return new ProjectMapModule
            {
                Id = $"{ModuleIdPrefix}{filePath}",
                Name = FileDisplayName(relKey, allKeys),
                Path = filePath,
                NodeKindOverride = NodeKind.File,
                IsRootFiles = true
            };
        }

        if (key == RootKey)
        {
            var rootPath = baseDir.TrimEnd(SlashChar);
            return new ProjectMapModule
            {
                Id = $"{ModuleIdPrefix}{rootPath}{RootModuleIdSuffix}",
                Name = RootFilesDisplayName,
                Path = rootPath,
                IsRootFiles = true
            };
        }

        if (key == OtherKey)
        {
            var mergedCount = allKeys.Values.Count(v => v == OtherKey);
            var rootPath = baseDir.TrimEnd(SlashChar);
            return new ProjectMapModule
            {
                Id = $"{ModuleIdPrefix}{rootPath}{OtherModuleIdSuffix}",
                Name = $"(other: {mergedCount} files)",
                Path = rootPath,
                IsMerged = true
            };
        }

        if (granularity == ProjectMapGranularity.File)
        {
            var filePath = firstFilePath.Replace(BackslashChar, SlashChar);
            return new ProjectMapModule { Id = $"{ModuleIdPrefix}{filePath}", Name = FileDisplayName(key, allKeys), Path = filePath };
        }

        var path = CombinePath(baseDir, key).TrimEnd(SlashChar);
        return new ProjectMapModule { Id = $"{ModuleIdPrefix}{path}", Name = key, Path = path };
    }

    private static string NormalizeDisplayKey(string key) =>
        IsRootFileKey(key) ? key[RootFileKeyPrefix.Length..] : key;

    private static string FileDisplayName(string relKey, Dictionary<string, string> allKeys)
    {
        var fileName = relKey[(relKey.LastIndexOf(SlashChar) + 1)..];
        var slashFileName = SlashString + fileName;
        var duplicates = allKeys.Values
            .Where(v => v != RootKey && v != OtherKey)
            .Select(NormalizeDisplayKey)
            .Count(v => v.EndsWith(slashFileName, StringComparison.OrdinalIgnoreCase) || v.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        return duplicates > 1 ? relKey : fileName;
    }

    private static string NormalizeDir(string? path)
    {
        var p = (path ?? string.Empty).Replace(BackslashChar, SlashChar).Trim(SlashChar);
        return p.Length == 0 ? string.Empty : p + SlashString;
    }

    private static string ToRelative(string filePath, string projectPrefix)
    {
        var p = filePath.Replace(BackslashChar, SlashChar).TrimStart(SlashChar);
        if (projectPrefix.Length == 0) return p;
        return p.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase) ? p[projectPrefix.Length..] : p;
    }

    private static string CombinePath(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        return a.TrimEnd(SlashChar) + SlashString + b.TrimStart(SlashChar);
    }
}
