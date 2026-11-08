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
        foreach (var dir in dirsToCheck)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            var normCurrentDir = dir.NormalizeFullPath(trimTrailingSlash: true);
            ProjectNode? absorbingOwner = null;
            foreach (var (absPath, ownerNode) in absorbedDirectoryOwners)
            {
                if (normCurrentDir.Equals(absPath, StringComparison.OrdinalIgnoreCase) ||
                    normCurrentDir.StartsWith(absPath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    absorbingOwner = ownerNode;
                    break;
                }
            }

            var filesInDir = Directory.GetFiles(dir);

            // A manifest nested inside an already detected project that owns no source code of its own
            // (e.g. a package.json template or lockfile kept under src/) is a resource of that project,
            // not an independent architectural component.
            absorbingOwner ??= SourcelessManifestDetector.FindOwner(dir, filesInDir, projects, ctx);

            if (absorbingOwner != null)
            {
                // Merge any dependencies declared inside an absorbed subfolder (e.g. workers-site/package.json or webview/package.json) into the parent owner project
                foreach (var mp in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
                {
                    if (mp.IsManifestFile(Path.GetFileName(filesInDir.FirstOrDefault() ?? "")) ||
                        filesInDir.Any(f => mp.IsManifestFile(Path.GetFileName(f))))
                    {
                        var absorbedDep = await ParseProjectDependenciesAsync(mp, dir, ctx);
                        if (absorbedDep != null)
                        {
                            AttachDependencies(absorbingOwner, absorbingOwner.Id, absorbedDep, dir, dependencies, packages, mp, ctx);
                            projectDepList.Add((absorbingOwner, absorbedDep));
                        }
                    }
                }
                continue;
            }

            var parseContext = new ProjectParseContext(dir, filesInDir, ctx.AbsoluteWorkspacePath);
            var matchedResults = new List<(IProjectParser Parser, ProjectParseResult Result)>();
            foreach (var parser in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
            {
                if (parser.TryParse(parseContext, out var res) && res != null)
                {
                    matchedResults.Add((parser, res));
                    if (res.ClaimMode == ParserClaimMode.StopAllOtherParsersInDirectory)
                    {
                        break;
                    }
                }
            }

            if (matchedResults.Count == 0)
            {
                continue;
            }

            var matchingParsers = matchedResults.Select(m => m.Parser).ToList();
            ProjectParserPrecedence.RemoveSuperseded(matchingParsers);
            var activeResults = matchedResults.Where(m => matchingParsers.Contains(m.Parser)).ToList();
            if (activeResults.Count == 0)
            {
                continue;
            }

            // Separate Primary vs Secondary parsers when Primary claims AbsorbSecondaryInDirectory
            var hasPrimaryAbsorbing = activeResults.Any(r =>
                ProjectParserPrecedence.GetTier(r.Parser) == ParserTier.Primary &&
                r.Result.ClaimMode == ParserClaimMode.AbsorbSecondaryInDirectory);

            var independentEntries = new List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor, List<IProjectParser> MergedSecondaryParsers)>();
            if (hasPrimaryAbsorbing)
            {
                var secondaryParsers = activeResults
                    .Where(r => ProjectParserPrecedence.GetTier(r.Parser) != ParserTier.Primary)
                    .Select(r => r.Parser)
                    .ToList();

                var primaryEntries = activeResults
                    .Where(r => ProjectParserPrecedence.GetTier(r.Parser) == ParserTier.Primary)
                    .ToList();

                for (var idx = 0; idx < primaryEntries.Count; idx++)
                {
                    var (pParser, pRes) = primaryEntries[idx];
                    independentEntries.Add((pParser, pRes.Project, idx == 0 ? secondaryParsers : []));
                    if (pRes.AdditionalSiblingProjects != null)
                    {
                        foreach (var sib in pRes.AdditionalSiblingProjects)
                        {
                            independentEntries.Add((pParser, sib, []));
                        }
                    }
                }
            }
            else
            {
                var first = activeResults[0];
                var restParsers = activeResults.Skip(1).Select(r => r.Parser).ToList();
                independentEntries.Add((first.Parser, first.Result.Project, restParsers));
                if (first.Result.AdditionalSiblingProjects != null)
                {
                    foreach (var sib in first.Result.AdditionalSiblingProjects)
                    {
                        independentEntries.Add((first.Parser, sib, []));
                    }
                }
            }

            foreach (var (projectParser, descriptor, mergedSecondaryParsers) in independentEntries)
            {
                var projectName = descriptor.Name;
                if (string.IsNullOrEmpty(projectName)) projectName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(projectName)) projectName = dir;
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName)) folderName = projectName;

                var relativeProjectDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, dir).Replace('\\', '/');
                if (relativeProjectDir == ".") relativeProjectDir = "";

                var idKey = !string.IsNullOrEmpty(descriptor.RelativeIdPathOverride)
                    ? descriptor.RelativeIdPathOverride
                    : relativeProjectDir;
                var projectNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{idKey}:";
                if (projects.Any(p => p.Id.Equals(projectNodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Parse project dependencies across primary + absorbed secondary parsers in hybrid directories (e.g. .csproj + package.json)
                ProjectDependencyInfo? depInfo = null;
                foreach (var mp in new[] { projectParser }.Concat(mergedSecondaryParsers))
                {
                    var subDep = await ParseProjectDependenciesAsync(mp, dir, ctx);
                    if (subDep != null)
                    {
                        if (depInfo == null)
                        {
                            depInfo = subDep;
                        }
                        else
                        {
                            var mergedLocal = depInfo.LocalProjectPaths
                                .Concat(subDep.LocalProjectPaths)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                            var mergedExt = depInfo.ExternalPackages
                                .Concat(subDep.ExternalPackages)
                                .GroupBy(p => $"{p.Ecosystem}:{p.Name}", StringComparer.OrdinalIgnoreCase)
                                .Select(g => g.First())
                                .ToList();
                            depInfo = new ProjectDependencyInfo(mergedLocal, mergedExt);
                        }
                    }
                }
                var extPkgNames = depInfo?.ExternalPackages.Select(p => p.Name).ToList();

                var manifestProps = new Dictionary<string, string>(descriptor.ManifestProperties, StringComparer.OrdinalIgnoreCase);
                if (mergedSecondaryParsers.Count > 0)
                {
                    foreach (var secParser in mergedSecondaryParsers)
                    {
                        var secProps = secParser.ExtractManifestProperties(dir, filesInDir);
                        if (secProps.TryGetValue("primary_role", out var secPrimary) &&
                            string.Equals(secPrimary, "App", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(manifestProps.GetValueOrDefault("primary_role"), "Service", StringComparison.OrdinalIgnoreCase) &&
                            !projectName.EndsWith(".API", StringComparison.OrdinalIgnoreCase) &&
                            !projectName.EndsWith(".Api", StringComparison.OrdinalIgnoreCase))
                        {
                            manifestProps["primary_role"] = "App";
                            manifestProps["role"] = "FrontendApp";
                            manifestProps["kind"] = "App";
                            manifestProps["framework_type"] = "frontend";
                            manifestProps["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Service" });
                            manifestProps["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "App", "Service" });
                        }
                    }
                }

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
                    WorkspaceConventions.RegisterServiceAlias(projectName, normName);
                }
                if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase) && independentEntries.Count == 1)
                {
                    packageToProjectMap[folderName] = projectNode;
                    WorkspaceConventions.RegisterServiceAlias(folderName, normName ?? projectName);
                }

                // Register absorbed subpaths so nested technical folders (e.g. workers-site/, webview/) are merged into this project
                if (descriptor.AbsorbedSubpaths != null)
                {
                    foreach (var sub in descriptor.AbsorbedSubpaths)
                    {
                        var fullAbs = Path.GetFullPath(Path.Combine(dir, sub)).Replace('\\', '/').TrimEnd('/');
                        absorbedDirectoryOwners[fullAbs] = projectNode;
                    }
                }

                // Register declarative SubProjects (e.g. angular.json, nest-cli.json) if their directories aren't already created
                if (descriptor.SubProjects != null)
                {
                    foreach (var subDesc in descriptor.SubProjects)
                    {
                        if (!string.IsNullOrEmpty(subDesc.RelativeIdPathOverride))
                        {
                            continue; // EntryPoint-based targets (like Cargo [[bin]]) are handled in the additional targets pass below
                        }
                        var subFullDir = Path.GetFullPath(subDesc.DirectoryPath).Replace('\\', '/').TrimEnd('/');
                        explicitChildToParentMap[subFullDir] = projectNode.Id;
                    }
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
            }
        }

        // Pass 2: Fallback project detection for directories with loose scripts or unmanaged projects not enclosed in any standard project
        var fallbackParsers = WorkspaceIndexer._projectParsers.Where(p => p.IsFallback).ToList();
        if (!ctx.IsSubtreeScan && fallbackParsers.Count > 0)
        {
            foreach (var dir in dirsToCheck)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var relativeProjectDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, dir).Replace('\\', '/');
                if (relativeProjectDir == ".") relativeProjectDir = "";

                // Only trigger if NOT inside an already detected project
                var isCovered = projects.Any(p =>
                    relativeProjectDir == p.Path ||
                    (!string.IsNullOrEmpty(p.Path) && relativeProjectDir.StartsWith(p.Path.TrimEnd('/') + "/")));

                if (isCovered) continue;

                var filesInDir = Directory.GetFiles(dir);
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
            var filesInDir = Directory.GetFiles(dir);
            foreach (var parser in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
            {
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
        foreach (var (proj, depInfo) in projectDepList)
        {
            foreach (var extPack in depInfo.ExternalPackages)
            {
                if (string.IsNullOrWhiteSpace(extPack.Name)) continue;

                var matched = packageToProjectMap.TryGetValue(extPack.Name, out var targetProj) ||
                              packageToProjectMap.TryGetValue($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{extPack.Name.ToLowerInvariant()}", out targetProj);

                if (!matched && extPack.Name.Contains('/'))
                {
                    var shortName = extPack.Name.Split('/')[^1];
                    matched = packageToProjectMap.TryGetValue(shortName, out targetProj);
                }

                if (matched && targetProj != null)
                {
                    if (!string.Equals(proj.Id, targetProj.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        var depRel = Relationship.FromRelationship(new DependsOnRelationship(proj.Id, targetProj.Id, new() { ["dependency_type"] = "library" }));
                        if (!dependencies.Any(d => d.From == proj.Id && d.To == targetProj.Id && d.Kind == CodeExplorer.Cypher.Common.RelationshipKind.DependsOn))
                        {
                            dependencies.Add(depRel);
                            ctx.AddGlobalProjectDependency(depRel);
                        }
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
                var parentNormPath = candidateParent.Path.Replace('\\', '/').Trim('/');
                if (string.IsNullOrEmpty(parentNormPath))
                {
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

                if (childProj.IsLibrary && !bestParent.IsLibrary &&
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
            ctx.Layer2Log.DependencyParseFailed(projectParser.ProjectType, projectDir, ex.Message, ex);
            ctx.RecordScanIssue("Layer2_Boundaries", "manifest_error", projectDir, $"Error parsing dependencies for {projectParser.ProjectType}: {ex.Message}", name: "DependencyParseError", exception: ex, language: projectParser.ProjectType, isFileParent: false, logToConsole: false);
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
        // A. Process local project dependencies (DependsOn relationships)
        foreach (var localPath in depInfo.LocalProjectPaths)
        {
            var targetDir = Path.GetFullPath(Path.Combine(projectDir, localPath)).Replace('\\', '/');

            var relativeTargetDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, targetDir).Replace('\\', '/');
            if (relativeTargetDir == ".") relativeTargetDir = "";
            var targetProjectNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{relativeTargetDir}:";

            var dependsOnRel = Relationship.FromRelationship(new DependsOnRelationship(projectNodeId, targetProjectNodeId, new() { ["dependency_type"] = "library" }));
            if (!dependencies.Any(d => d.From == projectNodeId && d.To == targetProjectNodeId && d.Kind == CodeExplorer.Cypher.Common.RelationshipKind.DependsOn))
            {
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

                var dependsOnPkgRel = Relationship.FromRelationship(new DependsOnRelationship(projectNodeId, packageNodeId, new() { ["dependency_type"] = "package", ["category"] = "library" }));
                if (!dependencies.Any(d => d.From == projectNodeId && d.To == packageNodeId && d.Kind == CodeExplorer.Cypher.Common.RelationshipKind.DependsOn))
                {
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
            ctx.Layer2Log.ProducedPackageParseFailed(projectParser.ProjectType, projectDir, ex.Message, ex);
            ctx.RecordScanIssue("Layer2_Boundaries", "manifest_error", projectDir, $"Error getting produced package for {projectParser.ProjectType}: {ex.Message}", name: "ProducedPackageParseError", exception: ex, language: projectParser.ProjectType, isFileParent: false, logToConsole: false);
        }

        if (!packageDetected)
        {
            var dirName = Path.GetFileName(projectDir);

            if (!string.IsNullOrEmpty(dirName))
            {
                var ecosystem = projectParser.ProjectType.ToLowerInvariant();
                var packageNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{ecosystem}:{dirName.ToLowerInvariant()}";
                var packageNode = new PackageNode(
                    Id: packageNodeId,
                    Name: dirName,
                    Version: "1.0.0",
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
