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

public record IndexingStatus
{
    public string State { get; init; } = nameof(IndexingState.Idle);
    public string? Directory { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public double? DurationSeconds { get; init; }
    public int NodesCount { get; init; }
    public int RelationshipsCount { get; init; }
    public Dictionary<string, int> NodesByKind { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public int NodesPersisted { get; init; }
    public int RelationshipsPersisted { get; init; }
}

public class IndexingTaskContext(CancellationTokenSource cts, IndexingStatus status)
{
    public CancellationTokenSource Cts { get; } = cts;
    public IndexingStatus Status { get; set; } = status;
    public Task? RunningTask { get; set; }
}

public class IndexingTaskManager(WorkspaceIndexer indexer)
{
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, IndexingTaskContext> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastStartedTaskId;

    public IndexingStatus? GetStatus(string? taskId)
    {
        var id = taskId ?? _lastStartedTaskId;

        if (id == null || !_tasks.TryGetValue(id, out var taskContext))
        {
            return null;
        }

        return GetTaskStatusSnapshot(taskContext);
    }

    public List<IndexingStatus> GetAllStatuses()
    {
        return _tasks.Values.OrderByDescending(t => t.Status.StartTime).Select(GetTaskStatusSnapshot).ToList();
    }

    private IndexingStatus GetTaskStatusSnapshot(IndexingTaskContext taskContext)
    {
        var status = taskContext.Status;

        if (status.StartTime.HasValue)
        {
            var end = status.EndTime ?? DateTime.UtcNow;
            var duration = Math.Round((end - status.StartTime.Value).TotalSeconds, 2);
            return status with { DurationSeconds = duration };
        }

        return status;
    }

    public string? StartIndex(string hostWorkspacePath, string containerWorkspacePath, bool clear, out string message)
    {
        lock (_lock)
        {
            // Check if there is already a running task on the same directory
            foreach (var existingTask in _tasks.Values)
            {
                if (existingTask.Status.State == nameof(IndexingState.Running) &&
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
                State = nameof(IndexingState.Running),
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

            var taskContext = new IndexingTaskContext(cts, status);
            _tasks[taskId] = taskContext;
            _lastStartedTaskId = taskId;

            var progressReporter = new Progress<IndexingProgress>(p =>
            {
                taskContext.Status = taskContext.Status with
                {
                    NodesPersisted = p.NodesPersisted,
                    RelationshipsPersisted = p.RelationshipsPersisted,
                    NodesCount = p.NodesCount,
                    RelationshipsCount = p.RelationshipsCount,
                    NodesByKind = p.NodesByKind as Dictionary<string, int> ?? new Dictionary<string, int>(p.NodesByKind)
                };
            });

            async Task<IndexingStatus> RunIndexAsync()
            {
                try
                {
                    var (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(
                        hostWorkspacePath,
                        containerWorkspacePath,
                        clear,
                        token,
                        progressReporter);

                    return taskContext.Status with
                    {
                        State = nameof(IndexingState.Completed),
                        EndTime = DateTime.UtcNow,
                        ErrorMessage = null,
                        NodesCount = nodesCount,
                        RelationshipsCount = relsCount,
                        NodesByKind = nodesByKind
                    };
                }
                catch (OperationCanceledException)
                {
                    return taskContext.Status with
                    {
                        State = nameof(IndexingState.Cancelled),
                        EndTime = DateTime.UtcNow
                    };
                }
                catch (Exception ex)
                {
                    return taskContext.Status with
                    {
                        State = nameof(IndexingState.Failed),
                        EndTime = DateTime.UtcNow,
                        ErrorMessage = ex.ToString()
                    };
                }
            }

            var runningTask = Task.Run(async () =>
            {
                try
                {
                    var finalStatus = await RunIndexAsync();
                    taskContext.Status = finalStatus;
                }
                finally
                {
                    taskContext.Cts.Dispose();
                }
            }, token);

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

            if (taskContext.Status.State != nameof(IndexingState.Running))
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
