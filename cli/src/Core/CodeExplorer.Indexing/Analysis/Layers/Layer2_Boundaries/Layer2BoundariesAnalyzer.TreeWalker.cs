using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    private sealed class DirectoryTreeNode(
        string directoryPath,
        string normalizedPath,
        string[] filesInDir,
        bool hasCandidateManifest)
    {
        public string DirectoryPath { get; } = directoryPath;
        public string NormalizedPath { get; } = normalizedPath;
        public string[] FilesInDir { get; } = filesInDir;
        public bool HasCandidateManifest { get; set; } = hasCandidateManifest;
        public bool SubtreeHasCandidateManifest { get; set; } = hasCandidateManifest;
        public List<DirectoryTreeNode> Children { get; } = [];
    }

    private sealed record EnclosingProjectScope(
        ProjectNode Project,
        IProjectParser Parser,
        ParsedProjectDescriptor Descriptor,
        bool IsOrchestrator,
        IReadOnlyList<string> ExcludedFullPaths)
    {
        public bool IsExcludedSubtree(string normDir)
        {
            for (var i = 0; i < ExcludedFullPaths.Count; i++)
            {
                var excl = ExcludedFullPaths[i];
                if (normDir.Equals(excl, StringComparison.OrdinalIgnoreCase) ||
                    normDir.StartsWith(excl + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

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
        var nodesByNormPath = new Dictionary<string, DirectoryTreeNode>(dirsToCheck.Count, StringComparer.OrdinalIgnoreCase);
        var allNodes = new List<(DirectoryTreeNode Node, DirectoryTreeNode? Parent)>(dirsToCheck.Count);
        var roots = new List<DirectoryTreeNode>();

        foreach (var dir in dirsToCheck)
        {
            var norm = dir.NormalizeFullPath(trimTrailingSlash: true);
            if (nodesByNormPath.ContainsKey(norm))
            {
                continue;
            }

            var filesInDir = PhysicalFileStore.GetDirectoryFiles(dir);
            var hasCandidate = HasCandidateProjectTrigger(dir, filesInDir);
            var node = new DirectoryTreeNode(dir, norm, filesInDir, hasCandidate);
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
            allNodes.Add((node, parentNode));
        }

        for (var i = allNodes.Count - 1; i >= 0; i--)
        {
            var (node, parent) = allNodes[i];
            if (!node.HasCandidateManifest && HasChildContainerProjectTrigger(node))
            {
                node.HasCandidateManifest = true;
                node.SubtreeHasCandidateManifest = true;
            }
            if (node.SubtreeHasCandidateManifest && parent != null)
            {
                parent.SubtreeHasCandidateManifest = true;
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
        if (!node.SubtreeHasCandidateManifest)
        {
            return;
        }

        state.Ctx.CancellationToken.ThrowIfCancellationRequested();

        var dir = node.DirectoryPath;
        if (enclosingScope != null && enclosingScope.IsExcludedSubtree(node.NormalizedPath))
        {
            return;
        }

        var filesInDir = node.FilesInDir;
        if (!node.HasCandidateManifest)
        {
            foreach (var child in node.Children)
            {
                await WalkDirectoryNodeAsync(child, enclosingScope, state);
            }
            return;
        }

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

    private static readonly HashSet<string> CandidateTriggerFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "tsconfig.json", "tsconfig.base.json", "jsconfig.json",
        "project.json", "ng-package.json", "wrangler.toml", "wrangler.jsonc",
        "wrangler.json", "angular.json", "nest-cli.json",
        "pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts", "AndroidManifest.xml",
        "go.mod", "Cargo.toml",
        "requirements.txt", "pyproject.toml", "setup.py", "setup.cfg", "Pipfile", "alembic.ini", "manage.py",
        "composer.json", "composer.lock",
        "cpanfile", "Makefile.PL", "Build.PL", "META.json", "META.yml", "dist.ini",
        "terragrunt.hcl", "root.hcl",
        "box.json", "Application.cfc", "Application.cfm",
        "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "packages.config"
    };

    private static readonly HashSet<string> CandidateTriggerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj", ".fsproj", ".vbproj", ".nuspec",
        ".tf", ".tofu",
        ".cfc", ".cfm", ".cfml",
        ".php", ".phtml",
        ".pl", ".pm", ".t", ".psgi"
    };

    private const string BuiltInParserNamespacePrefix = "CodeExplorer.Parsers";

    private static bool HasCandidateProjectTrigger(string dir, string[] filesInDir)
    {
        if (filesInDir.Length == 0)
        {
            return IsPotentialManifestlessBinarySubfolder(dir);
        }

        for (var i = 0; i < filesInDir.Length; i++)
        {
            var file = filesInDir[i];
            var fileName = Path.GetFileName(file);
            if (CandidateTriggerFileNames.Contains(fileName))
            {
                return true;
            }

            var ext = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext) && CandidateTriggerExtensions.Contains(ext))
            {
                return true;
            }
        }

        return IsPotentialManifestlessBinarySubfolder(dir) ||
               WorkspaceIndexer._projectParsers.Any(p => !p.IsFallback && p.GetType().Namespace?.StartsWith(BuiltInParserNamespacePrefix, StringComparison.Ordinal) != true);
    }

    private static bool IsPotentialManifestlessBinarySubfolder(string dir)
    {
        var parentDir = Path.GetDirectoryName(dir.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(parentDir)) return false;
        var parentName = Path.GetFileName(parentDir);
        return parentName.Equals("bin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasChildContainerProjectTrigger(DirectoryTreeNode node)
    {
        if (node.Children.Count == 0)
        {
            return false;
        }

        var phpChildren = 0;
        var perlChildren = 0;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            var childName = Path.GetFileName(child.NormalizedPath);
            if (childName.Equals("requirements", StringComparison.OrdinalIgnoreCase) ||
                childName.Equals("public", StringComparison.OrdinalIgnoreCase) ||
                childName.Equals("www", StringComparison.OrdinalIgnoreCase) ||
                childName.Equals("htdocs", StringComparison.OrdinalIgnoreCase) ||
                childName.Equals("web", StringComparison.OrdinalIgnoreCase) ||
                childName.Equals("html", StringComparison.OrdinalIgnoreCase) ||
                childName.Equals("application", StringComparison.OrdinalIgnoreCase))
            {
                if (child.SubtreeHasCandidateManifest)
                {
                    return true;
                }
            }

            var hasPhp = false;
            var hasPerl = false;
            for (var f = 0; f < child.FilesInDir.Length; f++)
            {
                var ext = Path.GetExtension(child.FilesInDir[f]);
                if (ext.Equals(".php", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".phtml", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".inc", StringComparison.OrdinalIgnoreCase))
                {
                    hasPhp = true;
                }
                else if (ext.Equals(".pl", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".pm", StringComparison.OrdinalIgnoreCase))
                {
                    hasPerl = true;
                }
            }

            if (hasPhp) phpChildren++;
            if (hasPerl) perlChildren++;
            if (phpChildren >= 2 || perlChildren >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasExplicitManifestForParser(IProjectParser parser, string[] filesInDir)
    {
        for (var i = 0; i < filesInDir.Length; i++)
        {
            var fileName = Path.GetFileName(filesInDir[i]);
            if (parser.IsManifestFile(fileName) ||
                CandidateTriggerFileNames.Contains(fileName))
            {
                return true;
            }

            var ext = Path.GetExtension(fileName);
            if (ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".nuspec", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".tf", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".tofu", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
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
            if (enclosingScope != null)
            {
                if (!enclosingScope.IsOrchestrator &&
                    !HasExplicitManifestForParser(parser, filesInDir) &&
                    !IsPotentialManifestlessBinarySubfolder(dir))
                {
                    continue;
                }

                if (!enclosingScope.Parser.CanContainNestedProject(enclosingScope.Descriptor, dir, filesInDir, parser))
                {
                    continue;
                }
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
            primaryScope ??= CreateEnclosingProjectScope(dir, projectNode, projectParser, descriptor);
        }

        return primaryScope;
    }

    private const string PropIsBuildOrchestratorKey = "is_build_orchestrator";
    private const string PropIsOrchestratorKey = "is_orchestrator";

    private static EnclosingProjectScope CreateEnclosingProjectScope(
        string dir,
        ProjectNode projectNode,
        IProjectParser projectParser,
        ParsedProjectDescriptor descriptor)
    {
        var isOrchestrator =
            projectNode.IsBuildOrchestrator == true ||
            string.Equals(descriptor.ManifestProperties.GetValueOrDefault(PropIsBuildOrchestratorKey), "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(descriptor.ManifestProperties.GetValueOrDefault(PropIsOrchestratorKey), "true", StringComparison.OrdinalIgnoreCase);

        var excludedFull = descriptor.ExcludedSubpaths is { Count: > 0 }
            ? descriptor.ExcludedSubpaths.Select(s => Path.GetFullPath(Path.Combine(dir, s)).Replace('\\', '/').TrimEnd('/')).ToArray()
            : [];

        return new EnclosingProjectScope(
            projectNode,
            projectParser,
            descriptor,
            isOrchestrator,
            excludedFull);
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
