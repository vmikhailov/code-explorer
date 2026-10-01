using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Parser;

public class WorkspaceIndexer
{
    internal static readonly List<IProjectParser> _projectParsers = [];
    internal static readonly List<IFileParser> _fileParsers = [];

    public static void Register(object parser)
    {
        if (parser is IProjectParser projectParser)
        {
            if (_projectParsers.All(p => p.GetType() != projectParser.GetType()))
                _projectParsers.Add(projectParser);

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

        if (parser is ILibraryConfigurationDescriptor configDesc)
        {
            LibraryConfigurationRegistry.Register(configDesc);
        }
    }

    public static IReadOnlyList<IProjectParser> GetAllProjectParsers() => _projectParsers;

    public static IFileParser? GetParserForFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return _fileParsers.FirstOrDefault(p => p.CanParse(ext));
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
            var fullTarget = Path.GetFullPath(targetPath).Replace('\\', '/');
            var fullRoot = Path.GetFullPath(workspaceRoot).Replace('\\', '/');

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

        var ctx = CreateContext(root, scanPath, clear, enableIntentAnalysis, cancellationToken, progress);

        await RunParsingPipelineAsync(ctx);

        ctx.Log(
            $"Indexing process completed successfully! Total Nodes: {ctx.TotalNodesCount}, Total Relationships: {ctx.TotalRelsCount}.");

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

        var absoluteWorkspacePath = Path.GetFullPath(workspacePath).Replace('\\', '/');
        string? normalizedScanPath = null;

        if (!string.IsNullOrWhiteSpace(scanPath))
        {
            normalizedScanPath = Path.GetFullPath(scanPath).Replace('\\', '/');
            var rel = Path.GetRelativePath(absoluteWorkspacePath, normalizedScanPath).Replace('\\', '/');
            if (rel.StartsWith("..") || Path.IsPathRooted(rel))
            {
                throw new ArgumentException($"Target scan path '{scanPath}' must be inside workspace root '{workspacePath}'.");
            }
        }

        var sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        return new ParsingContext(
            absoluteWorkspacePath,
            absoluteWorkspacePath,
            _dbClient,
            sharedChannel,
            clear,
            scanPath: normalizedScanPath,
            cancellationToken: cancellationToken,
            progress: progress,
            logger: _logger,
            enableIntentAnalysis: enableIntentAnalysis);
    }

    private async Task RunParsingPipelineAsync(ParsingContext ctx)
    {
        List<Common.Nodes.Layer1_Physical.FileNode> indexedFiles;
        IReadOnlyList<Common.Nodes.Layer2_Boundaries.ProjectNode> projects = [];
        IReadOnlyList<SyntaxTree> syntaxTrees = [];
        await using (new DatabasePersistenceWriter(ctx))
        {
            await PrepareDatabaseAsync(ctx);
            var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
            indexedFiles = l1.Files;
            ctx.TriggerProgressReport();
            var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
            projects = l2.Projects;
            ctx.TriggerProgressReport();
            var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
            syntaxTrees = l3.SyntaxTrees;
            ctx.TriggerProgressReport();
            var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);
            ctx.TriggerProgressReport();
            await new Layer5AnalysisParser().ParseAsync(l4, ctx);
            ctx.TriggerProgressReport();
        }

