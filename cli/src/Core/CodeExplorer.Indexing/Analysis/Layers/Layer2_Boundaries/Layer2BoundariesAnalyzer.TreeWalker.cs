using System.Text.Json;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

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
        Layer1Result L1Result,
        Layer2Context Ctx,
        ProjectsStructureNode ProjectsStructureNode,
        List<ProjectNode> Projects,
        List<PackageNode> Packages,
        List<Relationship> Dependencies,
        List<(ProjectNode Project, ProjectDependencyInfo DepInfo)> ProjectDepList,
        Dictionary<string, ProjectNode> PackageToProjectMap,
        Dictionary<string, ProjectNode> AbsorbedDirectoryOwners,
        Dictionary<string, string> ExplicitChildToParentMap);

    private static List<DirectoryTreeNode> BuildDirectoryForest(List<string> dirsToCheck, Layer2Context ctx)
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

            var filesInDir = ctx.DirectoryIndex.GetDirectoryFiles(dir);
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
        var forest = BuildDirectoryForest(dirsToCheck, state.Ctx);
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

        var isRoot = string.Equals(
            node.NormalizedPath,
            state.Ctx.AbsoluteWorkspacePath.NormalizeFullPath(trimTrailingSlash: true),
            StringComparison.OrdinalIgnoreCase);
        var activeResults = MatchProjectParsersInDirectory(dir, filesInDir, enclosingScope, state.Ctx);
        if (activeResults.Count == 0)
        {
            InspectUmbrellaDirectoryManifests(dir, filesInDir, state);
            foreach (var child in node.Children)
            {
                await WalkDirectoryNodeAsync(child, enclosingScope, state);
            }
            return;
        }

        if (isRoot)
        {
            InspectUmbrellaDirectoryManifests(dir, filesInDir, state, activeResults);
        }

        var nextScope = await InstantiateMatchedProjectsInDirectoryAsync(dir, filesInDir, activeResults, state)
                        ?? enclosingScope;

        foreach (var child in node.Children)
        {
            await WalkDirectoryNodeAsync(child, nextScope, state);
        }
    }

    private static bool HasCandidateProjectTrigger(string dir, string[] filesInDir) =>
        ProjectTriggerDetector.HasCandidateProjectTrigger(dir, filesInDir);

    private static bool IsPotentialManifestlessBinarySubfolder(string dir) =>
        ProjectTriggerDetector.IsPotentialManifestlessBinarySubfolder(dir);

    private static bool HasChildContainerProjectTrigger(DirectoryTreeNode node)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i].SubtreeHasCandidateManifest)
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
                ProjectTriggerDetector.CandidateTriggerFileNames.Contains(fileName))
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
        DirectoryScanState state,
        bool? precomputedIsLeafGitRepo = null,
        bool? precomputedHasSourceFiles = null)
    {
        var dirIndex = state.Ctx.DirectoryIndex;
        var isLeafGitRepo = precomputedIsLeafGitRepo
                            ?? (dirIndex.IsGitRepositoryRoot(dir) && !dirIndex.HasChildGitRepositories(dir));
        if ((dirIndex.HasAnyConfiguredProjects && dirIndex.TryGetConfiguredProject(dir, out _)) || isLeafGitRepo)
        {
            return null;
        }

        foreach (var (absPath, ownerNode) in state.AbsorbedDirectoryOwners)
        {
            if (normCurrentDir.Equals(absPath, StringComparison.OrdinalIgnoreCase) ||
                normCurrentDir.StartsWith(absPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return ownerNode;
            }
        }

        var sourcelessOwner = SourcelessManifestDetector.FindOwner(dir, filesInDir, state.Projects, state.Ctx, precomputedHasSourceFiles);
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
        InspectUmbrellaDirectoryManifests(dir, filesInDir, state);
        var parseContext = new ProjectParseContext(dir, filesInDir, state.Ctx.AbsoluteWorkspacePath, state.Ctx.DirectoryIndex);
        foreach (var mp in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
        {
            if (mp.IsManifestFile(Path.GetFileName(filesInDir.FirstOrDefault() ?? "")) ||
                filesInDir.Any(f => mp.IsManifestFile(Path.GetFileName(f))))
            {
                ProjectDependencyInfo? absorbedDep;
                if (mp.TryParse(parseContext, out var absorbedRes) && absorbedRes != null)
                {
                    absorbedDep = absorbedRes.Project.Dependencies;
                    RecordDirectoryManifestMetadata(dir, absorbedRes.Project.ManifestMetadata, isRoot: false, state);
                }
                else
                {
                    absorbedDep = await ParseProjectDependenciesAsync(mp, dir, state.Ctx);
                }

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

    private static void InspectUmbrellaDirectoryManifests(
        string dir,
        string[] filesInDir,
        DirectoryScanState state,
        List<(IProjectParser Parser, ProjectParseResult Result)>? alreadyMatched = null)
    {
        if (filesInDir.Length == 0) return;

        var normDir = dir.NormalizeFullPath(trimTrailingSlash: true);
        var normRoot = state.Ctx.AbsoluteWorkspacePath.NormalizeFullPath(trimTrailingSlash: true);
        var isRoot = string.Equals(normDir, normRoot, StringComparison.OrdinalIgnoreCase);

        if (alreadyMatched != null)
        {
            for (var i = 0; i < alreadyMatched.Count; i++)
            {
                RecordDirectoryManifestMetadata(dir, alreadyMatched[i].Result.Project.ManifestMetadata, isRoot, state);
            }
        }

        var parseContext = new ProjectParseContext(dir, filesInDir, state.Ctx.AbsoluteWorkspacePath, state.Ctx.DirectoryIndex);
        var hasTsParser = WorkspaceIndexer._projectParsers.Any(p => string.Equals(p.ProjectType, "typescript", StringComparison.OrdinalIgnoreCase));

        foreach (var parser in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
        {
            if (hasTsParser && string.Equals(parser.ProjectType, "javascript", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (alreadyMatched != null && alreadyMatched.Any(m => ReferenceEquals(m.Parser, parser)))
            {
                continue;
            }

            if (!filesInDir.Any(f => parser.IsManifestFile(Path.GetFileName(f))))
            {
                continue;
            }

            var meta = parser.InspectDirectoryManifestMetadata(parseContext);
            RecordDirectoryManifestMetadata(dir, meta, isRoot, state);
        }
    }

    private static void RecordDirectoryManifestMetadata(
        string dir,
        ParsedProjectManifestMetadata? meta,
        bool isRoot,
        DirectoryScanState state)
    {
        if (meta == null) return;

        var normDir = dir.NormalizeFullPath(trimTrailingSlash: true);
        if (!string.IsNullOrEmpty(meta.DatabaseDriverEngine) && !string.IsNullOrEmpty(meta.DatabaseDriverFamily))
        {
            state.Ctx.DirectoryManifestDrivers[normDir] = (meta.DatabaseDriverEngine, meta.DatabaseDriverFamily);
        }

        if (!string.IsNullOrEmpty(meta.GoModuleName))
        {
            var relDir = Path.GetRelativePath(state.Ctx.AbsoluteWorkspacePath, dir).Replace('\\', '/');
            if (string.IsNullOrEmpty(relDir)) relDir = ".";
            state.Ctx.WorkspaceGoModules.TryAdd(meta.GoModuleName, relDir);
        }

        if (isRoot && meta.TsConfigPaths != null)
        {
            foreach (var (k, v) in meta.TsConfigPaths)
            {
                state.Ctx.TsConfigPaths[k] = v;
            }
        }

        if (isRoot ||
            !string.IsNullOrEmpty(meta.NuspecId) ||
            !string.IsNullOrEmpty(meta.NuspecVersion) ||
            meta.NuspecDependencies is { Count: > 0 })
        {
            var wsMetaList = state.L1Result.Workspace.Signals().WorkspaceManifestMetadata;
            if (!wsMetaList.Contains(meta))
            {
                wsMetaList.Add(meta);
            }
        }
    }

    private static List<(IProjectParser Parser, ProjectParseResult Result)> MatchProjectParsersInDirectory(
        string dir,
        string[] filesInDir,
        EnclosingProjectScope? enclosingScope,
        Layer2Context ctx,
        bool? precomputedIsLeafGitRepo = null,
        SubtreeLanguageCounts? precomputedCounts = null)
    {
        var dirIndex = ctx.DirectoryIndex;
        WorkspaceProjectSetting? configuredSetting = null;
        var isExplicitConfigured = dirIndex.HasAnyConfiguredProjects &&
                                   dirIndex.TryGetConfiguredProject(dir, out configuredSetting) &&
                                   configuredSetting != null;
        if (!isExplicitConfigured && dirIndex.HasAnyConfiguredProjects && dirIndex.HasChildConfiguredProjects(dir))
        {
            return [];
        }

        var isRootDir = string.Equals(
            dir.NormalizeFullPath(trimTrailingSlash: true),
            ctx.AbsoluteWorkspacePath.NormalizeFullPath(trimTrailingSlash: true),
            StringComparison.OrdinalIgnoreCase);
        var isNestedGitRepo = !isRootDir &&
                              (precomputedIsLeafGitRepo ?? (dirIndex.IsGitRepositoryRoot(dir) && !dirIndex.HasChildGitRepositories(dir)));
        var parseContext = new ProjectParseContext(dir, filesInDir, ctx.AbsoluteWorkspacePath, dirIndex);
        var matchedResults = new List<(IProjectParser Parser, ProjectParseResult Result)>();

        foreach (var parser in WorkspaceIndexer._projectParsers.Where(p => !p.IsFallback))
        {
            if (enclosingScope != null && !isExplicitConfigured && !isNestedGitRepo)
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

        if (isExplicitConfigured && configuredSetting != null)
        {
            if (matchedResults.Count == 0)
            {
                var fallbackParser = ResolveConfiguredProjectParser(dir, configuredSetting, dirIndex);
                if (fallbackParser != null)
                {
                    var props = fallbackParser.ExtractManifestProperties(dir, filesInDir);
                    var desc = new ParsedProjectDescriptor(configuredSetting.Name, dir, fallbackParser.ProjectType, props);
                    matchedResults.Add((fallbackParser, new ProjectParseResult(desc)));
                }
            }
            else if (!string.IsNullOrWhiteSpace(configuredSetting.Name))
            {
                for (var i = 0; i < matchedResults.Count; i++)
                {
                    var (p, r) = matchedResults[i];
                    matchedResults[i] = (p, r with { Project = r.Project with { Name = configuredSetting.Name } });
                }
            }
        }
        else if (isNestedGitRepo && matchedResults.Count == 0 && (precomputedCounts?.HasAnyCount == true || dirIndex.EnumerateCachedFilesInSubtree(dir).Any()))
        {
            var repoName = Path.GetFileName(dir.TrimEnd('/', '\\'));
            if (!string.IsNullOrWhiteSpace(repoName))
            {
                var repoSetting = new WorkspaceProjectSetting { Name = repoName, Path = dir };
                var gitParser = ResolveConfiguredProjectParser(dir, repoSetting, dirIndex);
                if (gitParser != null)
                {
                    var props = gitParser.ExtractManifestProperties(dir, filesInDir);
                    var desc = new ParsedProjectDescriptor(repoName, dir, gitParser.ProjectType, props);
                    matchedResults.Add((gitParser, new ProjectParseResult(desc)));
                }
            }
        }

        if (matchedResults.Count == 0)
        {
            return [];
        }

        var matchingParsers = matchedResults.Select(m => m.Parser).ToList();
        ProjectParserPrecedence.RemoveSuperseded(matchingParsers);
        var filtered = matchedResults.Where(m => matchingParsers.Contains(m.Parser)).ToList();
        return RankMultiEcosystemMatches(dir, filtered, precomputedCounts);
    }

    private static IProjectParser? ResolveConfiguredProjectParser(string dir, WorkspaceProjectSetting setting, WorkspaceDirectoryIndex dirIndex)
    {
        if (!string.IsNullOrWhiteSpace(setting.Language))
        {
            var byLang = WorkspaceIndexer._projectParsers.FirstOrDefault(p =>
                !p.IsFallback && p.ProjectType.Equals(setting.Language, StringComparison.OrdinalIgnoreCase));
            if (byLang != null)
            {
                return byLang;
            }
        }

        var phpCount = 0;
        var pyCount = 0;
        var perlCount = 0;
        var tsCount = 0;
        var jsCount = 0;
        var csCount = 0;
        var goCount = 0;

        foreach (var file in dirIndex.EnumerateCachedFilesInSubtree(dir))
        {
            switch (Path.GetExtension(file).ToLowerInvariant())
            {
                case ".php" or ".phtml" or ".inc":
                    phpCount++;
                    break;
                case ".py":
                    pyCount++;
                    break;
                case ".pl" or ".pm":
                    perlCount++;
                    break;
                case ".ts" or ".tsx":
                    tsCount++;
                    break;
                case ".js" or ".jsx":
                    jsCount++;
                    break;
                case ".cs":
                    csCount++;
                    break;
                case ".go":
                    goCount++;
                    break;
            }
        }

        var dominantType = "php";
        var maxCount = phpCount;
        if (pyCount > maxCount) { dominantType = "python"; maxCount = pyCount; }
        if (perlCount > maxCount) { dominantType = "perl"; maxCount = perlCount; }
        if (tsCount > maxCount) { dominantType = "typescript"; maxCount = tsCount; }
        if (jsCount > maxCount) { dominantType = "javascript"; maxCount = jsCount; }
        if (csCount > maxCount) { dominantType = "csharp"; maxCount = csCount; }
        if (goCount > maxCount) { dominantType = "go"; }

        return WorkspaceIndexer._projectParsers.FirstOrDefault(p =>
                   !p.IsFallback && p.ProjectType.Equals(dominantType, StringComparison.OrdinalIgnoreCase))
               ?? WorkspaceIndexer._projectParsers.FirstOrDefault(p => !p.IsFallback);
    }

    private static List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor, List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor)> MergedSecondaries)>
        BuildIndependentEntries(List<(IProjectParser Parser, ProjectParseResult Result)> activeResults)
    {
        var hasPrimaryAbsorbing = activeResults.Any(r =>
            ProjectParserPrecedence.GetTier(r.Parser) == ParserTier.Primary &&
            r.Result.ClaimMode == ParserClaimMode.AbsorbSecondaryInDirectory);

        var entries = new List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor, List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor)> MergedSecondaries)>();
        if (hasPrimaryAbsorbing)
        {
            var secondaryEntries = activeResults
                .Where(r => ProjectParserPrecedence.GetTier(r.Parser) != ParserTier.Primary)
                .Select(r => (r.Parser, r.Result.Project))
                .ToList();

            var primaryEntries = activeResults
                .Where(r => ProjectParserPrecedence.GetTier(r.Parser) == ParserTier.Primary)
                .ToList();

            for (var idx = 0; idx < primaryEntries.Count; idx++)
            {
                var (pParser, pRes) = primaryEntries[idx];
                entries.Add((pParser, pRes.Project, idx == 0 ? secondaryEntries : []));
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
            var restEntries = activeResults.Skip(1).Select(r => (r.Parser, r.Result.Project)).ToList();
            entries.Add((first.Parser, first.Result.Project, restEntries));
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
        var isRoot = string.Equals(
            dir.NormalizeFullPath(trimTrailingSlash: true),
            state.Ctx.AbsoluteWorkspacePath.NormalizeFullPath(trimTrailingSlash: true),
            StringComparison.OrdinalIgnoreCase);

        foreach (var (projectParser, descriptor, mergedSecondaries) in independentEntries)
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

            RecordDirectoryManifestMetadata(dir, descriptor.ManifestMetadata, isRoot, state);
            for (var s = 0; s < mergedSecondaries.Count; s++)
            {
                RecordDirectoryManifestMetadata(dir, mergedSecondaries[s].Descriptor.ManifestMetadata, isRoot, state);
            }

            var depInfo = MergeDescriptorDependencies(descriptor, mergedSecondaries);
            var extPkgNames = depInfo?.ExternalPackages.Select(p => p.Name).ToList();
            var manifestProps = BuildMergedManifestProperties(projectName, descriptor, mergedSecondaries);

            var projectNode = ProjectNodeFactory.Create(
                projectNodeId,
                projectName,
                relativeProjectDir,
                projectParser.ProjectType,
                dir,
                filesInDir,
                extPkgNames,
                manifestProps,
                descriptor.ManifestMetadata);

            state.ProjectsStructureNode.Children.Add(projectNode);
            state.Projects.Add(projectNode);
            RegisterProjectAliases(projectNode, projectName, folderName, independentEntries.Count == 1, state.PackageToProjectMap);
            RegisterAbsorbedAndSubProjects(dir, projectNode, descriptor, state);

            if (depInfo != null)
            {
                AttachDependencies(projectNode, projectNodeId, depInfo, dir, state.Dependencies, state.Packages, projectParser, state.Ctx);
                state.ProjectDepList.Add((projectNode, depInfo));
            }

            await RegisterProducedPackageAliasesAsync(projectNode, projectNodeId, projectParser, dir, descriptor.ProducedPackage, state);
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

    private static ProjectDependencyInfo? MergeDescriptorDependencies(
        ParsedProjectDescriptor primaryDescriptor,
        List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor)> mergedSecondaries)
    {
        var depInfo = primaryDescriptor.Dependencies;
        for (var i = 0; i < mergedSecondaries.Count; i++)
        {
            var subDep = mergedSecondaries[i].Descriptor.Dependencies;
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
        string projectName,
        ParsedProjectDescriptor descriptor,
        List<(IProjectParser Parser, ParsedProjectDescriptor Descriptor)> mergedSecondaries)
    {
        var manifestProps = new Dictionary<string, string>(descriptor.ManifestProperties, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < mergedSecondaries.Count; i++)
        {
            var secProps = mergedSecondaries[i].Descriptor.ManifestProperties;
            if (secProps.TryGetValue(PropPrimaryRole, out var secPrimary) &&
                string.Equals(secPrimary, "App", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(manifestProps.GetValueOrDefault(PropPrimaryRole), "Service", StringComparison.OrdinalIgnoreCase) &&
                !projectName.EndsWith(".API", StringComparison.OrdinalIgnoreCase))
            {
                manifestProps[PropPrimaryRole] = "App";
                manifestProps["role"] = "FrontendApp";
                manifestProps["kind"] = "App";
                manifestProps["framework_type"] = "frontend";
                manifestProps["secondary_roles"] = JsonSerializer.Serialize(new[] { "Service" });
                manifestProps["all_roles"] = JsonSerializer.Serialize(new[] { "App", "Service" });
            }
        }
        return manifestProps;
    }

    internal static void RegisterPackageAlias(
        Dictionary<string, ProjectNode> packageToProjectMap,
        string key,
        ProjectNode candidate)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!packageToProjectMap.TryGetValue(key, out var existing))
        {
            packageToProjectMap[key] = candidate;
            return;
        }

        if (string.Equals(existing.Id, candidate.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Never let a test project overwrite a non-test project (e.g. duplicated repos under test/)
        if (!existing.IsTest && candidate.IsTest)
        {
            return;
        }

        if (existing.IsTest && !candidate.IsTest)
        {
            packageToProjectMap[key] = candidate;
            return;
        }

        // When two non-test projects claim the same package alias, prefer an explicit library
        if (!existing.IsLibrary && candidate.IsLibrary)
        {
            packageToProjectMap[key] = candidate;
        }
    }

    internal static void RegisterEcosystemPackageAlias(
        Dictionary<string, ProjectNode> packageToProjectMap,
        string? rawEcosystem,
        string key,
        ProjectNode candidate,
        bool isExplicitPackageCoordinate = false)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var trimmed = key.Trim();
        if (!isExplicitPackageCoordinate && ProjectNodeFactory.IsGenericPackageShortName(trimmed))
        {
            return;
        }

        var eco = ProjectNodeFactory.NormalizePackageEcosystem(rawEcosystem, candidate.ProjectType);
        RegisterPackageAlias(packageToProjectMap, $"{eco}:{trimmed}", candidate);

        if (eco == "pypi" && (trimmed.Contains('_') || trimmed.Contains('-')))
        {
            var pep503 = trimmed.Replace('_', '-');
            if (!ProjectNodeFactory.IsGenericPackageShortName(pep503))
            {
                RegisterPackageAlias(packageToProjectMap, $"pypi:{pep503}", candidate);
            }
        }
    }

    private static void RegisterProjectAliases(
        ProjectNode projectNode,
        string projectName,
        string folderName,
        bool isSingleEntryInDir,
        Dictionary<string, ProjectNode> packageToProjectMap)
    {
        RegisterEcosystemPackageAlias(packageToProjectMap, null, projectName, projectNode);
        var normName = WorkspaceConventions.NormalizeServiceName(projectName);
        if (!string.IsNullOrEmpty(normName))
        {
            RegisterEcosystemPackageAlias(packageToProjectMap, null, normName, projectNode);
            WorkspaceConventions.RegisterServiceAlias(projectName, normName);
        }
        if (!string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase) && isSingleEntryInDir)
        {
            RegisterEcosystemPackageAlias(packageToProjectMap, null, folderName, projectNode);
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
        ProducedPackageInfo? preParsedProducedPackage,
        DirectoryScanState state)
    {
        var (prodPkg, prodEco, explicitCoord) = await LinkProducedPackageAsync(
            projectNode,
            projectNodeId,
            projectParser,
            dir,
            preParsedProducedPackage,
            state.Ctx);
        if (!string.IsNullOrEmpty(prodPkg))
        {
            RegisterEcosystemPackageAlias(state.PackageToProjectMap, prodEco, prodPkg, projectNode, isExplicitPackageCoordinate: explicitCoord);
        }
    }
}
