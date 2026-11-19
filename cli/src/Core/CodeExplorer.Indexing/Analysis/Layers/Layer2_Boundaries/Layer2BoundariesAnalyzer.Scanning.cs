using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    private static async Task ScanProjectsInDirectoriesAsync(
        Layer1Result l1Result,
        ParsingContext ctx,
        ProjectsStructureNode projectsStructureNode,
        List<string> dirsToCheck,
        List<ProjectNode> projects,
        List<PackageNode> packages,
        List<Relationship> dependencies,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> projectDepList,
        Dictionary<string, ProjectNode> packageToProjectMap,
        Dictionary<string, ProjectNode> absorbedDirectoryOwners,
        Dictionary<string, string> explicitChildToParentMap)
    {
        var scanState = new DirectoryScanState(
            ctx,
            projectsStructureNode,
            projects,
            packages,
            dependencies,
            projectDepList,
            packageToProjectMap,
            absorbedDirectoryOwners,
            explicitChildToParentMap);

        await WalkDirectoryForestAsync(dirsToCheck, scanState);

        // Pass 2: Fallback project detection for directories with loose scripts or unmanaged projects not enclosed in any standard project
        var fallbackParsers = WorkspaceIndexer._projectParsers.Where(p => p.IsFallback).ToList();
        if (!ctx.IsSubtreeScan && fallbackParsers.Count > 0)
        {
            var hasRootNonScriptOwner = projects.Any(p =>
                string.IsNullOrEmpty(p.Path) &&
                p.EntityKind != ProjectEntityKind.ScriptProject &&
                p.IsBuildOrchestrator != true);
            var coveredPaths = projects
                .Where(p => !string.IsNullOrEmpty(p.Path))
                .Select(p => (Exact: p.Path, Prefix: p.Path.TrimEnd('/') + "/"))
                .ToList();

            foreach (var dir in dirsToCheck)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var filesInDir = PhysicalFileStore.GetDirectoryFiles(dir);
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
                        relativeProjectDir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        isCovered = true;
                        break;
                    }
                }

                if (isCovered) continue;

                var fallbackParser = fallbackParsers.FirstOrDefault(p => p.IsProjectDirectory(dir, filesInDir));
                if (fallbackParser != null)
                {
                    var folderName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(folderName)) folderName = Path.GetFileName(ctx.AbsoluteWorkspacePath.TrimEnd('/', '\\'));
                    var projectName = fallbackParser.GetProjectName(dir, filesInDir);
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

                    var manifestProps = fallbackParser.ExtractManifestProperties(dir, filesInDir);
                    var dominantLang = manifestProps.GetValueOrDefault("language") ?? fallbackParser.ProjectType;

                    var projectNode = ProjectNodeFactory.Create(
                        projectNodeId,
                        projectName,
                        relativeProjectDir,
                        dominantLang,
                        dir,
                        filesInDir,
                        null,
                        manifestProps);

                    projectsStructureNode.Children.Add(projectNode);
                    projects.Add(projectNode);
                    if (!string.IsNullOrEmpty(relativeProjectDir))
                    {
                        coveredPaths.Add((relativeProjectDir, relativeProjectDir.TrimEnd('/') + "/"));
                    }
                    packageToProjectMap[projectName] = projectNode;
                    var normName = WorkspaceConventions.NormalizeServiceName(projectName);
                    if (!string.IsNullOrEmpty(normName))
                    {
                        packageToProjectMap[normName] = projectNode;
                        WorkspaceConventions.RegisterServiceAlias(projectName, normName);
                    }
                    if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        packageToProjectMap[folderName] = projectNode;
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
                        var filesInDir = Directory.GetFiles(dir);
                        var projectParser = WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback).FirstOrDefault(p => p.IsProjectDirectory(dir, filesInDir));
                        if (projectParser != null)
                        {
                            var projectName = projectParser.GetProjectName(dir, filesInDir);
                            if (string.IsNullOrEmpty(projectName)) projectName = Path.GetFileName(dir);
                            if (string.IsNullOrEmpty(projectName)) projectName = dir;
                            var folderName = Path.GetFileName(dir);
                            if (string.IsNullOrEmpty(folderName)) folderName = projectName;

                            var depInfo = await ParseProjectDependenciesAsync(projectParser, dir, ctx);
                            var extPkgNames = depInfo?.ExternalPackages.Select(p => p.Name).ToList();

                            var manifestProps = projectParser.ExtractManifestProperties(dir, filesInDir);

                            var projectNode = ProjectNodeFactory.Create(
                                projectNodeId,
                                projectName,
                                relativeProjectDir,
                                projectParser.ProjectType,
                                dir,
                                filesInDir,
                                extPkgNames,
                                manifestProps);

                            projectsStructureNode.Children.Add(projectNode);
                            projects.Add(projectNode);
                            packageToProjectMap[projectName] = projectNode;
                            var normName = WorkspaceConventions.NormalizeServiceName(projectName);
                            if (!string.IsNullOrEmpty(normName))
                            {
                                packageToProjectMap[normName] = projectNode;
                            }
                            if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase))
                            {
                                packageToProjectMap[folderName] = projectNode;
                            }

                            if (depInfo != null)
                            {
                                AttachDependencies(projectNode, projectNodeId, depInfo, dir, dependencies, packages, projectParser, ctx);
                                projectDepList.Add((projectNode, depInfo));
                            }

                            var prodPkg = await LinkProducedPackageAsync(projectNode, projectNodeId, projectParser, dir, ctx);
                            if (!string.IsNullOrEmpty(prodPkg))
                            {
                                packageToProjectMap[prodPkg] = projectNode;
                                packageToProjectMap[$"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{prodPkg.ToLowerInvariant()}"] = projectNode;
                                if (prodPkg.Contains('/'))
                                {
                                    var shortName = prodPkg.Split('/')[^1];
                                    if (!string.IsNullOrEmpty(shortName))
                                    {
                                        packageToProjectMap.TryAdd(shortName, projectNode);
                                    }
                                }
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
                    packageToProjectMap[folderName] = proj;
                    packageToProjectMap[oldName] = proj;
                    var normFolder = WorkspaceConventions.NormalizeServiceName(folderName);
                    if (!string.IsNullOrEmpty(normFolder))
                    {
                        packageToProjectMap[normFolder] = proj;
                    }
                }
            }
        }
    }

    private static void NormalizeProjectFolderNames(
        List<ProjectNode> projects,
        Dictionary<string, ProjectNode> packageToProjectMap,
        ParsingContext ctx)
    {
        foreach (var proj in projects)
        {
            var fullProjPath = Path.IsPathRooted(proj.Path)
                ? proj.Path
                : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, proj.Path));
            var folderName = Path.GetFileName(fullProjPath.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(folderName) ||
                proj.ProjectType is CodeExplorer.Cypher.Common.ProjectLanguage.Script or CodeExplorer.Cypher.Common.ProjectLanguage.Batch or CodeExplorer.Cypher.Common.ProjectLanguage.Shell or CodeExplorer.Cypher.Common.ProjectLanguage.PowerShell ||
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
                packageToProjectMap[folderName] = proj;
                packageToProjectMap[oldName] = proj;
                var normFolder = WorkspaceConventions.NormalizeServiceName(folderName);
                if (!string.IsNullOrEmpty(normFolder))
                {
                    packageToProjectMap[normFolder] = proj;
                }
            }
        }
    }

    private static async Task ScanSiblingLibrariesAsync(
        ParsingContext ctx,
        ProjectsStructureNode projectsStructureNode,
        List<ProjectNode> projects,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> projectDepList,
        Dictionary<string, ProjectNode> packageToProjectMap)
    {
        var unresolvedPackages = projectDepList
            .SelectMany(p => p.DepInfo.ExternalPackages)
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) &&
                        !packageToProjectMap.ContainsKey(p.Name) &&
                        (!p.Name.Contains('/') || !packageToProjectMap.ContainsKey(p.Name.Split('/')[^1])))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                    var filesInSubDir = Directory.GetFiles(subDir);
                    var subParser = WorkspaceIndexer._projectParsers.FirstOrDefault(p => p.IsProjectDirectory(subDir, filesInSubDir));
                    if (subParser != null)
                    {
                        var prod = await subParser.GetProducedPackageAsync(subDir);
                        if (prod != null && (unresolvedPackages.Contains(prod.Name) || (prod.Name.Contains('/') && unresolvedPackages.Contains(prod.Name.Split('/')[^1]))))
                        {
                            var folderName = Path.GetFileName(subDir);
                            var relDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, subDir).Replace('\\', '/');
                            var subProjId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{relDir}:";

                            if (!projects.Any(p => p.Id == subProjId))
                            {
                                var libProjectNode = ProjectNodeFactory.Create(
                                    subProjId,
                                    folderName,
                                    relDir,
                                    subParser.ProjectType,
                                    subDir,
                                    filesInSubDir);
                                projectsStructureNode.Children.Add(libProjectNode);
                                projects.Add(libProjectNode);

                                var ecosystem = !string.IsNullOrEmpty(prod.Ecosystem) ? prod.Ecosystem.ToLowerInvariant() : subParser.ProjectType.ToLowerInvariant();
                                var packageNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{ecosystem}:{prod.Name.ToLowerInvariant()}";
                                packageToProjectMap[folderName] = libProjectNode;
                                packageToProjectMap[prod.Name] = libProjectNode;
                                packageToProjectMap[packageNodeId] = libProjectNode;
                                if (prod.Name.Contains('/'))
                                {
                                    packageToProjectMap.TryAdd(prod.Name.Split('/')[^1], libProjectNode);
                                }

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
                ctx.RecordScanIssue("Layer2_Boundaries", "manifest_error", libRoot, ex.Message, name: "SiblingLibraryScanError", exception: ex, isFileParent: false, logToConsole: false);
            }
        }
    }

    private static async Task ScanAdditionalProjectsAsync(
        ParsingContext ctx,
        ProjectsStructureNode projectsStructureNode,
        List<string> dirsToCheck,
        List<ProjectNode> projects,
        List<PackageNode> packages,
        List<Relationship> dependencies,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> projectDepList,
        Dictionary<string, ProjectNode> packageToProjectMap)
    {
        foreach (var dir in dirsToCheck)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var filesInDir = PhysicalFileStore.GetDirectoryFiles(dir);
            if (filesInDir.Length == 0) continue;

            foreach (var parser in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
            {
                if (!filesInDir.Any(f => parser.IsManifestFile(Path.GetFileName(f))))
                {
                    continue;
                }

                foreach (var descriptor in parser.GetAdditionalProjects(dir, filesInDir))
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
                    packageToProjectMap[descriptor.Name] = projectNode;

                    var dependencyInfo = await ParseProjectDependenciesAsync(parser, manifestDirectory, ctx);
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

    private static void ResolveCrossProjectDependencies(
        ParsingContext ctx,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> projectDepList,
        Dictionary<string, ProjectNode> packageToProjectMap,
        List<Relationship> dependencies)
    {
        var seenDependsOn = dependencies
            .Where(d => d.Kind == CodeExplorer.Cypher.Common.RelationshipKind.DependsOn)
            .Select(d => (d.From, d.To))
            .ToHashSet();

        foreach (var (proj, depInfo) in projectDepList)
        {
            foreach (var extPack in depInfo.ExternalPackages)
            {
                if (string.IsNullOrWhiteSpace(extPack.Name)) continue;
                if (string.Equals(extPack.Scope, "dev", StringComparison.OrdinalIgnoreCase)) continue;

                var matched = packageToProjectMap.TryGetValue(extPack.Name, out var targetProj) ||
                              packageToProjectMap.TryGetValue($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{extPack.Name.ToLowerInvariant()}", out targetProj);

                if (!matched && extPack.Name.Contains('/'))
                {
                    var shortName = extPack.Name.Split('/')[^1];
                    matched = packageToProjectMap.TryGetValue(shortName, out targetProj);
                }

                if (matched && targetProj != null)
                {
                    if (!string.Equals(proj.Id, targetProj.Id, StringComparison.OrdinalIgnoreCase) &&
                        seenDependsOn.Add((proj.Id, targetProj.Id)))
                    {
                        var depRel = Relationship.FromRelationship(new DependsOnRelationship(proj.Id, targetProj.Id, new() { ["dependency_type"] = "library" }));
                        dependencies.Add(depRel);
                        ctx.AddGlobalProjectDependency(depRel);
                    }
                }
            }
        }
    }

    private static void LinkNestedSubProjects(
        ParsingContext ctx,
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
                if (!CodeExplorer.Core.Analysis.ProjectNodeFactory.AreCompatibleEcosystems(candidateParent.ProjectType, childProj.ProjectType)) continue;
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
                var containsProjRel = new Relationship(bestParent.Id, childProj.Id, CodeExplorer.Cypher.Common.RelationshipKind.Contains);
                if (!dependencies.Any(d => d.From == bestParent.Id && d.To == childProj.Id && d.Kind == CodeExplorer.Cypher.Common.RelationshipKind.Contains))
                {
                    dependencies.Add(containsProjRel);
                }

                if (childProj.IsLibrary && !bestParent.IsLibrary && bestParent.IsBuildOrchestrator != true &&
                    bestParent.ProjectType != ProjectLanguage.CSharp &&
                    !dependencies.Any(d => d.From == bestParent.Id && d.To == childProj.Id && d.Kind == CodeExplorer.Cypher.Common.RelationshipKind.DependsOn))
                {
                    var depRel = Relationship.FromRelationship(new DependsOnRelationship(bestParent.Id, childProj.Id, new() { ["dependency_type"] = "library" }));
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
        var canonicalProjectIds = projects.ToDictionary(p => p.Id, p => p.Id, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dependencies.Count; i++)
        {
            var dep = dependencies[i];
            var normFrom = canonicalProjectIds.GetValueOrDefault(dep.From, dep.From);
            var normTo = canonicalProjectIds.GetValueOrDefault(dep.To, dep.To);
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
        ParsingContext ctx)
    {
        try
        {
            return await projectParser.ParseDependenciesAsync(projectDir);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            const string layer2Name = "Layer2_Boundaries", manifestErr = "manifest_error", depParseErr = "DependencyParseError";
            ctx.Layer2Log.DependencyParseFailed(projectParser.ProjectType, projectDir, ex.Message, ex);
            ctx.RecordScanIssue(layer2Name, manifestErr, projectDir, $"Error parsing dependencies for {projectParser.ProjectType}: {ex.Message}", name: depParseErr, exception: ex, language: projectParser.ProjectType, isFileParent: false, logToConsole: false);
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

            var relative = absolute.ToDictionary(
                entry => ToRelative(entry.Key),
                entry => entry.Value.Select(ToRelative).ToArray(),
                StringComparer.OrdinalIgnoreCase);
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
        ParsingContext ctx)
    {
        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < dependencies.Count; i++)
        {
            var d = dependencies[i];
            if (d.From == projectNodeId && d.Kind == CodeExplorer.Cypher.Common.RelationshipKind.DependsOn)
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
                var dependsOnRel = Relationship.FromRelationship(new DependsOnRelationship(projectNodeId, targetProjectNodeId, new() { ["dependency_type"] = "library" }));
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

    private static async Task<string?> LinkProducedPackageAsync(
        ProjectNode projectNode,
        string projectNodeId,
        IProjectParser projectParser,
        string projectDir,
        ParsingContext ctx)
    {
        var packageDetected = false;
        string? resultName = null;

        try
        {
            var producedPackage = await projectParser.GetProducedPackageAsync(projectDir);

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
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            const string layer2Name = "Layer2_Boundaries", manifestErr = "manifest_error", prodPkgErr = "ProducedPackageParseError";
            ctx.Layer2Log.ProducedPackageParseFailed(projectParser.ProjectType, projectDir, ex.Message, ex);
            ctx.RecordScanIssue(layer2Name, manifestErr, projectDir, $"Error getting produced package for {projectParser.ProjectType}: {ex.Message}", name: prodPkgErr, exception: ex, language: projectParser.ProjectType, isFileParent: false, logToConsole: false);
        }

        if (!packageDetected)
        {
            var dirName = Path.GetFileName(projectDir);

            if (!string.IsNullOrEmpty(dirName))
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
            }
        }

        return resultName;
    }
}
