using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeExplorer.Core.Parser;

public enum IndexingState
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled
}

public class IndexingStatus
{
    public string State { get; set; } = IndexingState.Idle.ToString();
    public string? Directory { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? DurationSeconds { get; set; }
    public int NodesCount { get; set; }
    public int RelationshipsCount { get; set; }
    public Dictionary<string, int> NodesByKind { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int NodesPersisted { get; set; }
    public int RelationshipsPersisted { get; set; }
}

public class IndexingTaskManager
{
    private readonly WorkspaceIndexer _indexer;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _runningTask;
    private readonly IndexingStatus _status = new();
    private ParsingContext? _activeContext;

    public IndexingTaskManager(WorkspaceIndexer indexer)
    {
        _indexer = indexer;
    }

    public IndexingStatus GetStatus()
    {
        lock (_lock)
        {
            var status = new IndexingStatus
            {
                State = _status.State,
                Directory = _status.Directory,
                StartTime = _status.StartTime,
                EndTime = _status.EndTime,
                ErrorMessage = _status.ErrorMessage,
                NodesByKind = new Dictionary<string, int>(_status.NodesByKind)
            };

            var context = _activeContext;
            if (context != null)
            {
                status.NodesPersisted = context.GetTotalNodesPersisted();
                status.RelationshipsPersisted = context.GetTotalRelsPersisted();
                status.NodesCount = context.TotalNodesCount;
                status.RelationshipsCount = context.TotalRelsCount;
                lock (context.NodesByKind)
                {
                    status.NodesByKind = new Dictionary<string, int>(context.NodesByKind);
                }
            }
            else
            {
                status.NodesPersisted = _status.NodesPersisted;
                status.RelationshipsPersisted = _status.RelationshipsPersisted;
                status.NodesCount = _status.NodesCount;
                status.RelationshipsCount = _status.RelationshipsCount;
            }

            if (status.StartTime.HasValue)
            {
                var end = status.EndTime ?? DateTime.UtcNow;
                status.DurationSeconds = Math.Round((end - status.StartTime.Value).TotalSeconds, 2);
            }

            return status;
        }
    }

    public bool StartIndex(string hostWorkspacePath, string containerWorkspacePath, bool clear, out string message)
    {
        lock (_lock)
        {
            if (_status.State == IndexingState.Running.ToString())
            {
                message = "Indexing is already running.";
                return false;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _status.State = IndexingState.Running.ToString();
            _status.Directory = hostWorkspacePath;
            _status.StartTime = DateTime.UtcNow;
            _status.EndTime = null;
            _status.DurationSeconds = null;
            _status.ErrorMessage = null;
            _status.NodesCount = 0;
            _status.RelationshipsCount = 0;
            _status.NodesPersisted = 0;
            _status.RelationshipsPersisted = 0;
            _status.NodesByKind.Clear();
            _activeContext = null;

            _runningTask = Task.Run(async () =>
            {
                try
                {
                    var (nodesCount, relsCount, nodesByKind) = await _indexer.IndexAsync(
                        hostWorkspacePath,
                        containerWorkspacePath,
                        clear,
                        token,
                        ctx =>
                        {
                            lock (_lock)
                            {
                                _activeContext = ctx;
                            }
                        }
                    );

                    lock (_lock)
                    {
                        _status.State = IndexingState.Completed.ToString();
                        _status.EndTime = DateTime.UtcNow;
                        _status.NodesCount = nodesCount;
                        _status.RelationshipsCount = relsCount;
                        _status.NodesByKind = new Dictionary<string, int>(nodesByKind);
                        if (_activeContext != null)
                        {
                            _status.NodesPersisted = _activeContext.GetTotalNodesPersisted();
                            _status.RelationshipsPersisted = _activeContext.GetTotalRelsPersisted();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    lock (_lock)
                    {
                        _status.State = IndexingState.Cancelled.ToString();
                        _status.EndTime = DateTime.UtcNow;
                        if (_activeContext != null)
                        {
                            _status.NodesPersisted = _activeContext.GetTotalNodesPersisted();
                            _status.RelationshipsPersisted = _activeContext.GetTotalRelsPersisted();
                            _status.NodesCount = _activeContext.TotalNodesCount;
                            _status.RelationshipsCount = _activeContext.TotalRelsCount;
                            _status.NodesByKind = new Dictionary<string, int>(_activeContext.NodesByKind);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (_lock)
                    {
                        _status.State = IndexingState.Failed.ToString();
                        _status.EndTime = DateTime.UtcNow;
                        _status.ErrorMessage = ex.Message;
                        if (_activeContext != null)
                        {
                            _status.NodesPersisted = _activeContext.GetTotalNodesPersisted();
                            _status.RelationshipsPersisted = _activeContext.GetTotalRelsPersisted();
                            _status.NodesCount = _activeContext.TotalNodesCount;
                            _status.RelationshipsCount = _activeContext.TotalRelsCount;
                            _status.NodesByKind = new Dictionary<string, int>(_activeContext.NodesByKind);
                        }
                    }
                }
                finally
                {
                    lock (_lock)
                    {
                        _activeContext = null;
                        if (_cts != null)
                        {
                            _cts.Dispose();
                            _cts = null;
                        }
                    }
                }
            });

            message = "Indexing started in the background.";
            return true;
        }
    }

    public bool StopIndex(out string message)
    {
        lock (_lock)
        {
            if (_status.State != IndexingState.Running.ToString())
            {
                message = "Indexing is not currently running.";
                return false;
            }

            _cts?.Cancel();
            message = "Stop request sent.";
            return true;
        }
    }
}