        LogPersistenceSummary(ctx);
        await SyncFileRegistryAsync(indexedFiles, projects, syntaxTrees, ctx);
        await _dbClient.SetSchemaVersionAsync(SqliteGraphClient.CurrentSchemaVersion);
    }


    private async Task SyncFileRegistryAsync(
        List<Common.Nodes.Layer1_Physical.FileNode> files,
        IReadOnlyList<Common.Nodes.Layer2_Boundaries.ProjectNode> projects,
        IReadOnlyList<SyntaxTree> syntaxTrees,
        ParsingContext ctx)
    {
        ctx.Log($"[Incremental] Synchronizing file registry and caching AST snapshots for {files.Count} files in parallel...");

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ctx.CancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        var treesByFullPath = syntaxTrees
            .GroupBy(t => t.FilePath.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var entries = new System.Collections.Concurrent.ConcurrentBag<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath, string? SnapshotJson)>();
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
        {
            if (!File.Exists(file.FullPath)) return;
            var lastMod = File.GetLastWriteTimeUtc(file.FullPath);
            var bytes = await File.ReadAllBytesAsync(file.FullPath, ct);
            var hash = Incremental.HashUtility.ComputeSha256(bytes);
            seen.TryAdd(file.Path, 0);

            string? snapshotJson = null;
            Incremental.FileGraphSnapshot? snapshot = null;

            var normalizedFullPath = file.FullPath.Replace('\\', '/');
            if (treesByFullPath.TryGetValue(normalizedFullPath, out var syntaxTree) && syntaxTree.FileNode != null)
            {
                var symbols = new Dictionary<string, Incremental.SymbolFootprint>(StringComparer.Ordinal);
                CollectSymbols(syntaxTree.FileNode, syntaxTree.FileNode.References, symbols);
                snapshot = new Incremental.FileGraphSnapshot(file.Path, hash, lastMod, symbols);
                try
                {
                    snapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot);
                }
                catch
                {
                    // Ignore serialization failure
                }
            }

            _fileRegistry.UpdateEntry(file.Path, hash, lastMod, file.Path, snapshot);
            entries.Add((file.Path, hash, lastMod, file.Path, snapshotJson));
        });

        // Register all project manifests across the workspace
        var gitignore = new GitIgnoreMatcher(ctx.AbsoluteWorkspacePath);
        var candidateManifests = WorkspaceFileFilter.EnumerateCandidateFiles(ctx.AbsoluteWorkspacePath, ctx.AbsoluteWorkspacePath, gitignore)
            .Where(f => WorkspaceFileFilter.IsManifestFile(Path.GetFileName(f)));

        foreach (var relPath in candidateManifests)
        {
            if (seen.ContainsKey(relPath)) continue;
            seen.TryAdd(relPath, 0);

            var manifestPath = Path.Combine(ctx.AbsoluteWorkspacePath, relPath);
            if (!File.Exists(manifestPath)) continue;

            var lastMod = File.GetLastWriteTimeUtc(manifestPath);
            var bytes = await File.ReadAllBytesAsync(manifestPath, ctx.CancellationToken);
            var hash = Incremental.HashUtility.ComputeSha256(bytes);
            _fileRegistry.UpdateEntry(relPath, hash, lastMod, relPath);
            entries.Add((relPath, hash, lastMod, relPath, null));
        }

        ctx.Log($"[Incremental] Persisting {entries.Count} file registry metadata and snapshot entries to SQLite...");
        await _dbClient.SaveFileRegistryEntriesAsync(entries, ctx.CancellationToken);

        // Prune stale entries that were deleted or are now ignored/excluded
        var staleKeys = _fileRegistry.Entries.Select(e => e.Key).Where(k => !seen.ContainsKey(k)).ToList();
        if (staleKeys.Count > 0)
        {
            ctx.Log($"[Incremental] Pruning {staleKeys.Count} obsolete file registry entries from SQLite...");
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

        var gitignore = new GitIgnoreMatcher(absoluteWorkspacePath);
        var allFiles = WorkspaceFileFilter.EnumerateCandidateFiles(
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
            .Where(f => WorkspaceFileFilter.IsManifestFile(Path.GetFileName(f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (changedManifests.Count > 0)
        {
            _logger.LogInformation(
                "[Incremental:Decision] Full rescan triggered: Project manifest modified ({Manifests}). Reindexing workspace structure...",
                string.Join(", ", changedManifests));
            await IndexAsync(targetPath, workspaceRoot, clear: false, cancellationToken: cancellationToken, progress: progress, enableIntentAnalysis: enableIntentAnalysis);
            return true;
        }

        if (changeset.Deleted.Count > 0)
        {
            _logger.LogInformation(
                "[Incremental] Pruning {Count} deleted file(s) from graph and registry: {Files}",
                changeset.Deleted.Count, string.Join(", ", changeset.Deleted));
            await _dbClient.DeleteFileRegistryEntriesAsync(changeset.Deleted, cancellationToken);
            foreach (var del in changeset.Deleted)
            {
                await _dbClient.ClearWorkspaceAsync(del);
                _fileRegistry.RemoveEntry(del);
            }
        }

        var workspaceId = await _dbClient.GetOrCreateWorkspaceIdAsync(absoluteWorkspacePath);
        var filesToUpdateRegistry = new List<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath, string? SnapshotJson)>();
        var structuralReasons = new List<string>();

        if (changeset.Added.Count > 0)
        {
            var addedReason = $"New file(s) added ({changeset.Added.Count}): [{string.Join(", ", changeset.Added)}]";
            _logger.LogInformation("[Incremental:Decision] Structural change detected: {Reason}", addedReason);
            structuralReasons.Add(addedReason);
        }

        foreach (var relPath in changeset.Modified)
        {
            var fullPath = Path.Combine(absoluteWorkspacePath, relPath);
            if (!File.Exists(fullPath)) continue;

            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            var newHash = Incremental.HashUtility.ComputeSha256(bytes);
            var lastMod = File.GetLastWriteTimeUtc(fullPath);

            if (!_fileRegistry.TryGetSnapshot(relPath, out var oldSnapshot))
            {
                // Cold snapshot fallback: build initial snapshot for this file and persist it without forcing full rescan
                var initialSnapshot = await TryBuildSnapshotAsync(fullPath, relPath, workspaceId, absoluteWorkspacePath, newHash, lastMod, cancellationToken);
                if (initialSnapshot != null)
                {
                    _fileRegistry.SetSnapshot(relPath, initialSnapshot);
                    var snapJson = System.Text.Json.JsonSerializer.Serialize(initialSnapshot);
                    filesToUpdateRegistry.Add((relPath, newHash, lastMod, "", snapJson));
                    _logger.LogInformation("[Incremental:Decision] Seeded initial AST snapshot for '{File}' without full rescan.", relPath);
                    continue;
                }

                var reason = $"No prior AST snapshot in database for '{relPath}' and AST parsing failed.";
                _logger.LogInformation("[Incremental:Decision] Full rescan triggered for '{File}': {Reason}", relPath, reason);
                structuralReasons.Add(reason);
                continue;
            }

            var newSnapshot = await TryBuildSnapshotAsync(fullPath, relPath, workspaceId, absoluteWorkspacePath, newHash, lastMod, cancellationToken);
            if (newSnapshot == null)
            {
                var reason = $"No AST parser available or syntax parsing failed for '{relPath}'.";
                _logger.LogInformation("[Incremental:Decision] Full rescan triggered for '{File}': {Reason}", relPath, reason);
                structuralReasons.Add(reason);
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
                _fileRegistry.UpdateEntry(relPath, newHash, lastMod, "", newSnapshot);
                var snapJson = System.Text.Json.JsonSerializer.Serialize(newSnapshot);
                filesToUpdateRegistry.Add((relPath, newHash, lastMod, "", snapJson));
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
            _logger.LogInformation("[Incremental:Decision] Full rescan triggered for '{File}': {Reason}", relPath, fullReason);
            structuralReasons.Add(fullReason);
        }

        if (filesToUpdateRegistry.Count > 0)
        {
            await _dbClient.SaveFileRegistryEntriesAsync(filesToUpdateRegistry, cancellationToken);
        }

        if (structuralReasons.Count > 0)
        {
            _logger.LogInformation(
                "[Incremental:Decision] Executing full graph update due to {Count} structural trigger(s):\n  * {Reasons}",
                structuralReasons.Count,
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
        Dictionary<string, Incremental.SymbolFootprint> symbols)
    {
        foreach (var child in node.Children)
        {
            var (name, startLine, endLine, startCol, endCol) = child switch
            {
                FunctionNode fn => (fn.Name, fn.StartLine, fn.EndLine, fn.StartCol, fn.EndCol),
                TypeNode tn => (tn.Name, tn.StartLine, tn.EndLine, tn.StartCol, tn.EndCol),
                MemberNode mn => (mn.Name, mn.StartLine, mn.EndLine, mn.StartCol, mn.EndCol),
                _ => (child.Id, 0, 0, 0, 0)
            };

            var symbolRefs = child.References.Concat(references.Where(r => r.ScopeSymbolId == child.Id));

            var calls = symbolRefs
                .Where(r => r.Kind.Contains("CALL", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.TargetName)
                .ToHashSet();

            var types = symbolRefs
                .Where(r => r.Kind.Contains("TYPE", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.TargetName)
                .ToHashSet();

            var sig = $"{child.Kind}:{name}";
            var sigHash = Incremental.HashUtility.ComputeSha256(sig);

            var bodySummary = string.Join(";", calls.OrderBy(x => x).Concat(types.OrderBy(x => x)));
            var bodyHash = Incremental.HashUtility.ComputeSha256(bodySummary);

            var footprint = new Incremental.SymbolFootprint(
                child.Id,
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

            symbols[child.Id] = footprint;

            if (child.Children.Count > 0)
            {
                CollectSymbols(child, references, symbols);
            }
        }
    }


    private async Task PrepareDatabaseAsync(ParsingContext ctx)
    {
        if (ctx.Clear)
        {
            var clearTarget = ctx.IsSubtreeScan ? ctx.ScanPath : ctx.HostWorkspacePath;
            ctx.Log($"Clearing previous data for '{clearTarget}'...");
            await _dbClient.ClearWorkspaceAsync(clearTarget);
        }

        await _dbClient.CreateIndicesAsync();
    }

    private void LogPersistenceSummary(ParsingContext ctx)
    {
        ctx.Log(
            $"All background channel persistence writes completed! Total parsed: {ctx.GetTotalNodesPersisted()} nodes, {ctx.GetTotalRelsPersisted()} relationships.");
    }
}
