using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Parser;

public class WorkspaceIndexer
{
    private const string Layer1PhysicalProgressTitle = "Layer 1: Physical";
    private const string Layer1PhysicalProgressMessage = "Preparing database and physical scanning...";
    private const string Layer2BoundariesProgressTitle = "Layer 2: Boundaries";
    private const string Layer3SyntacticProgressTitle = "Layer 3: Syntactic";
    private const string Layer4SemanticProgressTitle = "Layer 4: Semantic";
    private const string Layer5GlobalProgressTitle = "Layer 5: Global";
    private const string Layer5GlobalProgressMessage = "Semantic enrichment complete. Resolving global references...";
    private const string FinalizingProgressTitle = "Finalizing";
    private const string PostIndexingCompleteProgressMessage = "Post-indexing analysis pass complete.";
    private const string RebuildingIndicesProgressMessage = "Rebuilding database indices...";
    private const string InvalidatingViewCacheProgressMessage = "Invalidating view cache...";
    private const string CompletedProgressTitle = "Completed";
    private const string CompletedProgressMessage = "Indexing process completed successfully.";

    public static Func<Layer3Result, ParsingContext, Task>? PostSyntacticPipelineHook { get; set; } = static async (l3, ctx) =>
    {
        var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);
        ctx.ReportProgress(68, Layer5GlobalProgressTitle, Layer5GlobalProgressMessage, l3.SyntaxTrees.Count, l3.Prev.Prev.Files.Count);
        await new Layer5AnalysisParser().ParseAsync(l4, ctx);
    };

    public static Action? OnIndexCompletedHook
    {
        get => ParserRegistry.OnIndexCompletedHook;
        set => ParserRegistry.OnIndexCompletedHook = value;
    }

    internal static List<IProjectParser> _projectParsers => ParserRegistry.ProjectParsers;
    internal static List<IFileParser> _fileParsers => ParserRegistry.FileParsers;

    public static void Register(object parser) => ParserRegistry.Register(parser);

    /// <summary>
    /// Discovers parser assemblies (or scans the given ones) and registers every parser type they export.
    /// </summary>
    public static IReadOnlyList<Type> AutoRegisterParsers(IEnumerable<Assembly>? assemblies = null)
    {
        if (assemblies == null)
        {
            BuiltInParserAssemblyLoader.EnsureLoaded();
        }
        return ParserRegistry.AutoRegisterParsers(assemblies);
    }

    public static IReadOnlyList<IProjectParser> GetAllProjectParsers() => ParserRegistry.GetAllProjectParsers();
    public static IReadOnlyList<IFileParser> GetAllFileParsers() => ParserRegistry.GetAllFileParsers();
    public static IFileParser? GetParserForFile(string filePath) => ParserRegistry.GetParserForFile(filePath);
    public static IFileParser? GetParserByLanguage(string languageName) => ParserRegistry.GetParserByLanguage(languageName);
    public static IProjectParser? GetProjectParser(string projectType) => ParserRegistry.GetProjectParser(projectType);

    private readonly IGraphClient _dbClient;
    private readonly ILogger<WorkspaceIndexer> _logger;
    private readonly Incremental.FileRegistry _fileRegistry = new();

    public Incremental.FileRegistry FileRegistry => _fileRegistry;

    public WorkspaceIndexer(IGraphClient dbClient, ILogger<WorkspaceIndexer>? logger = null)
    {
        _dbClient = dbClient;
        _logger = logger ?? NullLogger<WorkspaceIndexer>.Instance;
    }

    public Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync(
        string workspacePath,
        bool clear,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        bool enableIntentAnalysis = false) =>
        IndexAsync(workspacePath, workspaceRoot: null, clear, cancellationToken, progress, enableIntentAnalysis);

    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync(
        string targetPath,
        string? workspaceRoot,
        bool clear,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        bool enableIntentAnalysis = false)
    {
        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        string root;
        string? scanPath = null;

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            root = targetPath;
        }
        else
        {
            var fullTarget = targetPath.NormalizeFullPath();
            var fullRoot = workspaceRoot.NormalizeFullPath();

            if (string.Equals(fullTarget, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                root = fullRoot;
            }
            else
            {
                root = fullRoot;
                scanPath = fullTarget;
            }
        }

        if (_dbClient.IsSchemaOutdated && !clear && !string.IsNullOrWhiteSpace(scanPath))
        {
            _logger.LogInformation(
                "[Database   ] Schema version outdated (v{SchemaVersion} < v{CurrentSchemaVersion}). Attempting schema conversion...",
                _dbClient.SchemaVersion, IGraphClient.CurrentSchemaVersion);
            await _dbClient.UpgradeSchemaAsync(cancellationToken);
        }

        if (_dbClient.IsSchemaOutdated)
        {
            _logger.LogInformation(
                "[Database   ] Schema version outdated (v{SchemaVersion} < v{CurrentSchemaVersion}). Resetting database to current schema before full rebuild...",
                _dbClient.SchemaVersion, IGraphClient.CurrentSchemaVersion);
            clear = true;
        }

        var ctx = CreateContext(root, scanPath, clear, enableIntentAnalysis, cancellationToken, progress);

        await RunParsingPipelineAsync(ctx);

        ctx.ReportProgress(100, CompletedProgressTitle, CompletedProgressMessage, ctx.TotalNodesCount, ctx.TotalNodesCount);
        ctx.PipelineLog.IndexingCompleted(ctx.TotalNodesCount, ctx.TotalRelsCount, totalSw.ElapsedMilliseconds);

        return (ctx.TotalNodesCount, ctx.TotalRelsCount, ctx.NodesByKind);
    }

    private ParsingContext CreateContext(
        string workspacePath,
        string? scanPath,
        bool clear,
        bool enableIntentAnalysis,
        CancellationToken cancellationToken,
        IProgress<IndexingProgress>? progress)
    {
        if (!Directory.Exists(workspacePath))
        {
            throw new DirectoryNotFoundException($"Directory '{workspacePath}' does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(scanPath) && !Directory.Exists(scanPath))
        {
            throw new DirectoryNotFoundException($"Scan path '{scanPath}' does not exist.");
        }

        var absoluteWorkspacePath = workspacePath.NormalizeFullPath();
        string? normalizedScanPath = null;

        if (!string.IsNullOrWhiteSpace(scanPath))
        {
            normalizedScanPath = scanPath.NormalizeFullPath();
            var rel = normalizedScanPath.GetNormalizedRelativePath(absoluteWorkspacePath);
            if (rel.StartsWith("..") || Path.IsPathRooted(rel))
            {
                throw new ArgumentException($"Target scan path '{scanPath}' must be inside workspace root '{workspacePath}'.");
            }
        }

        var sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        var ctx = new ParsingContext(
            absoluteWorkspacePath,
            absoluteWorkspacePath,
            _dbClient,
            sharedChannel,
            clear,
            scanPath: normalizedScanPath,
            cancellationToken: cancellationToken,
            progress: progress,
            logger: _logger,
            enableIntentAnalysis: enableIntentAnalysis)
        {
            FileFilter = new WorkspaceFileFilter(_projectParsers, _fileParsers)
        };
        return ctx;
    }

    private async Task RunParsingPipelineAsync(ParsingContext ctx)
    {
        ConfigurationIndex.Clear();
        ConstantRegistry.Clear();
        RouteDictionaryRegistry.Clear();
        AnalysisSignalStore.Clear();

        List<FileNode> indexedFiles;
        IReadOnlyList<ProjectNode> projects;
        IReadOnlyList<SyntaxTree> syntaxTrees;
        Dictionary<string, Incremental.FileGraphSnapshot> precomputedSnapshots;
        int parsedTreeCount;
        try
        {
            await using (new DatabasePersistenceWriter(ctx))
            {
                ctx.ReportProgress(1, Layer1PhysicalProgressTitle, Layer1PhysicalProgressMessage, 0, 0);
                await PrepareDatabaseAsync(ctx);
                var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
                indexedFiles = l1.Files;
                ctx.ReportProgress(8, Layer2BoundariesProgressTitle, $"Discovered {indexedFiles.Count} files across workspace.", 0, indexedFiles.Count);

                var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
                projects = l2.Projects;
                ctx.ReportProgress(14, Layer3SyntacticProgressTitle, $"Discovered {projects.Count} projects. Starting syntax parsing...", 0, indexedFiles.Count);

                var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
                syntaxTrees = l3.SyntaxTrees;
                parsedTreeCount = syntaxTrees.Count;
                precomputedSnapshots = PrecomputeFileSnapshots(syntaxTrees);
                ctx.ReportProgress(52, Layer4SemanticProgressTitle, $"Parsed {parsedTreeCount} syntax trees. Analyzing semantics...", parsedTreeCount, indexedFiles.Count);

                if (PostSyntacticPipelineHook != null) await PostSyntacticPipelineHook(l3, ctx);
                ctx.ReportProgress(68, Layer5GlobalProgressTitle, Layer5GlobalProgressMessage, parsedTreeCount, indexedFiles.Count);

                
                ctx.ReportProgress(86, FinalizingProgressTitle, PostIndexingCompleteProgressMessage, parsedTreeCount, indexedFiles.Count);
            }
        }
        finally
        {
            await _dbClient.CommitBulkLoadAsync(CancellationToken.None);
        }

        await CodeIntentAnalyzer.EnrichAsync(ctx, ctx.CancellationToken);

        LogPersistenceSummary(ctx);
        ctx.ReportProgress(88, FinalizingProgressTitle, $"Synchronizing file registry for {indexedFiles.Count} files...", parsedTreeCount, indexedFiles.Count);
        await SyncFileRegistryAsync(indexedFiles, projects, syntaxTrees, precomputedSnapshots, ctx);
        precomputedSnapshots.Clear();
        ctx.ReportProgress(94, FinalizingProgressTitle, RebuildingIndicesProgressMessage, parsedTreeCount, indexedFiles.Count);
        await _dbClient.SetSchemaVersionAsync(IGraphClient.CurrentSchemaVersion);
        await _dbClient.CreateIndicesAsync();
        ctx.ReportProgress(99, FinalizingProgressTitle, InvalidatingViewCacheProgressMessage, parsedTreeCount, indexedFiles.Count);
        OnIndexCompletedHook?.Invoke();
        if (!ctx.ScanIssues.IsEmpty)
        {
            var issuesList = ctx.ScanIssues.Values.Select(v => v.Issue).ToList();
            var syntaxErrCount = issuesList.Count(i => i.Category == CodeExplorer.Cypher.Common.ScanIssueCategory.SyntaxError);
            var otherErrCount = issuesList.Count - syntaxErrCount;
            ctx.PipelineLog.RecordedScanIssues(issuesList.Count, syntaxErrCount, otherErrCount);
        }
    }


    private static Dictionary<string, Incremental.FileGraphSnapshot> PrecomputeFileSnapshots(
        IReadOnlyList<SyntaxTree> syntaxTrees)
    {
        var snapshots = new Dictionary<string, Incremental.FileGraphSnapshot>(syntaxTrees.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < syntaxTrees.Count; i++)
        {
            var st = syntaxTrees[i];
            if (st.FileNode == null) continue;
            var normPath = st.FilePath.NormalizeSlashes();
            if (snapshots.ContainsKey(normPath)) continue;

            var relPath = !string.IsNullOrEmpty(st.FileNode.Path) ? st.FileNode.Path : st.RelativePath;
            var hash = st.FileNode.ContentHash ?? string.Empty;
            var lastMod = st.FileNode.LastModifiedUtc ?? DateTime.MinValue;
            snapshots[normPath] = Incremental.FileSnapshotBuilder.BuildFromSyntaxTree(
                st,
                relPath,
                hash,
                lastMod,
                includeSemanticFootprint: false);
        }
        return snapshots;
    }

    private async Task SyncFileRegistryAsync(
        List<FileNode> files,
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<SyntaxTree> syntaxTrees,
        Dictionary<string, Incremental.FileGraphSnapshot> precomputedSnapshots,
        ParsingContext ctx)
    {
        var regSw = System.Diagnostics.Stopwatch.StartNew();
        ctx.PipelineLog.SyncingFileRegistry(files.Count);

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ctx.CancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        var treesByFullPath = syntaxTrees.Count > 0
            ? syntaxTrees
                .GroupBy(t => t.FilePath.NormalizeSlashes(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, SyntaxTree>(StringComparer.OrdinalIgnoreCase);

        var entries = new System.Collections.Concurrent.ConcurrentBag<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes, string ProjectPath, string? SnapshotJson)>();
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
        {
            string hash;
            DateTime lastMod;
            long sizeBytes = file.SizeBytes ?? -1L;
            if (!string.IsNullOrEmpty(file.ContentHash) && file.LastModifiedUtc.HasValue)
            {
                hash = file.ContentHash;
                lastMod = file.LastModifiedUtc.Value;
            }
            else
            {
                try
                {
                    var fi = new FileInfo(file.FullPath);
                    if (!fi.Exists) return;
                    lastMod = fi.LastWriteTimeUtc;
                    sizeBytes = fi.Length;
                    hash = await Incremental.HashUtility.ComputeSha256FromFileAsync(file.FullPath, ct);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Skip dangling symlinks or unreadable files during registry sync
                    return;
                }
            }
            seen.TryAdd(file.Path, 0);

            string? snapshotJson = null;
            Incremental.FileGraphSnapshot? snapshot = null;

            var normalizedFullPath = file.FullPath.Replace('\\', '/');
            if (precomputedSnapshots.TryGetValue(normalizedFullPath, out var precomputed))
            {
                snapshot = (precomputed.ContentHash == hash && precomputed.LastModifiedUtc == lastMod)
                    ? precomputed
                    : precomputed with { ContentHash = hash, LastModifiedUtc = lastMod };
                snapshotJson = snapshot.ComputeStructuralHash();
            }
            else if (treesByFullPath.TryGetValue(normalizedFullPath, out var syntaxTree) && syntaxTree.FileNode != null)
            {
                snapshot = Incremental.FileSnapshotBuilder.BuildFromSyntaxTree(
                    syntaxTree,
                    file.Path,
                    hash,
                    lastMod,
                    includeSemanticFootprint: false);
                snapshotJson = snapshot.ComputeStructuralHash();
            }

            _fileRegistry.UpdateEntry(file.Path, hash, lastMod, file.Path, snapshot, sizeBytes);
            entries.Add((file.Path, hash, lastMod, sizeBytes, file.Path, snapshotJson));
        });

        if (ctx.IncrementalChangedFiles == null)
        {
            // Register all project manifests across the workspace
            var gitignore = new GitIgnoreMatcher(ctx.AbsoluteWorkspacePath, ctx.FileFilter);
            // Register all project manifests discovered in Layer 1 without re-scanning disk
            var fileByRelPath = files.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var candidateManifests = ctx.DiscoveredManifestFiles.Count > 0
                ? ctx.DiscoveredManifestFiles.Keys.Where(f => ctx.FileFilter.IsManifestFile(Path.GetFileName(f)))
                : ctx.FileFilter.EnumerateCandidateFiles(ctx.AbsoluteWorkspacePath, ctx.AbsoluteWorkspacePath, gitignore)
                .Where(f => ctx.FileFilter.IsManifestFile(Path.GetFileName(f)));

            foreach (var relPath in candidateManifests)
            {
                if (seen.ContainsKey(relPath)) continue;
                seen.TryAdd(relPath, 0);

                if (ctx.DiscoveredManifestFiles.TryGetValue(relPath, out var discManifest))
                {
                    _fileRegistry.UpdateEntry(relPath, discManifest.ContentHash, discManifest.LastModifiedUtc, relPath, sizeBytes: discManifest.SizeBytes);
                    entries.Add((relPath, discManifest.ContentHash, discManifest.LastModifiedUtc, discManifest.SizeBytes, relPath, null));
                    continue;
                }
                if (fileByRelPath.TryGetValue(relPath, out var existingFileNode) &&
                    !string.IsNullOrEmpty(existingFileNode.ContentHash) &&
                    existingFileNode.LastModifiedUtc.HasValue)
                {
                    var sz = existingFileNode.SizeBytes ?? -1L;
                    _fileRegistry.UpdateEntry(relPath, existingFileNode.ContentHash, existingFileNode.LastModifiedUtc.Value, relPath, sizeBytes: sz);
                    entries.Add((relPath, existingFileNode.ContentHash, existingFileNode.LastModifiedUtc.Value, sz, relPath, null));
                    continue;
                }
                var manifestPath = Path.Combine(ctx.AbsoluteWorkspacePath, relPath);
                var manifestInfo = new FileInfo(manifestPath);
                if (!manifestInfo.Exists) continue;

                var lastMod = manifestInfo.LastWriteTimeUtc;
                var manifestSize = manifestInfo.Length;
                var hash = await Incremental.HashUtility.ComputeSha256FromFileAsync(manifestPath, ctx.CancellationToken);
                _fileRegistry.UpdateEntry(relPath, hash, lastMod, relPath, sizeBytes: manifestSize);
                entries.Add((relPath, hash, lastMod, manifestSize, relPath, null));
            }
        }

        foreach (var (relPath, skippedInfo) in ctx.DiscoveredContentSkippedFiles)
        {
            if (seen.ContainsKey(relPath)) continue;
            seen.TryAdd(relPath, 0);
            const string skippedHash = "__SKIPPED_CONTENT__";
            _fileRegistry.UpdateEntry(relPath, skippedHash, skippedInfo.LastModifiedUtc, relPath, sizeBytes: skippedInfo.SizeBytes);
            entries.Add((relPath, skippedHash, skippedInfo.LastModifiedUtc, skippedInfo.SizeBytes, relPath, null));
        }

        ctx.PipelineLog.PersistingFileRegistry(entries.Count);
        await _dbClient.SaveFileRegistryEntriesAsync(entries, ctx.CancellationToken);

        // Prune stale entries that were deleted or are now ignored/excluded (only on full workspace scans)
        if (!ctx.IsSubtreeScan && ctx.IncrementalChangedFiles == null)
        {
            var staleKeys = _fileRegistry.Entries.Select(e => e.Key).Where(k => !seen.ContainsKey(k)).ToList();
            if (staleKeys.Count > 0)
            {
                ctx.PipelineLog.PruningFileRegistry(staleKeys.Count);
                foreach (var k in staleKeys)
                {
                    _fileRegistry.RemoveEntry(k);
                }
                await _dbClient.DeleteFileRegistryEntriesAsync(staleKeys, ctx.CancellationToken);
            }
        }

        ctx.PipelineLog.FileRegistrySynced(entries.Count, regSw.ElapsedMilliseconds);
    }

    public bool HasLoadedFileRegistry => _fileRegistry.Count > 0;

    public void ResetFileRegistry() => _fileRegistry.Clear();

    public Task<bool> IndexIncrementalAsync(
        string workspacePath,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        bool enableIntentAnalysis = false,
        IReadOnlyCollection<string>? knownChangedFiles = null) =>
        IndexIncrementalAsync(workspacePath, workspaceRoot: null, cancellationToken, progress, enableIntentAnalysis, knownChangedFiles);


    public async Task<bool> IndexIncrementalAsync(
        string targetPath,
        string? workspaceRoot,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        bool enableIntentAnalysis = false,
        IReadOnlyCollection<string>? knownChangedFiles = null)
    {
        var incSw = System.Diagnostics.Stopwatch.StartNew();
        var root = string.IsNullOrWhiteSpace(workspaceRoot) ? targetPath : workspaceRoot;
        var absoluteWorkspacePath = Path.GetFullPath(root).Replace('\\', '/');
        var absoluteTargetPath = Path.GetFullPath(targetPath).Replace('\\', '/');

        if (_dbClient.IsSchemaOutdated)
        {
            _logger.LogInformation(
                "[Incremental:Decision] Database schema is outdated (v{SchemaVersion} < v{CurrentSchemaVersion}). Converting database schema...",
                _dbClient.SchemaVersion, IGraphClient.CurrentSchemaVersion);
            if (!await _dbClient.UpgradeSchemaAsync(cancellationToken))
            {
                _logger.LogInformation(
                    "[Incremental:Decision] Full rescan triggered: Schema conversion from v{SchemaVersion} to v{CurrentSchemaVersion} could not complete in-place.",
                    _dbClient.SchemaVersion, IGraphClient.CurrentSchemaVersion);
                await IndexAsync(targetPath, workspaceRoot, clear: true, cancellationToken: cancellationToken, progress: progress, enableIntentAnalysis: enableIntentAnalysis);
                return true;
            }
        }

        if (_fileRegistry.Count == 0)
        {
            await _fileRegistry.LoadAsync(_dbClient, cancellationToken);
        }

        if (_fileRegistry.Count == 0)
        {
            _logger.LogInformation("[Incremental:Decision] Full rescan triggered: File registry in database is empty (workspace not indexed yet).");
            await IndexAsync(targetPath, workspaceRoot, clear: false, cancellationToken: cancellationToken, progress: progress, enableIntentAnalysis: enableIntentAnalysis);
            return true;
        }

        var filter = new WorkspaceFileFilter(_projectParsers, _fileParsers);
        var gitignore = new GitIgnoreMatcher(absoluteWorkspacePath, filter);

        var targetSubPath = string.Equals(absoluteTargetPath, absoluteWorkspacePath, StringComparison.OrdinalIgnoreCase)
            ? null
            : Path.GetRelativePath(absoluteWorkspacePath, absoluteTargetPath).Replace('\\', '/');

        Incremental.FileChangeset changeset;
        if (knownChangedFiles is { Count: > 0 })
        {
            var filteredPaths = knownChangedFiles
                .Select(p => p.Replace('\\', '/').TrimStart('/'))
                .Where(p => !string.IsNullOrWhiteSpace(p) && !gitignore.IsIgnored(p, false))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            changeset = _fileRegistry.ComputeChangesetForKnownPaths(absoluteWorkspacePath, filteredPaths);
        }
        else
        {
            var allFiles = filter.EnumerateCandidateFiles(
                absoluteTargetPath,
                absoluteWorkspacePath,
                gitignore,
                msg => _logger.LogInformation("{Msg}", msg));
            changeset = _fileRegistry.ComputeChangeset(absoluteWorkspacePath, allFiles, targetSubPath);
        }
        if (changeset.IsEmpty)
        {
            _logger.LogInformation("[Incremental] Workspace is up to date (0 changes detected) in {ElapsedMs:N0}ms.", incSw.ElapsedMilliseconds);
            return false;
        }

        _logger.LogInformation(
            "[Incremental] Changes detected: {AddedCount} added, {ModifiedCount} modified, {DeletedCount} deleted.",
            changeset.Added.Count, changeset.Modified.Count, changeset.Deleted.Count);

        var changedManifests = changeset.Added.Concat(changeset.Modified).Concat(changeset.Deleted)
            .Where(f => filter.IsManifestFile(Path.GetFileName(f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (changedManifests.Count > 0)
        {
            var manifestDesc = changedManifests.Count <= 3
                ? string.Join(", ", changedManifests)
                : $"{string.Join(", ", changedManifests.Take(3))} (+{changedManifests.Count - 3} more)";
            var hasDeletedManifests = changeset.Deleted.Any(f => filter.IsManifestFile(Path.GetFileName(f)));
            var shouldClear = hasDeletedManifests && targetSubPath == null;
            _logger.LogInformation(
                "[Incremental:Decision] Full rescan triggered: Project manifest modified ({Manifests}). Reindexing workspace structure (clear={Clear})...",
                manifestDesc,
                shouldClear);
            await IndexAsync(targetPath, workspaceRoot, clear: shouldClear, cancellationToken: cancellationToken, progress: progress, enableIntentAnalysis: enableIntentAnalysis);
            return true;
        }

        if (changeset.Deleted.Count > 0)
        {
            var filesDesc = changeset.Deleted.Count <= 3
                ? string.Join(", ", changeset.Deleted)
                : $"{string.Join(", ", changeset.Deleted.Take(3))} (+{changeset.Deleted.Count - 3} more)";
            _logger.LogInformation(
                "[Incremental] Pruning {Count} deleted file(s) from graph and registry: {Files}",
                changeset.Deleted.Count, filesDesc);
            await _dbClient.DeleteFileRegistryEntriesAsync(changeset.Deleted, cancellationToken);
            foreach (var del in changeset.Deleted)
            {
                await _dbClient.ClearWorkspaceAsync(del);
                _fileRegistry.RemoveEntry(del);
            }
        }

        var workspaceId = await _dbClient.GetOrCreateWorkspaceIdAsync(absoluteWorkspacePath);
        var filesToUpdateRegistry = new List<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes, string ProjectPath, string? SnapshotJson)>();
        var structuralReasons = new List<string>();
        var modifiedWithStructuralChanges = new List<string>();
        var effectiveAdded = new List<string>();
        const string skippedHash = "__SKIPPED_CONTENT__";

        foreach (var relPath in changeset.Added)
        {
            var fileInfo = new FileInfo(Path.Combine(absoluteWorkspacePath, relPath));
            if (!fileInfo.Exists) continue;
            var normalizedFullPath = fileInfo.FullName.NormalizeSlashes();
            try
            {
                var streamed = await PhysicalFileStore.StreamAndAnalyzeFileAsync(normalizedFullPath, relPath, fileInfo.Name, fileInfo.Length, false, true, filter, cancellationToken, computeHash: false);
                if (streamed.SkipByContent)
                {
                    _fileRegistry.UpdateEntry(relPath, skippedHash, fileInfo.LastWriteTimeUtc, relPath, sizeBytes: fileInfo.Length);
                    filesToUpdateRegistry.Add((relPath, skippedHash, fileInfo.LastWriteTimeUtc, fileInfo.Length, relPath, null));
                }
                else effectiveAdded.Add(relPath);
            }
            finally { PhysicalFileStore.EvictFileText(normalizedFullPath); }
        }

        if (effectiveAdded.Count > 0)
        {
            var addedDesc = effectiveAdded.Count <= 3 ? string.Join(", ", effectiveAdded) : $"{string.Join(", ", effectiveAdded.Take(3))} (+{effectiveAdded.Count - 3} more)";
            var addedReason = $"New file(s) added ({effectiveAdded.Count}): [{addedDesc}]";
            _logger.LogInformation("[Incremental:Decision] Structural change detected: {Reason}", addedReason);
            structuralReasons.Add(addedReason);
        }

        foreach (var relPath in changeset.Modified)
        {
            var fullPath = Path.Combine(absoluteWorkspacePath, relPath);
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists) continue;

            var normalizedFullPath = fileInfo.FullName.Replace('\\', '/');
            var lastMod = fileInfo.LastWriteTimeUtc;
            var sizeBytes = fileInfo.Length;

            try
            {
                var streamed = await PhysicalFileStore.StreamAndAnalyzeFileAsync(normalizedFullPath, relPath, fileInfo.Name, sizeBytes, isManifest: false, isSupportedSourceOrConfig: true, filter, cancellationToken);
                if (streamed.SkipByContent)
                {
                    _fileRegistry.UpdateEntry(relPath, skippedHash, lastMod, relPath, sizeBytes: sizeBytes);
                    filesToUpdateRegistry.Add((relPath, skippedHash, lastMod, sizeBytes, relPath, null));
                    continue;
                }

                var newHash = streamed.ContentHash ?? await Incremental.HashUtility.ComputeSha256FromFileAsync(fullPath, cancellationToken);

                if (_fileRegistry.TryGetEntry(relPath, out var existingEntry) &&
                    string.Equals(existingEntry.ContentHash, newHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Size changed solely due to CRLF <-> LF normalization; content is identical
                    _fileRegistry.UpdateEntry(relPath, newHash, lastMod, existingEntry.ProjectPath, sizeBytes: sizeBytes);
                    filesToUpdateRegistry.Add((relPath, newHash, lastMod, sizeBytes, existingEntry.ProjectPath, null));
                    continue;
                }

                if (!_fileRegistry.TryGetSnapshot(relPath, out var oldSnapshot))
                {
                    // Cold snapshot fallback: build initial snapshot for this file and persist it without forcing full rescan
                    var initialSnapshot = await TryBuildSnapshotAsync(normalizedFullPath, relPath, workspaceId, absoluteWorkspacePath, newHash, lastMod, cancellationToken);
                    if (initialSnapshot != null)
                    {
                        _fileRegistry.UpdateEntry(relPath, newHash, lastMod, "", initialSnapshot, sizeBytes);
                        var snapJson = initialSnapshot.ComputeStructuralHash();
                        filesToUpdateRegistry.Add((relPath, newHash, lastMod, sizeBytes, "", snapJson));
                        _logger.LogInformation("[Incremental:Decision] Seeded initial AST snapshot for '{File}' without full rescan.", relPath);
                        continue;
                    }

                    var reason = $"No prior AST snapshot in database for '{relPath}' and AST parsing failed.";
                    _logger.LogInformation("[Incremental:Decision] Structural change in '{File}'.", relPath);
                    _logger.LogDebug("[Incremental:Decision] Structural details for '{File}': {Reason}", relPath, reason);
                    structuralReasons.Add(reason);
                    modifiedWithStructuralChanges.Add(relPath);
                    continue;
                }

                var newSnapshot = await TryBuildSnapshotAsync(normalizedFullPath, relPath, workspaceId, absoluteWorkspacePath, newHash, lastMod, cancellationToken);
                if (newSnapshot == null)
                {
                    var reason = $"No AST parser available or syntax parsing failed for '{relPath}'.";
                    _logger.LogInformation("[Incremental:Decision] Structural change in '{File}'.", relPath);
                    _logger.LogDebug("[Incremental:Decision] Structural details for '{File}': {Reason}", relPath, reason);
                    structuralReasons.Add(reason);
                    modifiedWithStructuralChanges.Add(relPath);
                    continue;
                }

                var patch = Incremental.SemanticGraphDiffer.ComputeDiff(oldSnapshot, newSnapshot);
                if (!patch.HasGraphStructuralChanges)
                {
                    var logicCount = patch.LogicOnlyChangedSymbols.Count;
                    var shiftCount = patch.LineShiftedSymbols.Count;
                    _logger.LogInformation(
                        "[Incremental:Decision] Logic-only change in '{File}' ({LogicCount} logic edits, {ShiftCount} line shifts, 0 graph modifications). Preserving graph.",
                        relPath, logicCount, shiftCount);
                    _fileRegistry.UpdateEntry(relPath, newHash, lastMod, "", newSnapshot, sizeBytes);
                    var snapJson = newSnapshot.ComputeStructuralHash();
                    filesToUpdateRegistry.Add((relPath, newHash, lastMod, sizeBytes, "", snapJson));
                    continue;
                }

                var fileChangeReasons = new List<string>();
                if (patch.AddedSymbols.Count > 0)
                {
                    var addedDesc = string.Join(", ", patch.AddedSymbols.Select(s => $"{s.Kind} '{s.QualifiedName}'"));
                    fileChangeReasons.Add($"Added symbols: [{addedDesc}]");
                }
                if (patch.RemovedSymbols.Count > 0)
                {
                    var removedDesc = string.Join(", ", patch.RemovedSymbols.Select(s => $"{s.Kind} '{s.QualifiedName}'"));
                    fileChangeReasons.Add($"Removed symbols: [{removedDesc}]");
                }
                if (patch.OutgoingCallDiffs.Count > 0)
                {
                    var callDesc = string.Join("; ", patch.OutgoingCallDiffs.Select(kv =>
                    {
                        var addedStr = kv.Value.Added.Count > 0 ? $"+[{string.Join(", ", kv.Value.Added)}]" : "";
                        var remStr = kv.Value.Removed.Count > 0 ? $"-[{string.Join(", ", kv.Value.Removed)}]" : "";
                        return $"{kv.Key} ({string.Join(" ", new[] { addedStr, remStr }.Where(s => !string.IsNullOrEmpty(s)))})";
                    }));
                    fileChangeReasons.Add($"Call changes: [{callDesc}]");
                }
                if (patch.OutgoingTypeDiffs.Count > 0)
                {
                    var typeDesc = string.Join("; ", patch.OutgoingTypeDiffs.Select(kv =>
                    {
                        var addedStr = kv.Value.Added.Count > 0 ? $"+[{string.Join(", ", kv.Value.Added)}]" : "";
                        var remStr = kv.Value.Removed.Count > 0 ? $"-[{string.Join(", ", kv.Value.Removed)}]" : "";
                        return $"{kv.Key} ({string.Join(" ", new[] { addedStr, remStr }.Where(s => !string.IsNullOrEmpty(s)))})";
                    }));
                    fileChangeReasons.Add($"Type changes: [{typeDesc}]");
                }

                var fullReason = $"Structural changes in '{relPath}': {string.Join(" | ", fileChangeReasons)}";
                _logger.LogInformation("[Incremental:Decision] Structural change in '{File}'.", relPath);
                _logger.LogDebug("[Incremental:Decision] Structural details for '{File}': {Reason}", relPath, fullReason);
                structuralReasons.Add(fullReason);
                modifiedWithStructuralChanges.Add(relPath);
            }
            finally
            {
                PhysicalFileStore.EvictFileText(normalizedFullPath);
            }
        }

        if (filesToUpdateRegistry.Count > 0)
        {
            await _dbClient.SaveFileRegistryEntriesAsync(filesToUpdateRegistry, cancellationToken);
        }

        if (structuralReasons.Count > 0)
        {
            if (modifiedWithStructuralChanges.Count > 0)
            {
                _logger.LogInformation(
                    "[Incremental] Removing stale graph nodes for {Count} updated file(s)...",
                    modifiedWithStructuralChanges.Count);

                foreach (var mod in modifiedWithStructuralChanges)
                {
                    await _dbClient.ClearWorkspaceAsync(mod);
                }
            }

            _logger.LogInformation(
                "[Incremental:Decision] Executing graph update due to {Count} structural trigger(s)...",
                structuralReasons.Count);
            _logger.LogDebug(
                "[Incremental:Decision] Structural triggers:\n  * {Reasons}",
                string.Join("\n  * ", structuralReasons));

            var structuralFiles = new HashSet<string>(
                effectiveAdded.Concat(modifiedWithStructuralChanges).Select(p => p.NormalizeSlashes()),
                StringComparer.OrdinalIgnoreCase);

            var relevantDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty, "." };
            void AddAncestorDirs(string relFilePath)
            {
                var dir = Path.GetDirectoryName(relFilePath)?.NormalizeSlashes();
                while (!string.IsNullOrEmpty(dir) && dir != ".")
                {
                    if (!relevantDirs.Add(dir)) break;
                    dir = Path.GetDirectoryName(dir)?.NormalizeSlashes();
                }
            }

            foreach (var f in structuralFiles) AddAncestorDirs(f);

            var hasRootTarget = string.IsNullOrWhiteSpace(workspaceRoot) ||
                                string.Equals(
                                    Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                    Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                    StringComparison.OrdinalIgnoreCase);
            var effectiveWorkspace = string.IsNullOrWhiteSpace(workspaceRoot) ? targetPath : workspaceRoot;
            var effectiveScanPath = hasRootTarget ? null : targetPath;

            var incCtx = CreateContext(effectiveWorkspace, effectiveScanPath, clear: false, enableIntentAnalysis, cancellationToken, progress);
            incCtx.IncrementalChangedFiles = structuralFiles;
            incCtx.IncrementalRelevantDirectories = relevantDirs;

            await RunParsingPipelineAsync(incCtx);
            incCtx.PipelineLog.IndexingCompleted(incCtx.TotalNodesCount, incCtx.TotalRelsCount, incSw.ElapsedMilliseconds);
        }
        else if (changeset.Deleted.Count == 0 && filesToUpdateRegistry.Count == 0)
        {
            _logger.LogInformation("[Incremental] Workspace is up to date (0 effective changes detected) in {ElapsedMs:N0}ms.", incSw.ElapsedMilliseconds);
            return false;
        }
        else
        {
            _logger.LogInformation("[Incremental:Decision] All modifications were logic-only; graph remains unchanged ({ElapsedMs:N0}ms).", incSw.ElapsedMilliseconds);
        }

        return true;
    }

    public static async Task<Incremental.FileGraphSnapshot?> TryBuildSnapshotAsync(
        string fullPath,
        string relPath,
        string workspaceId,
        string workspaceRoot,
        string contentHash,
        DateTime lastModifiedUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parser = GetParserForFile(fullPath);
            if (parser == null) return null;

            var syntaxTree = await parser.ParseAsync(fullPath, "", workspaceId, workspaceRoot);
            if (syntaxTree.Tree == null) return null;

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, workspaceId, workspaceRoot);

            return Incremental.FileSnapshotBuilder.BuildFromSyntaxTree(
                syntaxTree,
                relPath,
                contentHash,
                lastModifiedUtc,
                includeSemanticFootprint: true);
        }
        catch
        {
            return null;
        }
    }

    private async Task PrepareDatabaseAsync(ParsingContext ctx)
    {
        if (ctx.Clear)
        {
            var clearTarget = ctx.IsSubtreeScan ? ctx.ScanPath : ctx.HostWorkspacePath;
            ctx.PipelineLog.ClearingWorkspaceData(clearTarget);
            await _dbClient.ClearWorkspaceAsync(clearTarget);
        }

        if (!ctx.IsSubtreeScan && ctx.IncrementalChangedFiles == null && ctx.PersistToGraphDb)
        {
            await _dbClient.BeginBulkLoadAsync(ctx.CancellationToken);
        }
    }

    public static async Task<WorkspaceScanResult> ScanInMemoryAsync(
        string workspacePath,
        WorkspaceScanOptions? options = null,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        ILogger? logger = null)
    {
        options ??= new WorkspaceScanOptions();
        if (_fileParsers.Count == 0 || _projectParsers.Count == 0)
        {
            AutoRegisterParsers();
        }

        var fullDir = Path.GetFullPath(workspacePath).Replace('\\', '/');
        if (!Directory.Exists(fullDir))
        {
            throw new DirectoryNotFoundException($"Directory '{fullDir}' does not exist.");
        }

        var fileFilter = new WorkspaceFileFilter(
            _projectParsers,
            _fileParsers,
            options.ExcludedDirectories)
        {
            IncludeDistAndMinifiedArtifacts = options.IncludeDistAndMinifiedArtifacts
        };

        var buildInMemoryGraph = options.PersistToGraphDb || options.MaxLayer >= AnalysisLayer.Layer4_Semantic;
        var inMemoryGraph = buildInMemoryGraph ? new InMemoryGraphRepository() : null;
        var sharedChannel = buildInMemoryGraph
            ? Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false })
            : null!;

        var ctx = new ParsingContext(
            absoluteWorkspacePath: fullDir,
            hostWorkspacePath: fullDir,
            dbClient: inMemoryGraph!,
            sharedChannel: sharedChannel,
            clear: true,
            cancellationToken: cancellationToken,
            progress: progress,
            logger: logger)
        {
            PersistToGraphDb = buildInMemoryGraph,
            LightweightAstScanMode = options.LightweightAstMode,
            CustomFileExclusionPredicate = options.FileExclusionPredicate,
            CustomProjectFileFilter = options.ProjectFileFilter,
            FileFilter = fileFilter
        };

        var prevDataflow = CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Enabled;
        if (options.LightweightAstMode)
        {
            CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Enabled = false;
        }

        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Layer1Result l1Result;
        Layer2Result? l2Result = null;
        Layer3Result? l3Result = null;
        Layer4Result? l4Result = null;
        Layer5Result? l5Result = null;
        long l1Ms = 0, l2Ms = 0, l3Ms = 0, l4Ms = 0, l5Ms = 0;

        try
        {
            await using var writer = buildInMemoryGraph ? new DatabasePersistenceWriter(ctx) : null;

            l1Result = await new Layer1PhysicalParser().ParseAsync(ctx);
            l1Ms = sw.ElapsedMilliseconds;

            if (options.MaxLayer >= AnalysisLayer.Layer2_Boundaries)
            {
                sw.Restart();
                l2Result = await new Layer2ProjectParser().ParseAsync(l1Result, ctx);
                l2Ms = sw.ElapsedMilliseconds;
            }

            if (options.MaxLayer >= AnalysisLayer.Layer3_Syntactic && l2Result != null)
            {
                sw.Restart();
                l3Result = await new Layer3SyntacticParser().ParseAsync(l2Result, ctx);
                l3Ms = sw.ElapsedMilliseconds;
            }

            if (options.MaxLayer >= AnalysisLayer.Layer4_Semantic && l3Result != null)
            {
                sw.Restart();
                l4Result = await new Layer4SemanticParser().ParseAsync(l3Result, ctx);
                l4Ms = sw.ElapsedMilliseconds;
            }

            if (options.MaxLayer >= AnalysisLayer.Layer5_Global && l4Result != null)
            {
                sw.Restart();
                l5Result = await new Layer5AnalysisParser().ParseAsync(l4Result, ctx);
                l5Ms = sw.ElapsedMilliseconds;
            }
        }
        finally
        {
            CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Enabled = prevDataflow;
        }

        totalSw.Stop();
        var (finalNodesCount, finalRelsCount) = inMemoryGraph != null
            ? await inMemoryGraph.GetGraphCountsAsync(cancellationToken)
            : (ctx.TotalNodesCount, ctx.TotalRelsCount);

        return new WorkspaceScanResult
        {
            Layer1 = l1Result,
            Layer2 = l2Result,
            Layer3 = l3Result,
            Layer4 = l4Result,
            Layer5 = l5Result,
            Graph = inMemoryGraph,
            Timings = new LayerScanTimings(
                L1Ms: l1Ms,
                L2Ms: l2Ms,
                L3Ms: l3Ms,
                L4Ms: l4Ms,
                L5Ms: l5Ms,
                TotalMs: totalSw.ElapsedMilliseconds,
                NodeCount: finalNodesCount,
                RelationshipCount: finalRelsCount)
        };
    }

    private void LogPersistenceSummary(ParsingContext ctx)
    {
        ctx.PipelineLog.PersistenceSummary(ctx.TotalNodesPersisted, ctx.TotalRelsPersisted);
    }
}
