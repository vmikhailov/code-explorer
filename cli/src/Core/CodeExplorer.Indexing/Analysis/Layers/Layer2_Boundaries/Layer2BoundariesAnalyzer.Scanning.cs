using System.Text.Json;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    private static async Task ScanProjectsInDirectoriesAsync(
        Layer1Result l1Result,
        Layer2Context ctx,
        ProjectsStructureNode projectsStructureNode,
        List<string> dirsToCheck,
        List<ProjectNode> projects,
        List<PackageNode> packages,
        List<Relationship> dependencies,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> projectDepList,
        Dictionary<string, ProjectNode> packageToProjectMap,
        Dictionary<string, ProjectNode> absorbedDirectoryOwners,
        Dictionary<string, string> explicitChildToParentMap,
        UnifiedWorkspaceScanPlan? unifiedPlan = null)
    {
        var scanState = new DirectoryScanState(
            l1Result,
            ctx,
            projectsStructureNode,
            projects,
            packages,
            dependencies,
            projectDepList,
            packageToProjectMap,
            absorbedDirectoryOwners,
            explicitChildToParentMap);

        if (unifiedPlan != null)
        {
            await WalkPreDiscoveredTreeAsync(unifiedPlan.RootNodes, scanState);
        }
        else
        {
            await WalkDirectoryForestAsync(dirsToCheck, scanState);
        }

        // Pass 1.5: Manifestless leaf nested git repositories that contain recognized source files
        // but had no manifest-backed projects discovered anywhere inside them during Pass 1.
        if (!ctx.IsSubtreeScan)
        {
            var rootNorm = ctx.AbsoluteWorkspacePath.NormalizeFullPath(trimTrailingSlash: true);
            var candidateGitRoots = unifiedPlan != null
                ? CollectLeafNestedGitRepoNodes(unifiedPlan.RootNodes)
                : dirsToCheck
                    .Where(d => !string.Equals(d.NormalizeFullPath(trimTrailingSlash: true), rootNorm, StringComparison.OrdinalIgnoreCase) &&
                                ctx.DirectoryIndex.IsGitRepositoryRoot(d) &&
                                !ctx.DirectoryIndex.HasChildGitRepositories(d))
                    .Select(d => (Dir: d, Counts: (SubtreeLanguageCounts?)null))
                    .ToList();

            foreach (var (gitDir, precomputedCounts) in candidateGitRoots)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var relGitDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, gitDir).Replace('\\', '/');
                if (relGitDir == ".") continue;
                var relPrefix = relGitDir.TrimEnd('/') + "/";

                var alreadyHasProject = projects.Any(p =>
                    string.Equals(p.Path, relGitDir, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(p.Path) && p.Path.StartsWith(relPrefix, StringComparison.OrdinalIgnoreCase)));
                if (alreadyHasProject) continue;

                var repoName = Path.GetFileName(gitDir.TrimEnd('/', '\\'));
                if (string.IsNullOrWhiteSpace(repoName)) continue;

                var repoSetting = new WorkspaceProjectSetting { Name = repoName, Path = gitDir };
                var gitParser = ResolveConfiguredProjectParser(gitDir, repoSetting, ctx.DirectoryIndex, precomputedCounts);
                if (gitParser == null) continue;

                var filesInGitDir = ctx.DirectoryIndex.GetDirectoryFiles(gitDir);
                var props = gitParser.ExtractManifestProperties(gitDir, filesInGitDir);
                var desc = new ParsedProjectDescriptor(repoName, gitDir, gitParser.ProjectType, props);
                await InstantiateMatchedProjectsInDirectoryAsync(
                    gitDir,
                    filesInGitDir,
                    [(gitParser, new ProjectParseResult(desc))],
                    scanState);
            }
        }

        // Pass 2: Fallback project detection for directories with loose scripts or unmanaged projects not enclosed in any standard project
        var fallbackParsers = WorkspaceIndexer._projectParsers.Where(p => p.IsFallback).ToList();
        var fallbackCandidateDirs = unifiedPlan != null ? unifiedPlan.FallbackCandidateDirectories : dirsToCheck;
        if (!ctx.IsSubtreeScan && fallbackParsers.Count > 0 && fallbackCandidateDirs.Count > 0)
        {
            var hasRootNonScriptOwner = projects.Any(p =>
                string.IsNullOrEmpty(p.Path) &&
                p.EntityKind != ProjectEntityKind.ScriptProject &&
                p.IsBuildOrchestrator != true);
            var coveredPaths = projects
                .Where(p => !string.IsNullOrEmpty(p.Path))
                .Select(p => (Exact: p.Path, Prefix: p.Path.TrimEnd('/') + "/"))
                .ToList();

            foreach (var dir in fallbackCandidateDirs)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var filesInDir = ctx.DirectoryIndex.GetDirectoryFiles(dir);
                if (filesInDir.Length == 0) continue;

                var relativeProjectDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, dir).Replace('\\', '/');
                if (relativeProjectDir == ".") relativeProjectDir = "";

                // Only trigger if NOT inside an already detected project
                if (hasRootNonScriptOwner) continue;
                if (string.IsNullOrEmpty(relativeProjectDir) && projects.Any(p => string.IsNullOrEmpty(p.Path))) continue;

                var isCovered = false;
                for (var i = 0; i < coveredPaths.Count; i++)
                {
                    var (exact, prefix) = coveredPaths[i];
                    if (string.Equals(relativeProjectDir, exact, StringComparison.OrdinalIgnoreCase) ||
                        relativeProjectDir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(relativeProjectDir) &&
                         exact.StartsWith(relativeProjectDir.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
                    {
                        isCovered = true;
                        break;
                    }
                }

                if (isCovered) continue;

                var fbContext = new ProjectParseContext(dir, filesInDir, ctx.AbsoluteWorkspacePath, ctx.DirectoryIndex);
                IProjectParser? fallbackParser = null;
                ProjectParseResult? fbResult = null;
                for (var fbi = 0; fbi < fallbackParsers.Count; fbi++)
                {
                    if (fallbackParsers[fbi].TryParse(fbContext, out var candidateFb) && candidateFb != null)
                    {
                        fallbackParser = fallbackParsers[fbi];
                        fbResult = candidateFb;
                        break;
                    }
                }
                if (fallbackParser != null && fbResult != null)
                {
                    var folderName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(folderName)) folderName = Path.GetFileName(ctx.AbsoluteWorkspacePath.TrimEnd('/', '\\'));
                    var projectName = fbResult.Project.Name;
                    if (string.IsNullOrEmpty(projectName)) projectName = folderName;
                    if (projects.Any(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase)))
                    {
                        projectName = string.IsNullOrEmpty(relativeProjectDir) ? $"{folderName}.Scripts" : relativeProjectDir.Replace('/', '-');
                        if (projects.Any(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase)))
                        {
                            projectName = $"{projectName}-scripts";
                        }
                    }

                    var projectNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{relativeProjectDir}:";
                    if (projects.Any(p => p.Id == projectNodeId)) continue;

                    var manifestProps = new Dictionary<string, string>(fbResult.Project.ManifestProperties, StringComparer.OrdinalIgnoreCase);
                    var dominantLang = manifestProps.GetValueOrDefault("language") ?? fallbackParser.ProjectType;

                    var projectNode = ProjectNodeFactory.Create(
                        projectNodeId,
                        projectName,
                        relativeProjectDir,
                        dominantLang,
                        dir,
                        filesInDir,
                        null,
                        manifestProps,
                        fbResult.Project.ManifestMetadata);

                    projectsStructureNode.Children.Add(projectNode);
                    projects.Add(projectNode);
                    if (!string.IsNullOrEmpty(relativeProjectDir))
                    {
                        coveredPaths.Add((relativeProjectDir, relativeProjectDir.TrimEnd('/') + "/"));
                    }
                    RegisterEcosystemPackageAlias(packageToProjectMap, null, projectName, projectNode);
                    var normName = WorkspaceConventions.NormalizeServiceName(projectName);
                    if (!string.IsNullOrEmpty(normName))
                    {
                        RegisterEcosystemPackageAlias(packageToProjectMap, null, normName, projectNode);
                        WorkspaceConventions.RegisterServiceAlias(projectName, normName);
                    }
                    if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        RegisterEcosystemPackageAlias(packageToProjectMap, null, folderName, projectNode);
                        WorkspaceConventions.RegisterServiceAlias(folderName, normName ?? projectName);
                    }
                }
            }
        }

        // If subtree scan and some files are not covered by discovered projects,
        // look upwards for enclosing project(s)
        if (ctx.IsSubtreeScan)
        {
            var uncoveredFiles = l1Result.Files
                .Where(f => !projects.Any(p => IsEnclosedInProject(f, p, projects)))
                .ToList();

            if (uncoveredFiles.Count > 0)
            {
                var currentDir = new DirectoryInfo(ctx.ScanPath);
                while (currentDir != null && currentDir.FullName.Length >= ctx.AbsoluteWorkspacePath.Length)
                {
                    var dir = currentDir.FullName.Replace('\\', '/');
                    var relativeProjectDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, dir).Replace('\\', '/');
                    if (relativeProjectDir == ".") relativeProjectDir = "";

                    var projectNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{relativeProjectDir}:";

                    if (!projects.Any(p => p.Id == projectNodeId))
                    {
                        var filesInDir = ctx.DirectoryIndex.GetDirectoryFiles(dir);
                        var upContext = new ProjectParseContext(dir, filesInDir, ctx.AbsoluteWorkspacePath, ctx.DirectoryIndex);
                        IProjectParser? projectParser = null;
                        ProjectParseResult? upResult = null;
                        foreach (var candidateParser in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
                        {
                            if (candidateParser.TryParse(upContext, out var candidateRes) && candidateRes != null)
                            {
                                projectParser = candidateParser;
                                upResult = candidateRes;
                                break;
                            }
                        }
                        if (projectParser != null && upResult != null)
                        {
                            var upDesc = upResult.Project;
                            var projectName = upDesc.Name;
                            if (string.IsNullOrEmpty(projectName)) projectName = Path.GetFileName(dir);
                            if (string.IsNullOrEmpty(projectName)) projectName = dir;
                            var folderName = Path.GetFileName(dir);
                            if (string.IsNullOrEmpty(folderName)) folderName = projectName;

                            var depInfo = upDesc.Dependencies;
                            var extPkgNames = depInfo?.ExternalPackages.Select(p => p.Name).ToList();

                            var manifestProps = new Dictionary<string, string>(upDesc.ManifestProperties, StringComparer.OrdinalIgnoreCase);

                            var projectNode = ProjectNodeFactory.Create(
                                projectNodeId,
                                projectName,
                                relativeProjectDir,
                                projectParser.ProjectType,
                                dir,
                                filesInDir,
                                extPkgNames,
                                manifestProps,
                                upDesc.ManifestMetadata);

                            projectsStructureNode.Children.Add(projectNode);
                            projects.Add(projectNode);
                            RegisterEcosystemPackageAlias(packageToProjectMap, null, projectName, projectNode);
                            var normName = WorkspaceConventions.NormalizeServiceName(projectName);
                            if (!string.IsNullOrEmpty(normName))
                            {
                                RegisterEcosystemPackageAlias(packageToProjectMap, null, normName, projectNode);
                            }
                            if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase))
                            {
                                RegisterEcosystemPackageAlias(packageToProjectMap, null, folderName, projectNode);
                            }

                            if (depInfo != null)
                            {
                                AttachDependencies(projectNode, projectNodeId, depInfo, dir, dependencies, packages, projectParser, ctx);
                                projectDepList.Add((projectNode, depInfo));
                            }

                            var (prodPkg, prodEco, explicitCoord) = await LinkProducedPackageAsync(projectNode, projectNodeId, projectParser, dir, upDesc.ProducedPackage, ctx);
                            if (!string.IsNullOrEmpty(prodPkg))
                            {
                                RegisterEcosystemPackageAlias(packageToProjectMap, prodEco, prodPkg, projectNode, isExplicitPackageCoordinate: explicitCoord);
                            }

                            if (uncoveredFiles.All(f => projects.Any(p => IsEnclosedInProject(f, p, projects))))
                            {
                                break;
                            }
                        }
                    }

                    if (string.Equals(dir, ctx.AbsoluteWorkspacePath, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    currentDir = currentDir.Parent;
                }
            }
        }

        // Disambiguate duplicate project names if multiple projects share the exact same Name (e.g. copied package.json)
        var duplicateGroups = projects.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
        foreach (var group in duplicateGroups)
        {
            foreach (var proj in group)
            {
                var fullProjPath = Path.IsPathRooted(proj.Path)
                    ? proj.Path
                    : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, proj.Path));
                var folderName = Path.GetFileName(fullProjPath.TrimEnd('/', '\\'));
                if (!string.IsNullOrEmpty(folderName) &&
                    !IsGenericFolderName(folderName) &&
                    !string.Equals(folderName, proj.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var oldName = proj.Name;
                    proj.Name = folderName;
                    RegisterEcosystemPackageAlias(packageToProjectMap, null, folderName, proj);
                    RegisterEcosystemPackageAlias(packageToProjectMap, null, oldName, proj);
                    var normFolder = WorkspaceConventions.NormalizeServiceName(folderName);
                    if (!string.IsNullOrEmpty(normFolder))
                    {
                        RegisterEcosystemPackageAlias(packageToProjectMap, null, normFolder, proj);
                    }
                }
            }
        }
    }

    private static void NormalizeProjectFolderNames(
        List<ProjectNode> projects,
        Dictionary<string, ProjectNode> packageToProjectMap,
        Layer2Context ctx)
    {
        foreach (var proj in projects)
        {
            var fullProjPath = Path.IsPathRooted(proj.Path)
                ? proj.Path
                : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, proj.Path));
            var folderName = Path.GetFileName(fullProjPath.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(folderName) ||
                proj.ProjectType is ProjectLanguage.Script or ProjectLanguage.Batch or ProjectLanguage.Shell or ProjectLanguage.PowerShell ||
                proj.Name.EndsWith(".Scripts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderName, proj.Name, StringComparison.OrdinalIgnoreCase) ||
                projects.Any(p => p != proj && string.Equals(p.Name, folderName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (IsGenericFolderName(folderName))
            {
                continue;
            }

            // Only rename if there is no other project whose folder actually matches proj.Name
            var hasMatchingFolder = projects.Any(p =>
            {
                var pFullPath = Path.IsPathRooted(p.Path) ? p.Path : Path.Combine(ctx.AbsoluteWorkspacePath, p.Path);
                return string.Equals(Path.GetFileName(pFullPath.TrimEnd('/', '\\')), proj.Name, StringComparison.OrdinalIgnoreCase);
            });

            if (!hasMatchingFolder)
            {
                var oldName = proj.Name;
                proj.Name = folderName;
                RegisterEcosystemPackageAlias(packageToProjectMap, null, folderName, proj);
                RegisterEcosystemPackageAlias(packageToProjectMap, null, oldName, proj);
                var normFolder = WorkspaceConventions.NormalizeServiceName(folderName);
                if (!string.IsNullOrEmpty(normFolder))
                {
                    RegisterEcosystemPackageAlias(packageToProjectMap, null, normFolder, proj);
                }
            }
        }
    }

    private static async Task ScanSiblingLibrariesAsync(
        Layer2Context ctx,
        ProjectsStructureNode projectsStructureNode,
        List<ProjectNode> projects,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> projectDepList,
        Dictionary<string, ProjectNode> packageToProjectMap)
    {
        var unresolvedPackages = projectDepList
            .SelectMany(p => p.DepInfo.ExternalPackages.Select(pkg => (Proj: p.Project, Pkg: pkg)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Pkg.Name) &&
                        !packageToProjectMap.ContainsKey($"{ProjectNodeFactory.NormalizePackageEcosystem(x.Pkg.Ecosystem, x.Proj.ProjectType)}:{x.Pkg.Name}"))
            .Select(x => (Eco: ProjectNodeFactory.NormalizePackageEcosystem(x.Pkg.Ecosystem, x.Proj.ProjectType), Name: x.Pkg.Name))
            .ToHashSet();

        if (unresolvedPackages.Count == 0) return;

        var parentDir = Directory.GetParent(ctx.AbsoluteWorkspacePath)?.FullName;
        if (parentDir == null) return;

        var candidateLibraryDirs = new List<string>();
        var searchFolderNames = new[] { "libs", "libraries", "packages", "shared", "integrations/libs", "integrations\\libs" };
        foreach (var folder in searchFolderNames)
        {
            var fullCandidate = Path.GetFullPath(Path.Combine(parentDir, folder));
            if (Directory.Exists(fullCandidate) && !fullCandidate.StartWithIC(ctx.AbsoluteWorkspacePath))
            {
                candidateLibraryDirs.Add(fullCandidate);
            }
        }

        foreach (var libRoot in candidateLibraryDirs)
        {
            try
            {
                var subDirs = ctx.FileFilter.EnumerateDirectories(libRoot);
                foreach (var subDir in subDirs)
                {
                    var filesInSubDir = ctx.DirectoryIndex.GetDirectoryFiles(subDir);
                    var subParseContext = new ProjectParseContext(subDir, filesInSubDir, ctx.AbsoluteWorkspacePath, ctx.DirectoryIndex);
                    IProjectParser? subParser = null;
                    ProjectParseResult? subRes = null;
                    foreach (var candidateSub in WorkspaceIndexer._projectParsers)
                    {
                        if (candidateSub.TryParse(subParseContext, out var candidateSubRes) && candidateSubRes != null)
                        {
                            subParser = candidateSub;
                            subRes = candidateSubRes;
                            break;
                        }
                    }
                    if (subParser != null && subRes != null)
                    {
                        var prod = subRes.Project.ProducedPackage;
                        var prodEco = prod != null
                            ? ProjectNodeFactory.NormalizePackageEcosystem(prod.Ecosystem, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(subParser.ProjectType))
                            : string.Empty;
                        if (prod != null && unresolvedPackages.Any(u => string.Equals(u.Eco, prodEco, StringComparison.OrdinalIgnoreCase) && string.Equals(u.Name, prod.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            var folderName = Path.GetFileName(subDir);
                            var relDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, subDir).Replace('\\', '/');
                            var subProjId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{relDir}:";

                            if (!projects.Any(p => p.Id == subProjId))
                            {
                                var libManifestProps = new Dictionary<string, string>(subRes.Project.ManifestProperties, StringComparer.OrdinalIgnoreCase);
                                var libProjectNode = ProjectNodeFactory.Create(
                                    subProjId,
                                    folderName,
                                    relDir,
                                    subParser.ProjectType,
                                    subDir,
                                    filesInSubDir,
                                    manifestProps: libManifestProps,
                                    manifestMetadata: subRes.Project.ManifestMetadata);
                                projectsStructureNode.Children.Add(libProjectNode);
                                projects.Add(libProjectNode);

                                var ecosystem = !string.IsNullOrEmpty(prod.Ecosystem) ? prod.Ecosystem.ToLowerInvariant() : subParser.ProjectType.ToLowerInvariant();
                                var packageNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{ecosystem}:{prod.Name.ToLowerInvariant()}";
                                RegisterEcosystemPackageAlias(packageToProjectMap, prod.Ecosystem, folderName, libProjectNode);
                                RegisterEcosystemPackageAlias(packageToProjectMap, prod.Ecosystem, prod.Name, libProjectNode, isExplicitPackageCoordinate: true);

                                var packageNode = new PackageNode(
                                    Id: packageNodeId,
                                    Name: prod.Name,
                                    Version: prod.Version,
                                    IsInternal: true,
                                    Ecosystem: ecosystem,
                                    Path: libProjectNode.Path);
                                libProjectNode.Children.Add(packageNode);
                                var producesRel = Relationship.FromRelationship(new ProducesRelationship(subProjId, packageNodeId));
                                var implRel = Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, subProjId));
                                await ctx.EnqueueUploadRelationshipsAsync([producesRel, implRel]);
                                ctx.AddRelsCount(2);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ctx.Layer2Log.SiblingLibraryScanError(libRoot, ex);
                ctx.RecordScanIssue("Layer2_Boundaries", "manifest_error", libRoot, ex.Message, name: "SiblingLibraryScanError", exception: ex, isFileParent: false);
            }
        }
    }

    private static async Task ScanAdditionalProjectsAsync(
        Layer2Context ctx,
        ProjectsStructureNode projectsStructureNode,
        IReadOnlyList<string> dirsToCheck,
        List<ProjectNode> projects,
        List<PackageNode> packages,
        List<Relationship> dependencies,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> projectDepList,
        Dictionary<string, ProjectNode> packageToProjectMap)
    {
        foreach (var dir in dirsToCheck)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var filesInDir = ctx.DirectoryIndex.GetDirectoryFiles(dir);
            if (filesInDir.Length == 0) continue;

            foreach (var parser in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
            {
                if (!filesInDir.Any(f => parser.IsManifestFile(Path.GetFileName(f))))
                {
                    continue;
                }

                IReadOnlyList<AdditionalProjectDescriptor> additionalDescriptors;
                ProjectDependencyInfo? cachedDirDeps = null;
                if (ctx.DirectoryIndex.TryGetCachedProjectParse(parser, dir, out var cachedParse) &&
                    cachedParse.Matched &&
                    cachedParse.Result != null)
                {
                    additionalDescriptors = cachedParse.Result.Project.AdditionalTargets ?? [];
                    cachedDirDeps = cachedParse.Result.Project.Dependencies;
                }
                else
                {
                    additionalDescriptors = parser.GetAdditionalProjects(dir, filesInDir);
                }

                foreach (var descriptor in additionalDescriptors)
                {
                    if (!File.Exists(descriptor.EntryPointPath))
                    {
                        continue;
                    }

                    var entryPoint = Path.GetFullPath(descriptor.EntryPointPath);
                    var relativeEntryPoint = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, entryPoint).Replace('\\', '/');
                    var relativeTargetDir = Path.GetDirectoryName(relativeEntryPoint)?.Replace('\\', '/') ?? "";
                    if (projects.Any(p => p.Name.Equals(descriptor.Name, StringComparison.OrdinalIgnoreCase) &&
                                          (p.Path.TrimEnd('/') == relativeTargetDir ||
                                           p.Path.TrimEnd('/') == relativeEntryPoint.TrimEnd('/'))))
                    {
                        continue;
                    }

                    var projectNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{relativeEntryPoint}:";
                    if (projects.Any(p => p.Id.Equals(projectNodeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var manifestDirectory = descriptor.Properties.GetValueOrDefault("cargo_manifest_dir") ?? dir;
                    var extensions = descriptor.Properties.ToDictionary(
                        property => property.Key,
                        property => property.Value,
                        StringComparer.OrdinalIgnoreCase);
                    string? relativeReachability = null;
                    if (extensions.Remove(OntologyConstants.PropertyNames.SourceReachability, out var reachabilityJson))
                    {
                        relativeReachability = RelativizeSourceReachability(reachabilityJson, ctx.AbsoluteWorkspacePath);
                    }

                    var projectNode = ProjectNodeFactory.Create(
                        projectNodeId,
                        descriptor.Name,
                        relativeEntryPoint,
                        parser.ProjectType,
                        manifestDirectory,
                        [entryPoint],
                        manifestProps: extensions);
                    if (!string.IsNullOrEmpty(relativeReachability))
                    {
                        projectNode.Signals().SourceReachability = relativeReachability;
                    }

                    projectsStructureNode.Children.Add(projectNode);
                    projects.Add(projectNode);
                    RegisterEcosystemPackageAlias(packageToProjectMap, null, descriptor.Name, projectNode);

                    ProjectDependencyInfo? dependencyInfo;
                    if (string.Equals(
                            manifestDirectory.NormalizeFullPath(trimTrailingSlash: true),
                            dir.NormalizeFullPath(trimTrailingSlash: true),
                            StringComparison.OrdinalIgnoreCase) &&
                        cachedDirDeps != null)
                    {
                        dependencyInfo = cachedDirDeps;
                    }
                    else if (ctx.DirectoryIndex.TryGetCachedProjectParse(parser, manifestDirectory, out var cachedManifestParse) &&
                             cachedManifestParse.Matched &&
                             cachedManifestParse.Result != null)
                    {
                        dependencyInfo = cachedManifestParse.Result.Project.Dependencies;
                    }
                    else
                    {
                        dependencyInfo = await ParseProjectDependenciesAsync(parser, manifestDirectory, ctx);
                    }
                    if (dependencyInfo != null)
                    {
                        var localDependenciesOnly = new ProjectDependencyInfo(
                            descriptor.LocalDependencyPaths ?? dependencyInfo.LocalProjectPaths,
                            []);
                        AttachDependencies(
                            projectNode,
                            projectNodeId,
                            localDependenciesOnly,
                            manifestDirectory,
                            dependencies,
                            packages,
                            parser,
                            ctx);
                        projectDepList.Add((projectNode, localDependenciesOnly));
                    }
                }
            }
        }
    }

    private const string DepTypeLibrary = "library";
    private const string ReasonPackageDep = "package_dep";
    private const string ReasonLocalProjectRef = "local_project_ref";
    private const string ReasonSubmoduleLib = "submodule_lib";

    private static bool TryResolveSameEcosystemPackageTarget(
        Dictionary<string, ProjectNode> packageToProjectMap,
        ProjectNode callerProj,
        ProducedPackageInfo extPack,
        out ProjectNode? targetProj)
    {
        targetProj = null;
        var rawName = extPack.Name.Trim();
        var depEco = ProjectNodeFactory.NormalizePackageEcosystem(extPack.Ecosystem, callerProj.ProjectType);

        if (packageToProjectMap.TryGetValue($"{depEco}:{rawName}", out var direct) &&
            ProjectNodeFactory.AreCompatibleEcosystems(callerProj.ProjectType, direct.ProjectType))
        {
            targetProj = direct;
            return true;
        }

        if (depEco == "pypi" && (rawName.Contains('_') || rawName.Contains('-')))
        {
            var pep503 = rawName.Replace('_', '-');
            if (!ProjectNodeFactory.IsGenericPackageShortName(pep503) &&
                packageToProjectMap.TryGetValue($"pypi:{pep503}", out var pyMatch) &&
                ProjectNodeFactory.AreCompatibleEcosystems(callerProj.ProjectType, pyMatch.ProjectType))
            {
                targetProj = pyMatch;
                return true;
            }
        }

        // Go subpackage imports (e.g. "example.com/service-a/pkg/foo" -> module "example.com/service-a")
        if (depEco == "go" && rawName.Contains('/'))
        {
            var segments = rawName.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var len = segments.Length - 1; len >= 2; len--)
            {
                var prefix = string.Join('/', segments.Take(len));
                if (packageToProjectMap.TryGetValue($"go:{prefix}", out var goModMatch) &&
                    ProjectNodeFactory.AreCompatibleEcosystems(callerProj.ProjectType, goModMatch.ProjectType))
                {
                    targetProj = goModMatch;
                    return true;
                }
            }
        }

        return false;
    }

    private static void ResolveCrossProjectDependencies(
        Layer2Context ctx,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> projectDepList,
        Dictionary<string, ProjectNode> packageToProjectMap,
        List<Relationship> dependencies)
    {
        var seenDependsOn = dependencies
            .Where(d => d.Kind == RelationshipKind.DependsOn)
            .Select(d => (d.From, d.To))
            .ToHashSet();

        foreach (var (proj, depInfo) in projectDepList)
        {
            foreach (var extPack in depInfo.ExternalPackages)
            {
                if (string.IsNullOrWhiteSpace(extPack.Name)) continue;
                if (string.Equals(extPack.Scope, "dev", StringComparison.OrdinalIgnoreCase)) continue;

                if (TryResolveSameEcosystemPackageTarget(packageToProjectMap, proj, extPack, out var targetProj) &&
                    targetProj != null)
                {
                    if (!string.Equals(proj.Id, targetProj.Id, StringComparison.OrdinalIgnoreCase) &&
                        seenDependsOn.Add((proj.Id, targetProj.Id)))
                    {
                        var depRel = Relationship.FromRelationship(new DependsOnRelationship(proj.Id, targetProj.Id)
                        {
                            DependencyType = DepTypeLibrary,
                            Reason = ReasonPackageDep,
                            SourceDep = extPack.Name
                        });
                        dependencies.Add(depRel);
                        ctx.AddGlobalProjectDependency(depRel);
                    }
                }
            }
        }
    }

    private static void LinkNestedSubProjects(
        Layer2Context ctx,
        List<ProjectNode> projects,
        List<Relationship> dependencies)
    {
        foreach (var childProj in projects)
        {
            if (string.IsNullOrEmpty(childProj.Path)) continue;
            var childNormPath = childProj.Path.Replace('\\', '/').Trim('/');

            ProjectNode? bestParent = null;
            var bestParentLen = -1;
            foreach (var candidateParent in projects)
            {
                if (string.Equals(candidateParent.Id, childProj.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (!ProjectNodeFactory.AreCompatibleEcosystems(candidateParent.ProjectType, childProj.ProjectType)) continue;
                var parentNormPath = candidateParent.Path.Replace('\\', '/').Trim('/');
                if (string.IsNullOrEmpty(parentNormPath))
                {
                    if (candidateParent.EntityKind == ProjectEntityKind.ScriptProject ||
                        candidateParent.ProjectType is ProjectLanguage.Script or ProjectLanguage.Batch or ProjectLanguage.PowerShell or ProjectLanguage.Shell)
                    {
                        continue;
                    }
                    if (bestParentLen < 0)
                    {
                        bestParent = candidateParent;
                        bestParentLen = 0;
                    }
                    continue;
                }

                if (childNormPath.StartsWith(parentNormPath + "/", StringComparison.OrdinalIgnoreCase) &&
                    parentNormPath.Length > bestParentLen)
                {
                    bestParent = candidateParent;
                    bestParentLen = parentNormPath.Length;
                }
            }

            if (bestParent != null)
            {
                childProj.Signals().ParentProjectId = bestParent.Id;
                var containsProjRel = new Relationship(bestParent.Id, childProj.Id, RelationshipKind.Contains);
                if (!dependencies.Any(d => d.From == bestParent.Id && d.To == childProj.Id && d.Kind == RelationshipKind.Contains))
                {
                    dependencies.Add(containsProjRel);
                }

                if (childProj.IsLibrary && !bestParent.IsLibrary && bestParent.IsBuildOrchestrator != true &&
                    bestParent.ProjectType != ProjectLanguage.CSharp &&
                    !dependencies.Any(d => d.From == bestParent.Id && d.To == childProj.Id && d.Kind == RelationshipKind.DependsOn))
                {
                    var depRel = Relationship.FromRelationship(new DependsOnRelationship(bestParent.Id, childProj.Id)
                    {
                        DependencyType = DepTypeLibrary,
                        Reason = ReasonSubmoduleLib,
                        SourceDep = childProj.Path
                    });
                    dependencies.Add(depRel);
                    ctx.AddGlobalProjectDependency(depRel);
                }
            }
        }
    }

    private static void NormalizeDependencyProjectIds(
        List<ProjectNode> projects,
        List<Relationship> dependencies)
    {
        var projectsById = projects.ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);
        for (var i = dependencies.Count - 1; i >= 0; i--)
        {
            var dep = dependencies[i];
            var normFrom = projectsById.TryGetValue(dep.From, out var fromProj) ? fromProj.Id : dep.From;
            var normTo = projectsById.TryGetValue(dep.To, out var toProj) ? toProj.Id : dep.To;

            if (dep.Kind == RelationshipKind.DependsOn &&
                fromProj != null &&
                toProj != null &&
                !ProjectNodeFactory.AreCompatibleEcosystems(fromProj.ProjectType, toProj.ProjectType))
            {
                dependencies.RemoveAt(i);
                continue;
            }

            if (!string.Equals(normFrom, dep.From, StringComparison.Ordinal) ||
                !string.Equals(normTo, dep.To, StringComparison.Ordinal))
            {
                dependencies[i] = dep with { From = normFrom, To = normTo };
            }
        }
    }

    private static async Task<ProjectDependencyInfo?> ParseProjectDependenciesAsync(
        IProjectParser projectParser,
        string projectDir,
        Layer2Context ctx)
    {
        try
        {
            return await projectParser.ParseDependenciesAsync(projectDir);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            const string layer2Name = "Layer2_Boundaries", manifestErr = "manifest_error", depParseErr = "DependencyParseError";
            ctx.Layer2Log.DependencyParseFailed(projectParser.ProjectType, projectDir, ex.Message, ex);
            ctx.RecordScanIssue(layer2Name, manifestErr, projectDir, $"Error parsing dependencies for {projectParser.ProjectType}: {ex.Message}", name: depParseErr, exception: ex, language: projectParser.ProjectType, isFileParent: false);
            return null;
        }
    }

    private static string? RelativizeSourceReachability(string json, string workspaceRoot)
    {
        try
        {
            var absolute = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
            if (absolute is null || absolute.Count == 0)
            {
                return null;
            }

            string ToRelative(string path) =>
                path.GetNormalizedRelativePath(workspaceRoot).TrimEnd('/');

            var relative = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in absolute)
            {
                var relKey = ToRelative(entry.Key);
                var relVals = entry.Value.Select(ToRelative).ToArray();
                if (relative.TryGetValue(relKey, out var existing))
                {
                    relative[relKey] = [.. existing.Union(relVals, StringComparer.OrdinalIgnoreCase)];
                }
                else
                {
                    relative[relKey] = relVals;
                }
            }
            return JsonSerializer.Serialize(relative);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void AttachDependencies(
        ProjectNode projectNode,
        string projectNodeId,
        ProjectDependencyInfo depInfo,
        string projectDir,
        List<Relationship> dependencies,
        List<PackageNode> packages,
        IProjectParser projectParser,
        Layer2Context ctx)
    {
        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < dependencies.Count; i++)
        {
            var d = dependencies[i];
            if (d.From == projectNodeId && d.Kind == RelationshipKind.DependsOn)
            {
                seenTargets.Add(d.To);
            }
        }

        // A. Process local project dependencies (DependsOn relationships)
        foreach (var localPath in depInfo.LocalProjectPaths)
        {
            var targetDir = Path.GetFullPath(Path.Combine(projectDir, localPath)).Replace('\\', '/');

            var relativeTargetDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, targetDir).Replace('\\', '/');
            if (relativeTargetDir == ".") relativeTargetDir = "";
            var targetProjectNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{relativeTargetDir}:";

            if (seenTargets.Add(targetProjectNodeId))
            {
                var dependsOnRel = Relationship.FromRelationship(new DependsOnRelationship(projectNodeId, targetProjectNodeId)
                {
                    DependencyType = DepTypeLibrary,
                    Reason = ReasonLocalProjectRef,
                    SourceDep = localPath
                });
                dependencies.Add(dependsOnRel);
                ctx.AddGlobalProjectDependency(dependsOnRel);
            }
        }

        // B. Process external package dependencies
        if (depInfo.ExternalPackages.Count > 0)
        {
            foreach (var extPack in depInfo.ExternalPackages)
            {
                var ecosystem = !string.IsNullOrEmpty(extPack.Ecosystem) ? extPack.Ecosystem.ToLowerInvariant() : projectParser.ProjectType.ToLowerInvariant();
                var packageNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{ecosystem}:{extPack.Name.ToLowerInvariant()}";

                var packageNode = new PackageNode(
                    Id: packageNodeId,
                    Name: extPack.Name,
                    Version: extPack.Version,
                    IsInternal: false,
                    Ecosystem: ecosystem,
                    Path: string.Empty,
                    Archetype: OntologyConstants.Archetypes.PureUtility);
                ApplyExternalPackageMetadata(packageNode, extPack);
                projectNode.Children.Add(packageNode);
                packages.Add(packageNode);

                if (seenTargets.Add(packageNodeId))
                {
                    var dependsOnPkgRel = Relationship.FromRelationship(new DependsOnRelationship(projectNodeId, packageNodeId, new() { ["dependency_type"] = "package", ["category"] = "library" }));
                    dependencies.Add(dependsOnPkgRel);
                    ctx.AddGlobalProjectDependency(dependsOnPkgRel);
                }
            }
        }
    }

    private static async Task<(string? Name, string? Ecosystem, bool IsExplicitCoordinate)> LinkProducedPackageAsync(
        ProjectNode projectNode,
        string projectNodeId,
        IProjectParser projectParser,
        string projectDir,
        ProducedPackageInfo? producedPackage,
        Layer2Context ctx)
    {
        var packageDetected = false;
        string? resultName = null;
        string? resultEco = null;

        if (producedPackage != null)
        {
            var ecosystem = !string.IsNullOrEmpty(producedPackage.Ecosystem) ? producedPackage.Ecosystem.ToLowerInvariant() : projectParser.ProjectType.ToLowerInvariant();
            var packageNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{ecosystem}:{producedPackage.Name.ToLowerInvariant()}";

            var packageNode = new PackageNode(
                Id: packageNodeId,
                Name: producedPackage.Name,
                Version: producedPackage.Version,
                IsInternal: true,
                Ecosystem: ecosystem,
                Path: projectNode.Path);

            projectNode.Children.Add(packageNode);

            var producesRel = Relationship.FromRelationship(new ProducesRelationship(projectNodeId, packageNodeId));
            var implRel = Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, projectNodeId));
            await ctx.EnqueueUploadRelationshipsAsync([producesRel, implRel]);
            ctx.AddRelsCount(2);

            packageDetected = true;
            resultName = producedPackage.Name;
            resultEco = ecosystem;
        }

        if (!packageDetected)
        {
            var dirName = Path.GetFileName(projectDir);

            if (!string.IsNullOrEmpty(dirName) && !ProjectNodeFactory.IsGenericPackageShortName(dirName))
            {
                const string defaultInternalPkgVersion = "1.0.0";
                var ecosystem = projectParser.ProjectType.ToLowerInvariant();
                var packageNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{ecosystem}:{dirName.ToLowerInvariant()}";
                var packageNode = new PackageNode(
                    Id: packageNodeId,
                    Name: dirName,
                    Version: defaultInternalPkgVersion,
                    IsInternal: true,
                    Ecosystem: ecosystem,
                    Path: projectNode.Path);

                projectNode.Children.Add(packageNode);

                var producesRel = Relationship.FromRelationship(new ProducesRelationship(projectNodeId, packageNodeId));
                var implRel = Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, projectNodeId));
                await ctx.EnqueueUploadRelationshipsAsync([producesRel, implRel]);
                ctx.AddRelsCount(2);

                resultName = dirName;
                resultEco = ecosystem;
            }
        }

        return (resultName, resultEco, packageDetected);
    }
}
