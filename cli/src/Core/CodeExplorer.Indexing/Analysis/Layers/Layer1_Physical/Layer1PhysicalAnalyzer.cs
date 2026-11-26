using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer1PhysicalParser
{
    private const int ProgressFileStep = 1000;
    private const string Layer1ProgressTitle = "Layer 1: Physical";
    public async Task<Layer1Result> ParseAsync(ParsingContext ctx)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ctx.Layer1Log.ScanStarted();

        // 0. Get or create Workspace ID from database (auto-incremented)
        var wsId = ctx.PersistToGraphDb && ctx.DbClient != null
            ? await ctx.DbClient.GetOrCreateWorkspaceIdAsync(ctx.HostWorkspacePath)
            : "ws0";
        ctx.WorkspaceId = wsId;
        ctx.CancellationToken.ThrowIfCancellationRequested();

        if (ctx.PersistToGraphDb && ctx.DbClient != null)
        {
            await ctx.DbClient.SaveEmptyWorkspaceNodeAsync(wsId, ctx.HostWorkspacePath);
            ctx.CancellationToken.ThrowIfCancellationRequested();
        }

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

        var hostPath = PathTools.NormalizeToHostPath(ctx.HostWorkspacePath);
        var workspaceNode = new WorkspaceNode(wsId, workspaceName, hostPath)
        {
            IndexedAt = DateTime.UtcNow.ToString("o")
        };

        var filesNodeId = $"{wsId}:{OntologyConstants.IdPrefixes.FilesStructure}";
        var filesStructureNode = new FilesStructureNode(filesNodeId, "FilesStructure", hostPath);
        workspaceNode.Children.Add(filesStructureNode);

        if (!ctx.LightweightAstScanMode)
        {
            var rootRepoDir = GitSettingsParser.FindRepoRoot(ctx.AbsoluteWorkspacePath);
            if (rootRepoDir != null)
            {
                var gitSettingsNode = GitSettingsParser.Parse(wsId, ctx.AbsoluteWorkspacePath, rootRepoDir);
                if (gitSettingsNode != null)
                {
                    filesStructureNode.Children.Add(gitSettingsNode);
                    ctx.RegisterGitRepository(rootRepoDir, gitSettingsNode);
                }
            }
        }

        var files = new List<FileNode>();
        var folders = new List<FolderNode>();
        var gitignore = new GitIgnoreMatcher(ctx.AbsoluteWorkspacePath, ctx.FileFilter);
        PhysicalFileStore.ClearWorkspace(ctx.AbsoluteWorkspacePath);
        PhysicalFileStore.RegisterConfiguredProjects(ctx.AbsoluteWorkspacePath, WorkspaceSettings.Load(ctx.AbsoluteWorkspacePath).Projects);

        var discoveredDirs = new List<DiscoveredDirectoryEntry>();
        var vscodeTaskDirs = new List<string>();
        var discoveredRawFiles = 0;
        var nextDiscoveryReportThreshold = ProgressFileStep;

        if (!ctx.IsSubtreeScan)
        {
            DiscoverDirectoryTree(ctx.AbsoluteWorkspacePath, filesStructureNode, workspaceNode, gitignore, null, discoveredDirs, vscodeTaskDirs, ctx, ref discoveredRawFiles, ref nextDiscoveryReportThreshold);
        }
        else
        {
            var initialParsers = ctx.FileFilter.ResolveAncestorProjectParsers(ctx.ScanPath, ctx.AbsoluteWorkspacePath);
            var currentParent = BuildSubtreeIntermediateFolders(wsId, filesStructureNode, folders, ctx);
            DiscoverDirectoryTree(ctx.ScanPath, currentParent, workspaceNode, gitignore, initialParsers, discoveredDirs, vscodeTaskDirs, ctx, ref discoveredRawFiles, ref nextDiscoveryReportThreshold);
        }

        RefreshContainerProjectParsers(discoveredDirs, ctx);

        foreach (var taskDir in vscodeTaskDirs)
        {
            await TryStreamVscodeTasksAsync(taskDir, ctx);
        }

        var candidates = CollectCandidateFiles(discoveredDirs, gitignore, ctx);
        var streamedFiles = new FileNode?[candidates.Count];

        if (candidates.Count > 0)
        {
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

            for (int i = 0; i < candidates.Count; i++)
            {
                var fileNode = streamedFiles[i];
                if (fileNode == null) continue;
                candidates[i].ParentNode.Children.Add(fileNode);
                files.Add(fileNode);
            }
        }

        AttachNonEmptyFolders(discoveredDirs, folders);

        ctx.Layer1Log.ScanCompleted(files.Count, folders.Count, sw.ElapsedMilliseconds);
        return new Layer1Result(workspaceNode, filesStructureNode, files, folders);
    }

    private sealed record DiscoveredDirectoryEntry(
        string CurrentDir,
        string RelativeDir,
        bool IsRoot,
        IOntologyNode ParentNode,
        FolderNode? FolderNode,
        FileInfo[] DirFiles,
        IReadOnlyList<IProjectParser>? InheritedParsers,
        IReadOnlyList<IProjectParser>? ActiveParsers,
        bool IsProjectRoot,
        bool KeptForNegation = false);

    private sealed record CandidateFileItem(
        int Index,
        FileInfo FileInfo,
        string RelativeFile,
        string AbsoluteFilePath,
        string FileId,
        IOntologyNode ParentNode,
        bool IsManifest,
        bool IsSupportedSourceOrConfig);

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

        for (int i = 0; i < segments.Length - 1; i++)
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

    private static void DiscoverDirectoryTree(
        string currentDir,
        IOntologyNode parentNode,
        WorkspaceNode workspaceNode,
        GitIgnoreMatcher gitignore,
        IReadOnlyList<IProjectParser>? inheritedParsers,
        List<DiscoveredDirectoryEntry> discoveredDirs,
        List<string> vscodeTaskDirs,
        ParsingContext ctx,
        ref int discoveredRawFiles,
        ref int nextDiscoveryReportThreshold)
    {
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
            return;
        }

        if (!isRoot && ctx.FileFilter.IsExcludedDirectory(dirName, currentDir, inheritedParsers))
        {
            ctx.Layer1Log.SkippingExcludedDirectory(relativeDir);
            return;
        }

        var keptForNegation = false;
        if (!string.IsNullOrEmpty(relativeDir) && gitignore.IsIgnoredDirectory(relativeDir, out keptForNegation))
        {
            ctx.Layer1Log.SkippingGitIgnoredDirectory(relativeDir);
            return;
        }

        var dirInfo = new DirectoryInfo(currentDir);
        var dirFiles = dirInfo.GetFiles();
        Array.Sort(dirFiles, static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

        var hasGitPointerFile = false;
        string? gitIgnoreFilePath = null;
        string? ceIgnoreFilePath = null;
        for (int i = 0; i < dirFiles.Length; i++)
        {
            var fn = dirFiles[i].Name;
            if (fn.Equals(".git", StringComparison.OrdinalIgnoreCase))
            {
                hasGitPointerFile = true;
            }
            else if (fn.Equals(".gitignore", StringComparison.OrdinalIgnoreCase))
            {
                gitIgnoreFilePath = dirFiles[i].FullName;
            }
            else if (fn.Equals(".codeexplorerignore", StringComparison.OrdinalIgnoreCase))
            {
                ceIgnoreFilePath = dirFiles[i].FullName;
            }
        }

        if (!isRoot && hasGitPointerFile && GitSettingsParser.IsGitWorktreeDirectory(currentDir))
        {
            ctx.Layer1Log.SkippingExcludedDirectory(relativeDir);
            return;
        }

        if (!string.IsNullOrEmpty(relativeDir) && !ctx.FileFilter.IncludeDistAndMinifiedArtifacts)
        {
            if (gitIgnoreFilePath != null) gitignore.LoadScopedFile(gitIgnoreFilePath, relativeDir);
            if (ceIgnoreFilePath != null) gitignore.LoadScopedFile(ceIgnoreFilePath, relativeDir);
        }

        var dirFilePaths = new string[dirFiles.Length];
        for (int i = 0; i < dirFiles.Length; i++)
        {
            dirFilePaths[i] = dirFiles[i].FullName;
        }
        PhysicalFileStore.SetDirectoryFiles(currentDir, dirFilePaths);

        var directParsers = ctx.FileFilter.MatchDirectProjectParsers(
            currentDir,
            dirFiles,
            dirFilePaths,
            allowFallback: inheritedParsers is not { Count: > 0 });
        var activeParsers = directParsers is { Count: > 0 }
            ? directParsers
            : inheritedParsers is { Count: > 0 }
                ? inheritedParsers
                : ctx.FileFilter.MatchFileSupportedProjectParsers(dirFiles, includeFallback: false)
                  ?? ctx.FileFilter.MatchFileSupportedProjectParsers(dirFiles, includeFallback: true);
        var parsersToNotify = activeParsers is { Count: > 0 } ? activeParsers : ctx.FileFilter.ConnectedProjectParsers;

        if (dirFiles.Length > 0)
        {
            for (int i = 0; i < parsersToNotify.Count; i++)
            {
                parsersToNotify[i].OnDirectoryScan(currentDir, relativeDir, gitignore, ctx.Layer1Log.DirectoryScanHookMessage);
                parsersToNotify[i].InspectDirectoryFiles(currentDir, dirFiles, workspaceNode);
            }
        }

        var subDirs = dirInfo.GetDirectories();
        Array.Sort(subDirs, static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
        var hasGitSubDir = false;
        var hasVscodeSubDir = false;
        for (int i = 0; i < subDirs.Length; i++)
        {
            var sName = subDirs[i].Name;
            if (sName.Equals(".git", StringComparison.OrdinalIgnoreCase)) hasGitSubDir = true;
            else if (sName.Equals(".vscode", StringComparison.OrdinalIgnoreCase)) hasVscodeSubDir = true;
        }

        if (hasGitPointerFile || hasGitSubDir)
        {
            PhysicalFileStore.RegisterGitRepositoryRoot(currentDir);
        }

        FolderNode? folderNode = null;
        var currentParentNode = parentNode;

        if (!isRoot)
        {
            var absoluteFolderPath = currentDir.NormalizeFullPath();
            var folderId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Folder}:{absoluteFolderPath}";
            folderNode = new FolderNode(folderId, dirName, absoluteFolderPath);
            currentParentNode = folderNode;

            if (hasGitPointerFile || hasGitSubDir)
            {
                AttachNestedGitSettingsIfPresent(currentDir, folderNode, ctx);
            }
        }

        if (hasVscodeSubDir && File.Exists(Path.Combine(currentDir, ".vscode", OntologyConstants.ManifestFields.TasksJsonFileName)))
        {
            vscodeTaskDirs.Add(currentDir);
        }

        discoveredDirs.Add(new DiscoveredDirectoryEntry(
            currentDir,
            relativeDir,
            isRoot,
            parentNode,
            folderNode,
            dirFiles,
            inheritedParsers,
            activeParsers,
            IsProjectRoot: directParsers is { Count: > 0 },
            KeptForNegation: keptForNegation));

        discoveredRawFiles += dirFiles.Length;
        if (discoveredRawFiles >= nextDiscoveryReportThreshold)
        {
            nextDiscoveryReportThreshold = ((discoveredRawFiles / ProgressFileStep) + 1) * ProgressFileStep;
            ctx.Layer1Log.DiscoveringProgress(discoveredRawFiles, discoveredDirs.Count);
            ctx.ReportProgress(
                2,
                Layer1ProgressTitle,
                $"Discovering directory tree: {discoveredRawFiles:N0} files across {discoveredDirs.Count:N0} folders...",
                discoveredRawFiles,
                discoveredRawFiles);
        }

        for (int i = 0; i < subDirs.Length; i++)
        {
            DiscoverDirectoryTree(subDirs[i].FullName, currentParentNode, workspaceNode, gitignore, activeParsers, discoveredDirs, vscodeTaskDirs, ctx, ref discoveredRawFiles, ref nextDiscoveryReportThreshold);
        }
    }

    private static void RefreshContainerProjectParsers(
        List<DiscoveredDirectoryEntry> discoveredDirs,
        ParsingContext ctx)
    {
        if (discoveredDirs.Count == 0)
        {
            return;
        }

        IReadOnlyList<IProjectParser>? rootParsers = null;
        var parsersByFolderId = new Dictionary<string, IReadOnlyList<IProjectParser>>(discoveredDirs.Count, StringComparer.Ordinal);

        for (int i = 0; i < discoveredDirs.Count; i++)
        {
            var entry = discoveredDirs[i];
            IReadOnlyList<IProjectParser>? inherited = null;
            if (entry.ParentNode is FolderNode pf && parsersByFolderId.TryGetValue(pf.Id, out var parentParsers))
            {
                inherited = parentParsers;
            }
            else if (!entry.IsRoot)
            {
                inherited = rootParsers;
            }

            if (entry.IsProjectRoot &&
                (PhysicalFileStore.HasChildGitRepositories(entry.CurrentDir) ||
                 PhysicalFileStore.HasChildConfiguredProjects(entry.CurrentDir)))
            {
                var cachedPaths = PhysicalFileStore.GetDirectoryFiles(entry.CurrentDir);
                var rechecked = ctx.FileFilter.MatchDirectProjectParsers(
                    entry.CurrentDir,
                    entry.DirFiles,
                    cachedPaths,
                    allowFallback: false);
                if (rechecked is not { Count: > 0 })
                {
                    entry = entry with { ActiveParsers = inherited, IsProjectRoot = false };
                    discoveredDirs[i] = entry;
                }
            }

            if (!entry.IsProjectRoot)
            {
                var hasConfigured = PhysicalFileStore.HasAnyConfiguredProjects &&
                                    PhysicalFileStore.TryGetConfiguredProject(entry.CurrentDir, out _);
                var allowFallbackNow = inherited is not { Count: > 0 };
                var allowFallbackBefore = entry.InheritedParsers is not { Count: > 0 };
                var needRecheckDirect = hasConfigured || (allowFallbackNow && !allowFallbackBefore);

                IReadOnlyList<IProjectParser>? direct = null;
                if (needRecheckDirect)
                {
                    var cachedPaths = PhysicalFileStore.GetDirectoryFiles(entry.CurrentDir);
                    direct = ctx.FileFilter.MatchDirectProjectParsers(
                        entry.CurrentDir,
                        entry.DirFiles,
                        cachedPaths,
                        allowFallback: allowFallbackNow);
                    if (direct is not { Count: > 0 } &&
                        hasConfigured &&
                        PhysicalFileStore.TryGetConfiguredProject(entry.CurrentDir, out var cfgProj) &&
                        cfgProj != null)
                    {
                        direct = ctx.FileFilter.ConnectedProjectParsers
                            .Where(p => string.IsNullOrEmpty(cfgProj.Language) ||
                                        p.ProjectType.Equals(cfgProj.Language, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                }

                if (direct is { Count: > 0 })
                {
                    entry = entry with { ActiveParsers = direct, IsProjectRoot = true };
                    discoveredDirs[i] = entry;
                }
                else if (!ReferenceEquals(entry.InheritedParsers, inherited) && inherited is { Count: > 0 })
                {
                    entry = entry with { ActiveParsers = inherited };
                    discoveredDirs[i] = entry;
                }
                else if (entry.ActiveParsers is not { Count: > 0 } && inherited is { Count: > 0 })
                {
                    entry = entry with { ActiveParsers = inherited };
                    discoveredDirs[i] = entry;
                }
            }

            if (entry.ActiveParsers is { Count: > 0 })
            {
                if (entry.IsRoot)
                {
                    rootParsers = entry.ActiveParsers;
                }
                else if (entry.FolderNode != null)
                {
                    parsersByFolderId[entry.FolderNode.Id] = entry.ActiveParsers;
                }
            }
        }
    }

    private static void AttachNestedGitSettingsIfPresent(string currentDir, FolderNode folderNode, ParsingContext ctx)
    {
        var gitPath = Path.Combine(currentDir, ".git");
        if (!Directory.Exists(gitPath) && !File.Exists(gitPath)) return;

        var subGitNode = GitSettingsParser.Parse(ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, currentDir);
        if (subGitNode != null)
        {
            folderNode.Children.Add(subGitNode);
            ctx.RegisterGitRepository(currentDir, subGitNode);
        }
    }

    private static List<CandidateFileItem> CollectCandidateFiles(
        List<DiscoveredDirectoryEntry> discoveredDirs,
        GitIgnoreMatcher gitignore,
        ParsingContext ctx)
    {
        if (discoveredDirs.Count == 0)
        {
            return [];
        }

        var hasAnyOwnedDirectory = discoveredDirs.Any(d => d.ActiveParsers is { Count: > 0 });
        var perDirResults = new List<CandidateFileItem>?[discoveredDirs.Count];

        void ProcessDirectory(int dirIdx)
        {
            var dirEntry = discoveredDirs[dirIdx];
            if (dirEntry.DirFiles.Length == 0) return;

            var fileParentNode = (IOntologyNode?)dirEntry.FolderNode ?? dirEntry.ParentNode;
            var skipDirOnlyRules = !dirEntry.KeptForNegation;
            List<CandidateFileItem>? localList = null;

            for (var f = 0; f < dirEntry.DirFiles.Length; f++)
            {
                var fileInfo = dirEntry.DirFiles[f];
                var file = fileInfo.FullName;
                var relativeFile = file.GetNormalizedRelativePath(ctx.AbsoluteWorkspacePath);

                if (gitignore.IsIgnoredFile(relativeFile, skipDirOnlyRules)) continue;
                if (ctx.CustomFileExclusionPredicate?.Invoke(fileInfo.Name, relativeFile) == true) continue;

                var fileName = fileInfo.Name;
                var isManifest = ctx.FileFilter.IsManifestFile(fileName);
                var isInIncrementalScope = ctx.IncrementalChangedFiles == null || ctx.IncrementalChangedFiles.Contains(relativeFile);
                if (!isManifest && !isInIncrementalScope) continue;

                bool isSupportedSourceOrConfig;
                if (!isInIncrementalScope)
                {
                    isSupportedSourceOrConfig = false;
                }
                else if (hasAnyOwnedDirectory && dirEntry.ActiveParsers is not { Count: > 0 })
                {
                    isSupportedSourceOrConfig = WorkspaceFileFilter.IsStandaloneContractOrEnvFile(relativeFile, dirEntry.IsRoot);
                }
                else
                {
                    isSupportedSourceOrConfig = ctx.FileFilter.IsSupportedSourceOrConfigFile(relativeFile, dirEntry.ActiveParsers);
                }

                if (!isSupportedSourceOrConfig && !isManifest) continue;

                if (isSupportedSourceOrConfig && ctx.FileFilter.ShouldSkipFileMetadata(fileInfo))
                {
                    if (!isManifest) continue;
                    isSupportedSourceOrConfig = false;
                }

                var absoluteFilePath = file.NormalizeSlashes();
                var fileId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{relativeFile}";
                localList ??= new List<CandidateFileItem>(dirEntry.DirFiles.Length);
                localList.Add(new CandidateFileItem(
                    0,
                    fileInfo,
                    relativeFile,
                    absoluteFilePath,
                    fileId,
                    fileParentNode,
                    isManifest,
                    isSupportedSourceOrConfig));
            }

            perDirResults[dirIdx] = localList;
        }

        if (discoveredDirs.Count >= 64)
        {
            Parallel.For(0, discoveredDirs.Count, new ParallelOptions
            {
                CancellationToken = ctx.CancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, ProcessDirectory);
        }
        else
        {
            for (var i = 0; i < discoveredDirs.Count; i++)
            {
                ProcessDirectory(i);
            }
        }

        var totalCount = 0;
        for (var i = 0; i < perDirResults.Length; i++)
        {
            if (perDirResults[i] is { Count: > 0 } l) totalCount += l.Count;
        }

        var candidates = new List<CandidateFileItem>(totalCount);
        for (var i = 0; i < perDirResults.Length; i++)
        {
            if (perDirResults[i] is not { Count: > 0 } l) continue;
            for (var j = 0; j < l.Count; j++)
            {
                candidates.Add(l[j] with { Index = candidates.Count });
            }
        }

        return candidates;
    }

    private static void AttachNonEmptyFolders(
        List<DiscoveredDirectoryEntry> discoveredDirs,
        List<FolderNode> folders)
    {
        var parentMap = new Dictionary<string, FolderNode>(discoveredDirs.Count, StringComparer.Ordinal);
        for (int i = 0; i < discoveredDirs.Count; i++)
        {
            var e = discoveredDirs[i];
            if (e.FolderNode != null && e.ParentNode is FolderNode pf)
            {
                parentMap[e.FolderNode.Id] = pf;
            }
        }

        var keptFolderIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = discoveredDirs.Count - 1; i >= 0; i--)
        {
            var entry = discoveredDirs[i];
            if (entry.FolderNode == null) continue;

            var keep = entry.IsProjectRoot ||
                       entry.FolderNode.Children.Count > 0 ||
                       keptFolderIds.Contains(entry.FolderNode.Id);

            if (keep)
            {
                keptFolderIds.Add(entry.FolderNode.Id);
                var currentId = entry.FolderNode.Id;
                while (parentMap.TryGetValue(currentId, out var ancestorFolder))
                {
                    if (!keptFolderIds.Add(ancestorFolder.Id))
                    {
                        break;
                    }
                    currentId = ancestorFolder.Id;
                }
            }
        }

        for (int i = 0; i < discoveredDirs.Count; i++)
        {
            var entry = discoveredDirs[i];
            if (entry.FolderNode != null && keptFolderIds.Contains(entry.FolderNode.Id))
            {
                entry.ParentNode.Children.Add(entry.FolderNode);
                folders.Add(entry.FolderNode);
            }
        }
    }

    private const string Layer1Stage = "Layer1_Physical";
    private const string IssueCategoryIoError = "io_error";
    private const string SeverityWarning = "warning";
    private const string IssueTasksJsonReadError = "TasksJsonReadError";
    private const string IssueManifestReadError = "ManifestReadError";
    private const string IssueFileReadError = "FileReadError";

    private static async Task TryStreamVscodeTasksAsync(string currentDir, ParsingContext ctx)
    {
        var vscodeTasksPath = Path.Combine(currentDir, ".vscode", OntologyConstants.ManifestFields.TasksJsonFileName);
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
