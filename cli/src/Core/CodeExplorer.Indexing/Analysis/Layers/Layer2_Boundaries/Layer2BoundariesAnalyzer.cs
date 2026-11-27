using System.Diagnostics;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    public Task<Layer2Result> ParseAsync(Layer1Result l1Result, Layer2Context ctx) =>
        ParseAsync(ctx);

    public async Task<Layer2Result> ParseAsync(Layer2Context ctx)
    {
        var l1Result = ctx.Layer1;
        using var dirIndexScope = WorkspaceDirectoryIndex.EnterScope(ctx.DirectoryIndex);
        var sw = Stopwatch.StartNew();
        ctx.Layer2Log.ScanStarted();

        var projectsStructureNode = new ProjectsStructureNode(
            $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.ProjectsStructure}",
            "ProjectsStructure",
            l1Result.Workspace.Path
        );
        l1Result.Workspace.Children.Add(projectsStructureNode);
        ctx.ProjectsStructure = projectsStructureNode;

        var projects = new List<ProjectNode>();
        var packages = new List<PackageNode>();
        var dependencies = new List<Relationship>();

        var hasUnifiedPlan = UnifiedScanPlanStore.TryGet(l1Result, out var unifiedPlan) && unifiedPlan != null;
        var dirsToCheck = new List<string>();
        if (!hasUnifiedPlan)
        {
            if (ctx.IsSubtreeScan)
            {
                dirsToCheck.Add(ctx.ScanPath);
                dirsToCheck.AddRange(l1Result.Folders
                    .Where(f => f.Path.StartWithIC(ctx.ScanPath.TrimEnd('/') + "/"))
                    .Select(f => f.Path));
            }
            else
            {
                dirsToCheck.Add(ctx.AbsoluteWorkspacePath);
                dirsToCheck.AddRange(l1Result.Folders.Select(f => f.Path));
            }
            dirsToCheck = [.. dirsToCheck
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d.Replace('\\', '/').Trim('/').Count(c => c == '/'))
                .ThenBy(d => d.Replace('\\', '/').Length)
                .ThenBy(d => d, StringComparer.OrdinalIgnoreCase)];
        }

        var projectDepList = new List<(ProjectNode Project, ProjectDependencyInfo DepInfo)>();
        var packageToProjectMap = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        var absorbedDirectoryOwners = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        var explicitChildToParentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await ScanProjectsInDirectoriesAsync(
            l1Result,
            ctx,
            projectsStructureNode,
            dirsToCheck,
            projects,
            packages,
            dependencies,
            projectDepList,
            packageToProjectMap,
            absorbedDirectoryOwners,
            explicitChildToParentMap,
            unifiedPlan);

        NormalizeProjectFolderNames(projects, packageToProjectMap, ctx);

        await ScanSiblingLibrariesAsync(
            ctx,
            projectsStructureNode,
            projects,
            projectDepList,
            packageToProjectMap);

        var manifestDirsToCheck = hasUnifiedPlan ? unifiedPlan!.ManifestDirectories : dirsToCheck;
        await ScanAdditionalProjectsAsync(
            ctx,
            projectsStructureNode,
            manifestDirsToCheck,
            projects,
            packages,
            dependencies,
            projectDepList,
            packageToProjectMap);

        ResolveCrossProjectDependencies(ctx, projectDepList, packageToProjectMap, dependencies);

        LinkNestedSubProjects(ctx, projects, dependencies);

        NormalizeDependencyProjectIds(projects, dependencies);

        await SharedFileEnricher.EnrichAsync(l1Result.Files, projects, ctx);

        PopulatePackageLifecycleAndEntryPoints(l1Result, projects, packages, dependencies, ctx);

        if (dependencies.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(dependencies);
            ctx.AddRelsCount(dependencies.Count);
            ctx.Layer2Log.DependenciesEnqueued(dependencies.Count);
        }

        ctx.Layer2Log.ScanCompleted(projects.Count, packages.Count, sw.ElapsedMilliseconds);
        return new Layer2Result(
            projectsStructureNode,
            projects,
            packages,
            dependencies,
            ctx.WorkspaceEnrichments,
            ctx.TsConfigPaths,
            ctx.WorkspaceGoModules,
            ctx.DirectoryManifestDrivers);
    }

    public static ProjectNode? FindProjectForFilePath(string filePath, IEnumerable<ProjectNode> projects)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        var cleanPath = filePath.NormalizeSlashes().Trim('/');
        ProjectNode? bestMatch = null;
        var bestMatchLength = -1;

        foreach (var p in projects)
        {
            var pPath = p.Path.NormalizeSlashes().Trim('/');
            if (pPath == "")
            {
                var isRootScriptFallback = p.EntityKind == ProjectEntityKind.ScriptProject ||
                                           p.ProjectType is ProjectLanguage.Script or ProjectLanguage.Batch or ProjectLanguage.PowerShell or ProjectLanguage.Shell;
                var isRootBuildOrchestrator = p.IsBuildOrchestrator == true;
                if ((isRootScriptFallback || isRootBuildOrchestrator) && cleanPath.Contains('/'))
                {
                    continue;
                }
                if (bestMatchLength < 0)
                {
                    bestMatch = p;
                    bestMatchLength = 0;
                }
                continue;
            }

            var pPrefix = pPath + "/";
            if (cleanPath.StartWithIC(pPrefix) || cleanPath.Equals(pPath, StringComparison.OrdinalIgnoreCase))
            {
                if (pPrefix.Length > bestMatchLength)
                {
                    bestMatch = p;
                    bestMatchLength = pPrefix.Length;
                }
            }
        }

        return bestMatch;
    }

    public static Dictionary<string, ProjectNode> BuildFileToProjectMap(IEnumerable<FileNode> files, IEnumerable<ProjectNode> projects)
    {
        var projList = projects is IReadOnlyList<ProjectNode> ro ? ro : projects.ToList();
        var (byDir, rootDirectOnly, rootSubtree) = BuildProjectDirectoryIndex(projList);
        var spanLookup = byDir.GetAlternateLookup<ReadOnlySpan<char>>();
        var map = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var p = ResolveProjectFromDirectoryIndex(file.Path, spanLookup, rootDirectOnly, rootSubtree);
            if (p != null)
            {
                map[file.Id] = p;
                if (!string.IsNullOrEmpty(file.Path))
                {
                    map[file.Path] = p;
                }
            }
        }
        return map;
    }

    internal static (Dictionary<string, ProjectNode> ByDir, ProjectNode? RootDirectOnly, ProjectNode? RootSubtree) BuildProjectDirectoryIndex(
        IReadOnlyList<ProjectNode> projects)
    {
        var byDir = new Dictionary<string, ProjectNode>(projects.Count, StringComparer.OrdinalIgnoreCase);
        ProjectNode? rootDirectOnly = null;
        ProjectNode? rootSubtree = null;

        for (var i = 0; i < projects.Count; i++)
        {
            var p = projects[i];
            var pPath = (p.Path ?? string.Empty).NormalizeSlashes().Trim('/');
            if (pPath.Length == 0)
            {
                rootDirectOnly ??= p;
                var isRootScriptFallback = p.EntityKind == ProjectEntityKind.ScriptProject ||
                                           p.ProjectType is ProjectLanguage.Script or ProjectLanguage.Batch or ProjectLanguage.PowerShell or ProjectLanguage.Shell;
                var isRootBuildOrchestrator = p.IsBuildOrchestrator == true;
                if (!isRootScriptFallback && !isRootBuildOrchestrator)
                {
                    rootSubtree ??= p;
                }
                continue;
            }

            byDir.TryAdd(pPath, p);
        }

        return (byDir, rootDirectOnly, rootSubtree);
    }

    internal static ProjectNode? ResolveProjectFromDirectoryIndex(
        string? rawPath,
        Dictionary<string, ProjectNode>.AlternateLookup<ReadOnlySpan<char>> spanLookup,
        ProjectNode? rootDirectOnly,
        ProjectNode? rootSubtree)
    {
        if (string.IsNullOrEmpty(rawPath))
        {
            return null;
        }

        var cleanSpan = rawPath.Contains('\\')
            ? rawPath.Replace('\\', '/').AsSpan().Trim('/')
            : rawPath.AsSpan().Trim('/');
        if (cleanSpan.IsEmpty)
        {
            return rootDirectOnly;
        }

        var candidate = cleanSpan;
        while (true)
        {
            if (spanLookup.TryGetValue(candidate, out var matched))
            {
                return matched;
            }

            var lastSlash = candidate.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                break;
            }

            candidate = candidate[..lastSlash];
        }

        return cleanSpan.Contains('/') ? rootSubtree : rootDirectOnly;
    }

    public static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects)
    {
        return FindProjectForFilePath(file.Path, projects)?.Id == project.Id;
    }

    private static bool IsGenericFolderName(string folderName) =>
        folderName.Equals("src", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("app", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("apps", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("packages", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("libs", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("libraries", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("modules", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("shared", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("build", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("test", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("api", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("web", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("client", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("server", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("service", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("services", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("frontend", StringComparison.OrdinalIgnoreCase) ||
        folderName.Equals("backend", StringComparison.OrdinalIgnoreCase);
}
