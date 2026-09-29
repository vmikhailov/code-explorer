using System.Threading.Channels;
using CodeExplorer.Core.Common;
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
        await using (new DatabasePersistenceWriter(ctx))
        {
            await PrepareDatabaseAsync(ctx);
            var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
            indexedFiles = l1.Files;
            ctx.TriggerProgressReport();
            var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
            ctx.TriggerProgressReport();
            var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
            ctx.TriggerProgressReport();
            var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);
            ctx.TriggerProgressReport();
            await new Layer5AnalysisParser().ParseAsync(l4, ctx);
            ctx.TriggerProgressReport();
        }

        LogPersistenceSummary(ctx);
        await SyncFileRegistryAsync(indexedFiles, ctx);
        await _dbClient.SetSchemaVersionAsync(SqliteGraphClient.CurrentSchemaVersion);
    }

    private async Task SyncFileRegistryAsync(List<Common.Nodes.Layer1_Physical.FileNode> files, ParsingContext ctx)
    {
        var entries = new List<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (!File.Exists(file.FullPath)) continue;
            var lastMod = File.GetLastWriteTimeUtc(file.FullPath);
            var bytes = await File.ReadAllBytesAsync(file.FullPath, ctx.CancellationToken);
            var hash = Incremental.HashUtility.ComputeSha256(bytes);
            entries.Add((file.Path, hash, lastMod, file.Path));
            seen.Add(file.Path);

            try
            {
                var snapshot = await TryBuildSnapshotAsync(
                    file.FullPath, file.Path, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, hash, lastMod, ctx.CancellationToken);
                if (snapshot != null)
                {
                    _fileRegistry.UpdateEntry(file.Path, hash, lastMod, file.Path, snapshot);
                }
                else
                {
                    _fileRegistry.UpdateEntry(file.Path, hash, lastMod, file.Path);
                }
            }
            catch
            {
                _fileRegistry.UpdateEntry(file.Path, hash, lastMod, file.Path);
            }
        }

        // Register any project manifests or config files not included in files
        var allCandidates = Directory.EnumerateFiles(ctx.AbsoluteWorkspacePath, "*", SearchOption.AllDirectories)
            .Where(IsCandidateSourceFile)
            .Select(f => Path.GetRelativePath(ctx.AbsoluteWorkspacePath, f).Replace('\\', '/'));

        foreach (var cand in allCandidates)
        {
            if (seen.Contains(cand)) continue;
            var fullPath = Path.Combine(ctx.AbsoluteWorkspacePath, cand);
            if (!File.Exists(fullPath)) continue;

            var lastMod = File.GetLastWriteTimeUtc(fullPath);
            var bytes = await File.ReadAllBytesAsync(fullPath, ctx.CancellationToken);
            var hash = Incremental.HashUtility.ComputeSha256(bytes);
            entries.Add((cand, hash, lastMod, cand));
            seen.Add(cand);
            _fileRegistry.UpdateEntry(cand, hash, lastMod, cand);
        }

        await _dbClient.SaveFileRegistryEntriesAsync(entries, ctx.CancellationToken);
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
            await IndexAsync(targetPath, workspaceRoot, clear: false, cancellationToken: cancellationToken, progress: progress, enableIntentAnalysis: enableIntentAnalysis);
            return true;
        }

        var allFiles = Directory.EnumerateFiles(absoluteTargetPath, "*", SearchOption.AllDirectories)
            .Where(IsCandidateSourceFile)
            .Select(f => Path.GetRelativePath(absoluteWorkspacePath, f).Replace('\\', '/'))
            .ToList();

        var changeset = _fileRegistry.ComputeChangeset(absoluteWorkspacePath, allFiles);
        if (changeset.IsEmpty)
        {
            _logger.LogInformation("[Incremental] Workspace is up to date (0 changes detected).");
            return false;
        }

        _logger.LogInformation($"[Incremental] Changes detected: {changeset.Added.Count} added, {changeset.Modified.Count} modified, {changeset.Deleted.Count} deleted.");

        var hasManifestChanges = changeset.Added.Concat(changeset.Modified).Concat(changeset.Deleted)
            .Any(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                      f.EndsWith("package.json", StringComparison.OrdinalIgnoreCase) ||
                      f.EndsWith("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                      f.EndsWith("go.mod", StringComparison.OrdinalIgnoreCase));

        if (hasManifestChanges)
        {
            _logger.LogInformation("[Incremental] Project manifest changed, reindexing workspace structure.");
            await IndexAsync(targetPath, workspaceRoot, clear: false, cancellationToken: cancellationToken, progress: progress, enableIntentAnalysis: enableIntentAnalysis);
            return true;
        }

        if (changeset.Deleted.Count > 0)
        {
            await _dbClient.DeleteFileRegistryEntriesAsync(changeset.Deleted, cancellationToken);
            foreach (var del in changeset.Deleted)
            {
                await _dbClient.ClearWorkspaceAsync(del);
                _fileRegistry.RemoveEntry(del);
            }
        }

        var workspaceId = await _dbClient.GetOrCreateWorkspaceIdAsync(absoluteWorkspacePath);
        var filesToUpdateRegistry = new List<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, string ProjectPath)>();
        var hasStructuralChanges = changeset.Added.Count > 0;

        foreach (var relPath in changeset.Modified)
        {
            var fullPath = Path.Combine(absoluteWorkspacePath, relPath);
            if (!File.Exists(fullPath)) continue;

            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            var newHash = Incremental.HashUtility.ComputeSha256(bytes);
            var lastMod = File.GetLastWriteTimeUtc(fullPath);

            if (_fileRegistry.TryGetSnapshot(relPath, out var oldSnapshot))
            {
                var newSnapshot = await TryBuildSnapshotAsync(fullPath, relPath, workspaceId, absoluteWorkspacePath, newHash, lastMod, cancellationToken);
                if (newSnapshot != null)
                {
                    var patch = Incremental.SemanticGraphDiffer.ComputeDiff(oldSnapshot, newSnapshot);
                    if (!patch.HasGraphStructuralChanges)
                    {
                        _logger.LogInformation("[Incremental] {File}: logic-only change (2+2/formatting). Zero graph changes.", relPath);
                        _fileRegistry.UpdateEntry(relPath, newHash, lastMod, "", newSnapshot);
                        filesToUpdateRegistry.Add((relPath, newHash, lastMod, ""));
                        continue;
                    }
                }
            }

            hasStructuralChanges = true;
        }

        if (filesToUpdateRegistry.Count > 0)
        {
            await _dbClient.SaveFileRegistryEntriesAsync(filesToUpdateRegistry, cancellationToken);
        }

        if (hasStructuralChanges)
        {
            _logger.LogInformation("[Incremental] Structural graph changes detected. Updating graph...");
            await IndexAsync(targetPath, workspaceRoot, clear: false, cancellationToken: cancellationToken, progress: progress, enableIntentAnalysis: enableIntentAnalysis);
        }
        else
        {
            _logger.LogInformation("[Incremental] All modifications were logic-only; graph remains unchanged.");
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
        var parser = GetParserForFile(fullPath);
        if (parser == null) return null;

        var syntaxTree = await parser.ParseAsync(fullPath, "", workspaceId, workspaceRoot);
        if (syntaxTree.Tree == null) return null;

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, workspaceId, workspaceRoot);

        var symbols = new Dictionary<string, Incremental.SymbolFootprint>(StringComparer.Ordinal);
        CollectSymbols(syntaxTree.FileNode, syntaxTree.FileNode.References, symbols);

        return new Incremental.FileGraphSnapshot(relPath, contentHash, lastModifiedUtc, symbols);
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

    private static bool IsCandidateSourceFile(string fullPath)
    {
        var normalized = fullPath.Replace('\\', '/');
        var parts = normalized.Split('/');
        foreach (var part in parts)
        {
            if (part is ".git" or ".codeexplorer" or "bin" or "obj" or "node_modules" or ".Packages" or ".Build" or "packages" or ".vs" or ".idea" or "dist")
                return false;
        }

        var fileName = Path.GetFileName(normalized).ToLowerInvariant();
        var ext = Path.GetExtension(normalized).ToLowerInvariant();

        if (fileName.Contains("mock") || fileName.EndsWith("tests.cs") || fileName.EndsWith("test.cs") ||
            fileName.EndsWith("_test.go") || fileName.StartsWith("test_") || fileName.EndsWith("_test.py") ||
            fileName.EndsWith(".test.ts") || fileName.EndsWith(".spec.ts") || fileName.EndsWith(".test.js") ||
            fileName.EndsWith(".spec.js") || fileName.StartsWith("scratch") || fileName.StartsWith("temp_") ||
            fileName.StartsWith("tmp_"))
            return false;

        var hasParser = _fileParsers.Any(p => p.CanParse(ext));
        var isConfigFile = ConfigurationParser.IsConfigurationFile(fileName);
        var isManifest = ext is ".csproj" or ".fsproj" or ".vbproj" || fileName is "package.json" or "pom.xml" or "go.mod";

        return hasParser || isConfigFile || isManifest;
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
