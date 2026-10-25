using System.Text.Json;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer2ProjectParser
{
    public async Task<Layer2Result> ParseAsync(Layer1Result l1Result, ParsingContext ctx)
    {
        ctx.Log("[Layer2] Starting project boundary detection and dependency scan...");

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

        foreach (var dir in dirsToCheck)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            var normCurrentDir = Path.GetFullPath(dir).Replace('\\', '/').TrimEnd('/');
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
                                .GroupBy(p => $"{p.Type}:{p.Name}", StringComparer.OrdinalIgnoreCase)
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
                    string.IsNullOrEmpty(p.Path) ||
                    relativeProjectDir == p.Path ||
                    relativeProjectDir.StartsWith(p.Path.TrimEnd('/') + "/"));

                if (isCovered) continue;

                var filesInDir = Directory.GetFiles(dir);
                var fallbackParser = fallbackParsers.FirstOrDefault(p => p.IsProjectDirectory(dir, filesInDir));
                if (fallbackParser != null)
                {
                    var folderName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(folderName)) folderName = Path.GetFileName(ctx.AbsoluteWorkspacePath.TrimEnd('/', '\\'));
                    var projectName = folderName;
                    if (projects.Any(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase)))
                    {
                        projectName = string.IsNullOrEmpty(relativeProjectDir) ? folderName : relativeProjectDir.Replace('/', '-');
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
                if (!string.IsNullOrEmpty(folderName) && !IsGenericFolderName(folderName) && !string.Equals(folderName, proj.Name, StringComparison.OrdinalIgnoreCase))
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

        // Align project name with repository / folder name when a copied manifest (e.g. package.json)
        // retains the old template's name and no actual project folder matches that name.
        foreach (var proj in projects)
        {
            var fullProjPath = Path.IsPathRooted(proj.Path)
                ? proj.Path
                : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, proj.Path));
            var folderName = Path.GetFileName(fullProjPath.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(folderName) || string.Equals(folderName, proj.Name, StringComparison.OrdinalIgnoreCase))
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

        // Check for sibling or parent library directories if there are unresolved external packages
        var unresolvedPackages = projectDepList
            .SelectMany(p => p.DepInfo.ExternalPackages)
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) &&
                        !packageToProjectMap.ContainsKey(p.Name) &&
                        (!p.Name.Contains('/') || !packageToProjectMap.ContainsKey(p.Name.Split('/')[^1])))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (unresolvedPackages.Count > 0)
        {
            var parentDir = Directory.GetParent(ctx.AbsoluteWorkspacePath)?.FullName;
            if (parentDir != null)
            {
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

                                        var ecosystem = !string.IsNullOrEmpty(prod.Type) ? prod.Type.ToLowerInvariant() : subParser.ProjectType.ToLowerInvariant();
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
                        ctx.LogWarning($"[Layer2] Sibling library scan error in '{libRoot}': {ex.Message}", ex);
                    }
                }
            }
        }

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
                    if (extensions.TryGetValue(OntologyConstants.PropertyNames.SourceReachability, out var reachabilityJson))
                    {
                        var relativeReachability = RelativizeSourceReachability(reachabilityJson, ctx.AbsoluteWorkspacePath);
                        if (relativeReachability != null)
                        {
                            extensions[OntologyConstants.PropertyNames.SourceReachability] = relativeReachability;
                        }
                        else
                        {
                            extensions.Remove(OntologyConstants.PropertyNames.SourceReachability);
                        }
                    }

                    var projectNode = ProjectNodeFactory.Create(
                        projectNodeId,
                        descriptor.Name,
                        relativeEntryPoint,
                        parser.ProjectType,
                        manifestDirectory,
                        [entryPoint],
                        extensions: extensions);

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

        // Resolve cross-project dependencies via workspace packages/modules
        var crossProjectRels = new List<Relationship>();
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
                        if (!dependencies.Any(d => d.From == proj.Id && d.To == targetProj.Id && d.Kind == OntologyConstants.Relationships.DependsOn))
                        {
                            dependencies.Add(depRel);
                            ctx.AddGlobalProjectDependency(depRel);
                            crossProjectRels.Add(depRel);
                        }
                    }
                }
            }
        }

        // Link parent -> child SubProjects (:Project)-[:CONTAINS]->(:Project) when a project directory is nested inside an enclosing parent project
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
                if (childProj.Extensions != null)
                {
                    childProj.Extensions["parent_project_id"] = bestParent.Id;
                }
                var containsProjRel = new Relationship(bestParent.Id, childProj.Id, OntologyConstants.Relationships.Contains);
                if (!dependencies.Any(d => d.From == bestParent.Id && d.To == childProj.Id && d.Kind == OntologyConstants.Relationships.Contains))
                {
                    dependencies.Add(containsProjRel);
                }

                if (childProj.IsLibrary && !bestParent.IsLibrary &&
                    !dependencies.Any(d => d.From == bestParent.Id && d.To == childProj.Id && d.Kind == OntologyConstants.Relationships.DependsOn))
                {
                    var depRel = Relationship.FromRelationship(new DependsOnRelationship(bestParent.Id, childProj.Id, new() { ["dependency_type"] = "library" }));
                    dependencies.Add(depRel);
                    ctx.AddGlobalProjectDependency(depRel);
                }
            }
        }

        // Normalize any case-insensitive project IDs in dependencies to match canonical ProjectNode.Id casing
        var canonicalProjectIds = projects.ToDictionary(p => p.Id, p => p.Id, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dependencies.Count; i++)
        {
            var dep = dependencies[i];
            var normFrom = canonicalProjectIds.GetValueOrDefault(dep.From, dep.From);
            var normTo = canonicalProjectIds.GetValueOrDefault(dep.To, dep.To);
            if (!string.Equals(normFrom, dep.From, StringComparison.Ordinal) ||
                !string.Equals(normTo, dep.To, StringComparison.Ordinal))
            {
                dependencies[i] = new Relationship(normFrom, normTo, dep.Kind, dep.Properties);
            }
        }

        if (dependencies.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(dependencies);
            ctx.AddRelsCount(dependencies.Count);
            ctx.Log($"[Layer2] Resolved and enqueued {dependencies.Count} project dependencies.");
        }

        SharedFileEnricher.Enrich(l1Result.Files, projects, ctx);

        ctx.Log($"[Layer2] Project detection scan complete. Found {projects.Count} projects, {packages.Count} package nodes.");
        return new Layer2Result(l1Result, projectsStructureNode, projects, packages, dependencies);
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
        catch (Exception ex)
        {
            ctx.LogWarning($"[Layer2] Error parsing dependencies for {projectParser.ProjectType} in '{projectDir}': {ex.Message}", ex);
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
                Path.GetRelativePath(workspaceRoot, path).Replace('\\', '/').TrimEnd('/');

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
            if (!dependencies.Any(d => d.From == projectNodeId && d.To == targetProjectNodeId && d.Kind == OntologyConstants.Relationships.DependsOn))
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
                var ecosystem = !string.IsNullOrEmpty(extPack.Type) ? extPack.Type.ToLowerInvariant() : projectParser.ProjectType.ToLowerInvariant();
                var packageNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{ecosystem}:{extPack.Name.ToLowerInvariant()}";

                var packageNode = new PackageNode(
                    Id: packageNodeId,
                    Name: extPack.Name,
                    Version: extPack.Version,
                    IsInternal: false,
                    Ecosystem: ecosystem,
                    Path: string.Empty);
                projectNode.Children.Add(packageNode);
                packages.Add(packageNode);

                var dependsOnPkgRel = Relationship.FromRelationship(new DependsOnRelationship(projectNodeId, packageNodeId, new() { ["dependency_type"] = "package", ["category"] = "library" }));
                if (!dependencies.Any(d => d.From == projectNodeId && d.To == packageNodeId && d.Kind == OntologyConstants.Relationships.DependsOn))
                {
                    dependencies.Add(dependsOnPkgRel);
                    ctx.AddGlobalProjectDependency(dependsOnPkgRel);
                }
            }
        }
    }

    private async Task<string?> LinkProducedPackageAsync(
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
                var ecosystem = !string.IsNullOrEmpty(producedPackage.Type) ? producedPackage.Type.ToLowerInvariant() : projectParser.ProjectType.ToLowerInvariant();
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
        catch (Exception ex)
        {
            ctx.LogWarning($"[Layer2] Error getting produced package from {projectParser.ProjectType} parser in '{projectDir}': {ex.Message}", ex);
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

    public static ProjectNode? FindProjectForFilePath(string filePath, IEnumerable<ProjectNode> projects)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        var cleanPath = filePath.Replace('\\', '/').Trim('/');
        ProjectNode? bestMatch = null;
        var bestMatchLength = -1;

        foreach (var p in projects)
        {
            var pPath = p.Path.Replace('\\', '/').Trim('/');
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
