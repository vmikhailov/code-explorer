using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Parser;

public class ParsingContext
{
    public string AbsoluteWorkspacePath { get; }
    public string HostWorkspacePath { get; }
    public string ScanPath { get; }
    public bool IsSubtreeScan => !string.Equals(ScanPath, AbsoluteWorkspacePath, StringComparison.OrdinalIgnoreCase);
    public IGraphClient DbClient { get; }
    public Channel<Func<Task>> SharedChannel { get; }
    public bool Clear { get; }
    public bool EnableIntentAnalysis { get; set; }
    public CancellationToken CancellationToken { get; }
    public string WorkspaceId { get; set; } = string.Empty;
    public ProjectsStructureNode? ProjectsStructure { get; set; }
    public SyntaxStructureNode? SyntaxStructure { get; set; }
    public SemanticStructureNode? SemanticStructure { get; set; }
    public ResourceReconciliationService ResourceRegistry { get; } = new();
    public WorkspaceFileFilter FileFilter { get; set; } = WorkspaceFileFilter.Default;

    /// <summary>Shared-parser results for files that belong to no project (workspace root files).</summary>
    public List<IProjectEnrichment> WorkspaceEnrichments { get; } = [];

    private readonly IProgress<IndexingProgress>? _progress;
    private readonly object _progressLock = new();
    private readonly ILogger _logger;

    public ILogger Logger => _logger;

    public void Log(string message)
    {
        _logger.LogInformation("{Message}", message);
    }

    public void LogDebug(string message)
    {
        _logger.LogDebug("{Message}", message);
    }

    public void LogWarning(string message, Exception? ex = null)
    {
        if (ex != null)
            _logger.LogWarning(ex, "{Message}", message);
        else
            _logger.LogWarning("{Message}", message);
    }

    public void LogError(string message, Exception? ex = null)
    {
        if (ex != null)
            _logger.LogError(ex, "{Message}", message);
        else
            _logger.LogError("{Message}", message);
    }

    public Dictionary<(string Kind, string Name), string> GlobalSymbols { get; }
    public Dictionary<(string FilePath, string Kind, string Name), string> FileScopedSymbols { get; } = new();
    public List<Reference> GlobalReferences { get; }
    public List<Relationship> GlobalProjectDependencies { get; }
    public List<Relationship> TreeRelationships { get; } = [];

    public List<RawImport> RawImports { get; } = [];
    public List<RawVariable> RawVariables { get; } = [];
    public List<RawTypeBinding> RawTypeBindings { get; } = [];
    public Dictionary<string, int> NodesByKind { get; } = new(StringComparer.OrdinalIgnoreCase);

    private int _totalNodesCount;
    private int _totalRelsCount;

    public int TotalNodesCount
    {
        get => _totalNodesCount;
        set => _totalNodesCount = value;
    }

    public int TotalRelsCount
    {
        get => _totalRelsCount;
        set => _totalRelsCount = value;
    }

    public void IncrementNodeKind(string kind)
    {
        lock (NodesByKind)
        {
            var count = NodesByKind.GetValueOrDefault(kind, 0);
            NodesByKind[kind] = count + 1;
        }
    }

    public void AddNodesCount(int count)
    {
        _totalNodesCount += count;
    }

    public void AddRelsCount(int count)
    {
        _totalRelsCount += count;
    }

    public void AddGlobalSymbol(string kind, string name, string id)
    {
        GlobalSymbols[(kind, name)] = id;
    }

    public void AddFileScopedSymbol(string filePath, string kind, string name, string id)
    {
        FileScopedSymbols[(filePath, kind, name)] = id;
    }

    public void AddGlobalReferences(IEnumerable<Reference> references)
    {
        GlobalReferences.AddRange(references);
    }

    public void AddGlobalProjectDependency(Relationship dependency)
    {
        GlobalProjectDependencies.Add(dependency);
    }

    private int _nodesPersisted;
    private int _relsPersisted;
    private int _lastReportedNodes;
    private int _lastReportedRels;
    private long _lastNodeReportTicks;
    private long _lastRelReportTicks;
    private int _currentPercentage;
    private string _currentPhase = "Starting";
    private string? _currentProgressMessage;
    private int _currentTotalFiles;
    private int _currentProcessedFiles;

    public void ReportProgress(int percentage, string phase, string? message = null, int processedFiles = 0, int totalFiles = 0)
    {
        _currentPercentage = Math.Clamp(percentage, 0, 100);
        _currentPhase = phase;
        _currentProgressMessage = message;
        if (totalFiles > 0) _currentTotalFiles = totalFiles;
        if (processedFiles > 0) _currentProcessedFiles = processedFiles;
        TriggerProgressReport();
    }

    public void RecordNodesPersisted(int count)
    {
        _nodesPersisted += count;
        ReportNodesProgressIfNeeded();
        TriggerProgressReport();
    }

    public void RecordRelationshipsPersisted(int count)
    {
        _relsPersisted += count;
        ReportRelationshipsProgressIfNeeded();
        TriggerProgressReport();
    }

    private void ReportNodesProgressIfNeeded()
    {
        var now = Environment.TickCount64;
        if (_nodesPersisted - _lastReportedNodes >= 10000)
        {
            Log($"[Database   ] Persisted {_nodesPersisted:N0} nodes to database...");
            _lastReportedNodes = _nodesPersisted;
            _lastNodeReportTicks = now;
        }
    }

    private void ReportRelationshipsProgressIfNeeded()
    {
        var now = Environment.TickCount64;
        if (_relsPersisted - _lastReportedRels >= 10000)
        {
            Log($"[Database   ] Persisted {_relsPersisted:N0} relationships to database...");
            _lastReportedRels = _relsPersisted;
            _lastRelReportTicks = now;
        }
    }

