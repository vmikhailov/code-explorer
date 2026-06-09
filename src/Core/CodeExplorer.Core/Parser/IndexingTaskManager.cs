using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

public class IndexingTaskContext
{
    public string TaskId { get; }
    public CancellationTokenSource Cts { get; }
    public IndexingStatus Status { get; }
    public ParsingContext? ActiveContext { get; set; }
    public Task? RunningTask { get; set; }

    public IndexingTaskContext(string taskId, CancellationTokenSource cts, IndexingStatus status)
    {
        TaskId = taskId;
        Cts = cts;
        Status = status;
    }
}

public class IndexingTaskManager
{
    private readonly WorkspaceIndexer _indexer;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, IndexingTaskContext> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastStartedTaskId;

    public IndexingTaskManager(WorkspaceIndexer indexer)
    {
        _indexer = indexer;
    }

    public IndexingStatus? GetStatus(string? taskId)
    {
        lock (_lock)
        {
            var id = taskId ?? _lastStartedTaskId;
            if (id == null || !_tasks.TryGetValue(id, out var taskContext))
            {
                return null;
            }

            return GetTaskStatusSnapshot(taskContext);
        }
    }

    public List<IndexingStatus> GetAllStatuses()
    {
        lock (_lock)
        {
            return _tasks.Values
                .OrderByDescending(t => t.Status.StartTime)
                .Select(GetTaskStatusSnapshot)
                .ToList();
        }
    }

    private IndexingStatus GetTaskStatusSnapshot(IndexingTaskContext taskContext)
    {
        var status = taskContext.Status;
        var activeContext = taskContext.ActiveContext;

        var snapshot = new IndexingStatus
        {
            State = status.State,
            Directory = status.Directory,
            StartTime = status.StartTime,
            EndTime = status.EndTime,
            ErrorMessage = status.ErrorMessage,
            NodesByKind = new Dictionary<string, int>(status.NodesByKind)
        };

        if (activeContext != null)
        {
            snapshot.NodesPersisted = activeContext.GetTotalNodesPersisted();
            snapshot.RelationshipsPersisted = activeContext.GetTotalRelsPersisted();
            snapshot.NodesCount = activeContext.TotalNodesCount;
            snapshot.RelationshipsCount = activeContext.TotalRelsCount;
            lock (activeContext.NodesByKind)
            {
                snapshot.NodesByKind = new Dictionary<string, int>(activeContext.NodesByKind);
            }
        }
        else
        {
            snapshot.NodesPersisted = status.NodesPersisted;
            snapshot.RelationshipsPersisted = status.RelationshipsPersisted;
            snapshot.NodesCount = status.NodesCount;
            snapshot.RelationshipsCount = status.RelationshipsCount;
        }

        if (snapshot.StartTime.HasValue)
        {
            var end = snapshot.EndTime ?? DateTime.UtcNow;
            snapshot.DurationSeconds = Math.Round((end - snapshot.StartTime.Value).TotalSeconds, 2);
        }

        return snapshot;
    }

    public string? StartIndex(string hostWorkspacePath, string containerWorkspacePath, bool clear, out string message)
    {
        lock (_lock)
        {
            // Check if there is already a running task on the same directory
            foreach (var existingTask in _tasks.Values)
            {
                if (existingTask.Status.State == IndexingState.Running.ToString() &&
                    string.Equals(existingTask.Status.Directory, hostWorkspacePath, StringComparison.OrdinalIgnoreCase))
                {
                    message = $"Indexing is already running for directory: {hostWorkspacePath}";
                    return null;
                }
            }

            var taskId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var status = new IndexingStatus
            {
                State = IndexingState.Running.ToString(),
                Directory = hostWorkspacePath,
                StartTime = DateTime.UtcNow,
                EndTime = null,
                DurationSeconds = null,
                ErrorMessage = null,
                NodesCount = 0,
                RelationshipsCount = 0,
                NodesPersisted = 0,
                RelationshipsPersisted = 0,
                NodesByKind = []
            };

            var taskContext = new IndexingTaskContext(taskId, cts, status);
            _tasks[taskId] = taskContext;
            _lastStartedTaskId = taskId;

            var runningTask = Task.Run(async () =>
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
                                taskContext.ActiveContext = ctx;
                            }
                        }
                    );

                    lock (_lock)
                    {
                        status.State = IndexingState.Completed.ToString();
                        status.EndTime = DateTime.UtcNow;
                        status.NodesCount = nodesCount;
                        status.RelationshipsCount = relsCount;
                        status.NodesByKind = new Dictionary<string, int>(nodesByKind);
                        if (taskContext.ActiveContext != null)
                        {
                            status.NodesPersisted = taskContext.ActiveContext.GetTotalNodesPersisted();
                            status.RelationshipsPersisted = taskContext.ActiveContext.GetTotalRelsPersisted();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    lock (_lock)
                    {
                        status.State = IndexingState.Cancelled.ToString();
                        status.EndTime = DateTime.UtcNow;
                        if (taskContext.ActiveContext != null)
                        {
                            status.NodesPersisted = taskContext.ActiveContext.GetTotalNodesPersisted();
                            status.RelationshipsPersisted = taskContext.ActiveContext.GetTotalRelsPersisted();
                            status.NodesCount = taskContext.ActiveContext.TotalNodesCount;
                            status.RelationshipsCount = taskContext.ActiveContext.TotalRelsCount;
                            status.NodesByKind = new Dictionary<string, int>(taskContext.ActiveContext.NodesByKind);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (_lock)
                    {
                        status.State = IndexingState.Failed.ToString();
                        status.EndTime = DateTime.UtcNow;
                        status.ErrorMessage = ex.ToString();
                        if (taskContext.ActiveContext != null)
                        {
                            status.NodesPersisted = taskContext.ActiveContext.GetTotalNodesPersisted();
                            status.RelationshipsPersisted = taskContext.ActiveContext.GetTotalRelsPersisted();
                            status.NodesCount = taskContext.ActiveContext.TotalNodesCount;
                            status.RelationshipsCount = taskContext.ActiveContext.TotalRelsCount;
                            status.NodesByKind = new Dictionary<string, int>(taskContext.ActiveContext.NodesByKind);
                        }
                    }
                }
                finally
                {
                    lock (_lock)
                    {
                        taskContext.ActiveContext = null;
                        taskContext.Cts.Dispose();
                    }
                }
            });

            taskContext.RunningTask = runningTask;

            message = "Indexing started in the background.";
            return taskId;
        }
    }

    public bool StopIndex(string? taskId, out string message)
    {
        lock (_lock)
        {
            var id = taskId ?? _lastStartedTaskId;
            if (id == null || !_tasks.TryGetValue(id, out var taskContext))
            {
                message = id == null ? "No task has been started yet." : $"Task with ID '{id}' not found.";
                return false;
            }

            if (taskContext.Status.State != IndexingState.Running.ToString())
            {
                message = $"Task with ID '{id}' is not currently running (State: {taskContext.Status.State}).";
                return false;
            }

            taskContext.Cts.Cancel();
            message = "Stop request sent.";
            return true;
        }
    }
}
