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
    private const string SemanticFootprintName = "<semantic>";

    static WorkspaceIndexer()
    {
        Layer3SyntacticParser.SemanticPostProcessor ??= (_, _) => { };
    }

    public static Func<Layer3Result, ParsingContext, Task>? PostSyntacticPipelineHook { get; set; }
    public static Action? OnIndexCompletedHook { get; set; }

    internal static readonly List<IProjectParser> _projectParsers = [];
    internal static readonly List<IFileParser> _fileParsers = [];
    private static readonly object _registrationLock = new();

    public static void Register(object parser)
    {
        lock (_registrationLock)
        {
            if (parser is IProjectParser projectParser)
            {
                if (_projectParsers.All(p => p.GetType() != projectParser.GetType()))
                {
                    var ordered = ProjectParserPrecedence.Order([.. _projectParsers, projectParser]);
                    _projectParsers.Clear();
                    _projectParsers.AddRange(ordered);
                }

                foreach (var desc in projectParser.ConfigurationDescriptors)
                {
                    LibraryConfigurationRegistry.Register(desc);
                }
            }

            if (parser is IFileParser fileParser)
            {
                if (_fileParsers.All(p => p.GetType() != fileParser.GetType()))
                    _fileParsers.Add(fileParser);
            }

            if (parser is ISharedFileParser sharedParser)
            {
                SharedFileParserRegistry.Register(sharedParser);
            }

            if (parser is ILibraryConfigurationDescriptor configDesc)
            {
                LibraryConfigurationRegistry.Register(configDesc);
            }

            if (parser is Components.IComponentLibraryParser componentParser)
            {
                Components.ComponentLibraryParserRegistry.Register(componentParser);
            }

            WorkspaceFileFilter.ResetDefault();
        }
    }

    /// <summary>
    /// Discovers parser assemblies (or scans the given ones) and registers every parser type they export.
    /// </summary>
    public static IReadOnlyList<Type> AutoRegisterParsers(IEnumerable<Assembly>? assemblies = null)
    {
        var parserTypes = (assemblies ?? ParserAssemblyDiscovery.Discover())
            .SelectMany(ParserAssemblyDiscovery.GetParserTypes)
            .ToList();

        foreach (var type in parserTypes)
        {
            Register(CreateParser(type));
        }

        return parserTypes;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Parser types are confirmed to have public parameterless constructor in IsParserType")]
    private static object CreateParser(Type type)
    {
        try
        {
            return Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Failed to instantiate parser '{type.FullName}'");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[AutoRegisterParsers] Failed to instantiate parser '{type.FullName}' from assembly '{type.Assembly.GetName().Name}': {ex.Message}",
                ex);
        }
    }

    public static IReadOnlyList<IProjectParser> GetAllProjectParsers() => _projectParsers;
    public static IReadOnlyList<IFileParser> GetAllFileParsers() => _fileParsers;

    public static IFileParser? GetParserForFile(string filePath)
    {
        if (_fileParsers.Count == 0) AutoRegisterParsers();
        return _fileParsers.FirstOrDefault(p => p.CanParseFile(filePath));
    }

    public static IFileParser? GetParserByLanguage(string languageName)
    {
        if (_fileParsers.Count == 0) AutoRegisterParsers();
        return _fileParsers.FirstOrDefault(p => string.Equals(p.LanguageName, languageName, StringComparison.OrdinalIgnoreCase));
    }

    public static IProjectParser? GetProjectParser(string projectType)
    {
        return _projectParsers.FirstOrDefault(p => string.Equals(p.ProjectType, projectType, StringComparison.OrdinalIgnoreCase));
    }

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

        if (_dbClient.IsSchemaOutdated && !clear)
        {
            _logger.LogInformation(
                "[Database   ] Schema version outdated (v{SchemaVersion} < v{CurrentSchemaVersion}). Attempting schema conversion...",
                _dbClient.SchemaVersion, IGraphClient.CurrentSchemaVersion);
            await _dbClient.UpgradeSchemaAsync(cancellationToken);
        }

        if (_dbClient.IsSchemaOutdated)
        {
            _logger.LogInformation(
                "[Database   ] Schema version outdated (v{SchemaVersion} < v{CurrentSchemaVersion}). Forcing full graph rebuild...",
                _dbClient.SchemaVersion, IGraphClient.CurrentSchemaVersion);
            clear = true;
        }

        var ctx = CreateContext(root, scanPath, clear, enableIntentAnalysis, cancellationToken, progress);

        await RunParsingPipelineAsync(ctx);

        ctx.ReportProgress(100, "Completed", "Indexing process completed successfully.", ctx.TotalNodesCount, ctx.TotalNodesCount);
        ctx.PipelineLog.IndexingCompleted(ctx.TotalNodesCount, ctx.TotalRelsCount);

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
        try
        {
            await using (new DatabasePersistenceWriter(ctx))
            {
                ctx.ReportProgress(1, "Layer 1: Physical", "Preparing database and physical scanning...", 0, 0);
                await PrepareDatabaseAsync(ctx);
                var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
                indexedFiles = l1.Files;
                ctx.ReportProgress(8, "Layer 2: Boundaries", $"Discovered {indexedFiles.Count} files across workspace.", 0, indexedFiles.Count);

                var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
                projects = l2.Projects;
                ctx.ReportProgress(14, "Layer 3: Syntactic", $"Discovered {projects.Count} projects. Starting syntax parsing...", 0, indexedFiles.Count);

                var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
                syntaxTrees = l3.SyntaxTrees;
                ctx.ReportProgress(52, "Layer 4: Semantic", $"Parsed {syntaxTrees.Count} syntax trees. Analyzing semantics...", syntaxTrees.Count, indexedFiles.Count);

                if (PostSyntacticPipelineHook != null) await PostSyntacticPipelineHook(l3, ctx);
                ctx.ReportProgress(68, "Layer 5: Global", "Semantic enrichment complete. Resolving global references...", syntaxTrees.Count, indexedFiles.Count);

                
                ctx.ReportProgress(86, "Finalizing", "Post-indexing analysis pass complete.", syntaxTrees.Count, indexedFiles.Count);
            }
        }
        finally
        {
            await _dbClient.CommitBulkLoadAsync(CancellationToken.None);
        }

        LogPersistenceSummary(ctx);
        ctx.ReportProgress(88, "Finalizing", $"Synchronizing file registry for {indexedFiles.Count} files...", syntaxTrees.Count, indexedFiles.Count);
        await SyncFileRegistryAsync(indexedFiles, projects, syntaxTrees, ctx);
        ctx.ReportProgress(94, "Finalizing", "Rebuilding database indices...", syntaxTrees.Count, indexedFiles.Count);
        await _dbClient.SetSchemaVersionAsync(IGraphClient.CurrentSchemaVersion);
        await _dbClient.CreateIndicesAsync();
        ctx.ReportProgress(99, "Finalizing", "Invalidating view cache...", syntaxTrees.Count, indexedFiles.Count);
        OnIndexCompletedHook?.Invoke();
        if (!ctx.ScanIssues.IsEmpty)
        {
            var issuesList = ctx.ScanIssues.Values.Select(v => v.Issue).ToList();
            var syntaxErrCount = issuesList.Count(i => i.Category == CodeExplorer.Cypher.Common.ScanIssueCategory.SyntaxError);
            var otherErrCount = issuesList.Count - syntaxErrCount;
            ctx.PipelineLog.RecordedScanIssues(issuesList.Count, syntaxErrCount, otherErrCount);
        }
    }


    private async Task SyncFileRegistryAsync(
        List<FileNode> files,
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<SyntaxTree> syntaxTrees,
        ParsingContext ctx)
    {
        ctx.PipelineLog.SyncingFileRegistry(files.Count);

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ctx.CancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        var treesByFullPath = syntaxTrees
            .GroupBy(t => t.FilePath.NormalizeSlashes(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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
                var fi = new FileInfo(file.FullPath);
                if (!fi.Exists) return;
                lastMod = fi.LastWriteTimeUtc;
                sizeBytes = fi.Length;
                hash = await Incremental.HashUtility.ComputeSha256FromFileAsync(file.FullPath, ct);
            }
            seen.TryAdd(file.Path, 0);

            string? snapshotJson = null;
            Incremental.FileGraphSnapshot? snapshot = null;

            var normalizedFullPath = file.FullPath.Replace('\\', '/');
            if (treesByFullPath.TryGetValue(normalizedFullPath, out var syntaxTree) && syntaxTree.FileNode != null)
            {
                var symbols = new Dictionary<string, Incremental.SymbolFootprint>(StringComparer.Ordinal);
                CollectSymbols(syntaxTree.FileNode, syntaxTree.FileNode.References, symbols);
                snapshot = new Incremental.FileGraphSnapshot(file.Path, hash, lastMod, symbols);
                snapshotJson = snapshot.ComputeStructuralHash();
            }

            _fileRegistry.UpdateEntry(file.Path, hash, lastMod, file.Path, snapshot, sizeBytes);
            entries.Add((file.Path, hash, lastMod, sizeBytes, file.Path, snapshotJson));
        });

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

        ctx.PipelineLog.PersistingFileRegistry(entries.Count);
        await _dbClient.SaveFileRegistryEntriesAsync(entries, ctx.CancellationToken);

        // Prune stale entries that were deleted or are now ignored/excluded
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

    public Task<bool> IndexIncrementalAsync(
        string workspacePath,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        bool enableIntentAnalysis = false) =>
        IndexIncrementalAsync(workspacePath, workspaceRoot: null, cancellationToken, progress, enableIntentAnalysis);


    public async Task<bool> IndexIncrementalAsync(
        string targetPath,
        string? workspaceRoot,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        bool enableIntentAnalysis = false)
    {
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
        var allFiles = filter.EnumerateCandidateFiles(
            absoluteTargetPath,
            absoluteWorkspacePath,
            gitignore,
            msg => _logger.LogInformation("{Msg}", msg));

        var targetSubPath = string.Equals(absoluteTargetPath, absoluteWorkspacePath, StringComparison.OrdinalIgnoreCase)
            ? null
            : Path.GetRelativePath(absoluteWorkspacePath, absoluteTargetPath).Replace('\\', '/');

        var changeset = _fileRegistry.ComputeChangeset(absoluteWorkspacePath, allFiles, targetSubPath);
        if (changeset.IsEmpty)
        {
            _logger.LogInformation("[Incremental] Workspace is up to date (0 changes detected).");
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

        if (changeset.Added.Count > 0)
        {
            var addedDesc = changeset.Added.Count <= 3
                ? string.Join(", ", changeset.Added)
                : $"{string.Join(", ", changeset.Added.Take(3))} (+{changeset.Added.Count - 3} more)";
            var addedReason = $"New file(s) added ({changeset.Added.Count}): [{addedDesc}]";
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
                var streamed = await PhysicalFileStore.StreamAndAnalyzeFileAsync(
                    normalizedFullPath,
                    relPath,
                    fileInfo.Name,
                    sizeBytes,
                    isManifest: false,
                    isSupportedSourceOrConfig: true,
                    filter,
                    cancellationToken);

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
            if (changeset.Modified.Count > 0)
            {
                _logger.LogInformation(
                    "[Incremental] Removing stale graph nodes for {Count} updated file(s)...",
                    changeset.Modified.Count);

                foreach (var mod in changeset.Modified)
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

            await IndexAsync(targetPath, workspaceRoot, clear: false, cancellationToken: cancellationToken, progress: progress, enableIntentAnalysis: enableIntentAnalysis);
        }
        else
        {
            _logger.LogInformation("[Incremental:Decision] All modifications were logic-only; graph remains unchanged.");
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

            var symbols = new Dictionary<string, Incremental.SymbolFootprint>(StringComparer.Ordinal);
            CollectSymbols(syntaxTree.FileNode, syntaxTree.FileNode.References, symbols);
            if (syntaxTree.SemanticSymbols.Count > 0)
            {
                var semSummary = string.Join(";", syntaxTree.SemanticSymbols
                    .Select(s => $"{s.Kind}:{s.Name}:{s.Callee}")
                    .OrderBy(x => x, StringComparer.Ordinal));
                var semHash = Incremental.HashUtility.ComputeSha256(semSummary);
                var semKey = $"{relPath}:{SemanticFootprintName}";
                symbols[semKey] = new Incremental.SymbolFootprint(
                    semKey,
                    SemanticFootprintName,
                    NodeKind.File,
                    semHash,
                    semHash,
                    new HashSet<string>(),
                    new HashSet<string>(),
                    0, 0, 0, 0);
            }

            return new Incremental.FileGraphSnapshot(relPath, contentHash, lastModifiedUtc, symbols);
        }
        catch
        {
            return null;
        }
    }

    private static void CollectSymbols(
        IOntologyNode node,
        List<CodeExplorer.Common.Reference> references,
        Dictionary<string, Incremental.SymbolFootprint> symbols,
        ILookup<string, CodeExplorer.Common.Reference>? refsByScope = null)
    {
        refsByScope ??= references.ToLookup(r => r.ScopeSymbolId ?? string.Empty, StringComparer.Ordinal);
        foreach (var child in node.Children)
        {
            if (child is not (FunctionNode or TypeNode or MemberNode { MemberKind: CodeExplorer.Cypher.Common.MemberKind.Field or CodeExplorer.Cypher.Common.MemberKind.Property or CodeExplorer.Cypher.Common.MemberKind.Event or CodeExplorer.Cypher.Common.MemberKind.Constant or CodeExplorer.Cypher.Common.MemberKind.EnumMember }))
            {
                if (child.Children.Count > 0)
                {
                    CollectSymbols(child, references, symbols, refsByScope);
                }
                continue;
            }

            if (child is FunctionNode { Name: var fnName } && fnName.StartsWith("<anon_", StringComparison.Ordinal))
            {
                if (child.Children.Count > 0)
                {
                    CollectSymbols(child, references, symbols, refsByScope);
                }
                continue;
            }

            var (name, startLine, endLine, startCol, endCol) = child switch
            {
                FunctionNode fn => (fn.Name, fn.StartLine, fn.EndLine, fn.StartCol, fn.EndCol),
                TypeNode tn => (tn.Name, tn.StartLine, tn.EndLine, tn.StartCol, tn.EndCol),
                MemberNode mn => (mn.Name, mn.StartLine, mn.EndLine, mn.StartCol, mn.EndCol),
                _ => (child.Id, 0, 0, 0, 0)
            };

            var symbolKey = StripLineSuffix(child.Id);
            var symbolRefs = child.References.Concat(refsByScope[child.Id]);

            var calls = symbolRefs
                .Where(r => r.RelationshipKind is RelationshipKind.Calls or RelationshipKind.CalledBy or RelationshipKind.CallsEndpoint or RelationshipKind.ServiceCall or RelationshipKind.TransitivelyCalls)
                .Select(r => r.TargetName)
                .ToHashSet();

            var types = symbolRefs
                .Where(r => r.RelationshipKind is RelationshipKind.UsesType or RelationshipKind.OfType)
                .Select(r => r.TargetName)
                .ToHashSet();

            var sig = $"{child.Kind.ToCypherLabel()}:{name}";
            var sigHash = Incremental.HashUtility.ComputeSha256(sig);

            var bodySummary = string.Join(";", calls.OrderBy(x => x).Concat(types.OrderBy(x => x)));
            var bodyHash = Incremental.HashUtility.ComputeSha256(bodySummary);

            var footprint = new Incremental.SymbolFootprint(
                symbolKey,
                name,
                child.Kind,
                sigHash,
                bodyHash,
                calls,
                types,
                startLine,
                endLine,
                startCol,
                endCol
            );

            symbols[symbolKey] = footprint;

            if (child.Children.Count > 0)
            {
                CollectSymbols(child, references, symbols, refsByScope);
            }
        }
    }

    private static string StripLineSuffix(string symbolId)
    {
        var atIdx = symbolId.LastIndexOf('@');
        if (atIdx <= 0) return symbolId;
        for (var i = atIdx + 1; i < symbolId.Length; i++)
        {
            if (!char.IsDigit(symbolId[i])) return symbolId;
        }
        return symbolId[..atIdx];
    }


    private async Task PrepareDatabaseAsync(ParsingContext ctx)
    {
        if (ctx.Clear)
        {
            var clearTarget = ctx.IsSubtreeScan ? ctx.ScanPath : ctx.HostWorkspacePath;
            ctx.PipelineLog.ClearingWorkspaceData(clearTarget);
            await _dbClient.ClearWorkspaceAsync(clearTarget);
        }

        if (!ctx.IsSubtreeScan && ctx.PersistToGraphDb)
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

        var ctx = new ParsingContext(
            absoluteWorkspacePath: fullDir,
            hostWorkspacePath: fullDir,
            dbClient: null!,
            sharedChannel: null!,
            clear: true,
            cancellationToken: cancellationToken,
            progress: progress,
            logger: logger)
        {
            PersistToGraphDb = false,
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
        long l1Ms = 0, l2Ms = 0, l3Ms = 0;

        try
        {
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
        }
        finally
        {
            CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Enabled = prevDataflow;
        }

        totalSw.Stop();
        return new WorkspaceScanResult
        {
            Layer1 = l1Result,
            Layer2 = l2Result,
            Layer3 = l3Result,
            Timings = new LayerScanTimings(
                L1Ms: l1Ms,
                L2Ms: l2Ms,
                L3Ms: l3Ms,
                L4Ms: 0,
                L5Ms: 0,
                TotalMs: totalSw.ElapsedMilliseconds,
                NodeCount: ctx.TotalNodesCount,
                RelationshipCount: ctx.TotalRelsCount)
        };
    }

    private void LogPersistenceSummary(ParsingContext ctx)
    {
        ctx.PipelineLog.PersistenceSummary(ctx.TotalNodesPersisted, ctx.TotalRelsPersisted);
    }
}
