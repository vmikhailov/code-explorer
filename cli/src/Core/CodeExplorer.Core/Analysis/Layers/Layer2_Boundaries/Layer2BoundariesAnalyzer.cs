using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    public async Task<Layer2Result> ParseAsync(Layer1Result l1Result, ParsingContext ctx)
    {
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

        var dirsToCheck = new List<string>();
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
            .OrderBy(d => d.Replace('\\', '/').Length)
            .ThenBy(d => d, StringComparer.OrdinalIgnoreCase)];

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
            explicitChildToParentMap);

        NormalizeProjectFolderNames(projects, packageToProjectMap, ctx);

        await ScanSiblingLibrariesAsync(
            ctx,
            projectsStructureNode,
            projects,
            projectDepList,
            packageToProjectMap);

        await ScanAdditionalProjectsAsync(
            ctx,
            projectsStructureNode,
            dirsToCheck,
            projects,
            packages,
            dependencies,
            projectDepList,
            packageToProjectMap);

        ResolveCrossProjectDependencies(ctx, projectDepList, packageToProjectMap, dependencies);

        LinkNestedSubProjects(ctx, projects, dependencies);

        NormalizeDependencyProjectIds(projects, dependencies);

        PopulatePackageLifecycleAndEntryPoints(l1Result, projects, packages, dependencies, ctx);

        if (dependencies.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(dependencies);
            ctx.AddRelsCount(dependencies.Count);
            ctx.Layer2Log.DependenciesEnqueued(dependencies.Count);
        }

        SharedFileEnricher.Enrich(l1Result.Files, projects, ctx);

        ctx.Layer2Log.ScanCompleted(projects.Count, packages.Count);
        return new Layer2Result(l1Result, projectsStructureNode, projects, packages, dependencies);
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
        var projList = projects is List<ProjectNode> l ? l : projects.ToList();
        var map = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var p = FindProjectForFilePath(file.Path, projList);
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
