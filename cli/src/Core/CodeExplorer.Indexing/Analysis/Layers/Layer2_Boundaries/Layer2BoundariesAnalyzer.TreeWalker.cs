using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    private sealed record DirectoryTreeNode(
        string DirectoryPath,
        string NormalizedPath,
        List<DirectoryTreeNode> Children);

    private sealed record EnclosingProjectScope(
        ProjectNode Project,
        IProjectParser Parser,
        ParsedProjectDescriptor Descriptor);

    private sealed record DirectoryScanState(
        ParsingContext Ctx,
        ProjectsStructureNode ProjectsStructureNode,
        List<ProjectNode> Projects,
        List<PackageNode> Packages,
        List<Relationship> Dependencies,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> ProjectDepList,
        Dictionary<string, ProjectNode> PackageToProjectMap,
        Dictionary<string, ProjectNode> AbsorbedDirectoryOwners,
        Dictionary<string, string> ExplicitChildToParentMap);

    private static List<DirectoryTreeNode> BuildDirectoryForest(List<string> dirsToCheck)
    {
        var nodesByNormPath = new Dictionary<string, DirectoryTreeNode>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<DirectoryTreeNode>();

        foreach (var dir in dirsToCheck)
        {
            var norm = dir.NormalizeFullPath(trimTrailingSlash: true);
            if (nodesByNormPath.ContainsKey(norm))
            {
                continue;
            }

            var node = new DirectoryTreeNode(dir, norm, []);
            nodesByNormPath[norm] = node;

            DirectoryTreeNode? parentNode = null;
            var slashIdx = norm.LastIndexOf('/');
            while (slashIdx > 0)
            {
                var candidateParent = norm[..slashIdx];
                if (nodesByNormPath.TryGetValue(candidateParent, out parentNode))
                {
                    break;
                }
                slashIdx = candidateParent.LastIndexOf('/');
            }

            if (parentNode != null)
            {
                parentNode.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    private static async Task WalkDirectoryForestAsync(
        List<string> dirsToCheck,
        DirectoryScanState state)
    {
        var forest = BuildDirectoryForest(dirsToCheck);
        foreach (var root in forest)
        {
            await WalkDirectoryNodeAsync(root, enclosingScope: null, state);
        }
    }

    private static async Task WalkDirectoryNodeAsync(
        DirectoryTreeNode node,
        EnclosingProjectScope? enclosingScope,
        DirectoryScanState state)
    {
        state.Ctx.CancellationToken.ThrowIfCancellationRequested();

        var dir = node.DirectoryPath;
        var filesInDir = PhysicalFileStore.GetDirectoryFiles(dir);

        var absorbingOwner = ResolveAbsorbingOwner(node.NormalizedPath, dir, filesInDir, enclosingScope, state);
        if (absorbingOwner != null)
        {
            await MergeAbsorbedDependenciesAsync(dir, filesInDir, absorbingOwner, state);
            foreach (var child in node.Children)
            {
                await WalkDirectoryNodeAsync(child, enclosingScope, state);
            }
            return;
        }

        var activeResults = MatchProjectParsersInDirectory(dir, filesInDir, enclosingScope, state.Ctx);
        if (activeResults.Count == 0)
        {
            foreach (var child in node.Children)
            {
                await WalkDirectoryNodeAsync(child, enclosingScope, state);
            }
            return;
        }

        var nextScope = await InstantiateMatchedProjectsInDirectoryAsync(dir, filesInDir, activeResults, state)
                        ?? enclosingScope;

        foreach (var child in node.Children)
        {
            await WalkDirectoryNodeAsync(child, nextScope, state);
        }
    }

    private static ProjectNode? ResolveAbsorbingOwner(
        string normCurrentDir,
        string dir,
        string[] filesInDir,
        EnclosingProjectScope? enclosingScope,
        DirectoryScanState state)
    {
        foreach (var (absPath, ownerNode) in state.AbsorbedDirectoryOwners)
        {
            if (normCurrentDir.Equals(absPath, StringComparison.OrdinalIgnoreCase) ||
                normCurrentDir.StartsWith(absPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return ownerNode;
            }
        }

        var sourcelessOwner = SourcelessManifestDetector.FindOwner(dir, filesInDir, state.Projects, state.Ctx);
        if (sourcelessOwner != null)
        {
            return sourcelessOwner;
        }

        if (enclosingScope != null)
        {
            var hasBlockedManifest = filesInDir.Any(f =>
            {
                var fileName = Path.GetFileName(f);
                return WorkspaceIndexer._projectParsers.Any(p =>
                    !p.IsFallback &&
                    p.IsManifestFile(fileName) &&
                    !enclosingScope.Parser.CanContainNestedProject(enclosingScope.Descriptor, dir, filesInDir, p));
            });

            if (hasBlockedManifest)
            {
                return enclosingScope.Project;
            }
        }

        return null;
    }

    private static async Task MergeAbsorbedDependenciesAsync(
        string dir,
        string[] filesInDir,
        ProjectNode absorbingOwner,
        DirectoryScanState state)
    {
        foreach (var mp in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
        {
            if (mp.IsManifestFile(Path.GetFileName(filesInDir.FirstOrDefault() ?? "")) ||
                filesInDir.Any(f => mp.IsManifestFile(Path.GetFileName(f))))
            {
                var absorbedDep = await ParseProjectDependenciesAsync(mp, dir, state.Ctx);
                if (absorbedDep != null)
                {
                    AttachDependencies(
                        absorbingOwner,
                        absorbingOwner.Id,
                        absorbedDep,
                        dir,
                        state.Dependencies,
                        state.Packages,
                        mp,
                        state.Ctx);
                    state.ProjectDepList.Add((absorbingOwner, absorbedDep));
                }
            }
        }
    }

    private static List<(IProjectParser Parser, ProjectParseResult Result)> MatchProjectParsersInDirectory(
        string dir,
        string[] filesInDir,
        EnclosingProjectScope? enclosingScope,
        ParsingContext ctx)
    {
        var parseContext = new ProjectParseContext(dir, filesInDir, ctx.AbsoluteWorkspacePath);
        var matchedResults = new List<(IProjectParser Parser, ProjectParseResult Result)>();

        foreach (var parser in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
        {
            if (enclosingScope != null &&
                !enclosingScope.Parser.CanContainNestedProject(enclosingScope.Descriptor, dir, filesInDir, parser))
            {
                continue;
            }

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
            return [];
        }

        var matchingParsers = matchedResults.Select(m => m.Parser).ToList();
        ProjectParserPrecedence.RemoveSuperseded(matchingParsers);
        return matchedResults.Where(m => matchingParsers.Contains(m.Parser)).ToList();
    }

    private static List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor, List<IProjectParser> MergedSecondaryParsers)>
        BuildIndependentEntries(List<(IProjectParser Parser, ProjectParseResult Result)> activeResults)
    {
        var hasPrimaryAbsorbing = activeResults.Any(r =>
            ProjectParserPrecedence.GetTier(r.Parser) == ParserTier.Primary &&
            r.Result.ClaimMode == ParserClaimMode.AbsorbSecondaryInDirectory);

        var entries = new List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor, List<IProjectParser> MergedSecondaryParsers)>();
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
                entries.Add((pParser, pRes.Project, idx == 0 ? secondaryParsers : []));
                if (pRes.AdditionalSiblingProjects != null)
                {
                    foreach (var sib in pRes.AdditionalSiblingProjects)
                    {
                        entries.Add((pParser, sib, []));
                    }
                }
            }
        }
        else
        {
            var first = activeResults[0];
            var restParsers = activeResults.Skip(1).Select(r => r.Parser).ToList();
            entries.Add((first.Parser, first.Result.Project, restParsers));
            if (first.Result.AdditionalSiblingProjects != null)
            {
                foreach (var sib in first.Result.AdditionalSiblingProjects)
                {
                    entries.Add((first.Parser, sib, []));
                }
            }
        }

        return entries;
    }

    private static async Task<EnclosingProjectScope?> InstantiateMatchedProjectsInDirectoryAsync(
        string dir,
        string[] filesInDir,
        List<(IProjectParser Parser, ProjectParseResult Result)> activeResults,
        DirectoryScanState state)
    {
        var independentEntries = BuildIndependentEntries(activeResults);
        EnclosingProjectScope? primaryScope = null;

        foreach (var (projectParser, descriptor, mergedSecondaryParsers) in independentEntries)
        {
            var projectName = descriptor.Name;
            if (string.IsNullOrEmpty(projectName)) projectName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(projectName)) projectName = dir;
            var folderName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(folderName)) folderName = projectName;

            var relativeProjectDir = Path.GetRelativePath(state.Ctx.AbsoluteWorkspacePath, dir).Replace('\\', '/');
            if (relativeProjectDir == ".") relativeProjectDir = "";

            var idKey = !string.IsNullOrEmpty(descriptor.RelativeIdPathOverride)
                ? descriptor.RelativeIdPathOverride
                : relativeProjectDir;
            var projectNodeId = $"{state.Ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{idKey}:";
            if (state.Projects.Any(p => p.Id.Equals(projectNodeId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var depInfo = await ParseMergedDependenciesAsync(dir, projectParser, mergedSecondaryParsers, state.Ctx);
            var extPkgNames = depInfo?.ExternalPackages.Select(p => p.Name).ToList();
            var manifestProps = BuildMergedManifestProperties(dir, filesInDir, projectName, descriptor, mergedSecondaryParsers);

            var projectNode = ProjectNodeFactory.Create(
                projectNodeId,
                projectName,
                relativeProjectDir,
                projectParser.ProjectType,
                dir,
                filesInDir,
                extPkgNames,
                manifestProps);

            state.ProjectsStructureNode.Children.Add(projectNode);
            state.Projects.Add(projectNode);
            RegisterProjectAliases(projectNode, projectName, folderName, independentEntries.Count == 1, state.PackageToProjectMap);
            RegisterAbsorbedAndSubProjects(dir, projectNode, descriptor, state);

            if (depInfo != null)
            {
                AttachDependencies(projectNode, projectNodeId, depInfo, dir, state.Dependencies, state.Packages, projectParser, state.Ctx);
                state.ProjectDepList.Add((projectNode, depInfo));
            }

            await RegisterProducedPackageAliasesAsync(projectNode, projectNodeId, projectParser, dir, state);
            primaryScope ??= new EnclosingProjectScope(projectNode, projectParser, descriptor);
        }

        return primaryScope;
    }

    private static async Task<ProjectDependencyInfo?> ParseMergedDependenciesAsync(
        string dir,
        IProjectParser primaryParser,
        List<IProjectParser> mergedSecondaryParsers,
        ParsingContext ctx)
    {
        ProjectDependencyInfo? depInfo = null;
        foreach (var mp in new[] { primaryParser }.Concat(mergedSecondaryParsers))
        {
            var subDep = await ParseProjectDependenciesAsync(mp, dir, ctx);
            if (subDep == null) continue;

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
        return depInfo;
    }

    private const string PropPrimaryRole = "primary_role";

    private static Dictionary<string, string> BuildMergedManifestProperties(
        string dir,
        string[] filesInDir,
        string projectName,
        ParsedProjectDescriptor descriptor,
        List<IProjectParser> mergedSecondaryParsers)
    {
        var manifestProps = new Dictionary<string, string>(descriptor.ManifestProperties, StringComparer.OrdinalIgnoreCase);
        foreach (var secParser in mergedSecondaryParsers)
        {
            var secProps = secParser.ExtractManifestProperties(dir, filesInDir);
            if (secProps.TryGetValue(PropPrimaryRole, out var secPrimary) &&
                string.Equals(secPrimary, "App", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(manifestProps.GetValueOrDefault(PropPrimaryRole), "Service", StringComparison.OrdinalIgnoreCase) &&
                !projectName.EndsWith(".API", StringComparison.OrdinalIgnoreCase))
            {
                manifestProps[PropPrimaryRole] = "App";
                manifestProps["role"] = "FrontendApp";
                manifestProps["kind"] = "App";
                manifestProps["framework_type"] = "frontend";
                manifestProps["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Service" });
                manifestProps["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "App", "Service" });
            }
        }
        return manifestProps;
    }

    private static void RegisterProjectAliases(
        ProjectNode projectNode,
        string projectName,
        string folderName,
        bool isSingleEntryInDir,
        Dictionary<string, ProjectNode> packageToProjectMap)
    {
        packageToProjectMap[projectName] = projectNode;
        var normName = WorkspaceConventions.NormalizeServiceName(projectName);
        if (!string.IsNullOrEmpty(normName))
        {
            packageToProjectMap[normName] = projectNode;
            WorkspaceConventions.RegisterServiceAlias(projectName, normName);
        }
        if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase) && isSingleEntryInDir)
        {
            packageToProjectMap[folderName] = projectNode;
            WorkspaceConventions.RegisterServiceAlias(folderName, normName ?? projectName);
        }
    }

    private static void RegisterAbsorbedAndSubProjects(
        string dir,
        ProjectNode projectNode,
        ParsedProjectDescriptor descriptor,
        DirectoryScanState state)
    {
        if (descriptor.AbsorbedSubpaths != null)
        {
            foreach (var sub in descriptor.AbsorbedSubpaths)
            {
                var fullAbs = Path.GetFullPath(Path.Combine(dir, sub)).Replace('\\', '/').TrimEnd('/');
                state.AbsorbedDirectoryOwners[fullAbs] = projectNode;
            }
        }

        if (descriptor.SubProjects != null)
        {
            foreach (var subDesc in descriptor.SubProjects)
            {
                if (!string.IsNullOrEmpty(subDesc.RelativeIdPathOverride))
                {
                    continue;
                }
                var subFullDir = Path.GetFullPath(subDesc.DirectoryPath).Replace('\\', '/').TrimEnd('/');
                state.ExplicitChildToParentMap[subFullDir] = projectNode.Id;
            }
        }
    }

    private static async Task RegisterProducedPackageAliasesAsync(
        ProjectNode projectNode,
        string projectNodeId,
        IProjectParser projectParser,
        string dir,
        DirectoryScanState state)
    {
        var prodPkg = await LinkProducedPackageAsync(projectNode, projectNodeId, projectParser, dir, state.Ctx);
        if (!string.IsNullOrEmpty(prodPkg))
        {
            state.PackageToProjectMap[prodPkg] = projectNode;
            state.PackageToProjectMap[$"{state.Ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:{prodPkg.ToLowerInvariant()}"] = projectNode;
            if (prodPkg.Contains('/'))
            {
                var shortName = prodPkg.Split('/')[^1];
                if (!string.IsNullOrEmpty(shortName))
                {
                    state.PackageToProjectMap.TryAdd(shortName, projectNode);
                }
            }
        }
    }
}
