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

        foreach (var dir in dirsToCheck)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            var filesInDir = Directory.GetFiles(dir);
            var projectParser = WorkspaceIndexer._projectParsers.FirstOrDefault(p => p.IsProjectDirectory(dir, filesInDir));
            
            if (projectParser != null)
            {
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName)) folderName = dir;
                
                var relativeProjectDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, dir).Replace('\\', '/');
                if (relativeProjectDir == ".") relativeProjectDir = "";

                var projectNodeId = $"{ctx.WorkspaceId}:project:{relativeProjectDir}:";
                var projectNode = new ProjectNode(projectNodeId, folderName, relativeProjectDir, projectParser.ProjectType);

                projectsStructureNode.Children.Add(projectNode);
                projects.Add(projectNode);

                // Parse project dependencies and packages
                await ParseDependenciesAsync(projectNode, projectNodeId, projectParser, dir, dependencies, packages, ctx);
                await LinkProducedPackageAsync(projectNode, projectNodeId, projectParser, dir, ctx);
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
                            var folderName = Path.GetFileName(dir);
                            if (string.IsNullOrEmpty(folderName)) folderName = dir;

                            var projectNode = new ProjectNode(projectNodeId, folderName, relativeProjectDir, projectParser.ProjectType);

                            projectsStructureNode.Children.Add(projectNode);
                            projects.Add(projectNode);

                            await ParseDependenciesAsync(projectNode, projectNodeId, projectParser, dir, dependencies, packages, ctx);
                            await LinkProducedPackageAsync(projectNode, projectNodeId, projectParser, dir, ctx);

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

        ctx.Log($"[Layer2] Project detection scan complete. Found {projects.Count} projects, {packages.Count} package nodes.");
        return new Layer2Result(l1Result, projectsStructureNode, projects, packages, dependencies);
    }

    private async Task ParseDependenciesAsync(
        ProjectNode projectNode,
        string projectNodeId,
        IProjectParser projectParser,
        string projectDir,
        List<Relationship> dependencies,
        List<PackageNode> packages,
        ParsingContext ctx)
    {
        try
        {
            var depInfo = await projectParser.ParseDependenciesAsync(projectDir);

            if (depInfo != null)
            {
                // A. Process local project dependencies (DependsOn relationships)
                foreach (var localPath in depInfo.LocalProjectPaths)
                {
                    var targetDir = Path.GetFullPath(Path.Combine(projectDir, localPath)).Replace('\\', '/');

                    var relativeTargetDir = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, targetDir).Replace('\\', '/');
                    if (relativeTargetDir == ".") relativeTargetDir = "";
                    var targetProjectNodeId = $"{ctx.WorkspaceId}:project:{relativeTargetDir}:";

                    var dependsOnRel = Relationship.FromRelationship(new DependsOnRelationship(projectNodeId, targetProjectNodeId));
                    dependencies.Add(dependsOnRel);
                    ctx.AddGlobalProjectDependency(dependsOnRel);
                }

                // B. Process external package dependencies
                if (depInfo.ExternalPackages.Count > 0)
                {
                    foreach (var extPack in depInfo.ExternalPackages)
                    {
                        var packageNodeId = $"{ctx.WorkspaceId}:package:{extPack.Name.ToLowerInvariant()}";

                        var packageNode = new PackageNode(packageNodeId, extPack.Name, extPack.Version, extPack.Type,
                            projectNode.Path);
                        projectNode.Children.Add(packageNode);
                        packages.Add(packageNode);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ctx.LogWarning($"[Layer2] Error parsing dependencies for {projectParser.ProjectType} in '{projectDir}': {ex.Message}", ex);
        }
    }

    private async Task LinkProducedPackageAsync(
        ProjectNode projectNode,
        string projectNodeId,
        IProjectParser projectParser,
        string projectDir,
        ParsingContext ctx)
    {
        var packageDetected = false;

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
            }
        }
    }

    public static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects)
    {
        ProjectNode? bestMatch = null;
        int bestMatchLength = -1;

        foreach (var p in projects)
        {
            var pPath = p.Path;
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
            if (file.Path.StartsWith(pPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (pPrefix.Length > bestMatchLength)
                {
                    bestMatch = p;
                    bestMatchLength = pPrefix.Length;
                }
            }
        }

        return bestMatch?.Id == project.Id;
    }
}