    public void FlushPersistenceProgressReport()
    {
        var now = Environment.TickCount64;
        if (_nodesPersisted > _lastReportedNodes)
        {
            LogDebug($"[Database   ] Persisted {_nodesPersisted:N0} nodes to database.");
            _lastReportedNodes = _nodesPersisted;
            _lastNodeReportTicks = now;
        }
        if (_relsPersisted > _lastReportedRels)
        {
            LogDebug($"[Database   ] Persisted {_relsPersisted:N0} relationships to database.");
            _lastReportedRels = _relsPersisted;
            _lastRelReportTicks = now;
        }
    }

    public int GetTotalNodesPersisted() => _nodesPersisted;
    public int GetTotalRelsPersisted() => _relsPersisted;

    public void TriggerProgressReport()
    {
        if (_progress == null) return;
        lock (_progressLock)
        {
            Dictionary<string, int> nodesByKindCopy;
            lock (NodesByKind)
            {
                nodesByKindCopy = new Dictionary<string, int>(NodesByKind);
            }
            var snapshot = new IndexingProgress(
                _nodesPersisted,
                _relsPersisted,
                _totalNodesCount,
                _totalRelsCount,
                nodesByKindCopy,
                _currentPercentage,
                _currentPhase,
                _currentProgressMessage,
                _currentTotalFiles,
                _currentProcessedFiles
            );
            _progress.Report(snapshot);
        }
    }

    public async Task EnqueueUploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        var copy = new List<Node>(nodes);
        await SharedChannel.Writer.WriteAsync(async () =>
        {
            await DbClient.UploadNodesAsync(copy);
            RecordNodesPersisted(copy.Count);
        }, CancellationToken).ConfigureAwait(false);
    }

    public async Task EnqueueUploadRelationshipsAsync(List<Relationship> rels)
    {
        if (rels.Count == 0) return;
        var copy = new List<Relationship>(rels);
        await SharedChannel.Writer.WriteAsync(async () =>
        {
            await DbClient.UploadRelationshipsAsync(copy);
            RecordRelationshipsPersisted(copy.Count);
        }, CancellationToken);
    }

    public async Task WaitForQueueDrainedAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await SharedChannel.Writer.WriteAsync(() =>
        {
            tcs.SetResult();
            return Task.CompletedTask;
        }, CancellationToken).ConfigureAwait(false);
        
        await tcs.Task;
        FlushPersistenceProgressReport();

        try
        {
            await DbClient.ExecuteWriteAsync("PRAGMA wal_checkpoint(PASSIVE); ANALYZE;", cancellationToken: CancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // In-memory or test databases might not support WAL checkpoint or ANALYZE
        }
    }

    public ParsingContext(
        string absoluteWorkspacePath,
        string hostWorkspacePath,
        ResourceReconciliationService? resourceRegistry = null)
        : this(absoluteWorkspacePath, hostWorkspacePath, null!, Channel.CreateUnbounded<Func<Task>>())
    {
        if (resourceRegistry != null)
        {
            ResourceRegistry = resourceRegistry;
        }
    }

    public ParsingContext(
        string absoluteWorkspacePath,
        string hostWorkspacePath,
        IGraphClient dbClient,
        Channel<Func<Task>> sharedChannel,
        bool clear = false,
        string? scanPath = null,
        Dictionary<(string Kind, string Name), string>? globalSymbols = null,
        List<Reference>? globalReferences = null,
        List<Relationship>? globalProjectDependencies = null,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        ILogger? logger = null,
        bool enableIntentAnalysis = false)
    {
        AbsoluteWorkspacePath = absoluteWorkspacePath.Replace('\\', '/');
        HostWorkspacePath = hostWorkspacePath;
        ScanPath = string.IsNullOrWhiteSpace(scanPath) ? AbsoluteWorkspacePath : scanPath.Replace('\\', '/');
        DbClient = dbClient;
        SharedChannel = sharedChannel;
        Clear = clear;
        EnableIntentAnalysis = enableIntentAnalysis;
        CancellationToken = cancellationToken;
        GlobalSymbols = globalSymbols ?? new Dictionary<(string Kind, string Name), string>();
        GlobalReferences = globalReferences ?? [];
        GlobalProjectDependencies = globalProjectDependencies ?? [];
        _progress = progress;
        _logger = logger ?? NullLogger.Instance;
    }

    public Dictionary<string, GitSettingsNode> DiscoveredGitRepositories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterGitRepository(string repoAbsDir, GitSettingsNode settings)
    {
        var norm = Path.GetFullPath(repoAbsDir).Replace('\\', '/').TrimEnd('/');
        DiscoveredGitRepositories[norm] = settings;
    }

    public GitSettingsNode? FindGitSettingsForPath(string path)
    {
        var absPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(AbsoluteWorkspacePath, path));
        var norm = absPath.Replace('\\', '/').TrimEnd('/');

        GitSettingsNode? bestMatch = null;
        int bestLen = -1;

        foreach (var (repoDir, settings) in DiscoveredGitRepositories)
        {
            if (norm.Equals(repoDir, StringComparison.OrdinalIgnoreCase) ||
                norm.StartWithIC(repoDir + "/"))
            {
                if (repoDir.Length > bestLen)
                {
                    bestLen = repoDir.Length;
                    bestMatch = settings;
                }
            }
        }

        if (bestMatch != null) return bestMatch;

        // Fallback: search upward for .git
        var root = GitSettingsParser.FindRepoRoot(absPath);
        if (root != null)
        {
            var parsed = GitSettingsParser.Parse(WorkspaceId, AbsoluteWorkspacePath, root);
            if (parsed != null)
            {
                RegisterGitRepository(root, parsed);
                return parsed;
            }
        }

        return null;
    }
}
