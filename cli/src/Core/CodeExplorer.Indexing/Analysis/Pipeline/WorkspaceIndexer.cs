using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser.Incremental;
using CodeExplorer.Core.Parser.Layers;
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
    internal const string IndexerVersionMetadataKey = "indexer_version";
    internal const string DetectorsHashMetadataKey = "detectors_hash";

    internal static readonly string CurrentIndexerVersion =
        typeof(WorkspaceIndexer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(WorkspaceIndexer).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static Func<IntentContext, CancellationToken, Task>? IntentEnrichmentHook { get; set; }

    public static Action? OnIndexCompletedHook
    {
        get => ParserRegistry.OnIndexCompletedHook;
        set => ParserRegistry.OnIndexCompletedHook = value;
    }

    internal static List<IProjectParser> _projectParsers => ParserRegistry.ProjectParsers;
    internal static List<IFileParser> _fileParsers => ParserRegistry.FileParsers;

    public static void Register(object parser) => ParserRegistry.Register(parser);

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
    private readonly FileRegistry _fileRegistry = new();
    private readonly IncrementalScanPlanner _planner;
    private readonly FileRegistrySynchronizer _registrySync;

    public FileRegistry FileRegistry => _fileRegistry;
    public bool HasLoadedFileRegistry => _fileRegistry.Count > 0;
    public void ResetFileRegistry() => _fileRegistry.Clear();

    public WorkspaceIndexer(IGraphClient dbClient, ILogger<WorkspaceIndexer>? logger = null)
    {
        _dbClient = dbClient;
        _logger = logger ?? NullLogger<WorkspaceIndexer>.Instance;
        _planner = new IncrementalScanPlanner(_dbClient, _fileRegistry, _logger);
        _registrySync = new FileRegistrySynchronizer(_dbClient, _fileRegistry);
    }

    public async Task<IndexResult> IndexAsync(
        string targetPath,
        string? workspaceRoot = null,
        bool clear = false,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        bool enableIntentAnalysis = false,
        IReadOnlyCollection<string>? knownChangedFiles = null)
    {
        var totalSw = Stopwatch.StartNew();
        var (absoluteWorkspacePath, absoluteTargetPath, scanPath) = NormalizePaths(targetPath, workspaceRoot);

        var forceClear = await EnsureSchemaCompatibleAsync(clear, cancellationToken);
        var fileFilter = new WorkspaceFileFilter(_projectParsers, _fileParsers);
        var detectorCatalog = WorkspaceDetectorLoader.LoadForWorkspace(absoluteWorkspacePath, strict: true);
        foreach (var warn in detectorCatalog.Warnings)
        {
            _logger.LogWarning("[Detectors  ] {File}: {Message}", warn.FilePath, warn.Message);
        }

        var plan = await _planner.PlanAsync(
            absoluteWorkspacePath,
            absoluteTargetPath,
            scanPath,
            forceClear,
            detectorCatalog,
            fileFilter,
            knownChangedFiles,
            totalSw,
            cancellationToken);

        if (!plan.GraphChanged)
        {
            var (existingNodes, existingRels) = await _dbClient.GetGraphCountsAsync(cancellationToken);
            var existingByKind = await _dbClient.GetNodesBreakdownAsync(cancellationToken);
            return new IndexResult(existingNodes, existingRels, existingByKind, plan.Mode, totalSw.ElapsedMilliseconds);
        }

        var scope = new WorkspaceScope(absoluteWorkspacePath, absoluteWorkspacePath, scanPath, cancellationToken)
        {
            FileFilter = fileFilter,
            DetectorCatalog = detectorCatalog,
            EnableIntentAnalysis = enableIntentAnalysis
        };
        var sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var sink = new GraphWriteSink(_dbClient, sharedChannel, cancellationToken, progress, _logger)
        {
            Clear = plan.ClearDatabase
        };

        await RunParsingPipelineAsync(scope, sink, plan);
        await _dbClient.SetMetadataAsync(IndexerVersionMetadataKey, CurrentIndexerVersion, cancellationToken);
        await _dbClient.SetMetadataAsync(DetectorsHashMetadataKey, detectorCatalog.CombinedHash, cancellationToken);

        sink.ReportProgress(100, CompletedProgressTitle, CompletedProgressMessage, sink.TotalNodesCount, sink.TotalNodesCount);
        sink.PipelineLog.IndexingCompleted(sink.TotalNodesCount, sink.TotalRelsCount, totalSw.ElapsedMilliseconds);

        return new IndexResult(sink.TotalNodesCount, sink.TotalRelsCount, sink.NodesByKind, plan.Mode, totalSw.ElapsedMilliseconds);
    }

    private static (string AbsoluteWorkspacePath, string AbsoluteTargetPath, string? ScanPath) NormalizePaths(
        string targetPath,
        string? workspaceRoot)
    {
        var effectiveRoot = string.IsNullOrWhiteSpace(workspaceRoot) ? targetPath : workspaceRoot;
        if (!Directory.Exists(effectiveRoot))
        {
            throw new DirectoryNotFoundException($"Directory '{effectiveRoot}' does not exist.");
        }

        var fullRoot = effectiveRoot.NormalizeFullPath();
        var fullTarget = targetPath.NormalizeFullPath();
        if (!Directory.Exists(fullTarget))
        {
            throw new DirectoryNotFoundException($"Scan path '{targetPath}' does not exist.");
        }

        if (string.Equals(fullTarget, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return (fullRoot, fullTarget, null);
        }

        var rel = fullTarget.GetNormalizedRelativePath(fullRoot);
        if (rel.StartsWith("..") || Path.IsPathRooted(rel))
        {
            throw new ArgumentException($"Target scan path '{targetPath}' must be inside workspace root '{effectiveRoot}'.");
        }

        return (fullRoot, fullTarget, fullTarget);
    }

    private async Task<bool> EnsureSchemaCompatibleAsync(bool clear, CancellationToken ct)
    {
        if (!_dbClient.IsSchemaOutdated) return clear;
        if (clear) return true;

        _logger.LogInformation(
            "[Database   ] Schema version outdated (v{SchemaVersion} < v{CurrentSchemaVersion}). Attempting schema conversion...",
            _dbClient.SchemaVersion,
            IGraphClient.CurrentSchemaVersion);

        if (await _dbClient.UpgradeSchemaAsync(ct))
        {
            return false;
        }

        _logger.LogInformation(
            "[Database   ] Schema conversion from v{SchemaVersion} to v{CurrentSchemaVersion} could not complete in-place. Performing full rebuild...",
            _dbClient.SchemaVersion,
            IGraphClient.CurrentSchemaVersion);
        return true;
    }

    private async Task RunParsingPipelineAsync(WorkspaceScope scope, GraphWriteSink sink, IndexScanPlan plan)
    {
        ConfigurationIndex.Clear();
        ConstantRegistry.Clear();
        RouteDictionaryRegistry.Clear();
        AnalysisSignalStore.Clear();

        var isIncremental = plan.IncrementalChangedFiles != null;
        var symbols = new SymbolResolutionBuffer();
        var globalProjectDeps = new List<Relationship>();
        var treeRels = new List<Relationship>();
        var semanticCtx = new SemanticParseContext(scope)
        {
            Layer4Log = sink.Layer4Log,
            GlobalSymbolSink = symbols.AddGlobalSymbol,
            ProjectDependencySink = dep =>
            {
                lock (globalProjectDeps)
                {
                    globalProjectDeps.Add(dep);
                }
            }
        };

        Layer1Result l1;
        Dictionary<string, FileGraphSnapshot> precomputedSnapshots;
        ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)>? skippedFiles;
        int parsedTreeCount;

        WorkspaceDetectorCatalog.CurrentScope = scope.DetectorCatalog;
        try
        {
            await using (new DatabasePersistenceWriter(sink))
            {
                sink.ReportProgress(1, Layer1PhysicalProgressTitle, Layer1PhysicalProgressMessage, 0, 0);
                await PrepareDatabaseAsync(scope, sink, isIncremental);

                Layer2Result l2;
                SyntaxStructureNode syntaxStructure;
                Layer4Result l4;
                {
                    var git = new GitRepositoryRegistry();
                    var dirIndex = new WorkspaceDirectoryIndex();
                    using var dirIndexScope = WorkspaceDirectoryIndex.EnterScope(dirIndex);

                    var l1Ctx = new Layer1Context(scope, sink, git, dirIndex)
                    {
                        IncrementalChangedFiles = plan.IncrementalChangedFiles,
                        IncrementalRelevantDirectories = plan.IncrementalRelevantDirectories
                    };
                    l1 = await new Layer1PhysicalParser().ParseAsync(l1Ctx);
                    sink.ReportProgress(8, Layer2BoundariesProgressTitle, $"Discovered {l1.Files.Count} files across workspace.", 0, l1.Files.Count);

                    var l2Ctx = new Layer2Context(scope, sink, l1, dirIndex, globalProjectDeps);
                    l2 = await new Layer2ProjectParser().ParseAsync(l2Ctx);
                    semanticCtx.ProjectsStructure = l2.ProjectsStructure;
                    sink.ReportProgress(14, Layer3SyntacticProgressTitle, $"Discovered {l2.Projects.Count} projects. Starting syntax parsing...", 0, l1.Files.Count);

                    var l3Ctx = new Layer3Context(scope, sink, l1, l2, dirIndex, symbols, semanticCtx, treeRels)
                    {
                        IncrementalChangedFiles = plan.IncrementalChangedFiles
                    };
                    var l3 = await new Layer3SyntacticParser().ParseAsync(l3Ctx);
                    syntaxStructure = l3.SyntaxStructure;
                    parsedTreeCount = l3.SyntaxTrees.Count;
                    skippedFiles = l3.DiscoveredContentSkippedFiles;
                    precomputedSnapshots = FileRegistrySynchronizer.PrecomputeSnapshots(l3.SyntaxTrees);
                    sink.ReportProgress(52, Layer4SemanticProgressTitle, $"Parsed {parsedTreeCount} syntax trees. Analyzing semantics...", parsedTreeCount, l1.Files.Count);

                    var l4Ctx = new Layer4Context(scope, sink, git, l1, l2, l3, symbols, semanticCtx, globalProjectDeps, treeRels)
                    {
                        IncrementalChangedFiles = plan.IncrementalChangedFiles
                    };
                    l4 = await new Layer4SemanticParser().ParseAsync(l4Ctx);
                    PipelineMemoryEvictor.ReleaseSyntaxTreesAfterLayer4(l3, scope.LightweightAstScanMode);
                }

                sink.ReportProgress(68, Layer5GlobalProgressTitle, Layer5GlobalProgressMessage, parsedTreeCount, l1.Files.Count);
                var l5Ctx = new Layer5Context(
                    scope,
                    sink,
                    l1.Workspace,
                    l1.FilesStructure,
                    l1.Files,
                    l2,
                    syntaxStructure,
                    l4,
                    symbols,
                    semanticCtx.NamedBeanTypes,
                    globalProjectDeps,
                    treeRels)
                {
                    IncrementalChangedFiles = plan.IncrementalChangedFiles
                };
                await new Layer5AnalysisParser().ParseAsync(l5Ctx);
                sink.ReportProgress(86, FinalizingProgressTitle, PostIndexingCompleteProgressMessage, parsedTreeCount, l1.Files.Count);
            }
        }
        finally
        {
            WorkspaceDetectorCatalog.CurrentScope = null;
            await _dbClient.CommitBulkLoadAsync(CancellationToken.None);
        }

        if (IntentEnrichmentHook != null)
        {
            await IntentEnrichmentHook(new IntentContext(scope, _dbClient, _logger), scope.CancellationToken);
        }

        sink.PipelineLog.PersistenceSummary(sink.TotalNodesPersisted, sink.TotalRelsPersisted);
        sink.ReportProgress(88, FinalizingProgressTitle, $"Synchronizing file registry for {l1.Files.Count} files...", parsedTreeCount, l1.Files.Count);
        await _registrySync.SyncAsync(l1.Files, precomputedSnapshots, scope, sink, l1, skippedFiles, isIncremental);
        precomputedSnapshots.Clear();
        sink.ReportProgress(94, FinalizingProgressTitle, RebuildingIndicesProgressMessage, parsedTreeCount, l1.Files.Count);
        await _dbClient.SetSchemaVersionAsync(IGraphClient.CurrentSchemaVersion);
        await _dbClient.CreateIndicesAsync();
        sink.ReportProgress(99, FinalizingProgressTitle, InvalidatingViewCacheProgressMessage, parsedTreeCount, l1.Files.Count);
        OnIndexCompletedHook?.Invoke();
        if (!sink.ScanIssues.IsEmpty)
        {
            var issuesList = sink.ScanIssues.Issues.Values.Select(v => v.Issue).ToList();
            var syntaxErrCount = issuesList.Count(i => i.Category == ScanIssueCategory.SyntaxError);
            sink.PipelineLog.RecordedScanIssues(issuesList.Count, syntaxErrCount, issuesList.Count - syntaxErrCount);
        }
    }

    private async Task PrepareDatabaseAsync(WorkspaceScope scope, GraphWriteSink sink, bool isIncremental)
    {
        if (sink.Clear)
        {
            var clearTarget = scope.IsSubtreeScan ? scope.ScanPath : scope.HostWorkspacePath;
            sink.PipelineLog.ClearingWorkspaceData(clearTarget);
            await _dbClient.ClearWorkspaceAsync(clearTarget);
        }

        if (!scope.IsSubtreeScan && !isIncremental && sink.PersistToGraphDb)
        {
            await _dbClient.BeginBulkLoadAsync(scope.CancellationToken);
        }
    }

    public static async Task<FileGraphSnapshot?> TryBuildSnapshotAsync(
        string fullPath,
        string relPath,
        string workspaceId,
        string workspaceRoot,
        string contentHash,
        DateTime lastModifiedUtc,
        CancellationToken cancellationToken = default,
        string? preloadedContent = null)
    {
        try
        {
            var parser = GetParserForFile(fullPath);
            if (parser == null) return null;

            var syntaxTree = await parser.ParseAsync(fullPath, "", workspaceId, workspaceRoot, preloadedContent);
            if (syntaxTree.Tree == null) return null;

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, workspaceId, workspaceRoot);

            return FileSnapshotBuilder.BuildFromSyntaxTree(
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

    public static Task<WorkspaceScanResult> ScanInMemoryAsync(
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

        return InMemoryWorkspaceScanner.RunAsync(
            workspacePath,
            _projectParsers,
            _fileParsers,
            options,
            cancellationToken,
            progress,
            logger);
    }
}
