using System.Diagnostics;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer1PhysicalParser
{
    private const int ProgressFileStep = 1000;
    private const string Layer1ProgressTitle = "Layer 1: Physical";
    private const string Layer1Stage = "Layer1_Physical";
    private const string IssueCategoryIoError = "io_error";
    private const string SeverityWarning = "warning";
    private const string IssueTasksJsonReadError = "TasksJsonReadError";
    private const string IssueManifestReadError = "ManifestReadError";
    private const string DefaultWsId = "ws0";
    private const string FilesStructureName = "FilesStructure";
    private const string DotGitName = ".git";
    private const string DotVscodeName = ".vscode";
    private const string IsoDateFmt = "o";

    private sealed record CandidateFileItem(
        int Index,
        DirectoryFileEntry Entry,
        string RelativeFile,
        string AbsoluteFilePath,
        string FileId,
        IOntologyNode ParentNode,
        FolderNode? OwningFolderNode,
        bool IsManifest,
        bool IsSupportedSourceOrConfig);

    private sealed class WalkCollectorState(Layer1Context ctx, WorkspaceNode workspaceNode, GitIgnoreTrieMatcher gitTrie)
    {
        public Layer1Context Ctx { get; } = ctx;
        public WorkspaceNode WorkspaceNode { get; } = workspaceNode;
        public GitIgnoreTrieMatcher GitTrie { get; } = gitTrie;
        public List<CandidateFileItem> OwnedCandidates { get; } = [];
        public List<CandidateFileItem> UnownedFallbackCandidates { get; } = [];
        public List<string> ManifestDirectories { get; } = [];
        public List<string> FallbackCandidateDirectories { get; } = [];
        public List<string> VscodeTaskDirs { get; } = [];
        public HashSet<FolderNode> KeptFolders { get; } = new(ReferenceEqualityComparer.Instance);
        public List<(FolderNode Folder, IOntologyNode Parent)> AllVisitedFolders { get; } = [];
        public int DiscoveredRawFiles;
        public int DiscoveredFolderCount;
        public int NextDiscoveryReportThreshold = ProgressFileStep;
        public bool HasAnyOwnedDirectory;
    }

    public async Task<Layer1Result> ParseAsync(Layer1Context ctx)
    {
        using var dirIndexScope = WorkspaceDirectoryIndex.EnterScope(ctx.DirectoryIndex);
        var sw = Stopwatch.StartNew();
        ctx.Layer1Log.ScanStarted();

        var wsId = await InitializeWorkspaceIdAsync(ctx);
        var workspaceName = await ResolveWorkspaceNameAsync(ctx);
        var hostPath = PathTools.NormalizeToHostPath(ctx.HostWorkspacePath);
        var workspaceNode = new WorkspaceNode(wsId, workspaceName, hostPath)
        {
            IndexedAt = DateTime.UtcNow.ToString(IsoDateFmt)
        };

        var filesNodeId = $"{wsId}:{OntologyConstants.IdPrefixes.FilesStructure}";
        var filesStructureNode = new FilesStructureNode(filesNodeId, FilesStructureName, hostPath);
        workspaceNode.Children.Add(filesStructureNode);

        AttachRootGitSettingsIfPresent(wsId, filesStructureNode, ctx);

        var folders = new List<FolderNode>();
        var gitTrie = new GitIgnoreTrieMatcher(ctx.AbsoluteWorkspacePath, ctx.FileFilter);
        ctx.DirectoryIndex.RegisterConfiguredProjects(ctx.AbsoluteWorkspacePath, WorkspaceSettings.Load(ctx.AbsoluteWorkspacePath).Projects);

        var walkState = new WalkCollectorState(ctx, workspaceNode, gitTrie);
        ScopedDirectoryTreeNode? rootTreeNode;

        if (!ctx.IsSubtreeScan)
        {
            rootTreeNode = WalkScopedDirectory(
                ctx.AbsoluteWorkspacePath,
                filesStructureNode,
                gitTrie.RootScope,
                tentativeInheritedParsers: null,
                walkState);
            if (rootTreeNode != null)
            {
                FinalizeScopesAndCollectFiles(rootTreeNode, inheritedParsers: null, walkState);
            }
        }
        else
        {
            var initialParsers = ctx.FileFilter.ResolveAncestorProjectParsers(ctx.ScanPath, ctx.AbsoluteWorkspacePath);
            var currentParent = BuildSubtreeIntermediateFolders(wsId, filesStructureNode, folders, ctx);
            rootTreeNode = WalkScopedDirectory(
                ctx.ScanPath,
                currentParent,
                gitTrie.RootScope,
                initialParsers,
                walkState);
            if (rootTreeNode != null)
            {
                FinalizeScopesAndCollectFiles(rootTreeNode, initialParsers, walkState);
            }
        }

        foreach (var taskDir in walkState.VscodeTaskDirs)
        {
            await TryStreamVscodeTasksAsync(taskDir, ctx);
        }

        var finalCandidates = BuildFinalCandidateList(walkState);
        var files = MaterializeMetadataFilesAndRecordManifests(finalCandidates, walkState.KeptFolders, ctx);
        FinalizeNonEmptyFolders(walkState, folders);

        var l1Result = new Layer1Result(
            workspaceNode,
            filesStructureNode,
            files,
            folders,
            ctx.DiscoveredManifestFiles,
            ctx.DiscoveredManifestPaths,
            ctx.DiscoveredVsCodeFolderOpenTasks);
        if (rootTreeNode != null)
        {
            var orderedFallbackDirs = walkState.FallbackCandidateDirectories
                .OrderBy(d => d.Replace('\\', '/').Trim('/').Count(c => c == '/'))
                .ThenBy(d => d.Replace('\\', '/').Length)
                .ThenBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
            UnifiedScanPlanStore.Attach(l1Result, new UnifiedWorkspaceScanPlan(
                [rootTreeNode],
                walkState.ManifestDirectories,
                orderedFallbackDirs));
        }

        ctx.Layer1Log.ScanCompleted(files.Count, folders.Count, sw.ElapsedMilliseconds);
        return l1Result;
    }

    private static async Task<string> InitializeWorkspaceIdAsync(Layer1Context ctx)
    {
        var wsId = ctx.PersistToGraphDb && ctx.DbClient != null
            ? await ctx.DbClient.GetOrCreateWorkspaceIdAsync(ctx.HostWorkspacePath)
            : DefaultWsId;
        ctx.WorkspaceId = wsId;
        ctx.CancellationToken.ThrowIfCancellationRequested();

        if (ctx.PersistToGraphDb && ctx.DbClient != null)
        {
            await ctx.DbClient.SaveEmptyWorkspaceNodeAsync(wsId, ctx.HostWorkspacePath);
            ctx.CancellationToken.ThrowIfCancellationRequested();
        }
        return wsId;
    }

    private static async Task<string> ResolveWorkspaceNameAsync(Layer1Context ctx)
    {
        var normalizedHostPath = ctx.HostWorkspacePath.NormalizePath();
        var folderName = Path.GetFileName(normalizedHostPath);
        if (string.IsNullOrEmpty(folderName)) folderName = normalizedHostPath;

        var workspaceName = folderName;
        if (!ctx.Clear && ctx.PersistToGraphDb && ctx.DbClient != null)
        {
            try
            {
                var existingWs = await ctx.DbClient.AsRepository().GetWorkspaceAsync(ctx.CancellationToken);
                if (!string.IsNullOrWhiteSpace(existingWs?.Name))
                {
                    workspaceName = existingWs.Name;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Layer1Log.FileReadFailed(normalizedHostPath, ex);
            }
        }
        return workspaceName;
    }

    private static void AttachRootGitSettingsIfPresent(string wsId, FilesStructureNode filesStructureNode, Layer1Context ctx)
    {
        if (ctx.LightweightAstScanMode) return;
        var rootRepoDir = GitSettingsParser.FindRepoRoot(ctx.AbsoluteWorkspacePath);
        if (rootRepoDir == null) return;
        var gitSettingsNode = GitSettingsParser.Parse(wsId, ctx.AbsoluteWorkspacePath, rootRepoDir);
        if (gitSettingsNode != null)
        {
            filesStructureNode.Children.Add(gitSettingsNode);
            ctx.RegisterGitRepository(rootRepoDir, gitSettingsNode);
        }
    }

    private static ScopedDirectoryTreeNode? WalkScopedDirectory(
        string currentDir,
        IOntologyNode parentNode,
        GitIgnoreScopeChain parentGitScope,
        IReadOnlyList<IProjectParser>? tentativeInheritedParsers,
        WalkCollectorState state)
    {
        var ctx = state.Ctx;
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var isRoot = string.Equals(currentDir, ctx.AbsoluteWorkspacePath, StringComparison.OrdinalIgnoreCase);
        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;
        var relativeDir = isRoot ? string.Empty : currentDir.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        if (relativeDir == ".") relativeDir = string.Empty;

        if (ctx.IncrementalRelevantDirectories != null && !string.IsNullOrEmpty(relativeDir) &&
            !ctx.IncrementalRelevantDirectories.Contains(relativeDir))
        {
            return null;
        }

        if (!isRoot && ctx.FileFilter.IsExcludedDirectory(dirName, currentDir, tentativeInheritedParsers))
        {
            ctx.Layer1Log.SkippingExcludedDirectory(relativeDir);
            return null;
        }

        var keptForNegation = false;
        if (!string.IsNullOrEmpty(relativeDir) &&
            state.GitTrie.IsIgnoredDirectoryInScope(parentGitScope, relativeDir, out keptForNegation))
        {
            ctx.Layer1Log.SkippingGitIgnoredDirectory(relativeDir);
            return null;
        }

        SinglePassDirectoryReader.ReadEntries(
            currentDir,
            out var dirFiles,
            out var dirFilePaths,
            out var subDirPaths,
            out var hasGitPointer,
            out var hasGitSubDir,
            out var hasVscodeSubDir,
            out var gitIgnorePath,
            out var ceIgnorePath);
        if (!isRoot && hasGitPointer && GitSettingsParser.IsGitWorktreeDirectory(currentDir))
        {
            ctx.Layer1Log.SkippingExcludedDirectory(relativeDir);
            return null;
        }

        var isGitRepoRoot = hasGitPointer || hasGitSubDir;
        if (isGitRepoRoot) ctx.DirectoryIndex.RegisterGitRepositoryRoot(currentDir);

        ctx.DirectoryIndex.SetDirectoryFiles(currentDir, dirFilePaths);

        var effectiveInherited = !isRoot && isGitRepoRoot ? null : tentativeInheritedParsers;
        var hasManifestInDir = HasAnyManifestFile(dirFiles, ctx.FileFilter);
        var tentativeDirect = WorkspacePathFilterHelpers.MatchDirectProjectParsersByPaths(ctx.FileFilter, currentDir, dirFilePaths, allowFallback: effectiveInherited is not { Count: > 0 });
        var tentativeActive = tentativeDirect
                              ?? effectiveInherited
                              ?? WorkspacePathFilterHelpers.MatchFileSupportedProjectParsersByPaths(ctx.FileFilter, dirFilePaths, includeFallback: false)
                              ?? WorkspacePathFilterHelpers.MatchFileSupportedProjectParsersByPaths(ctx.FileFilter, dirFilePaths, includeFallback: true);

        NotifyParsersAndCollectIgnorePatterns(currentDir, relativeDir, dirFilePaths, tentativeActive, state);
        var activeGitScope = state.GitTrie.CompileChildScope(parentGitScope, relativeDir, gitIgnorePath, ceIgnorePath);
        var (folderNode, currentParentNode) = CreateFolderNodeIfNonRoot(currentDir, dirName, isRoot, isGitRepoRoot, parentNode, state);

        if (hasVscodeSubDir && File.Exists(Path.Combine(currentDir, DotVscodeName, OntologyConstants.ManifestFields.TasksJsonFileName)))
        {
            state.VscodeTaskDirs.Add(currentDir);
        }
        if (hasManifestInDir) state.ManifestDirectories.Add(currentDir);

        var hasCandidateTrigger = isGitRepoRoot || hasManifestInDir || tentativeDirect is { Count: > 0 } ||
                                  (ctx.DirectoryIndex.HasAnyConfiguredProjects && ctx.DirectoryIndex.TryGetConfiguredProject(currentDir, out _)) ||
                                  ProjectTriggerDetector.HasCandidateProjectTrigger(currentDir, dirFilePaths);
        var treeNode = new ScopedDirectoryTreeNode(
            currentDir, currentDir.NormalizeFullPath(trimTrailingSlash: true), relativeDir, isRoot,
            dirFiles, dirFilePaths, currentParentNode, folderNode, activeGitScope, keptForNegation, hasManifestInDir, hasCandidateTrigger)
        {
            IsGitRepoRoot = isGitRepoRoot
        };

        var totalLangCounts = SubtreeLanguageCounts.FromDirectoryFiles(dirFiles, out var hasAnySource);
        ReportDiscoveryProgressIfNeeded(dirFiles.Length, state);

        var descendantGitRepos = 0;
        var nextTentativeInherited = ExtractNonFallbackParsers(tentativeActive) ?? effectiveInherited;
        for (var i = 0; i < subDirPaths.Length; i++)
        {
            var childNode = WalkScopedDirectory(subDirPaths[i], currentParentNode, activeGitScope, nextTentativeInherited, state);
            if (childNode == null) continue;
            treeNode.Children.Add(childNode);
            totalLangCounts = totalLangCounts.Add(childNode.LanguageCounts);
            hasAnySource |= childNode.HasSourceFiles;
            descendantGitRepos += childNode.DescendantGitRepoCount + (childNode.IsGitRepoRoot ? 1 : 0);
            if (childNode.SubtreeHasCandidateManifest)
            {
                treeNode.HasCandidateManifest = true;
                treeNode.SubtreeHasCandidateManifest = true;
            }
        }

        treeNode.LanguageCounts = totalLangCounts;
        treeNode.HasSourceFiles = hasAnySource;
        treeNode.DescendantGitRepoCount = descendantGitRepos;
        return treeNode;
    }

    private static (FolderNode? Folder, IOntologyNode CurrentParent) CreateFolderNodeIfNonRoot(
        string currentDir,
        string dirName,
        bool isRoot,
        bool isGitRepoRoot,
        IOntologyNode parentNode,
        WalkCollectorState state)
    {
        if (isRoot) return (null, parentNode);

        var absoluteFolderPath = currentDir.NormalizeFullPath();
        var folderId = $"{state.Ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Folder}:{absoluteFolderPath}";
        var folderNode = new FolderNode(folderId, dirName, absoluteFolderPath);
        state.AllVisitedFolders.Add((folderNode, parentNode));
        if (isGitRepoRoot)
        {
            AttachNestedGitSettingsIfPresent(currentDir, folderNode, state.Ctx);
        }
        return (folderNode, folderNode);
    }

    private static bool FinalizeScopesAndCollectFiles(
        ScopedDirectoryTreeNode node,
        IReadOnlyList<IProjectParser>? inheritedParsers,
        WalkCollectorState state)
    {
        var ctx = state.Ctx;
        var effectiveInherited = !node.IsRoot && node.IsGitRepoRoot ? null : inheritedParsers;
        var wrapsChildProjects = node.DescendantGitRepoCount > 0 ||
                                 ctx.DirectoryIndex.HasChildConfiguredProjects(node.DirectoryPath);

        var (directNonFallback, localFallback) = ResolveFinalDirectoryParsers(node, effectiveInherited, wrapsChildProjects, ctx);

        IReadOnlyList<IProjectParser>? activeFileParsers;
        if (wrapsChildProjects && directNonFallback is not { Count: > 0 } && effectiveInherited is not { Count: > 0 })
        {
            activeFileParsers = null;
        }
        else
        {
            activeFileParsers = directNonFallback
                                ?? effectiveInherited
                                ?? localFallback
                                ?? WorkspacePathFilterHelpers.MatchFileSupportedProjectParsersByPaths(ctx.FileFilter, node.FilesInDir, includeFallback: false)
                                ?? WorkspacePathFilterHelpers.MatchFileSupportedProjectParsersByPaths(ctx.FileFilter, node.FilesInDir, includeFallback: true);
        }

        if (activeFileParsers is { Count: > 0 })
        {
            state.HasAnyOwnedDirectory = true;
            if (effectiveInherited is not { Count: > 0 } && directNonFallback is not { Count: > 0 } && localFallback is { Count: > 0 })
            {
                state.FallbackCandidateDirectories.Add(node.DirectoryPath);
            }
        }

        CollectScopedDirectoryFiles(
            node.IsRoot,
            node.KeptForNegation,
            node.DirFiles,
            node.FileParentNode,
            node.FolderNode,
            node.GitScope,
            activeFileParsers,
            state);

        if (directNonFallback is { Count: > 0 } && !node.HasCandidateManifest)
        {
            node.HasCandidateManifest = true;
            node.SubtreeHasCandidateManifest = true;
        }

        var subtreeHasCandidate = node.SubtreeHasCandidateManifest;
        var nextInheritedParsers = ExtractNonFallbackParsers(activeFileParsers) ?? effectiveInherited;
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (FinalizeScopesAndCollectFiles(node.Children[i], nextInheritedParsers, state))
            {
                subtreeHasCandidate = true;
            }
        }

        if (subtreeHasCandidate && !node.SubtreeHasCandidateManifest)
        {
            node.HasCandidateManifest = true;
            node.SubtreeHasCandidateManifest = true;
        }

        if (node.FolderNode != null && (directNonFallback is { Count: > 0 } || node.FolderNode.Children.Count > 0))
        {
            state.KeptFolders.Add(node.FolderNode);
        }

        return node.SubtreeHasCandidateManifest;
    }

    private static IReadOnlyList<IProjectParser>? ExtractNonFallbackParsers(IReadOnlyList<IProjectParser>? parsers)
    {
        if (parsers is not { Count: > 0 }) return null;

        var allNonFallback = true;
        var anyNonFallback = false;
        for (var i = 0; i < parsers.Count; i++)
        {
            if (parsers[i].IsFallback) allNonFallback = false;
            else anyNonFallback = true;
        }

        if (!anyNonFallback) return null;
        if (allNonFallback) return parsers;

        var filtered = new List<IProjectParser>(parsers.Count);
        for (var i = 0; i < parsers.Count; i++)
        {
            if (!parsers[i].IsFallback) filtered.Add(parsers[i]);
        }
        return filtered;
    }

    private static bool HasAnyManifestFile(DirectoryFileEntry[] dirFiles, WorkspaceFileFilter filter)
    {
        for (var i = 0; i < dirFiles.Length; i++)
        {
            if (filter.IsManifestFile(dirFiles[i].Name)) return true;
        }
        return false;
    }

    private static (IReadOnlyList<IProjectParser>? DirectNonFallback, IReadOnlyList<IProjectParser>? LocalFallback)
        ResolveFinalDirectoryParsers(
            ScopedDirectoryTreeNode node,
            IReadOnlyList<IProjectParser>? inheritedProjectParsers,
            bool wrapsChildProjects,
            Layer1Context ctx)
    {
        var hasConfigured = ctx.DirectoryIndex.HasAnyConfiguredProjects &&
                            ctx.DirectoryIndex.TryGetConfiguredProject(node.DirectoryPath, out _);

        IReadOnlyList<IProjectParser>? directNonFallback = null;
        var canProbeDirectNonFallback = (!wrapsChildProjects || node.HasManifestInDir || hasConfigured) &&
                                        (inheritedProjectParsers is not { Count: > 0 } ||
                                         node.HasManifestInDir ||
                                         node.IsGitRepoRoot ||
                                         hasConfigured ||
                                         ProjectTriggerDetector.IsPotentialManifestlessBinarySubfolder(node.DirectoryPath));

        if (canProbeDirectNonFallback)
        {
            directNonFallback = WorkspacePathFilterHelpers.MatchDirectProjectParsersByPaths(
                ctx.FileFilter,
                node.DirectoryPath,
                node.FilesInDir,
                allowFallback: false);
            if (directNonFallback is not { Count: > 0 } &&
                hasConfigured &&
                ctx.DirectoryIndex.TryGetConfiguredProject(node.DirectoryPath, out var cfgProj) &&
                cfgProj != null)
            {
                directNonFallback = ctx.FileFilter.ConnectedProjectParsers
                    .Where(p => !p.IsFallback &&
                                (string.IsNullOrEmpty(cfgProj.Language) ||
                                 p.ProjectType.Equals(cfgProj.Language, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        IReadOnlyList<IProjectParser>? localFallback = null;
        if (!wrapsChildProjects &&
            directNonFallback is not { Count: > 0 } &&
            inheritedProjectParsers is not { Count: > 0 } &&
            node.FilesInDir.Length > 0)
        {
            localFallback = WorkspacePathFilterHelpers.MatchDirectProjectParsersByPaths(
                ctx.FileFilter,
                node.DirectoryPath,
                node.FilesInDir,
                allowFallback: true);
            if (localFallback is not { Count: > 0 } &&
                hasConfigured &&
                ctx.DirectoryIndex.TryGetConfiguredProject(node.DirectoryPath, out var fbProj) &&
                fbProj != null)
            {
                localFallback = ctx.FileFilter.ConnectedProjectParsers
                    .Where(p => string.IsNullOrEmpty(fbProj.Language) ||
                                p.ProjectType.Equals(fbProj.Language, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        return (directNonFallback is { Count: > 0 } ? directNonFallback : null,
                localFallback is { Count: > 0 } ? localFallback : null);
    }

    private static void NotifyParsersAndCollectIgnorePatterns(
        string currentDir,
        string relativeDir,
        string[] dirFilePaths,
        IReadOnlyList<IProjectParser>? activeFileParsers,
        WalkCollectorState state)
    {
        if (dirFilePaths.Length == 0) return;

        var parsersToNotify = activeFileParsers is { Count: > 0 }
            ? activeFileParsers
            : state.Ctx.FileFilter.ConnectedProjectParsers;

        var scratch = new List<string>();
        for (var i = 0; i < parsersToNotify.Count; i++)
        {
            scratch.Clear();
            parsersToNotify[i].CollectDirectoryIgnorePatterns(
                currentDir,
                relativeDir,
                state.Ctx.AbsoluteWorkspacePath,
                scratch,
                state.Ctx.Layer1Log.DirectoryScanHookMessage);
            for (var p = 0; p < scratch.Count; p++)
            {
                state.GitTrie.AddPattern(scratch[p]);
            }
            parsersToNotify[i].InspectDirectoryFilePaths(currentDir, dirFilePaths, state.WorkspaceNode);
        }
    }

    private static void CollectScopedDirectoryFiles(
        bool isRoot,
        bool keptForNegation,
        DirectoryFileEntry[] dirFiles,
        IOntologyNode fileParentNode,
        FolderNode? owningFolder,
        GitIgnoreScopeChain gitScope,
        IReadOnlyList<IProjectParser>? activeFileParsers,
        WalkCollectorState state)
    {
        if (dirFiles.Length == 0) return;

        var ctx = state.Ctx;
        var skipDirOnlyRules = !keptForNegation;
        var isOwnedScope = activeFileParsers is { Count: > 0 };

        for (var i = 0; i < dirFiles.Length; i++)
        {
            var entry = dirFiles[i];
            var file = entry.FullPath;
            var relativeFile = file.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);

            if (state.GitTrie.IsIgnoredFileInScope(gitScope, relativeFile, skipDirOnlyRules)) continue;
            if (ctx.CustomFileExclusionPredicate?.Invoke(entry.Name, relativeFile) == true) continue;

            var fileName = entry.Name;
            var isManifest = ctx.FileFilter.IsManifestFile(fileName);
            var isInIncrementalScope = ctx.IncrementalChangedFiles == null || ctx.IncrementalChangedFiles.Contains(relativeFile);
            if (!isManifest && !isInIncrementalScope) continue;

            var isStandaloneContract = false;
            bool isSupportedSourceOrConfig;
            if (!isInIncrementalScope)
            {
                isSupportedSourceOrConfig = false;
            }
            else if (isOwnedScope)
            {
                isSupportedSourceOrConfig = ctx.FileFilter.IsSupportedSourceOrConfigFile(relativeFile, activeFileParsers);
            }
            else
            {
                isStandaloneContract = WorkspaceFileFilter.IsStandaloneContractOrEnvFile(relativeFile, isRoot);
                isSupportedSourceOrConfig = isStandaloneContract || ctx.FileFilter.IsSupportedSourceOrConfigFile(relativeFile, null);
            }

            if (!isSupportedSourceOrConfig && !isManifest) continue;

            if (isSupportedSourceOrConfig && WorkspacePathFilterHelpers.ShouldSkipFileMetadata(ctx.FileFilter, fileName, entry.SizeBytes))
            {
                if (!isManifest) continue;
                isSupportedSourceOrConfig = false;
            }

            var absoluteFilePath = file.NormalizeSlashes();
            var fileId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{relativeFile}";
            var item = new CandidateFileItem(
                0,
                entry,
                relativeFile,
                absoluteFilePath,
                fileId,
                fileParentNode,
                owningFolder,
                isManifest,
                isSupportedSourceOrConfig);

            if (isOwnedScope || isManifest || isStandaloneContract)
            {
                state.OwnedCandidates.Add(item);
            }
            else
            {
                state.UnownedFallbackCandidates.Add(item);
            }
        }
    }

    private static List<CandidateFileItem> BuildFinalCandidateList(WalkCollectorState state)
    {
        var sourceList = state.HasAnyOwnedDirectory || state.UnownedFallbackCandidates.Count == 0
            ? state.OwnedCandidates
            : state.OwnedCandidates.Concat(state.UnownedFallbackCandidates).ToList();

        var result = new List<CandidateFileItem>(sourceList.Count);
        for (var i = 0; i < sourceList.Count; i++)
        {
            result.Add(sourceList[i] with { Index = i });
        }
        return result;
    }

    private static List<FileNode> MaterializeMetadataFilesAndRecordManifests(
        List<CandidateFileItem> candidates,
        HashSet<FolderNode> keptFolders,
        Layer1Context ctx)
    {
        var files = new List<FileNode>(candidates.Count);
        if (candidates.Count == 0) return files;

        for (var i = 0; i < candidates.Count; i++)
        {
            var item = candidates[i];
            if (item.IsManifest)
            {
                if (!File.Exists(item.AbsoluteFilePath))
                {
                    ctx.Layer1Log.SkippingBrokenSymlink(item.RelativeFile);
                    ctx.DirectoryIndex.RemoveDirectoryFileEntry(item.AbsoluteFilePath);
                    continue;
                }

                ctx.DiscoveredManifestPaths[item.RelativeFile] = (
                    item.AbsoluteFilePath,
                    item.Entry.LastModifiedUtc,
                    item.Entry.SizeBytes);
            }

            if (!item.IsSupportedSourceOrConfig) continue;

            var fileNode = new FileNode(
                item.FileId,
                item.Entry.Name,
                item.RelativeFile,
                item.AbsoluteFilePath,
                false,
                null,
                null,
                null,
                item.Entry.SizeBytes)
            {
                LastModifiedUtc = item.Entry.LastModifiedUtc,
                ArtifactRole = ctx.FileFilter.ClassifyFileArtifactRole(item.RelativeFile, item.Entry.Name)
            };

            item.ParentNode.Children.Add(fileNode);
            if (item.OwningFolderNode is { } folder)
            {
                keptFolders.Add(folder);
            }
            files.Add(fileNode);
        }

        return files;
    }

    private static void FinalizeNonEmptyFolders(WalkCollectorState state, List<FolderNode> folders)
    {
        if (state.AllVisitedFolders.Count == 0) return;

        for (var i = state.AllVisitedFolders.Count - 1; i >= 0; i--)
        {
            var (folder, parent) = state.AllVisitedFolders[i];
            if ((state.KeptFolders.Contains(folder) || folder.Children.Count > 0) && parent is FolderNode parentFolder)
            {
                state.KeptFolders.Add(parentFolder);
            }
        }

        for (var i = 0; i < state.AllVisitedFolders.Count; i++)
        {
            var (folder, parent) = state.AllVisitedFolders[i];
            if (state.KeptFolders.Contains(folder) || folder.Children.Count > 0)
            {
                parent.Children.Add(folder);
                folders.Add(folder);
            }
        }
    }

    private static void ReportDiscoveryProgressIfNeeded(int addedFiles, WalkCollectorState state)
    {
        state.DiscoveredFolderCount++;
        state.DiscoveredRawFiles += addedFiles;
        if (state.DiscoveredRawFiles < state.NextDiscoveryReportThreshold) return;

        state.NextDiscoveryReportThreshold = ((state.DiscoveredRawFiles / ProgressFileStep) + 1) * ProgressFileStep;
        state.Ctx.Layer1Log.DiscoveringProgress(state.DiscoveredRawFiles, state.DiscoveredFolderCount);
        state.Ctx.ReportProgress(
            2,
            Layer1ProgressTitle,
            $"Discovering directory tree: {state.DiscoveredRawFiles:N0} files across {state.DiscoveredFolderCount:N0} folders...",
            state.DiscoveredRawFiles,
            state.DiscoveredRawFiles);
    }

    private static IOntologyNode BuildSubtreeIntermediateFolders(
        string wsId,
        FilesStructureNode filesStructureNode,
        List<FolderNode> folders,
        Layer1Context ctx)
    {
        var relativeSubtree = ctx.ScanPath.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        var segments = relativeSubtree.Split('/', StringSplitOptions.RemoveEmptyEntries);

        IOntologyNode currentParent = filesStructureNode;
        var currentPath = ctx.AbsoluteWorkspacePath;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            currentPath = Path.Combine(currentPath, segments[i]).NormalizeSlashes();
            var folderId = $"{wsId}:{OntologyConstants.IdPrefixes.Folder}:{currentPath}";
            var intermediateFolder = new FolderNode(folderId, segments[i], currentPath);

            currentParent.Children.Add(intermediateFolder);
            folders.Add(intermediateFolder);
            currentParent = intermediateFolder;
        }

        return currentParent;
    }

    private static void AttachNestedGitSettingsIfPresent(string currentDir, FolderNode folderNode, Layer1Context ctx)
    {
        var gitPath = Path.Combine(currentDir, DotGitName);
        if (!Directory.Exists(gitPath) && !File.Exists(gitPath)) return;

        var subGitNode = GitSettingsParser.Parse(ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, currentDir);
        if (subGitNode != null)
        {
            folderNode.Children.Add(subGitNode);
            ctx.RegisterGitRepository(currentDir, subGitNode);
        }
    }

    private static async Task TryStreamVscodeTasksAsync(string currentDir, Layer1Context ctx)
    {
        var vscodeTasksPath = Path.Combine(currentDir, DotVscodeName, OntologyConstants.ManifestFields.TasksJsonFileName);
        if (!File.Exists(vscodeTasksPath)) return;

        var relTasks = vscodeTasksPath.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        try
        {
            var absTasks = vscodeTasksPath.NormalizeFullPath();
            var tasksInfo = new FileInfo(vscodeTasksPath);
            var streamedTasks = await FileStreamReader.StreamAndAnalyzeFileAsync(
                absTasks,
                relTasks,
                OntologyConstants.ManifestFields.TasksJsonFileName,
                tasksInfo.Length,
                isManifest: true,
                isSupportedSourceOrConfig: false,
                ctx.FileFilter,
                ctx.CancellationToken,
                computeHash: ctx.PersistToGraphDb);
            if (streamedTasks.ContentHash != null)
            {
                ctx.DiscoveredManifestFiles[relTasks] = (absTasks, streamedTasks.ContentHash, tasksInfo.LastWriteTimeUtc, tasksInfo.Length);
            }
            else
            {
                ctx.DiscoveredManifestPaths[relTasks] = (absTasks, tasksInfo.LastWriteTimeUtc, tasksInfo.Length);
            }

            if (string.Equals(
                    currentDir.NormalizeFullPath(trimTrailingSlash: true),
                    ctx.AbsoluteWorkspacePath.NormalizeFullPath(trimTrailingSlash: true),
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(streamedTasks.DecodedText))
            {
                ExtractVsCodeFolderOpenTasks(streamedTasks.DecodedText, relTasks, ctx);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(relTasks, ex);
            ctx.RecordScanIssue(Layer1Stage, IssueCategoryIoError, relTasks, ex.Message, name: IssueTasksJsonReadError, severity: SeverityWarning, exception: ex);
        }
    }

    private static void ExtractVsCodeFolderOpenTasks(string tasksText, string relTasks, Layer1Context ctx)
    {
        const string folderOpenLiteral = "folderOpen";
        try
        {
            var tRoot = CypherValue.TryParseJsonText(tasksText);
            if (!tRoot.TryGetProperty(OntologyConstants.ManifestFields.Tasks, out var tasksArr) ||
                tasksArr.Kind != CypherValueKind.Array)
            {
                return;
            }

            foreach (var task in tasksArr.AsArray())
            {
                if (task.Kind != CypherValueKind.Object) continue;
                var isFolderOpen = task.TryGetProperty(OntologyConstants.ManifestFields.RunOptions, out var ro) &&
                                   ro.Kind == CypherValueKind.Object &&
                                   ro.TryGetProperty(OntologyConstants.ManifestFields.RunOn, out var runOn) &&
                                   string.Equals(runOn.AsNullableString(), folderOpenLiteral, StringComparison.OrdinalIgnoreCase);
                if (!isFolderOpen) continue;

                var label = task.TryGetProperty(OntologyConstants.ManifestFields.Label, out var lbl)
                    ? lbl.AsString(folderOpenLiteral)
                    : folderOpenLiteral;
                var cmd = task.TryGetProperty(OntologyConstants.ManifestFields.Command, out var c)
                    ? c.AsString()
                    : string.Empty;
                ctx.DiscoveredVsCodeFolderOpenTasks.Add((label, cmd));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(relTasks, ex);
        }
    }

    public static HashSet<string> ExcludedDirectoryNames => new(WorkspaceFileFilter.ExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
    public static bool IsExcludedDirectory(string dirName) => WorkspaceFileFilter.Default.IsExcludedDirectory(dirName);
    public static bool IsSupportedFile(string fileName) => WorkspaceFileFilter.Default.IsSupportedSourceOrConfigFile(fileName);
    public static bool ShouldSkipFileName(string fileName) => WorkspaceFileFilter.Default.ShouldSkipFileName(fileName);
    public static bool ShouldSkipFile(FileInfo fileInfo) => WorkspaceFileFilter.Default.ShouldSkipFile(fileInfo);
    public static bool IsMinifiedContent(string filePath) => WorkspaceFileFilter.IsMinifiedContent(filePath);
}
