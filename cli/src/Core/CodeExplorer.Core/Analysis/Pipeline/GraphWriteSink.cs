using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Database;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Parser;

public sealed class GraphWriteSink
{
    private readonly IProgress<IndexingProgress>? _progress;
    private readonly object _progressLock = new();
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;

    private int _totalNodesCount;
    private int _totalRelsCount;
    private int _nodesPersisted;
    private int _relsPersisted;
    private int _lastReportedNodes;
    private int _lastReportedRels;
    private int _currentPercentage;
    private string _currentPhase = "Starting";
    private string? _currentProgressMessage;
    private int _currentTotalFiles;
    private int _currentProcessedFiles;

    public IGraphClient DbClient { get; }
    public IGraphRepository Repository { get; }
    public Channel<Func<Task>>? SharedChannel { get; }
    public bool PersistToGraphDb { get; init; } = true;
    public bool Clear { get; init; }
    public ScanIssueCollector ScanIssues { get; } = new();
    public Dictionary<string, int> NodesByKind { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ILogger Logger => _logger;
    public Layer1Logger Layer1Log => new(_logger);
    public Layer2Logger Layer2Log => new(_logger);
    public Layer3Logger Layer3Log => new(_logger);
    public Layer4Logger Layer4Log => new(_logger);
    public Layer5Logger Layer5Log => new(_logger);
    public PostIndexLogger PostIndexLog => new(_logger);
    public IntentLogger IntentLog => new(_logger);
    public PipelineLogger PipelineLog => new(_logger);

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

    public int TotalNodesPersisted => _nodesPersisted;
    public int TotalRelsPersisted => _relsPersisted;

    public GraphWriteSink(
        IGraphClient dbClient,
        Channel<Func<Task>>? sharedChannel,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        ILogger? logger = null)
    {
        DbClient = dbClient;
        Repository = dbClient switch
        {
            IGraphRepository repo => repo,
            null => null!,
            _ => new PersistedGraphRepository(dbClient)
        };
        SharedChannel = sharedChannel;
        _cancellationToken = cancellationToken;
        _progress = progress;
        _logger = logger ?? NullLogger.Instance;
    }

    public void IncrementNodeKind(string kind)
    {
        lock (NodesByKind)
        {
            NodesByKind[kind] = NodesByKind.GetValueOrDefault(kind, 0) + 1;
        }
    }

    public void AddNodesCount(int count) => _totalNodesCount += count;
    public void AddRelsCount(int count) => _totalRelsCount += count;

    public ScanIssueNode RecordScanIssue(
        WorkspaceScope scope,
        string stage,
        string category,
        string? relativeOrFullPath,
        string message,
        string? name = null,
        string severity = "warning",
        int? startLine = null,
        int? endLine = null,
        int? startCol = null,
        int? endCol = null,
        Exception? exception = null,
        string? language = null,
        string? parentNodeId = null,
        bool isFileParent = true)
    {
        var (issue, added) = ScanIssues.Record(
            scope,
            stage,
            category,
            relativeOrFullPath,
            message,
            name,
            severity,
            startLine,
            endLine,
            startCol,
            endCol,
            exception,
            language,
            parentNodeId,
            isFileParent);

        if (added)
        {
            IncrementNodeKind(OntologyConstants.NodeLabels.ScanIssue);
        }

        return issue;
    }

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
        if (_nodesPersisted - _lastReportedNodes >= 10000)
        {
            PipelineLog.PersistedNodesProgress(_nodesPersisted);
            _lastReportedNodes = _nodesPersisted;
        }
        TriggerProgressReport();
    }

    public void RecordRelationshipsPersisted(int count)
    {
        _relsPersisted += count;
        if (_relsPersisted - _lastReportedRels >= 10000)
        {
            PipelineLog.PersistedRelationshipsProgress(_relsPersisted);
            _lastReportedRels = _relsPersisted;
        }
        TriggerProgressReport();
    }

    public void FlushPersistenceProgressReport()
    {
        if (_nodesPersisted > _lastReportedNodes)
        {
            PipelineLog.PersistedNodesFinal(_nodesPersisted);
            _lastReportedNodes = _nodesPersisted;
        }
        if (_relsPersisted > _lastReportedRels)
        {
            PipelineLog.PersistedRelationshipsFinal(_relsPersisted);
            _lastReportedRels = _relsPersisted;
        }
    }

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
            _progress.Report(new IndexingProgress(
                _nodesPersisted,
                _relsPersisted,
                _totalNodesCount,
                _totalRelsCount,
                nodesByKindCopy,
                _currentPercentage,
                _currentPhase,
                _currentProgressMessage,
                _currentTotalFiles,
                _currentProcessedFiles));
        }
    }

    public async Task EnqueueUploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;
        if (!PersistToGraphDb)
        {
            RecordNodesPersisted(nodes.Count);
            return;
        }

        var copy = new List<Node>(nodes);
        if (SharedChannel == null)
        {
            await UploadNodesCoreAsync(copy);
            return;
        }

        await SharedChannel.Writer.WriteAsync(() => UploadNodesCoreAsync(copy), _cancellationToken).ConfigureAwait(false);
    }

    public async Task EnqueueUploadRelationshipsAsync(List<Relationship> rels)
    {
        if (rels.Count == 0) return;
        if (!PersistToGraphDb)
        {
            RecordRelationshipsPersisted(rels.Count);
            return;
        }

        var copy = new List<Relationship>(rels);
        if (SharedChannel == null)
        {
            await UploadRelationshipsCoreAsync(copy);
            return;
        }

        await SharedChannel.Writer.WriteAsync(() => UploadRelationshipsCoreAsync(copy), _cancellationToken);
    }

    public async Task FlushScanIssuesAsync(List<Relationship>? treeRelationships = null)
    {
        if (!PersistToGraphDb || DbClient == null || ScanIssues.IsEmpty) return;

        var (nodesToUpload, relsToUpload) = ScanIssues.DrainUnflushed();
        if (treeRelationships != null && relsToUpload.Count > 0)
        {
            treeRelationships.AddRange(relsToUpload);
        }

        if (nodesToUpload.Count > 0)
        {
            AddNodesCount(nodesToUpload.Count);
            await EnqueueUploadNodesAsync(nodesToUpload).ConfigureAwait(false);
        }

        if (relsToUpload.Count > 0)
        {
            AddRelsCount(relsToUpload.Count);
            await EnqueueUploadRelationshipsAsync(relsToUpload).ConfigureAwait(false);
        }
    }

    public async Task WaitForQueueDrainedAsync()
    {
        if (!PersistToGraphDb || SharedChannel == null) return;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await SharedChannel.Writer.WriteAsync(() =>
        {
            tcs.SetResult();
            return Task.CompletedTask;
        }, _cancellationToken).ConfigureAwait(false);

        await tcs.Task;
        FlushPersistenceProgressReport();
    }

    private async Task UploadNodesCoreAsync(List<Node> copy)
    {
        if (Repository != null)
        {
            await Repository.UpsertNodesAsync(copy, _cancellationToken);
        }
        else if (DbClient != null)
        {
            await DbClient.UploadNodesAsync(copy);
        }
        RecordNodesPersisted(copy.Count);
    }

    private async Task UploadRelationshipsCoreAsync(List<Relationship> copy)
    {
        if (Repository != null)
        {
            await Repository.UpsertRelationshipsAsync(copy, _cancellationToken);
        }
        else if (DbClient != null)
        {
            await DbClient.UploadRelationshipsAsync(copy);
        }
        RecordRelationshipsPersisted(copy.Count);
    }
}
