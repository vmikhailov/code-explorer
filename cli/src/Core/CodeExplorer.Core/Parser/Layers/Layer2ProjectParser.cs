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
            $"{ctx.WorkspaceId}:projects_structure", 
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
                .Where(f => f.Path.StartsWith(ctx.ScanPath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Path));
        }
        else
        {
            dirsToCheck.Add(ctx.AbsoluteWorkspacePath);
            dirsToCheck.AddRange(l1Result.Folders.Select(f => f.Path));
        }
        dirsToCheck = [.. dirsToCheck.Distinct(StringComparer.OrdinalIgnoreCase)];

        var projectDepList = new List<(ProjectNode Project, ProjectDependencyInfo DepInfo)>();
        var packageToProjectMap = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in dirsToCheck)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            var filesInDir = Directory.GetFiles(dir);
            var projectParser = WorkspaceIndexer._projectParsers.FirstOrDefault(p => p.IsProjectDirectory(dir, filesInDir));
            
            if (projectParser != null)
            {
                var projectName = projectParser.GetProjectName(dir, filesInDir);
                if (string.IsNullOrEmpty(projectName)) projectName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(projectName)) projectName = dir;
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName)) folderName = projectName;
                
                var relativeProjectDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, dir).Replace('\\', '/');
                if (relativeProjectDir == ".") relativeProjectDir = "";

                var projectNodeId = $"{ctx.WorkspaceId}:project:{relativeProjectDir}:";

                // Parse project dependencies to supply externalPackages for role classification
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
                if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase))
                {
                    packageToProjectMap[folderName] = projectNode;
                }

                if (depInfo != null)
                {
                    AttachDependencies(projectNode, projectNodeId, depInfo, dir, dependencies, packages, ctx);
                    projectDepList.Add((projectNode, depInfo));
                }

                var prodPkg = await LinkProducedPackageAsync(projectNode, projectNodeId, projectParser, dir, ctx);
                if (!string.IsNullOrEmpty(prodPkg))
                {
                    packageToProjectMap[prodPkg] = projectNode;
                    packageToProjectMap[$"{ctx.WorkspaceId}:package:{prodPkg.ToLowerInvariant()}"] = projectNode;
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

                    var projectNodeId = $"{ctx.WorkspaceId}:project:{relativeProjectDir}:";

                    if (!projects.Any(p => p.Id == projectNodeId))
                    {
                        var filesInDir = Directory.GetFiles(dir);
                        var projectParser = WorkspaceIndexer._projectParsers.FirstOrDefault(p => p.IsProjectDirectory(dir, filesInDir));
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
                            if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase))
                            {
                                packageToProjectMap[folderName] = projectNode;
                            }

                            if (depInfo != null)
                            {
                                AttachDependencies(projectNode, projectNodeId, depInfo, dir, dependencies, packages, ctx);
                                projectDepList.Add((projectNode, depInfo));
                            }

                            var prodPkg = await LinkProducedPackageAsync(projectNode, projectNodeId, projectParser, dir, ctx);
                            if (!string.IsNullOrEmpty(prodPkg))
                            {
                                packageToProjectMap[prodPkg] = projectNode;
                                packageToProjectMap[$"{ctx.WorkspaceId}:package:{prodPkg.ToLowerInvariant()}"] = projectNode;
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
                var folderName = Path.GetFileName(proj.Path.TrimEnd('/', '\\'));
                if (!string.IsNullOrEmpty(folderName) && !string.Equals(folderName, proj.Name, StringComparison.OrdinalIgnoreCase))
                {
                    proj.Name = folderName;
                    packageToProjectMap[folderName] = proj;
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
                    if (Directory.Exists(fullCandidate) && !fullCandidate.StartsWith(ctx.AbsoluteWorkspacePath, StringComparison.OrdinalIgnoreCase))
                    {
                        candidateLibraryDirs.Add(fullCandidate);
                    }
                }

                foreach (var libRoot in candidateLibraryDirs)
                {
                    try
                    {
                        var subDirs = Directory.GetDirectories(libRoot, "*", SearchOption.AllDirectories);
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
                                    var subProjId = $"{ctx.WorkspaceId}:project:{relDir}:";

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

                                        packageToProjectMap[folderName] = libProjectNode;
                                        packageToProjectMap[prod.Name] = libProjectNode;
                                        packageToProjectMap[$"{ctx.WorkspaceId}:package:{prod.Name.ToLowerInvariant()}"] = libProjectNode;
                                        if (prod.Name.Contains('/'))
                                        {
                                            packageToProjectMap.TryAdd(prod.Name.Split('/')[^1], libProjectNode);
                                        }

                                        var packageNodeId = $"{ctx.WorkspaceId}:package:{prod.Name.ToLowerInvariant()}";
                                        var packageNode = new PackageNode(packageNodeId, prod.Name, prod.Version, prod.Type, libProjectNode.Path);
                                        libProjectNode.Children.Add(packageNode);
                                        var implRel = Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, subProjId));
                                        await ctx.EnqueueUploadRelationshipsAsync([implRel]);
                                        ctx.AddRelsCount(1);
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

        // Resolve cross-project dependencies via workspace packages/modules
        var crossProjectRels = new List<Relationship>();
        foreach (var (proj, depInfo) in projectDepList)
        {
            foreach (var extPack in depInfo.ExternalPackages)
            {
                if (string.IsNullOrWhiteSpace(extPack.Name)) continue;

                var matched = packageToProjectMap.TryGetValue(extPack.Name, out var targetProj) ||
                              packageToProjectMap.TryGetValue($"{ctx.WorkspaceId}:package:{extPack.Name.ToLowerInvariant()}", out targetProj);

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

        if (dependencies.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(dependencies);
            ctx.AddRelsCount(dependencies.Count);
            ctx.Log($"[Layer2] Resolved and enqueued {dependencies.Count} project dependencies.");
        }

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

    private static void AttachDependencies(
        ProjectNode projectNode,
        string projectNodeId,
        ProjectDependencyInfo depInfo,
        string projectDir,
        List<Relationship> dependencies,
        List<PackageNode> packages,
        ParsingContext ctx)
    {
        // A. Process local project dependencies (DependsOn relationships)
        foreach (var localPath in depInfo.LocalProjectPaths)
        {
            var targetDir = Path.GetFullPath(Path.Combine(projectDir, localPath)).Replace('\\', '/');

            var relativeTargetDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, targetDir).Replace('\\', '/');
            if (relativeTargetDir == ".") relativeTargetDir = "";
            var targetProjectNodeId = $"{ctx.WorkspaceId}:project:{relativeTargetDir}:";

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
                var packageNodeId = $"{ctx.WorkspaceId}:package:{extPack.Name.ToLowerInvariant()}";

                var packageNode = new PackageNode(packageNodeId, extPack.Name, extPack.Version, extPack.Type,
                    string.Empty, IsExternal: true);
                projectNode.Children.Add(packageNode);
                packages.Add(packageNode);
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
                var packageNodeId = $"{ctx.WorkspaceId}:package:{producedPackage.Name.ToLowerInvariant()}";

                var packageNode = new PackageNode(packageNodeId, producedPackage.Name, producedPackage.Version,
                    producedPackage.Type, projectNode.Path);

                projectNode.Children.Add(packageNode);

                var implRel =
                    Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, projectNodeId));
                await ctx.EnqueueUploadRelationshipsAsync([implRel]);
                ctx.AddRelsCount(1);

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
                var packageNodeId = $"{ctx.WorkspaceId}:package:{dirName.ToLowerInvariant()}";
                var packageNode = new PackageNode(packageNodeId, dirName, "1.0.0", "unknown", projectNode.Path);

                projectNode.Children.Add(packageNode);

                var implRel =
                    Relationship.FromRelationship(new ImplementedByRelationship(packageNodeId, projectNodeId));
                await ctx.EnqueueUploadRelationshipsAsync([implRel]);
                ctx.AddRelsCount(1);

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
            if (cleanPath.StartsWith(pPrefix, StringComparison.OrdinalIgnoreCase) || cleanPath.Equals(pPath, StringComparison.OrdinalIgnoreCase))
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

    public static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects)
    {
        return FindProjectForFilePath(file.Path, projects)?.Id == project.Id;
    }


}
