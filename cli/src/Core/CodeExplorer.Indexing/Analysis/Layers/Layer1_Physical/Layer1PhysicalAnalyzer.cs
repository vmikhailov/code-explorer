using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

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
    private const string IssueFileReadError = "FileReadError";
    private const string DefaultWsId = "ws0";
    private const string FilesStructureName = "FilesStructure";
    private const string DotGitName = ".git";
    private const string DotGitIgnoreName = ".gitignore";
    private const string DotCeIgnoreName = ".codeexplorerignore";
    private const string DotVscodeName = ".vscode";
    private const string IsoDateFmt = "o";

    private sealed record CandidateFileItem(
        int Index,
        FileInfo FileInfo,
        string RelativeFile,
        string AbsoluteFilePath,
        string FileId,
        IOntologyNode ParentNode,
        FolderNode? OwningFolderNode,
        bool IsManifest,
        bool IsSupportedSourceOrConfig);

    private sealed class WalkCollectorState(ParsingContext ctx, WorkspaceNode workspaceNode, GitIgnoreTrieMatcher gitTrie)
    {
        public ParsingContext Ctx { get; } = ctx;
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

    public async Task<Layer1Result> ParseAsync(ParsingContext ctx)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
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
        PhysicalFileStore.ClearWorkspace(ctx.AbsoluteWorkspacePath);
        PhysicalFileStore.RegisterConfiguredProjects(ctx.AbsoluteWorkspacePath, WorkspaceSettings.Load(ctx.AbsoluteWorkspacePath).Projects);

        var walkState = new WalkCollectorState(ctx, workspaceNode, gitTrie);
        ScopedDirectoryTreeNode? rootTreeNode;

        if (!ctx.IsSubtreeScan)
        {
            rootTreeNode = WalkScopedDirectory(
                ctx.AbsoluteWorkspacePath,
                filesStructureNode,
                gitTrie.RootScope,
                inheritedProjectParsers: null,
                walkState);
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
        }

        foreach (var taskDir in walkState.VscodeTaskDirs)
        {
            await TryStreamVscodeTasksAsync(taskDir, ctx);
        }

        var finalCandidates = BuildFinalCandidateList(walkState);
        var files = await StreamAllCandidatesAsync(finalCandidates, walkState.KeptFolders, ctx);
        FinalizeNonEmptyFolders(walkState, folders);

        var l1Result = new Layer1Result(workspaceNode, filesStructureNode, files, folders);
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

    private static async Task<string> InitializeWorkspaceIdAsync(ParsingContext ctx)
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

    private static async Task<string> ResolveWorkspaceNameAsync(ParsingContext ctx)
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

    private static void AttachRootGitSettingsIfPresent(string wsId, FilesStructureNode filesStructureNode, ParsingContext ctx)
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
        IReadOnlyList<IProjectParser>? inheritedProjectParsers,
        WalkCollectorState state)
    {
        var ctx = state.Ctx;
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var isRoot = string.Equals(currentDir, ctx.AbsoluteWorkspacePath, StringComparison.OrdinalIgnoreCase);
        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

        var relativeDir = isRoot ? string.Empty : currentDir.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        if (relativeDir == ".") relativeDir = string.Empty;

        if (ctx.IncrementalRelevantDirectories != null &&
            !string.IsNullOrEmpty(relativeDir) &&
            !ctx.IncrementalRelevantDirectories.Contains(relativeDir))
        {
            return null;
        }

        if (!isRoot && ctx.FileFilter.IsExcludedDirectory(dirName, currentDir, inheritedProjectParsers))
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

        var dirInfo = new DirectoryInfo(currentDir);
        var dirFiles = dirInfo.GetFiles();
        Array.Sort(dirFiles, static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
        var subDirs = dirInfo.GetDirectories();
        Array.Sort(subDirs, static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

        InspectSpecialEntries(dirFiles, subDirs, out var hasGitPointer, out var hasGitSubDir, out var hasVscodeSubDir, out var gitIgnorePath, out var ceIgnorePath);

        if (!isRoot && hasGitPointer && GitSettingsParser.IsGitWorktreeDirectory(currentDir))
        {
            ctx.Layer1Log.SkippingExcludedDirectory(relativeDir);
            return null;
        }

        var isGitRepoRoot = hasGitPointer || hasGitSubDir;
        if (isGitRepoRoot)
        {
            PhysicalFileStore.RegisterGitRepositoryRoot(currentDir);
        }

        var dirFilePaths = new string[dirFiles.Length];
        for (var i = 0; i < dirFiles.Length; i++) dirFilePaths[i] = dirFiles[i].FullName;
        PhysicalFileStore.SetDirectoryFiles(currentDir, dirFilePaths);

        // Ask project parsers if this directory opens a new project scope (respecting parent scope!)
        var (directNonFallbackParsers, localFallbackParsers, hasManifestInDir) = ResolveScopedParsersForDirectory(
            currentDir, dirFiles, dirFilePaths, inheritedProjectParsers, isGitRepoRoot, ctx);

        var activeFileParsers = directNonFallbackParsers
                                ?? inheritedProjectParsers
                                ?? localFallbackParsers
                                ?? ctx.FileFilter.MatchFileSupportedProjectParsers(dirFiles, includeFallback: false)
                                ?? ctx.FileFilter.MatchFileSupportedProjectParsers(dirFiles, includeFallback: true);

        // Compile child .gitignore / .codeexplorerignore + dialect-specific exclusions (e.g. libman.json) into GitScope
        var dialectPatterns = NotifyParsersAndCollectIgnorePatterns(currentDir, relativeDir, dirFiles, activeFileParsers, state);
        var activeGitScope = state.GitTrie.CompileChildScope(parentGitScope, relativeDir, gitIgnorePath, ceIgnorePath, dialectPatterns);

        FolderNode? folderNode = null;
        var currentParentNode = parentNode;
        if (!isRoot)
        {
            var absoluteFolderPath = currentDir.NormalizeFullPath();
            var folderId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Folder}:{absoluteFolderPath}";
            folderNode = new FolderNode(folderId, dirName, absoluteFolderPath);
            currentParentNode = folderNode;
            state.AllVisitedFolders.Add((folderNode, parentNode));

            if (isGitRepoRoot)
            {
                AttachNestedGitSettingsIfPresent(currentDir, folderNode, ctx);
            }
        }

        if (hasVscodeSubDir && File.Exists(Path.Combine(currentDir, DotVscodeName, OntologyConstants.ManifestFields.TasksJsonFileName)))
        {
            state.VscodeTaskDirs.Add(currentDir);
        }

        var normDir = currentDir.NormalizeFullPath(trimTrailingSlash: true);
        var hasCandidateTrigger = isGitRepoRoot || hasManifestInDir || directNonFallbackParsers != null ||
                                  (PhysicalFileStore.HasAnyConfiguredProjects && PhysicalFileStore.TryGetConfiguredProject(currentDir, out _)) ||
                                  ProjectTriggerDetector.HasCandidateProjectTrigger(currentDir, dirFilePaths);

        var treeNode = new ScopedDirectoryTreeNode(currentDir, normDir, relativeDir, isRoot, dirFilePaths, hasCandidateTrigger)
        {
            IsGitRepoRoot = isGitRepoRoot
        };

        if (hasManifestInDir)
        {
            state.ManifestDirectories.Add(currentDir);
        }

        // Only non-fallback project parsers cascade down as enclosing project scope to child directories
        var nextInheritedParsers = directNonFallbackParsers ?? inheritedProjectParsers;

        // Collect files inside current directory according to active project/infra scope (remembering slice range in case container is demoted on post-order)
        var ownedStartIdx = state.OwnedCandidates.Count;
        var isTentativelyOwnedWithoutManifest = !hasManifestInDir && directNonFallbackParsers == null && inheritedProjectParsers == null && activeFileParsers is { Count: > 0 };
        var localLangCounts = SubtreeLanguageCounts.FromDirectoryFiles(dirFiles, out var localHasSource);
        CollectScopedDirectoryFiles(
            isRoot,
            keptForNegation,
            dirFiles,
            currentParentNode,
            folderNode,
            activeGitScope,
            activeFileParsers,
            state);

        ReportDiscoveryProgressIfNeeded(dirFiles.Length, state);

        // Recurse into child directories with updated GitIgnoreScopeChain & EnclosingProjectParsers
        var totalLangCounts = localLangCounts;
        var hasAnySource = localHasSource;
        var descendantGitRepos = 0;

        for (var i = 0; i < subDirs.Length; i++)
        {
            var childNode = WalkScopedDirectory(subDirs[i].FullName, currentParentNode, activeGitScope, nextInheritedParsers, state);
            if (childNode != null)
            {
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
        }

        treeNode.LanguageCounts = totalLangCounts;
        treeNode.HasSourceFiles = hasAnySource;
        treeNode.DescendantGitRepoCount = descendantGitRepos;

        // Post-order container guard: if this directory had no manifest/enclosing project and wraps child git repositories,
        // demote any loose root/container files that aren't manifests or standalone contracts
        if (isTentativelyOwnedWithoutManifest && descendantGitRepos > 0)
        {
            DemoteContainerLooseFiles(ownedStartIdx, isRoot, state);
        }
        else if (activeFileParsers is { Count: > 0 })
        {
            state.HasAnyOwnedDirectory = true;
            if (inheritedProjectParsers == null && directNonFallbackParsers == null && localFallbackParsers is { Count: > 0 })
            {
                state.FallbackCandidateDirectories.Add(currentDir);
            }
        }

        if (folderNode != null && (directNonFallbackParsers is { Count: > 0 } || folderNode.Children.Count > 0))
        {
            state.KeptFolders.Add(folderNode);
        }

        return treeNode;
    }

    private static (IReadOnlyList<IProjectParser>? DirectNonFallback, IReadOnlyList<IProjectParser>? LocalFallback, bool HasManifest)
        ResolveScopedParsersForDirectory(
            string currentDir,
            FileInfo[] dirFiles,
            string[] dirFilePaths,
            IReadOnlyList<IProjectParser>? inheritedProjectParsers,
            bool isGitRepoRoot,
            ParsingContext ctx)
    {
        var hasManifest = false;
        for (var i = 0; i < dirFiles.Length; i++)
        {
            if (ctx.FileFilter.IsManifestFile(dirFiles[i].Name))
            {
                hasManifest = true;
                break;
            }
        }

        IReadOnlyList<IProjectParser>? directNonFallback = null;
        if (inheritedProjectParsers is not { Count: > 0 } || hasManifest || isGitRepoRoot ||
            ProjectTriggerDetector.IsPotentialManifestlessBinarySubfolder(currentDir) ||
            (PhysicalFileStore.HasAnyConfiguredProjects && PhysicalFileStore.TryGetConfiguredProject(currentDir, out _)))
        {
            directNonFallback = ctx.FileFilter.MatchDirectProjectParsers(currentDir, dirFiles, dirFilePaths, allowFallback: false);
            if (directNonFallback is not { Count: > 0 } &&
                PhysicalFileStore.HasAnyConfiguredProjects &&
                PhysicalFileStore.TryGetConfiguredProject(currentDir, out var cfgProj) &&
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
        if (directNonFallback is not { Count: > 0 } && inheritedProjectParsers is not { Count: > 0 } && dirFiles.Length > 0)
        {
            localFallback = ctx.FileFilter.MatchDirectProjectParsers(currentDir, dirFiles, dirFilePaths, allowFallback: true);
        }

        return (directNonFallback is { Count: > 0 } ? directNonFallback : null,
                localFallback is { Count: > 0 } ? localFallback : null,
                hasManifest);
    }

    private static List<string>? NotifyParsersAndCollectIgnorePatterns(
        string currentDir,
        string relativeDir,
        FileInfo[] dirFiles,
        IReadOnlyList<IProjectParser>? activeFileParsers,
        WalkCollectorState state)
    {
        if (dirFiles.Length == 0) return null;

        var parsersToNotify = activeFileParsers is { Count: > 0 }
            ? activeFileParsers
            : state.Ctx.FileFilter.ConnectedProjectParsers;

        List<string>? globalPatterns = null;
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
            if (scratch.Count > 0)
            {
                for (var p = 0; p < scratch.Count; p++)
                {
                    state.GitTrie.AddPattern(scratch[p]);
                }
                globalPatterns ??= [];
                globalPatterns.AddRange(scratch);
            }
            parsersToNotify[i].InspectDirectoryFiles(currentDir, dirFiles, state.WorkspaceNode);
        }
        return globalPatterns;
    }

    private static void CollectScopedDirectoryFiles(
        bool isRoot,
        bool keptForNegation,
        FileInfo[] dirFiles,
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
            var fileInfo = dirFiles[i];
            var file = fileInfo.FullName;
            var relativeFile = file.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);

            if (state.GitTrie.IsIgnoredFileInScope(gitScope, relativeFile, skipDirOnlyRules)) continue;
            if (ctx.CustomFileExclusionPredicate?.Invoke(fileInfo.Name, relativeFile) == true) continue;

            var fileName = fileInfo.Name;
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

            if (isSupportedSourceOrConfig && ctx.FileFilter.ShouldSkipFileMetadata(fileInfo))
            {
                if (!isManifest) continue;
                isSupportedSourceOrConfig = false;
            }

            var absoluteFilePath = file.NormalizeSlashes();
            var fileId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{relativeFile}";
            var item = new CandidateFileItem(
                0,
                fileInfo,
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

    private static void DemoteContainerLooseFiles(int ownedStartIdx, bool isRoot, WalkCollectorState state)
    {
        for (var i = state.OwnedCandidates.Count - 1; i >= ownedStartIdx; i--)
        {
            var item = state.OwnedCandidates[i];
            if (!item.IsManifest && !WorkspaceFileFilter.IsStandaloneContractOrEnvFile(item.RelativeFile, isRoot))
            {
                state.OwnedCandidates.RemoveAt(i);
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

    private static async Task<List<FileNode>> StreamAllCandidatesAsync(
        List<CandidateFileItem> candidates,
        HashSet<FolderNode> keptFolders,
        ParsingContext ctx)
    {
        var files = new List<FileNode>(candidates.Count);
        if (candidates.Count == 0) return files;

        var streamedFiles = new FileNode?[candidates.Count];
        var totalCandidates = candidates.Count;
        var streamedCount = 0;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ctx.CancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        await Parallel.ForEachAsync(candidates, parallelOptions, async (item, _) =>
        {
            streamedFiles[item.Index] = await StreamCandidateFileAsync(item, ctx);
            var current = Interlocked.Increment(ref streamedCount);
            if (current % ProgressFileStep == 0 || (current == totalCandidates && totalCandidates >= ProgressFileStep))
            {
                var pct = (int)Math.Round(current * 100.0 / totalCandidates);
                var globalPct = 2 + (int)(6.0 * current / totalCandidates);
                ctx.Layer1Log.StreamingProgress(current, totalCandidates, pct);
                ctx.ReportProgress(
                    globalPct,
                    Layer1ProgressTitle,
                    $"Streaming physical files: {current:N0}/{totalCandidates:N0} ({pct}%)",
                    current,
                    totalCandidates);
            }
        });

        for (var i = 0; i < candidates.Count; i++)
        {
            var fileNode = streamedFiles[i];
            if (fileNode == null) continue;
            candidates[i].ParentNode.Children.Add(fileNode);
            if (candidates[i].OwningFolderNode is { } folder)
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

        // Propagate kept status bottom-up in reverse discovery order (children always appear after parents)
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

    private static void InspectSpecialEntries(
        FileInfo[] dirFiles,
        DirectoryInfo[] subDirs,
        out bool hasGitPointer,
        out bool hasGitSubDir,
        out bool hasVscodeSubDir,
        out string? gitIgnorePath,
        out string? ceIgnorePath)
    {
        hasGitPointer = false;
        gitIgnorePath = null;
        ceIgnorePath = null;
        for (var i = 0; i < dirFiles.Length; i++)
        {
            var fn = dirFiles[i].Name;
            if (fn.Equals(DotGitName, StringComparison.OrdinalIgnoreCase)) hasGitPointer = true;
            else if (fn.Equals(DotGitIgnoreName, StringComparison.OrdinalIgnoreCase)) gitIgnorePath = dirFiles[i].FullName;
            else if (fn.Equals(DotCeIgnoreName, StringComparison.OrdinalIgnoreCase)) ceIgnorePath = dirFiles[i].FullName;
        }

        hasGitSubDir = false;
        hasVscodeSubDir = false;
        for (var i = 0; i < subDirs.Length; i++)
        {
            var sName = subDirs[i].Name;
            if (sName.Equals(DotGitName, StringComparison.OrdinalIgnoreCase)) hasGitSubDir = true;
            else if (sName.Equals(DotVscodeName, StringComparison.OrdinalIgnoreCase)) hasVscodeSubDir = true;
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
        ParsingContext ctx)
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

    private static void AttachNestedGitSettingsIfPresent(string currentDir, FolderNode folderNode, ParsingContext ctx)
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

    private static async Task TryStreamVscodeTasksAsync(string currentDir, ParsingContext ctx)
    {
        var vscodeTasksPath = Path.Combine(currentDir, DotVscodeName, OntologyConstants.ManifestFields.TasksJsonFileName);
        if (!File.Exists(vscodeTasksPath)) return;

        var relTasks = vscodeTasksPath.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);
        try
        {
            var absTasks = vscodeTasksPath.NormalizeFullPath();
            var tasksInfo = new FileInfo(vscodeTasksPath);
            var streamedTasks = await PhysicalFileStore.StreamAndAnalyzeFileAsync(
                absTasks,
                relTasks,
                OntologyConstants.ManifestFields.TasksJsonFileName,
                tasksInfo.Length,
                isManifest: true,
                isSupportedSourceOrConfig: false,
                ctx.FileFilter,
                ctx.CancellationToken);
            if (streamedTasks.ContentHash != null)
            {
                ctx.DiscoveredManifestFiles[relTasks] = (absTasks, streamedTasks.ContentHash, tasksInfo.LastWriteTimeUtc, tasksInfo.Length);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(relTasks, ex);
            ctx.RecordScanIssue(Layer1Stage, IssueCategoryIoError, relTasks, ex.Message, name: IssueTasksJsonReadError, severity: SeverityWarning, exception: ex);
        }
    }

    private static async Task<FileNode?> StreamCandidateFileAsync(CandidateFileItem item, ParsingContext ctx)
    {
        long sizeBytes = 0;
        int? lineCount = null;
        string? contentHash = null;
        DateTime? lastModifiedUtc = null;
        string? artifactRole = null;
        string? decodedText = null;
        var skipByContent = false;
        try
        {
            sizeBytes = item.FileInfo.Length;
            lastModifiedUtc = item.FileInfo.LastWriteTimeUtc;
            var shouldComputeHash = item.IsManifest || ctx.PersistToGraphDb;
            var streamed = await PhysicalFileStore.StreamAndAnalyzeFileAsync(
                item.AbsoluteFilePath,
                item.RelativeFile,
                item.FileInfo.Name,
                sizeBytes,
                item.IsManifest,
                item.IsSupportedSourceOrConfig,
                ctx.FileFilter,
                ctx.CancellationToken,
                computeHash: shouldComputeHash);
            skipByContent = streamed.SkipByContent;
            contentHash = streamed.ContentHash;
            lineCount = streamed.LineCount;
            artifactRole = streamed.ArtifactRole;
            decodedText = streamed.DecodedText;
            if (item.IsManifest && contentHash != null && lastModifiedUtc.HasValue)
            {
                ctx.DiscoveredManifestFiles[item.RelativeFile] = (item.AbsoluteFilePath, contentHash, lastModifiedUtc.Value, sizeBytes);
            }
            if (skipByContent && lastModifiedUtc.HasValue)
            {
                ctx.DiscoveredContentSkippedFiles[item.RelativeFile] = (item.AbsoluteFilePath, lastModifiedUtc.Value, sizeBytes);
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            ctx.Layer1Log.SkippingBrokenSymlink(item.RelativeFile);
            PhysicalFileStore.RemoveCachedFile(item.AbsoluteFilePath);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(item.RelativeFile, ex);
            PhysicalFileStore.RemoveCachedFile(item.AbsoluteFilePath);
            if (item.IsSupportedSourceOrConfig || item.IsManifest)
            {
                var issueName = item.IsManifest ? IssueManifestReadError : IssueFileReadError;
                ctx.RecordScanIssue(
                    Layer1Stage,
                    IssueCategoryIoError,
                    item.RelativeFile,
                    ex.Message,
                    name: issueName,
                    severity: SeverityWarning,
                    exception: ex,
                    parentNodeId: null,
                    isFileParent: false,
                    logToConsole: false);
            }
            return null;
        }

        if (!item.IsSupportedSourceOrConfig || skipByContent)
        {
            return null;
        }

        return new FileNode(item.FileId, item.FileInfo.Name, item.RelativeFile, item.AbsoluteFilePath, false, null, null, lineCount, sizeBytes)
        {
            ContentHash = contentHash,
            LastModifiedUtc = lastModifiedUtc,
            ArtifactRole = artifactRole ?? ctx.FileFilter.ClassifyFileArtifactRole(item.RelativeFile, item.FileInfo.Name),
            PreloadedContent = decodedText
        };
    }

    public static HashSet<string> ExcludedDirectoryNames => new(WorkspaceFileFilter.ExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
    public static bool IsExcludedDirectory(string dirName) => WorkspaceFileFilter.Default.IsExcludedDirectory(dirName);
    public static bool IsSupportedFile(string fileName) => WorkspaceFileFilter.Default.IsSupportedSourceOrConfigFile(fileName);
    public static bool ShouldSkipFileName(string fileName) => WorkspaceFileFilter.Default.ShouldSkipFileName(fileName);
    public static bool ShouldSkipFile(FileInfo fileInfo) => WorkspaceFileFilter.Default.ShouldSkipFile(fileInfo);
    public static bool IsMinifiedContent(string filePath) => WorkspaceFileFilter.IsMinifiedContent(filePath);
}
